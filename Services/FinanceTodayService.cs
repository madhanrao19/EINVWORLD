using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using eInvWorld.Data;
using eInvWorld.Helpers;
using eInvWorld.Models.SmartCapture;
using EINVWORLD.Helpers;
using Microsoft.EntityFrameworkCore;

namespace eInvWorld.Services
{
    /// <summary>One thing Finance should act on, with the page that fixes it.</summary>
    public sealed record TodayItem(string Title, string Reason, string Url, string ActionLabel);

    /// <summary>What the Dashboard's Today card shows.</summary>
    public sealed class FinanceTodayModel
    {
        public int NeedsAttention => Items.Count + MoreItems;
        public int ReadyToSubmit { get; set; }
        public int ProcessingWithLhdn { get; set; }
        public int ValidatedToday { get; set; }
        public List<TodayItem> Items { get; } = new();
        /// <summary>Items found beyond the ones listed.</summary>
        public int MoreItems { get; set; }
    }

    /// <summary>
    /// "What does Finance need to do today?" for the signed-in user's own companies: invoices needing attention
    /// (InvoiceNeedsAttentionFilter), drafts that aren't Ready for LHDN (InvoiceReadinessCheck), failed recurring
    /// runs and Smart Capture documents waiting for review. Only existing rules are used; nothing is decided here.
    /// </summary>
    public class FinanceTodayService
    {
        public const int MaxItems = 10;
        public const int DraftBatch = 50;
        private readonly ApplicationDbContext _db;

        public FinanceTodayService(ApplicationDbContext db) => _db = db;

        public async Task<FinanceTodayModel> GetAsync(ClaimsPrincipal user, CancellationToken ct = default)
        {
            var model = new FinanceTodayModel();
            var userName = user?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(userName)) return model;

            var memberships = (await _db.UserCompanies.AsNoTracking()
                    .Where(uc => uc.User.UserName == userName)
                    .Select(uc => new { uc.PartyInfoId, uc.IsViewOnly, uc.PartyInfo.TIN })
                    .ToListAsync(ct))
                .Where(m => !GeneralTINHelper.IsGeneralTIN(m.TIN)) // shared placeholder parties never scope (v1.48.2)
                .ToList();
            var companyIds = memberships.Select(m => m.PartyInfoId).Distinct().ToList();
            if (companyIds.Count == 0) return model;
            var writable = memberships.Where(m => !m.IsViewOnly).Select(m => m.PartyInfoId).ToHashSet();

            var nowMyt = DateTimeHelper.ToMalaysiaTime(DateTime.UtcNow);
            var scoped = _db.InvoiceHeaders.AsNoTracking()
                .Where(i => companyIds.Contains(i.SupplierId ?? 0) || companyIds.Contains(i.CustomerId ?? 0));

            model.ProcessingWithLhdn = await scoped.CountAsync(i => i.LHDNStatusId == "Submitted", ct);
            var today = nowMyt.Date;
            model.ValidatedToday = await scoped.CountAsync(i => i.LHDNStatusId == "Valid"
                && i.DateTimeValidated >= today && i.DateTimeValidated < today.AddDays(1), ct); // stored in MYT

            var items = new List<TodayItem>();
            bool canFix(string? docType, int? supplierId, int? customerId) =>
                writable.Contains((TinHelper.IsSelfBilledDocType(docType) ? customerId : supplierId) ?? 0);

            // 1. Invoices needing attention: the same rule as the Invoice List's Needs Attention filter.
            var attention = await InvoiceNeedsAttentionFilter.Apply(scoped, _db.InvoiceHeaders.AsNoTracking())
                .OrderByDescending(i => i.LastUpdated ?? i.CreatedDate)
                .Select(i => new { i.InvoiceNo, i.UUID, i.DocTypeCode, i.SupplierId, i.CustomerId, i.LHDNStatusId, i.InternalStatusId })
                .Take(DraftBatch)
                .ToListAsync(ct);
            var listed = new HashSet<string>();
            foreach (var a in attention)
            {
                var fix = canFix(a.DocTypeCode, a.SupplierId, a.CustomerId);
                var (reason, onDetails) =
                    a.InternalStatusId == "RequestReject" ? ("The buyer asked to reject this invoice", true)
                    : a.InternalStatusId == "TransmissionError" ? ("Not sent to LHDN: transmission failed", false)
                    : a.LHDNStatusId == "Invalid" ? ("LHDN rejected this invoice", true)
                    : ("Draft not submitted for more than 3 days", false);
                var url = onDetails && !string.IsNullOrWhiteSpace(a.UUID)
                    ? $"/Invoices/InvoiceDetails2/{Uri.EscapeDataString(a.UUID)}"
                    : $"/Invoices/InvoiceEdit/{Uri.EscapeDataString(a.InvoiceNo)}";
                items.Add(new TodayItem(a.InvoiceNo, reason, url, fix ? (onDetails ? "Review" : "Fix invoice") : "View"));
                listed.Add(a.InvoiceNo);
            }

            // 2. Recent drafts: one batch load, then the same Ready-for-LHDN check Submit runs. Drafts older than the
            // LHDN issue-date limit already appear above as aging drafts and are filtered out before lines are loaded.
            var oldest = nowMyt.AddDays(-InvoiceReadinessCheck.MaxIssueAgeDays);
            var drafts = await scoped
                .Where(i => (i.InternalStatusId == "Draft" || i.InternalStatusId == "TransmissionError") && i.IssueDate >= oldest)
                .OrderByDescending(i => i.LastUpdated ?? i.CreatedDate)
                .Take(DraftBatch)
                .Include(i => i.Supplier).Include(i => i.Customer).Include(i => i.PublicCustomer)
                .Include(i => i.InvoiceLines).ThenInclude(l => l.InvoiceTaxes)
                .AsSplitQuery()
                .ToListAsync(ct);
            if (drafts.Count > 0)
            {
                var units = new HashSet<string>(await _db.UnitTypes.Where(u => u.IsActive).Select(u => u.Code).ToListAsync(ct), StringComparer.OrdinalIgnoreCase);
                foreach (var d in drafts)
                {
                    var first = InvoiceReadinessCheck.Check(d, units, nowMyt).FirstOrDefault(r => r.Blocking);
                    if (first == null) { model.ReadyToSubmit++; continue; }
                    if (!listed.Add(d.InvoiceNo)) continue;
                    items.Add(new TodayItem(d.InvoiceNo, "Not ready for LHDN: " + first.Message,
                        $"/Invoices/InvoiceEdit/{Uri.EscapeDataString(d.InvoiceNo)}",
                        canFix(d.DocTypeCode, d.SupplierId, d.CustomerId) ? "Fix invoice" : "View"));
                }
            }

            // 3. Recurring profiles whose latest run in the last 7 days failed, for profiles the user can change.
            var since = nowMyt.AddDays(-7); // RunTimestamp is MYT
            var latestRuns = await _db.RecurringRunHistories.AsNoTracking()
                .Where(r => r.RunTimestamp >= since)
                .GroupBy(r => r.RecurringProfileId)
                .Select(g => g.OrderByDescending(r => r.RunTimestamp).First())
                .ToListAsync(ct);
            var failed = latestRuns.Where(r => r.RunStatus.StartsWith("Failed")).ToDictionary(r => r.RecurringProfileId);
            if (failed.Count > 0)
            {
                var profiles = await _db.RecurringProfiles.AsNoTracking()
                    .Where(p => failed.Keys.Contains(p.Id))
                    .Select(p => new { p.Id, p.ProfileName, p.SupplierId, p.CustomerId, DocType = p.InvoiceTemplate != null ? p.InvoiceTemplate.DocTypeCode : null })
                    .ToListAsync(ct);
                foreach (var p in profiles.Where(p => canFix(p.DocType, p.SupplierId, p.CustomerId)))
                    items.Add(new TodayItem($"Recurring: {p.ProfileName}", "The last scheduled run failed", "/RecurringInvoices", "Review"));
            }

            // 4. Smart Capture documents waiting for a person.
            var capture = await _db.SmartCaptureDocuments.AsNoTracking()
                .Where(d => companyIds.Contains(d.CompanyPartyInfoId)
                    && (d.Status == SmartCaptureDocumentStatus.ReviewRequired
                        || d.Status == SmartCaptureDocumentStatus.ValidationFailed
                        || d.Status == SmartCaptureDocumentStatus.Failed))
                .OrderByDescending(d => d.UpdatedAtUtc)
                .Select(d => new { d.Id, d.OriginalFileName, d.Status, d.CompanyPartyInfoId })
                .Take(DraftBatch)
                .ToListAsync(ct);
            foreach (var c in capture)
                items.Add(new TodayItem($"Smart Capture: {c.OriginalFileName}",
                    c.Status == SmartCaptureDocumentStatus.ReviewRequired ? "Waiting for your review" : "Could not be read; check the document",
                    $"/Invoices/SmartCaptureReview/{c.Id}", writable.Contains(c.CompanyPartyInfoId) ? "Review" : "View"));

            // shortcut: each source is capped at DraftBatch, so the headline undercounts past 50 per source; count in SQL if that becomes real.
            model.Items.AddRange(items.Take(MaxItems));
            model.MoreItems = Math.Max(0, items.Count - MaxItems);
            return model;
        }
    }
}
