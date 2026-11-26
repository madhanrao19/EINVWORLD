using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using eInvWorld.Models;
using eInvWorld.Models.InputModel;
using eInvWorld.Services.Mappers;
using EINVWORLD.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static EINVWORLD.Tests.Integration.InvoiceEditAccessGuardTests;

namespace EINVWORLD.Tests.Integration
{
    /// <summary>
    /// DR313 (Prod, 2026-11-26): a credit note must send LHDN the original's e-Invoice number, looked up by Reference UUID
    /// within the issuing company, not the typed Reference Document Number. Real SQL Server, real mapper.
    /// </summary>
    public class AdjustmentOriginalReferenceTests : IClassFixture<SqlServerFixture>
    {
        private readonly SqlServerFixture _fx;
        public AdjustmentOriginalReferenceTests(SqlServerFixture fx) => _fx = fx;

        [Fact]
        public async Task Credit_Note_Sends_The_Originals_Number_And_Never_Another_Companys()
        {
            if (!_fx.Available) return;
            await using var ctx = _fx.CreateContext();
            if (!await ctx.UnitTypes.AnyAsync(u => u.Code == "C62"))
                ctx.UnitTypes.Add(new UnitType { Code = "C62", Name = "One", IsActive = true });
            if (!await ctx.Set<Status>().AnyAsync(s => s.StatusCode == "Draft"))
                ctx.Set<Status>().Add(new Status { StatusCode = "Draft", StatusType = "Internal", Name = "Draft" });
            var own = await CreateValidPartyInfoAsync(ctx, "DR313 Own Co");
            var buyer = await CreateValidPartyInfoAsync(ctx, "DR313 Buyer Co");
            var other = await CreateValidPartyInfoAsync(ctx, "DR313 Other Co");
            ctx.PartyInfos.AddRange(own, buyer, other);
            await ctx.SaveChangesAsync();

            string NewUuid() => $"U{Guid.NewGuid():N}"[..26];
            string AddSaved(int supplierId, string uuid, string docType = "01", string? refUuid = null)
            {
                var no = $"DR-{Guid.NewGuid():N}"[..20];
                ctx.InvoiceHeaders.Add(new InvoiceHeader
                {
                    InvoiceNo = no, PrefixedID = no, DocTypeCode = docType, Currency = "MYR", CreatedDate = DateTime.Now,
                    CreatedBy = "integration-test", InternalStatusId = "Draft", SupplierId = supplierId, CustomerId = buyer.PartyInfoId,
                    UUID = uuid, RefUUID = refUuid, RefDocumentNo = "CN-REF/26", IssueDate = DateTime.Now,
                });
                return no;
            }
            var ownUuid = NewUuid();
            var otherUuid = NewUuid();
            var original = AddSaved(own.PartyInfoId, ownUuid);
            AddSaved(other.PartyInfoId, otherUuid);
            var goodNote = AddSaved(own.PartyInfoId, NewUuid(), "02", ownUuid);
            var foreignNote = AddSaved(own.PartyInfoId, NewUuid(), "02", otherUuid);   // UUID of another company's invoice
            await ctx.SaveChangesAsync();

            // The mapper sends the original's number, whatever was typed.
            var note = InvoiceMapperTests.Header("02", InvoiceMapperTests.LineWithTax(1, 10, 0));
            // As Create/Edit build it (CreateInvoiceHeader): the party objects, but SupplierId left null.
            note.Supplier.PartyInfoId = own.PartyInfoId; note.Customer.PartyInfoId = buyer.PartyInfoId;
            note.SupplierId = null; note.CustomerId = null;
            note.RefUUID = ownUuid; note.RefDocumentNo = "CN-REF/26";
            Assert.Equal(original, ReferencedId(new InvoiceMapper(ctx).MapToJsonModel(note)));

            // Another company's UUID is never resolved: the typed text goes, and the check warns.
            note.RefUUID = otherUuid;
            Assert.Equal("CN-REF/26", ReferencedId(new InvoiceMapper(ctx).MapToJsonModel(note)));

            static bool Warns(System.Collections.Generic.List<ReadinessItem>? items) =>
                items!.Any(i => !i.Blocking && i.Field == "Invoice.RefDocumentNo");
            Assert.False(Warns(await InvoiceReadinessCheck.CheckAsync(ctx, goodNote)));
            Assert.True(Warns(await InvoiceReadinessCheck.CheckAsync(ctx, foreignNote)));
        }

        private static string? ReferencedId(string json)
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("Invoice")[0].GetProperty("BillingReference")[0]
                .GetProperty("InvoiceDocumentReference")[0].GetProperty("ID")[0].GetProperty("_").GetString();
        }
    }
}
