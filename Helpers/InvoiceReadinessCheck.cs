using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using eInvWorld.Data;
using eInvWorld.Helpers;
using eInvWorld.Models.InputModel;
using eInvWorld.Services.Mappers;
using Microsoft.EntityFrameworkCore;

namespace EINVWORLD.Helpers
{
    /// <summary>One "Ready for LHDN" finding. Field is the form field name to jump to, when there is one.</summary>
    public sealed record ReadinessItem(string Section, bool Blocking, string Message, string? Field = null);

    /// <summary>
    /// Local pre-submit check of a saved draft: every rule LHDN would reject on, as a list instead of the first
    /// exception, so Create/Edit can show them and every submit path (pages, Invoices list, background job) can
    /// refuse a draft that isn't ready. The rules are the existing ones (InvoiceMapper party/currency rules,
    /// TaxTypeRules, TinHelper), not copies. Pure: the caller loads the parties, lines, taxes and active unit codes.
    /// </summary>
    public static class InvoiceReadinessCheck
    {
        public const string Supplier = "Supplier", Buyer = "Buyer", Invoice = "Invoice", Items = "Items", Tax = "Tax", Totals = "Totals";

        /// <summary>Sections in display order.</summary>
        public static readonly string[] Sections = { Supplier, Buyer, Invoice, Items, Tax, Totals };

        /// <summary>LHDN refuses documents whose issue date is more than 72 hours old.</summary>
        public const int MaxIssueAgeDays = 3;

        public static List<ReadinessItem> Check(InvoiceHeader invoice, ISet<string>? activeUnitCodes, DateTime nowMyt)
        {
            ArgumentNullException.ThrowIfNull(invoice);
            var items = new List<ReadinessItem>();

            if (invoice.Supplier == null)
                items.Add(new(Supplier, true, "Select the supplier.", "Invoice.SupplierId"));
            else
                items.AddRange(InvoiceMapper.PartyErrors(invoice.Supplier, "Supplier").Select(e => new ReadinessItem(Supplier, true, e)));

            if (invoice.Customer != null)
                items.AddRange(InvoiceMapper.PartyErrors(invoice.Customer, "Customer").Select(e => new ReadinessItem(Buyer, true, e)));
            else if (invoice.PublicCustomer != null)
                items.AddRange(InvoiceMapper.PublicCustomerErrors(invoice.PublicCustomer, "Public Customer").Select(e => new ReadinessItem(Buyer, true, e)));
            else
                items.Add(new(Buyer, true, "Select the buyer.", "SelectedBuyerId"));

            var currencyError = InvoiceMapper.CurrencyError(invoice);
            if (currencyError != null) items.Add(new(Invoice, true, currencyError, "Invoice.ExchangeRate"));
            if (TinHelper.IsAdjustmentDocType(invoice.DocTypeCode) && string.IsNullOrWhiteSpace(invoice.RefUUID))
                items.Add(new(Invoice, true, "Reference UUID is required for credit, debit and refund notes.", "Invoice.RefUUID"));
            if (invoice.IssueDate == null)
                items.Add(new(Invoice, true, "Set the issue date.", "Invoice.IssueDate"));
            else if ((nowMyt - invoice.IssueDate.Value).TotalDays > MaxIssueAgeDays)
                items.Add(new(Invoice, true, $"The issue date ({invoice.IssueDate:dd-MMM-yyyy}) is more than {MaxIssueAgeDays} days old; LHDN will refuse it. Please create a new invoice.", "Invoice.IssueDate"));

            var lines = invoice.InvoiceLines.OrderBy(l => l.LineNumber).ToList();
            if (lines.Count == 0) items.Add(new(Items, true, "Add at least one item."));
            var supplierTin = invoice.Supplier?.TIN;
            var buyerTin = invoice.Customer?.TIN ?? invoice.PublicCustomer?.TIN;
            var buyerIdNo = invoice.Customer != null ? invoice.Customer.RegNo : invoice.PublicCustomer?.RegNo;
            for (int i = 0; i < lines.Count; i++)
            {
                var l = lines[i];
                string f(string name) => $"Invoice.InvoiceLines[{i}].{name}";
                var item = $"Item {i + 1}";
                if (string.IsNullOrWhiteSpace(l.ItemDescription)) items.Add(new(Items, true, $"{item}: description is required.", f("ItemDescription")));
                if ((l.Quantity ?? 0) <= 0) items.Add(new(Items, true, $"{item}: quantity must be more than 0.", f("Quantity")));
                if (l.UnitPrice == null || l.UnitPrice < 0) items.Add(new(Items, true, $"{item}: unit price is required.", f("UnitPrice")));
                if (string.IsNullOrWhiteSpace(l.ClassificationCode))
                    items.Add(new(Items, true, $"{item}: classification is required.", f("ClassificationCode")));
                else if (TinHelper.SelfBilledClassificationError(invoice.DocTypeCode, supplierTin, l.ClassificationCode) is { } sbError)
                    items.Add(new(Items, true, $"{item}: {sbError}", f("ClassificationCode")));
                else if (TinHelper.ConsolidatedClassificationError(invoice.DocTypeCode, buyerTin, buyerIdNo, l.ClassificationCode) is { } consolidatedError)
                    items.Add(new(Items, true, $"{item}: {consolidatedError}", f("ClassificationCode")));
                if (string.IsNullOrWhiteSpace(l.UnitOfMeasure))
                    items.Add(new(Items, true, $"{item}: unit is required.", f("UnitOfMeasure")));
                else if (activeUnitCodes != null && !activeUnitCodes.Contains(l.UnitOfMeasure))
                    items.Add(new(Items, true, $"{item}: unit \"{l.UnitOfMeasure}\" is not an active LHDN unit code.", f("UnitOfMeasure")));

                if (l.InvoiceTaxes.Count == 0)
                    items.Add(new(Tax, true, $"{item}: add a tax type.", f("Taxes[0].TaxCategory")));
                for (int j = 0; j < l.InvoiceTaxes.Count; j++)
                {
                    var t = l.InvoiceTaxes[j];
                    if (string.IsNullOrWhiteSpace(t.TaxCategory))
                        items.Add(new(Tax, true, $"{item}: tax type is required.", f($"Taxes[{j}].TaxCategory")));
                    else if (TaxTypeRules.IsInvalidNotApplicable(t.TaxCategory, t.TaxPercentage, t.TaxAmount))
                        items.Add(new(Tax, true, TaxTypeRules.Message((i + 1).ToString(), l.ItemDescription), f($"Taxes[{j}].TaxPercentage")));
                }
            }

            // LHDN receives totals recalculated from the lines (InvoiceMapper.MapLegalMonetaryTotal), so stored totals that
            // disagree can't cause a rejection, but the PDF and lists would show different figures: warn, don't block.
            if (lines.Count > 0)
            {
                var exclTax = lines.Sum(l => l.AmountExclTax ?? 0);
                var tax = lines.Sum(l => l.InvoiceTaxes.Sum(t => t.TaxAmount ?? 0));
                if (Math.Abs((invoice.TotalAmountExclTax ?? 0) - exclTax) > 0.01m || Math.Abs((invoice.TotalTaxAmount ?? 0) - tax) > 0.01m)
                    items.Add(new(Totals, false, $"The saved totals differ from the items (items: {exclTax:N2} excl. tax, {tax:N2} tax). Save the draft again to refresh them."));
            }
            return items;
        }

        /// <summary>Loads the saved draft (parties, lines, taxes) and the active unit codes, then runs <see cref="Check"/>. Null when the invoice doesn't exist.</summary>
        public static async Task<List<ReadinessItem>?> CheckAsync(ApplicationDbContext db, string invoiceNo, CancellationToken ct = default)
        {
            var invoice = await db.InvoiceHeaders.AsNoTracking()
                .Include(i => i.Supplier).Include(i => i.Customer).Include(i => i.PublicCustomer)
                .Include(i => i.InvoiceLines).ThenInclude(l => l.InvoiceTaxes)
                .AsSplitQuery()
                .FirstOrDefaultAsync(i => i.InvoiceNo == invoiceNo, ct);
            if (invoice == null) return null;
            var units = new HashSet<string>(await db.UnitTypes.Where(u => u.IsActive).Select(u => u.Code).ToListAsync(ct), StringComparer.OrdinalIgnoreCase);
            return Check(invoice, units, DateTimeHelper.ToMalaysiaTime(DateTime.UtcNow));
        }

        /// <summary>The blocking messages joined for a refusal message; null when the draft may be submitted.</summary>
        public static string? BlockingSummary(IEnumerable<ReadinessItem> items)
        {
            var blocking = items.Where(i => i.Blocking).Select(i => i.Message).ToList();
            return blocking.Count == 0 ? null : "Not ready for LHDN: " + string.Join(" ", blocking);
        }
    }
}
