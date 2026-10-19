using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using eInvWorld.Data;
using eInvWorld.Models.InputModel;
using EINVWORLD.Helpers;
using EINVWORLD.Services.Audit;
using Microsoft.EntityFrameworkCore;

namespace EINVWORLD.Services
{
    /// <summary>
    /// The only writer of <see cref="SupplierBankAccount"/> rows. Every change keeps exactly one default account
    /// per company (when the company has any) and mirrors it into <see cref="PartyInfo.BankName"/> /
    /// <see cref="PartyInfo.BankAccountNo"/> in the same SaveChanges, so the PDF / CSV / LHDN paths that read the
    /// company fields always see the default. Callers authorise the company; this service only guarantees an
    /// account id belongs to the company it is told about.
    /// </summary>
    public interface ISupplierBankAccountService
    {
        /// <summary>The company's accounts, default first, then by bank name.</summary>
        Task<IReadOnlyList<SupplierBankAccount>> ListAsync(int partyInfoId, CancellationToken ct = default);

        /// <summary>Adds an account. The company's first account always becomes the default.</summary>
        Task<SupplierBankAccount> AddAsync(int partyInfoId, string bankName, string bankAccountNo, bool makeDefault, string? user, CancellationToken ct = default);

        /// <summary>Renames an account; a blank <paramref name="newBankAccountNo"/> keeps the current number. False if not found.</summary>
        Task<bool> UpdateAsync(int partyInfoId, int accountId, string bankName, string? newBankAccountNo, string? user, CancellationToken ct = default);

        /// <summary>Makes the account the company default. False if not found.</summary>
        Task<bool> SetDefaultAsync(int partyInfoId, int accountId, string? user, CancellationToken ct = default);

        /// <summary>Deletes an account; if it was the default, the next account (by name) becomes default. False if not found.</summary>
        Task<bool> DeleteAsync(int partyInfoId, int accountId, string? user, CancellationToken ct = default);

        /// <summary>
        /// For screens that still edit the company's own bank fields (Create/Edit/Import Company): makes the default
        /// account match them, adding one if the company has none. A blank company account number changes nothing.
        /// </summary>
        Task SyncDefaultFromCompanyAsync(int partyInfoId, string? user, CancellationToken ct = default);
    }

    /// <inheritdoc />
    public sealed class SupplierBankAccountService : ISupplierBankAccountService
    {
        private readonly ApplicationDbContext _db;
        private readonly IAuditService _audit;

        public SupplierBankAccountService(ApplicationDbContext db, IAuditService audit)
        {
            _db = db;
            _audit = audit;
        }

        public async Task<IReadOnlyList<SupplierBankAccount>> ListAsync(int partyInfoId, CancellationToken ct = default)
        {
            // Ordered in memory: BankAccountNo is encrypted, and the list per company is tiny.
            var accounts = await _db.SupplierBankAccounts.AsNoTracking()
                .Where(a => a.PartyInfoId == partyInfoId)
                .ToListAsync(ct);
            return Ordered(accounts).ToList();
        }

        public async Task<SupplierBankAccount> AddAsync(int partyInfoId, string bankName, string bankAccountNo, bool makeDefault, string? user, CancellationToken ct = default)
        {
            var (party, accounts) = await LoadAsync(partyInfoId, ct);
            if (party == null) throw new InvalidOperationException($"Company {partyInfoId} not found.");

            var account = new SupplierBankAccount
            {
                PartyInfoId = partyInfoId,
                BankName = bankName.Trim(),
                BankAccountNo = bankAccountNo.Trim(),
                CreatedDate = DateTime.Now,
                CreatedBy = user,
            };
            _db.SupplierBankAccounts.Add(account);
            accounts.Add(account);

            if (makeDefault || accounts.Count == 1) MakeDefault(accounts, account, user);
            Mirror(party, accounts);
            await _db.SaveChangesAsync(ct);

            await AuditAsync("BankAccountAdded", party, account, ct);
            return account;
        }

        public async Task<bool> UpdateAsync(int partyInfoId, int accountId, string bankName, string? newBankAccountNo, string? user, CancellationToken ct = default)
        {
            var (party, accounts) = await LoadAsync(partyInfoId, ct);
            var account = accounts.FirstOrDefault(a => a.Id == accountId);
            if (party == null || account == null) return false;

            account.BankName = bankName.Trim();
            if (!string.IsNullOrWhiteSpace(newBankAccountNo)) account.BankAccountNo = newBankAccountNo.Trim();
            Touch(account, user);
            Mirror(party, accounts);
            await _db.SaveChangesAsync(ct);

            await AuditAsync("BankAccountUpdated", party, account, ct);
            return true;
        }

        public async Task<bool> SetDefaultAsync(int partyInfoId, int accountId, string? user, CancellationToken ct = default)
        {
            var (party, accounts) = await LoadAsync(partyInfoId, ct);
            var account = accounts.FirstOrDefault(a => a.Id == accountId);
            if (party == null || account == null) return false;

            MakeDefault(accounts, account, user);
            Mirror(party, accounts);
            await _db.SaveChangesAsync(ct);

            await AuditAsync("BankAccountDefaultChanged", party, account, ct);
            return true;
        }

        public async Task<bool> DeleteAsync(int partyInfoId, int accountId, string? user, CancellationToken ct = default)
        {
            var (party, accounts) = await LoadAsync(partyInfoId, ct);
            var account = accounts.FirstOrDefault(a => a.Id == accountId);
            if (party == null || account == null) return false;

            _db.SupplierBankAccounts.Remove(account);
            accounts.Remove(account);
            if (account.IsDefault && accounts.Count > 0) MakeDefault(accounts, Ordered(accounts).First(), user);
            Mirror(party, accounts);
            await _db.SaveChangesAsync(ct);

            await AuditAsync("BankAccountDeleted", party, account, ct);
            return true;
        }

        public async Task SyncDefaultFromCompanyAsync(int partyInfoId, string? user, CancellationToken ct = default)
        {
            var (party, accounts) = await LoadAsync(partyInfoId, ct);
            if (party == null || string.IsNullOrWhiteSpace(party.BankAccountNo)) return;

            var bankName = (party.BankName ?? string.Empty).Trim();
            var number = party.BankAccountNo.Trim();
            var match = accounts.FirstOrDefault(a => a.BankAccountNo == number);
            var current = accounts.FirstOrDefault(a => a.IsDefault);

            if (match != null)
            {
                // The company fields name an account we already hold: make it the default (and take the name).
                if (match.BankName != bankName) { match.BankName = bankName; Touch(match, user); }
                MakeDefault(accounts, match, user);
            }
            else if (current != null)
            {
                current.BankName = bankName;
                current.BankAccountNo = number;
                Touch(current, user);
            }
            else
            {
                var account = new SupplierBankAccount
                {
                    PartyInfoId = partyInfoId, BankName = bankName, BankAccountNo = number,
                    CreatedDate = DateTime.Now, CreatedBy = user,
                };
                _db.SupplierBankAccounts.Add(account);
                accounts.Add(account);
                MakeDefault(accounts, account, user);
            }

            Mirror(party, accounts);
            if (_db.ChangeTracker.HasChanges()) await _db.SaveChangesAsync(ct);
        }

        private async Task<(PartyInfo? Party, List<SupplierBankAccount> Accounts)> LoadAsync(int partyInfoId, CancellationToken ct)
        {
            var party = await _db.PartyInfos.FirstOrDefaultAsync(p => p.PartyInfoId == partyInfoId, ct);
            var accounts = await _db.SupplierBankAccounts.Where(a => a.PartyInfoId == partyInfoId).ToListAsync(ct);
            return (party, accounts);
        }

        private static IEnumerable<SupplierBankAccount> Ordered(IEnumerable<SupplierBankAccount> accounts) =>
            accounts.OrderByDescending(a => a.IsDefault).ThenBy(a => a.BankName, StringComparer.OrdinalIgnoreCase).ThenBy(a => a.Id);

        private static void MakeDefault(List<SupplierBankAccount> accounts, SupplierBankAccount chosen, string? user)
        {
            foreach (var a in accounts)
            {
                var shouldBe = ReferenceEquals(a, chosen);
                if (a.IsDefault == shouldBe) continue;
                a.IsDefault = shouldBe;
                Touch(a, user);
            }
        }

        private static void Touch(SupplierBankAccount account, string? user)
        {
            account.UpdatedDate = DateTime.Now;
            account.UpdatedBy = user;
        }

        // Only assigns when different, so an unchanged mirror never marks PartyInfo modified.
        private static void Mirror(PartyInfo party, List<SupplierBankAccount> accounts)
        {
            var def = accounts.FirstOrDefault(a => a.IsDefault);
            var name = def?.BankName;
            var number = def?.BankAccountNo;
            if (party.BankName != name) party.BankName = name;
            if (party.BankAccountNo != number) party.BankAccountNo = number;
        }

        private Task AuditAsync(string action, PartyInfo party, SupplierBankAccount account, CancellationToken ct) =>
            _audit.WriteAsync(action, new AuditEntry
            {
                Tin = party.TIN,
                // Never the full account number in the audit trail.
                NewValueJson = JsonSerializer.Serialize(new
                {
                    accountId = account.Id,
                    bankName = account.BankName,
                    account = MaskingHelper.MaskLast4(account.BankAccountNo),
                    isDefault = account.IsDefault,
                }),
            }, ct);
    }
}
