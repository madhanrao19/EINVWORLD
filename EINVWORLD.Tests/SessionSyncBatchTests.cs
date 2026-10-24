using System;
using System.Linq;
using eInvWorld.Models.InputModel;
using eInvWorld.Pages.Invoices;
using EINVWORLD.Helpers;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>
    /// Invoices list status refresh polls only a few invoices per 30s tick. Right after an app restart (in-memory cooldowns
    /// empty) it used to poll every recent invoice at once, tripping LHDN's 429 and holding the request ~1 min (Staging, 25 Oct).
    /// </summary>
    public class SessionSyncBatchTests
    {
        private static readonly DateTime Now = new(2026, 10, 25, 1, 33, 0);

        private static InvoiceHeader Inv(string no, string status, int checkedMinutesAgo) => new()
        {
            InvoiceNo = $"{no}-{Guid.NewGuid():N}"[..20], LHDNStatusId = status, LongId = "QR",
            CreatedDate = Now.AddDays(-1), LastUpdated = Now.AddMinutes(-checkedMinutesAgo),
        };

        [Fact]
        public void Caps_The_Batch_Submitted_First_Then_Least_Recently_Checked()
        {
            var submitted = Inv("SUB", "Submitted", 1);
            var valid = Enumerable.Range(1, 10).Select(i => Inv($"V{i}", "Valid", 20 + i)).ToList(); // all past the 10-min cooldown

            var batch = InvoiceListsModel.PickSessionSyncBatch(valid.Append(submitted), Now);

            Assert.Equal(InvoiceListsModel.MaxSessionSyncPerTick, batch.Count);
            Assert.Same(submitted, batch[0]);
            Assert.Equal(valid.OrderByDescending(v => Now - v.LastUpdated!.Value).Take(batch.Count - 1), batch.Skip(1)); // oldest first
        }

        [Fact]
        public void Valid_Invoices_Inside_The_List_Cooldown_Do_Not_Take_A_Slot()
        {
            var recent = Inv("RECENT", "Valid", InvoiceSyncRules.UiValidRefreshMinutes - 1);
            var due = Inv("DUE", "Valid", InvoiceSyncRules.UiValidRefreshMinutes + 5);

            var batch = InvoiceListsModel.PickSessionSyncBatch(new[] { recent, due }, Now);

            Assert.Equal(new[] { due }, batch);
        }
    }
}
