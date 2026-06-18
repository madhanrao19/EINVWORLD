using System;
using System.Threading.Tasks;
using eInvWorld.Data;
using eInvWorld.Models;
using eInvWorld.Models.Audit;
using eInvWorld.Models.Document;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace eInvWorld.Helpers
{
    public class InvoiceStatusSyncHelper
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<InvoiceStatusSyncHelper> _logger;

        public InvoiceStatusSyncHelper(ApplicationDbContext dbContext, ILogger<InvoiceStatusSyncHelper> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<bool> SyncInvoiceFromDocumentSummaryAsync(
            string invoiceNo,
            DocumentSummary summary,
            string updatedBy = "System",
            bool saveImmediately = true)
        {
            var invoice = await _dbContext.InvoiceHeaders.FirstOrDefaultAsync(i => i.UUID == summary.uuid);

            if (invoice == null)
            {
                _logger.LogWarning("Invoice with UUID {UUID} not found in local database.", summary.uuid);
                return false;
            }

            bool updated = false;

            // ---- MAP ALL POSSIBLE FIELDS ----
            // Status fields
            if (!string.IsNullOrWhiteSpace(summary.status) && invoice.LHDNStatusId != summary.status)
            {
                invoice.LHDNStatusId = summary.status;
                invoice.InternalStatusId = MapLHDNStatusToInternal(summary.status);
                updated = true;
                Log(invoiceNo, "StatusSync", updatedBy, $"Updated status: {summary.status}");
            }

            // IDs
            if (!string.IsNullOrWhiteSpace(summary.uuid) && invoice.UUID != summary.uuid)
            {
                invoice.UUID = summary.uuid;
                updated = true;
                Log(invoiceNo, "UUIDSync", updatedBy, summary.uuid);
            }
            if (!string.IsNullOrWhiteSpace(summary.longId) && invoice.LongId != summary.longId)
            {
                invoice.LongId = summary.longId;
                updated = true;
                Log(invoiceNo, "LongIdSync", updatedBy, summary.longId);
            }
            if (!string.IsNullOrWhiteSpace(summary.internalId) && invoice.PrefixedID != summary.internalId)
            {
                invoice.PrefixedID = summary.internalId;
                updated = true;
                Log(invoiceNo, "InternalIdSync", updatedBy, summary.internalId);
            }

            // Monetary fields
            if (summary.totalExcludingTax.HasValue && invoice.TotalAmountExclTax != summary.totalExcludingTax)
            {
                invoice.TotalAmountExclTax = summary.totalExcludingTax.Value;
                updated = true;
                Log(invoiceNo, "SubTotalSync", updatedBy, summary.totalExcludingTax.Value.ToString());
            }
            if (summary.totalDiscount.HasValue && invoice.TotalDiscountAmount != summary.totalDiscount)
            {
                invoice.TotalDiscountAmount = summary.totalDiscount.Value;
                updated = true;
                Log(invoiceNo, "DiscountAmountSync", updatedBy, summary.totalDiscount.Value.ToString());
            }
            if (summary.totalNetAmount.HasValue && invoice.TotalNetAmount != summary.totalNetAmount)
            {
                invoice.TotalNetAmount = summary.totalNetAmount.Value;
                updated = true;
                Log(invoiceNo, "NetAmountSync", updatedBy, summary.totalNetAmount.Value.ToString());
            }
            if (summary.totalPayableAmount.HasValue && invoice.TotalPayableAmount != summary.totalPayableAmount)
            {
                invoice.TotalPayableAmount = summary.totalPayableAmount.Value;
                updated = true;
                Log(invoiceNo, "TotalPayableSync", updatedBy, summary.totalPayableAmount.Value.ToString());
            }
            // Optionally update more total fields if you use them in the DB

            // Rejection details
            if (summary.rejectRequestDateTime.HasValue)
            {
                var myRejectDate = DateTimeHelper.ToMalaysiaTime(summary.rejectRequestDateTime.Value);
                if (invoice.RejectedTimestamp != myRejectDate)
                {
                    invoice.RejectedTimestamp = myRejectDate;
                    updated = true;
                    Log(invoiceNo, "CancelledReason", updatedBy, summary.documentStatusReason);
                    Log(invoiceNo, "RejectedTimestamp", updatedBy, myRejectDate.ToString("yyyy-MM-dd hh:mm:ss tt"));
                }
            }


            if (!string.IsNullOrEmpty(summary.documentStatusReason) && invoice.RejectedReason != summary.documentStatusReason)
            {
                invoice.RejectedReason = summary.documentStatusReason;
                invoice.RejectedBy = summary.rejectedByUserId ?? "LHDN System";

                if (summary.rejectRequestDateTime.HasValue)
                {
                    invoice.RejectedTimestamp = DateTimeHelper.ToMalaysiaTime(summary.rejectRequestDateTime.Value);
                }

                updated = true;
                // This triggers the activity log entry immediately after sync
                Log(invoiceNo, "RejectedReason", updatedBy, summary.documentStatusReason);
            }

            // Date fields
            if (summary.dateTimeIssued.HasValue)
            {
                var myIssueDate = DateTimeHelper.ToMalaysiaTime(summary.dateTimeIssued.Value);
                if (invoice.IssueDate != myIssueDate)
                {
                    invoice.IssueDate = myIssueDate;
                    updated = true;
                    Log(invoiceNo, "IssueDateSync", updatedBy, myIssueDate.ToString("yyyy-MM-dd hh:mm:ss tt"));
                }
            }

            if (summary.dateTimeReceived.HasValue)
            {
                var myReceiveDate = DateTimeHelper.ToMalaysiaTime(summary.dateTimeReceived.Value);
                if (invoice.DateTimeReceived != myReceiveDate)
                {
                    invoice.DateTimeReceived = myReceiveDate;
                    updated = true;
                    Log(invoiceNo, "DateTimeReceived", updatedBy, myReceiveDate.ToString("yyyy-MM-dd hh:mm:ss tt"));
                }
            }

            if (summary.dateTimeValidated.HasValue)
            {
                var myValidateDate = DateTimeHelper.ToMalaysiaTime(summary.dateTimeValidated.Value);
                if (invoice.DateTimeValidated != myValidateDate)
                {
                    invoice.DateTimeValidated = myValidateDate;
                    updated = true;
                    Log(invoiceNo, "DateTimeValidated", updatedBy, myValidateDate.ToString("yyyy-MM-dd hh:mm:ss tt"));
                }
            }

            if (summary.cancelDateTime.HasValue)
            {
                var myCancelDate = DateTimeHelper.ToMalaysiaTime(summary.cancelDateTime.Value);
                if (invoice.CancelDateTime != myCancelDate)
                {
                    invoice.CancelDateTime = myCancelDate;
                    updated = true;
                    Log(invoiceNo, "CancelDateTime", updatedBy, myCancelDate.ToString("yyyy-MM-dd hh:mm:ss tt"));
                }
            }

            // Supplier/Customer TIN mapping
            if (!string.IsNullOrEmpty(summary.supplierTIN) && invoice.Supplier.TIN != summary.supplierTIN)
            {
                invoice.Supplier.TIN = summary.supplierTIN;
                updated = true;
                Log(invoiceNo, "SupplierTINSynced", updatedBy, summary.supplierTIN);
            }
            if (!string.IsNullOrEmpty(summary.buyerTIN) && invoice.Customer.TIN != summary.buyerTIN)
            {
                invoice.Customer.TIN = summary.buyerTIN;
                updated = true;
                Log(invoiceNo, "CustomerTINSynced", updatedBy, summary.buyerTIN);
            }

            // Type code: API returns name, we want code
            if (!string.IsNullOrEmpty(summary.typeName))
            {
                var docTypeCode = MapTypeNameToCode(summary.typeName);
                if (!string.IsNullOrEmpty(docTypeCode) && invoice.DocTypeCode != docTypeCode)
                {
                    invoice.DocTypeCode = docTypeCode;
                    updated = true;
                    Log(invoiceNo, "DocTypeCodeSync", updatedBy, $"{summary.typeName} → {docTypeCode}");
                }
            }


            // Last updated
            if (updated)
            {

                invoice.LastUpdated = DateTimeHelper.ToMalaysiaTime(DateTime.UtcNow);

                _dbContext.InvoiceHeaders.Update(invoice);
                if (saveImmediately)
                {
                    await _dbContext.SaveChangesAsync();
                    _logger.LogInformation("✅ Invoice {InvoiceNo} updated and saved in MY Time.", invoiceNo);
                }
            }

            return updated;
        }

        private void Log(string invoiceNo, string action, string by, string? remarks = null)
        {
            _dbContext.InvoiceHistories.Add(new InvoiceHistory
            {
                InvoiceNo = invoiceNo,
                Action = action,
                PerformedBy = by,
                Remarks = remarks,
                Timestamp = DateTime.UtcNow
            });
        }

        //private DateTime ToMY(DateTime? utc = null)
        //{
        //    return TimeZoneInfo.ConvertTimeFromUtc(utc ?? DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur"));
        //}

        private string MapLHDNStatusToInternal(string lhdnStatus)
        {
            // Customize as per your business rules!
            return lhdnStatus switch
            {
                "Valid" => "Valid",
                "Submitted" => "Submitted",
                "Cancelled" => "Cancelled",
                "Rejected" => "Rejected",
                "Invalid" => "Invalid",
                _ => "Unknown"
            };
        }

        private string? MapTypeNameToCode(string? typeName)
        {
            return typeName?.Trim() switch
            {
                "Invoice" => "01",
                "Credit Note" => "02",
                "Debit Note" => "03",
                "Refund Note" => "04",
                "Self-Billed Invoice" => "11",
                "Self-Billed Credit Note" => "12",
                "Self-Billed Debit Note" => "13",
                "Self-Billed Refund Note" => "14",
                _ => null
            };
        }

    }
}
