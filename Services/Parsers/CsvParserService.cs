// File: Services/Parsers/CsvParserService.cs
using CsvHelper;
using eInvWorld.Models.InputModel;

namespace EINVWORLD.Services.Parsers
{
    public class CsvParserService
    {
        public List<InvoiceHeader> ParseToInvoicesWithMapping(CsvReader csv, Dictionary<string, string> fieldMap)
        {
            var invoiceDict = new Dictionary<string, InvoiceHeader>();

            csv.Read();           // Move to header row
            csv.ReadHeader();     // Register headers

            while (csv.Read())
            {
                var invoiceNo = csv.GetField(fieldMap["InvoiceNo"]);

                // Optional header-level values with fallbacks
                var issueDate = DateTime.TryParse(GetFieldSafe(csv, fieldMap, "IssueDate"), out var dt) ? dt : DateTime.Today;
                var refDoc = GetFieldSafe(csv, fieldMap, "RefDocumentNo");
                var startDate = DateTime.TryParse(GetFieldSafe(csv, fieldMap, "StartDate"), out var sdt) ? sdt : issueDate;
                var endDate = DateTime.TryParse(GetFieldSafe(csv, fieldMap, "EndDate"), out var edt) ? edt : issueDate;
                var exchangeRate = decimal.TryParse(GetFieldSafe(csv, fieldMap, "ExchangeRate"), out var rate) ? rate : 1;
                var docType = GetFieldSafe(csv, fieldMap, "DocTypeCode") ?? "01";
                var invoicePeriod = Enum.TryParse<InvoicePeriodEnum>(
                    GetFieldSafe(csv, fieldMap, "InvoicePeriod")?.Replace("-", "_")?.Replace(" ", "_"),
                    ignoreCase: true,
                    out var parsedPeriod)
                    ? parsedPeriod
                    : InvoicePeriodEnum.Not_Applicable;

                var line = new InvoiceLine
                {
                    ItemCode = GetFieldSafe(csv, fieldMap, "ItemCode"),
                    ItemDescription = GetFieldSafe(csv, fieldMap, "ItemDescription") ?? string.Empty,
                    UnitOfMeasure = GetFieldSafe(csv, fieldMap, "UnitOfMeasure") ?? string.Empty,
                    Quantity = decimal.TryParse(GetFieldSafe(csv, fieldMap, "Quantity"), out var qty) ? qty : 0,
                    UnitPrice = decimal.TryParse(GetFieldSafe(csv, fieldMap, "UnitPrice"), out var price) ? price : 0,
                    DiscountAmount = decimal.TryParse(GetFieldSafe(csv, fieldMap, "DiscountAmount"), out var disc) ? disc : 0,
                    ClassificationCode = GetFieldSafe(csv, fieldMap, "ClassificationCode") ?? string.Empty,
                    InvoiceTaxes = new List<InvoiceTax>()
                };


                // Parse TaxDetails like SST:6%:12.00|...
                if (fieldMap.TryGetValue("TaxDetails", out var taxCol))
                {
                    var taxRaw = csv.GetField(taxCol);
                    if (!string.IsNullOrEmpty(taxRaw))
                    {
                        var parts = taxRaw.Split('|');
                        foreach (var part in parts)
                        {
                            var tokens = part.Split(':');
                            if (tokens.Length == 3)
                            {
                                line.InvoiceTaxes.Add(new InvoiceTax
                                {
                                    TaxCategory = tokens[0],
                                    TaxPercentage = decimal.TryParse(tokens[1].Replace("%", ""), out var pct) ? pct : 0,
                                    TaxAmount = decimal.TryParse(tokens[2], out var amt) ? amt : 0
                                });
                            }
                        }
                    }
                }

                if (!invoiceDict.ContainsKey(invoiceNo ?? string.Empty))
                {
                    var header = new InvoiceHeader
                    {
                        InvoiceNo = invoiceNo ?? string.Empty,
                        IssueDate = issueDate,
                        Incoterms = GetFieldSafe(csv, fieldMap, "Incoterms"),
                        BankAccountNo = GetFieldSafe(csv, fieldMap, "BankAccountNo"),
                        PrepaymentReferenceNumber = GetFieldSafe(csv, fieldMap, "PrePaymentReferenceNumber"),
                        RefDocumentNo = refDoc,
                        StartDate = startDate,
                        EndDate = endDate,
                        ExchangeRate = exchangeRate,
                        DocTypeCode = docType,
                        InvoicePeriod = invoicePeriod,
                        Currency = "MYR",
                        Customer = new PartyInfo
                        {
                            CompanyName = GetFieldSafe(csv, fieldMap, "CustomerName") ?? string.Empty,
                            TIN = GetFieldSafe(csv, fieldMap, "CustomerTIN") ?? string.Empty,
                            RegNo = GetFieldSafe(csv, fieldMap, "CustomerBRN") ?? string.Empty,
                            Addr1 = GetFieldSafe(csv, fieldMap, "CustomerAddr1") ?? string.Empty,
                            Addr2 = GetFieldSafe(csv, fieldMap, "CustomerAddr2"),
                            Addr3 = GetFieldSafe(csv, fieldMap, "CustomerAddr3")
                        },
                        Supplier = new PartyInfo
                        {
                            CompanyName = GetFieldSafe(csv, fieldMap, "SupplierName") ?? string.Empty,
                            AuthorisationNumber = GetFieldSafe(csv, fieldMap, "SupplierAuthorisationNo"),
                            TIN = GetFieldSafe(csv, fieldMap, "SupplierTIN") ?? string.Empty,
                            RegNo = GetFieldSafe(csv, fieldMap, "SupplierBRN") ?? string.Empty,
                            Addr1 = GetFieldSafe(csv, fieldMap, "SupplierAddr1") ?? string.Empty,
                            Addr2 = GetFieldSafe(csv, fieldMap, "SupplierAddr2"),
                            Addr3 = GetFieldSafe(csv, fieldMap, "SupplierAddr3")
                        },

                        InvoiceLines = new List<InvoiceLine>()
                    };
                    invoiceDict[invoiceNo ?? string.Empty] = header;
                }

                invoiceDict[invoiceNo ?? string.Empty].InvoiceLines.Add(line);
            }

            // Calculate totals
            return invoiceDict.Values.Select(inv =>
            {
                decimal totalExcl = 0, totalTax = 0, totalDiscount = 0;

                foreach (var line in inv.InvoiceLines)
                {
                    line.CalculateAmounts();
                    totalExcl += line.AmountExclTax ?? 0;
                    totalTax += line.InvoiceTaxes?.Sum(t => t.TaxAmount ?? 0) ?? 0;
                    totalDiscount += line.DiscountAmount ?? 0;
                }

                inv.TotalAmountExclTax = totalExcl;
                inv.TotalTaxAmount = totalTax;
                inv.TotalAmountIncTax = totalExcl + totalTax;
                inv.TotalDiscountAmount = totalDiscount;
                inv.TotalPayableAmount = inv.TotalAmountIncTax;
                inv.TotalNetAmount = inv.TotalAmountIncTax;

                return inv;
            }).ToList();
        }

        // Helper for optional fields
        private string? GetFieldSafe(CsvReader csv, Dictionary<string, string> map, string key)
        {
            return map.TryGetValue(key, out var col) ? csv.GetField(col) : null;
        }

    }
}
