using System;
using System.Linq;
using System.Threading.Tasks;
using eInvWorld.Helpers;
using eInvWorld.Models;
using eInvWorld.Models.InputModel;
using eInvWorld.Services;
using EINVWORLD.Services.Mappers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace EINVWORLD.Tests.Integration
{
    /// <summary>
    /// EINV100567: the validated PDF went out with no UUID, no validation date and no QR. The submit page
    /// loads the invoice (tracked) before LHDN answers, then writes UUID/LongId/status with ExecuteUpdate,
    /// and the finalizer builds the PDF on that same request's DbContext. The PDF must read the row as
    /// stored, not the stale tracked copy.
    /// </summary>
    public class ValidatedPdfFreshReadTests : IClassFixture<SqlServerFixture>
    {
        private readonly SqlServerFixture _fx;
        public ValidatedPdfFreshReadTests(SqlServerFixture fx) => _fx = fx;

        [Fact]
        public async Task PdfViewModel_ReadsUuidAndLongIdWrittenAfterTheInvoiceWasLoaded()
        {
            if (!_fx.Available) return; // skipped where no SQL Server is available
            await using var ctx = _fx.CreateContext();

            var status = await ctx.Set<Status>().Select(s => s.StatusCode).FirstAsync(); // seeded by migrations
            var invoiceNo = $"INV-PDF-{Guid.NewGuid():N}"[..20];
            ctx.InvoiceHeaders.Add(new InvoiceHeader
            {
                InvoiceNo = invoiceNo, PrefixedID = invoiceNo, DocTypeCode = "01", Currency = "MYR",
                CreatedDate = DateTime.UtcNow, CreatedBy = "integration-test", InternalStatusId = status,
            });
            await ctx.SaveChangesAsync();

            // What the submit page does: invoice loaded and tracked before submission...
            var tracked = await ctx.InvoiceHeaders.FirstAsync(i => i.InvoiceNo == invoiceNo);
            Assert.Null(tracked.UUID);
            // ...then LHDN's answer is written straight to the row.
            await ctx.InvoiceHeaders.Where(i => i.InvoiceNo == invoiceNo).ExecuteUpdateAsync(s => s
                .SetProperty(i => i.UUID, "FA47DQETVKBPB2BHBM35XG5M10")
                .SetProperty(i => i.LongId, "LONGID123")
                .SetProperty(i => i.DateTimeValidated, DateTime.Now));

            var config = new ConfigurationBuilder().Build();
            var mapper = new InvoicePdfMapper(ctx, new QRCodeGeneratorService(config), new DropdownHelper(ctx),
                NullLogger<InvoicePdfMapper>.Instance, config, Options.Create(new FilePathConfig()));

            var vm = await mapper.CreateViewModelAsync(invoiceNo);

            Assert.Equal("FA47DQETVKBPB2BHBM35XG5M10", vm!.InvoiceDetail!.UUID);
            Assert.NotNull(vm.InvoiceDetail.DateTimeValidated);
            Assert.False(string.IsNullOrEmpty(vm.QRCodeBase64)); // QR present
        }
    }
}
