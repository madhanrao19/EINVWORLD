using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using eInvWorld.Data;
using eInvWorld.Models;
using Microsoft.EntityFrameworkCore;

namespace EINVWORLD.Services.Authorization
{
    public sealed class CompanyInvitationService : ICompanyInvitationService
    {
        private readonly ApplicationDbContext _context;

        private static readonly TimeSpan Lifetime = TimeSpan.FromHours(72);

        public CompanyInvitationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<(CompanyInvitation Invitation, string RawToken)> CreateAsync(int partyInfoId, string email, int? companyRoleId, string invitedByUserId, CancellationToken ct = default)
        {
            var rawToken = GenerateToken();
            var invitation = new CompanyInvitation
            {
                PartyInfoId = partyInfoId,
                Email = email.Trim(),
                InvitedByUserId = invitedByUserId,
                CompanyRoleId = companyRoleId,
                TokenHash = Hash(rawToken),
                ExpiresAt = DateTime.UtcNow.Add(Lifetime),
                CreatedDate = DateTime.UtcNow,
            };

            _context.CompanyInvitations.Add(invitation);
            await _context.SaveChangesAsync(ct);

            return (invitation, rawToken);
        }

        public async Task<InvitationValidationResult> ValidateAsync(int invitationId, string rawToken, CancellationToken ct = default)
        {
            var invitation = await _context.CompanyInvitations
                .Include(i => i.PartyInfo)
                .Include(i => i.CompanyRole)
                .FirstOrDefaultAsync(i => i.CompanyInvitationId == invitationId, ct);

            if (invitation == null)
                return new InvitationValidationResult(false, "This invitation link is invalid.", null);

            if (invitation.RevokedAt.HasValue)
                return new InvitationValidationResult(false, "This invitation has been revoked.", null);

            if (invitation.AcceptedAt.HasValue)
                return new InvitationValidationResult(false, "This invitation has already been accepted.", null);

            if (invitation.ExpiresAt < DateTime.UtcNow)
                return new InvitationValidationResult(false, "This invitation has expired. Ask an admin to send a new one.", null);

            if (!FixedTimeEquals(invitation.TokenHash, Hash(rawToken)))
                return new InvitationValidationResult(false, "This invitation link is invalid.", null);

            return new InvitationValidationResult(true, null, invitation);
        }

        public async Task MarkAcceptedAsync(int invitationId, CancellationToken ct = default)
        {
            var invitation = await _context.CompanyInvitations.FirstOrDefaultAsync(i => i.CompanyInvitationId == invitationId, ct);
            if (invitation == null) return;

            invitation.AcceptedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
        }

        public async Task RevokeAsync(int invitationId, int partyInfoId, CancellationToken ct = default)
        {
            var invitation = await _context.CompanyInvitations
                .FirstOrDefaultAsync(i => i.CompanyInvitationId == invitationId && i.PartyInfoId == partyInfoId, ct);
            if (invitation == null || invitation.AcceptedAt.HasValue) return;

            invitation.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
        }

        private static string GenerateToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        private static string Hash(string rawToken)
        {
            var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken));
            return Convert.ToHexString(bytes);
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            var bytesA = System.Text.Encoding.UTF8.GetBytes(a);
            var bytesB = System.Text.Encoding.UTF8.GetBytes(b);
            return bytesA.Length == bytesB.Length && CryptographicOperations.FixedTimeEquals(bytesA, bytesB);
        }
    }
}
