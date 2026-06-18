using eInvWorld.Models;
using eInvWorld.Models.InputModel;
using eInvWorld.Models.JsonModels;
using iTextSharp.text;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using InputModel = eInvWorld.Models.InputModel;
using JsonModels = eInvWorld.Models.JsonModels;

namespace eInvWorld.Services.Mappers
{
    public class InvoiceMapper
    {
        public string MapToJsonModel(InputModel.InvoiceHeader header)
        {
            if (header == null)
                throw new ArgumentNullException(nameof(header), "Invoice header cannot be null");

            Log.Debug("InvoiceMapper: RefUUID={RefUUID}, LineCount={LineCount}", header.RefUUID, header.InvoiceLines.Count);

            var isSelfBilledInvoice = header.DocTypeCode == "11" || header.DocTypeCode == "12" || header.DocTypeCode == "13" || header.DocTypeCode == "14";

            var supplier = header.Supplier;
            var customer = header.Customer;

            Log.Debug("InvoiceMapper: IsSelfBilled={IsSelfBilled}, SupplierTIN={SupplierTIN}, CustomerTIN={CustomerTIN}", isSelfBilledInvoice, supplier?.TIN ?? "NULL", customer?.TIN ?? "NULL");

            var root = new JsonModels.Root
            {
                _D = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2",
                _A = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2",
                _B = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2",
                Invoice = new List<JsonModels.Invoice>
                {
                    new JsonModels.Invoice
                    {
                        ID = new List<JsonModels.ID> { new JsonModels.ID { _ = header.InvoiceNo } },
                        IssueDate = new List<JsonModels.IssueDate> { new JsonModels.IssueDate {  _ = header.IssueDate?.ToUniversalTime().ToString("yyyy-MM-dd") ?? string.Empty } },
                        IssueTime = new List<JsonModels.IssueTime> { new JsonModels.IssueTime { _ = header.IssueDate?.ToUniversalTime().ToString("HH:mm:ssZ") ?? string.Empty } },
                        InvoiceTypeCode = new List<JsonModels.InvoiceTypeCode>
                        {
                            new JsonModels.InvoiceTypeCode { _ = header.DocTypeCode, listVersionID = "1.0" }
                        },
                        DocumentCurrencyCode = new List<JsonModels.DocumentCurrencyCode> { new JsonModels.DocumentCurrencyCode { _ = header.Currency } },
                    InvoicePeriod = (header.StartDate == null && header.EndDate == null && header.InvoicePeriod == null)
                        ? null!
                        : new List<JsonModels.InvoicePeriod>
                        {
                            new JsonModels.InvoicePeriod
                            {
                                StartDate = header.StartDate != null
                                    ? new List<JsonModels.StartDate> { new JsonModels.StartDate { _ = header.StartDate.Value.ToUniversalTime().ToString("yyyy-MM-dd") } }
                                    : null!,

                                EndDate = header.EndDate != null
                                    ? new List<JsonModels.EndDate> { new JsonModels.EndDate { _ = header.EndDate.Value.ToUniversalTime().ToString("yyyy-MM-dd") } }
                                    : null!,

                                Description = header.InvoicePeriod != null
                                    ? new List<JsonModels.Description> { new JsonModels.Description { _ = header.InvoicePeriod.Value.ToString() } }
                                    : null!
                            }
                        },


                        //BillingReference = new List<JsonModels.BillingReference>
                        //{
                        //    new JsonModels.BillingReference
                        //    {
                        //        AdditionalDocumentReference = new List<JsonModels.AdditionalDocumentReference>
                        //        {
                        //            new JsonModels.AdditionalDocumentReference
                        //            {
                        //                ID = new List<JsonModels.ID> { new JsonModels.ID { _ = header.RefDocumentNo } }
                        //            }
                        //        }
                        //    }
                        //},

                        BillingReference = MapBillingReference(header),
                        AccountingSupplierParty = MapSupplier(supplier!, header),
                        AccountingCustomerParty = customer != null
                            ? MapCustomer(customer, header)
                            : MapPublicCustomer(header.PublicCustomer!, header),
                        Delivery = MapDelivery(header.DeliveryParty, header),
                        PaymentMeans = new List<JsonModels.PaymentMeans>
                        {
                            new JsonModels.PaymentMeans
                            {
                                PaymentMeansCode = new List<JsonModels.PaymentMeansCode> { new JsonModels.PaymentMeansCode { _ = "" } },
                                PayeeFinancialAccount = new List<JsonModels.PayeeFinancialAccount>
                                {
                                    new JsonModels.PayeeFinancialAccount
                                    {
                                        ID = new List<JsonModels.FinancialAccountID>
                                        {
                                            // Maps Supplier's Bank Account (Max 150 chars)
                                            new JsonModels.FinancialAccountID { _ = header.BankAccountNo ?? "" }
                                        }
                                    }
                                }
                            }
                        },
                        PaymentTerms = MapPaymentTerms(header),
                        PrepaidPayment = MapPrepaidPayment(header),
                        AllowanceCharge = MapAllowanceCharges(header),
                        TaxTotal = MapTaxTotals(header),
                        InvoiceLine = header.InvoiceLines?
                            .Select((line, index) => MapInvoiceLine(line, index + 1))
                            .ToList() ?? new List<JsonModels.InvoiceLine>(),
                        LegalMonetaryTotal = MapLegalMonetaryTotal(header),
                        TaxExchangeRate = new List<JsonModels.TaxExchangeRate>
                        {
                            new JsonModels.TaxExchangeRate
                            {
                                SourceCurrencyCode = new List<JsonModels.SourceCurrencyCode> { new JsonModels.SourceCurrencyCode { _ = header.Currency } },
                                TargetCurrencyCode = new List<JsonModels.TargetCurrencyCode> { new JsonModels.TargetCurrencyCode { _ = "MYR" } },
                                CalculationRate = new List<JsonModels.CalculationRate> { new JsonModels.CalculationRate { _ = header.ExchangeRate ?? 1m } }
                            }
                        }
                    }
                }
            };


            //string generatedJson = JsonConvert.SerializeObject(root, Formatting.Indented);
            string generatedJson = JsonConvert.SerializeObject(root, new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore
            });


            Log.Debug("InvoiceMapper: Generated JSON length={Length}", generatedJson.Length);

            return generatedJson;

        }

        private void ValidatePartyInfo(PartyInfo party, string role)
        {
            if (party == null)
                throw new ArgumentNullException(nameof(party), $"{role} information is missing.");

            var errors = new List<string>();

            if (string.IsNullOrEmpty(party.IndustryClassificationCode))
                errors.Add($"{role} IndustryClassificationCode is required.");
            if (string.IsNullOrEmpty(party.BizDescription))
                errors.Add($"{role} Business Description is required.");
            if (string.IsNullOrEmpty(party.CompanyName))
                errors.Add($"{role} Company Name is required.");
            if (string.IsNullOrEmpty(party.TIN))
                errors.Add($"{role} TIN is required.");
            if (string.IsNullOrEmpty(party.RegNo))
                errors.Add($"{role} Registration Number is required.");
            if (string.IsNullOrEmpty(party.Addr1))
                errors.Add($"{role} Address Line 1 is required.");
            if (string.IsNullOrEmpty(party.CityName))
                errors.Add($"{role} City Name is required.");
            if (string.IsNullOrEmpty(party.PostalCode))
                errors.Add($"{role} Postal Code is required.");
            if (string.IsNullOrEmpty(party.CountryCode))
                errors.Add($"{role} Country Code is required.");
            if (string.IsNullOrEmpty(party.PhoneNo))
                errors.Add($"{role} Phone Number is required.");
            // 👇 This line replaces the previous email validation
            party.Email ??= ""; // ✅ Ensures empty string if no email

            if (errors.Any())
            {
                var errorMessage = $"Validation failed for {role}: {string.Join(", ", errors)}";
                Log.Warning("InvoiceMapper validation: {ErrorMessage}", errorMessage);
                throw new InvalidOperationException(errorMessage);
            }
        }


        //    private List<JsonModels.BillingReference> MapBillingReference(InputModel.InvoiceHeader header)
        //    {
        //        return new List<JsonModels.BillingReference>
        //{
        //    new JsonModels.BillingReference
        //    {
        //        InvoiceDocumentReference = header.DocTypeCode == "01"
        //            ? null // No InvoiceDocumentReference for Type "01"
        //            : new List<JsonModels.InvoiceDocumentReference>
        //            {
        //                new JsonModels.InvoiceDocumentReference
        //                {
        //                    ID = new List<JsonModels.ID>
        //                    {
        //                        new JsonModels.ID { _ = string.IsNullOrEmpty(header.RefDocumentNo) ? "NA" : header.RefDocumentNo } // Empty string if missing
        //                    },
        //                    UUID = new List<JsonModels.UUID>
        //                    {
        //                        new JsonModels.UUID { _ = string.IsNullOrEmpty(header.RefUUID) ? "NA" : header.RefUUID } // Space if missing
        //                    }
        //                }
        //            },

        //        AdditionalDocumentReference = header.DocTypeCode == "01"
        //            ? new List<JsonModels.AdditionalDocumentReference>
        //            {
        //                new JsonModels.AdditionalDocumentReference
        //                {
        //                    ID = new List<JsonModels.ID>
        //                    {
        //                        new JsonModels.ID { _ = string.IsNullOrEmpty(header.RefDocumentNo) ? "" : header.RefDocumentNo } // Empty string if missing
        //                    }
        //                }
        //            }
        //            : null // No AdditionalDocumentReference for other doc types
        //    }
        //};
        //    }

        private List<JsonModels.BillingReference> MapBillingReference(InputModel.InvoiceHeader header)
        {
            var docType = header.DocTypeCode;
            var isSelfBilled = new[] { "11", "12", "13", "14" }.Contains(docType);
            var isCreditOrDebitOrRefund = new[] { "02", "03", "04" }.Contains(docType);
            var isStandard = docType == "01";

            var billingReference = new JsonModels.BillingReference();

            // 🔹 For DocTypes 11-14 and 02/03/04: Add InvoiceDocumentReference
            if (isSelfBilled || isCreditOrDebitOrRefund)
            {
                billingReference.InvoiceDocumentReference = new List<JsonModels.InvoiceDocumentReference>
        {
            new JsonModels.InvoiceDocumentReference
            {
                ID = new List<JsonModels.ID>
                {
                    new JsonModels.ID { _ = string.IsNullOrWhiteSpace(header.RefDocumentNo) ? "NA" : header.RefDocumentNo }
                },
                UUID = new List<JsonModels.UUID>
                {
                    new JsonModels.UUID { _ = string.IsNullOrWhiteSpace(header.RefUUID) ? "NA" : header.RefUUID }
                }
            }
        };
            }

            // 🔹 For DocType 01 and 11-14: Add AdditionalDocumentReference inside BillingReference
            if (isStandard || isSelfBilled)
            {
                billingReference.AdditionalDocumentReference = new List<JsonModels.AdditionalDocumentReference>
        {
            new JsonModels.AdditionalDocumentReference
            {
                ID = new List<JsonModels.ID>
                {
                    new JsonModels.ID { _ = string.IsNullOrWhiteSpace(header.RefDocumentNo) ? "NA" : header.RefDocumentNo }
                }
            }
        };
            }

            return new List<JsonModels.BillingReference> { billingReference };
        }




        private List<JsonModels.AccountingSupplierParty> MapSupplier(PartyInfo supplier, InputModel.InvoiceHeader header)
        {
            ValidatePartyInfo(supplier, "Supplier");

            return new List<AccountingSupplierParty>
    {
        new JsonModels.AccountingSupplierParty
        {
            // Removed the empty AdditionalAccountID block to avoid validation noise
            //AdditionalAccountID = new List<JsonModels.AdditionalAccountID>
            //        {
            //            new JsonModels.AdditionalAccountID
            //            {
            //                _ = "",
            //                schemeAgencyName = ""
            //            }
            //        },
            AdditionalAccountID = null,

            Party = new List<JsonModels.Party>
            {
                new JsonModels.Party
                {
                    IndustryClassificationCode = new List<JsonModels.IndustryClassificationCode>
                    {
                        new JsonModels.IndustryClassificationCode
                        {
                            _ = supplier.IndustryClassificationCode,
                            name = supplier.BizDescription
                        }
                    },
                    // ✅ FIXED: Calling the helper to filter out empty/NA values
                    PartyIdentification = BuildPartyIdentifications(supplier, header),

                    PostalAddress = new List<JsonModels.PostalAddress>
                    {
                        new JsonModels.PostalAddress
                        {
                            CityName = new List<JsonModels.CityName> { new JsonModels.CityName { _ = supplier.CityName } },
                            PostalZone = new List<JsonModels.PostalZone> { new JsonModels.PostalZone { _ = supplier.PostalCode ?? string.Empty } },
                            CountrySubentityCode = new List<JsonModels.CountrySubentityCode> { new JsonModels.CountrySubentityCode { _ = supplier.StateCode } },
                             AddressLine = new List<JsonModels.AddressLine>
                            {
                                new JsonModels.AddressLine { Line = new List<JsonModels.Line> { new JsonModels.Line { _ = supplier.Addr1 ?? "" } } },
                                new JsonModels.AddressLine { Line = new List<JsonModels.Line> { new JsonModels.Line { _ = supplier.Addr2 ?? "" } } },
                                new JsonModels.AddressLine { Line = new List<JsonModels.Line> { new JsonModels.Line { _ = supplier.Addr3 ?? "" } } }
                            },
                            Country = new List<JsonModels.Country>
                            {
                                new JsonModels.Country
                                {
                                    IdentificationCode = new List<JsonModels.IdentificationCode>
                                    {
                                        new JsonModels.IdentificationCode
                                        {
                                            _ = supplier.CountryCode,
                                            listID = "3166-1",
                                            listAgencyID = "ISO"
                                        }
                                    }
                                }
                            }
                        }
                    },
                    PartyLegalEntity = new List<JsonModels.PartyLegalEntity>
                    {
                        new JsonModels.PartyLegalEntity
                        {
                            RegistrationName = new List<JsonModels.RegistrationName>
                            {
                                new JsonModels.RegistrationName { _ = supplier.CompanyName }
                            }
                        }
                    },
                    Contact = new List<JsonModels.Contact>
                    {
                        new JsonModels.Contact
                        {
                            Telephone = new List<JsonModels.Telephone> { new JsonModels.Telephone { _ = supplier.PhoneNo } },
                            ElectronicMail = new List<JsonModels.ElectronicMail> { new JsonModels.ElectronicMail { _ = supplier.Email ?? string.Empty } }
                        }
                    }
                }
            }
        }
    };
        }

        private List<JsonModels.AccountingCustomerParty> MapCustomer(PartyInfo customer,InputModel.InvoiceHeader header)
        {
            ValidatePartyInfo(customer, "Customer");

            return new List<JsonModels.AccountingCustomerParty>
    {
        new JsonModels.AccountingCustomerParty
        {
            Party = new List<JsonModels.Party>
            {
                new JsonModels.Party
                {
                    // ✅ FIXED: Calling the helper to filter out empty/NA values
                    PartyIdentification = BuildPartyIdentifications(customer,header),

                    PostalAddress = new List<JsonModels.PostalAddress>
                    {
                        new JsonModels.PostalAddress
                        {
                            CityName = new List<JsonModels.CityName> { new JsonModels.CityName { _ = customer.CityName } },
                            PostalZone = new List<JsonModels.PostalZone> { new JsonModels.PostalZone { _ = customer.PostalCode ?? string.Empty } },
                            CountrySubentityCode = new List<JsonModels.CountrySubentityCode> { new JsonModels.CountrySubentityCode { _ = customer.StateCode } },
                            AddressLine = new List<JsonModels.AddressLine>
                            {
                                new JsonModels.AddressLine { Line = new List<JsonModels.Line> { new JsonModels.Line { _ = customer.Addr1 ?? "" } } },
                                new JsonModels.AddressLine { Line = new List<JsonModels.Line> { new JsonModels.Line { _ = customer.Addr2 ?? "" } } },
                                new JsonModels.AddressLine { Line = new List<JsonModels.Line> { new JsonModels.Line { _ = customer.Addr3 ?? "" } } }
                            },
                            Country = new List<JsonModels.Country>
                            {
                                new JsonModels.Country
                                {
                                    IdentificationCode = new List<JsonModels.IdentificationCode>
                                    {
                                        new JsonModels.IdentificationCode
                                        {
                                            _ = customer.CountryCode,
                                            listID = "3166-1",
                                            listAgencyID = "ISO"
                                        }
                                    }
                                }
                            }
                        }
                    },
                    PartyLegalEntity = new List<JsonModels.PartyLegalEntity>
                    {
                        new JsonModels.PartyLegalEntity
                        {
                            RegistrationName = new List<JsonModels.RegistrationName>
                            {
                                new JsonModels.RegistrationName { _ = customer.CompanyName }
                            }
                        }
                    },
                    Contact = new List<JsonModels.Contact>
                    {
                        new JsonModels.Contact
                        {
                            Telephone = new List<JsonModels.Telephone> { new JsonModels.Telephone { _ = customer.PhoneNo } },
                            ElectronicMail = new List<JsonModels.ElectronicMail> { new JsonModels.ElectronicMail { _ = customer.Email ?? string.Empty } }
                        }
                    }
                }
            }
        }
    };
        }

        private List<JsonModels.PartyIdentification> BuildPartyIdentifications(PartyInfo party, InputModel.InvoiceHeader header)
        {
            var ids = new List<JsonModels.PartyIdentification>();

            // 1. TIN is Mandatory
            if (!string.IsNullOrWhiteSpace(party.TIN))
            {
                ids.Add(new JsonModels.PartyIdentification
                {
                    ID = new List<JsonModels.ID> { new JsonModels.ID { _ = party.TIN, schemeID = "TIN" } }
                });
            }

            string regNo = party.RegNo;
            string schemeID = party.RegTypeCode;

            var isSelfBilled = new[] { "11", "12", "13", "14" }.Contains(header.DocTypeCode);

            if (!string.IsNullOrWhiteSpace(regNo))
            {
                    ids.Add(new JsonModels.PartyIdentification
                    {
                        ID = new List<JsonModels.ID>
            {
                new JsonModels.ID
                {
                    _ = regNo,
                    schemeID = schemeID
                }
            }
                    });
            }


            // 3. SST (Optional - Only add if NOT "NA" and not empty)
            if (!string.IsNullOrWhiteSpace(party.SST) && party.SST.ToUpper() != "NA")
            {
                ids.Add(new JsonModels.PartyIdentification
                {
                    ID = new List<JsonModels.ID> { new JsonModels.ID { _ = party.SST, schemeID = "SST" } }
                });
            }

            // 4. TTX (Optional - Only add if NOT "NA" and not empty)
            if (!string.IsNullOrWhiteSpace(party.TTX) && party.TTX.ToUpper() != "NA")
            {
                ids.Add(new JsonModels.PartyIdentification
                {
                    ID = new List<JsonModels.ID> { new JsonModels.ID { _ = party.TTX, schemeID = "TTX" } }
                });
            }

            // 5. Authorisation Number (ONLY add if it has a value)
            if (!string.IsNullOrWhiteSpace(party.AuthorisationNumber))
            {
                ids.Add(new JsonModels.PartyIdentification
                {
                    ID = new List<JsonModels.ID> { new JsonModels.ID { _ = party.AuthorisationNumber, schemeID = "PASSPORT" } }
                });
            }

            return ids;
        }
        private List<JsonModels.AllowanceCharge> MapAllowanceCharges(InputModel.InvoiceHeader header)
        {
            // Ensure there is at least one AllowanceCharge, else return a default with zero amount
            if (header.AllowanceCharges == null || !header.AllowanceCharges.Any())
            {
                return new List<JsonModels.AllowanceCharge>
        {
            new JsonModels.AllowanceCharge
            {
                ChargeIndicator = new List<JsonModels.ChargeIndicator>
                {
                    new JsonModels.ChargeIndicator { _ = false }  // Default to 'false' if no data
                },
                AllowanceChargeReason = new List<JsonModels.AllowanceChargeReason>
                {
                    new JsonModels.AllowanceChargeReason { _ = "Default allowance charge" }
                },
                Amount = new List<JsonModels.Amount>
                {
                    new JsonModels.Amount
                    {
                        _ = 0.00m,  // Default amount set to 0.00
                        currencyID = header.Currency ?? "MYR"
                    }
                }
            }
        };
            }

            // If AllowanceCharges exist, map them correctly
            return header.AllowanceCharges.Select(ac => new JsonModels.AllowanceCharge
            {
                ChargeIndicator = new List<JsonModels.ChargeIndicator>
        {
            new JsonModels.ChargeIndicator { _ = ac.IsCharge }
        },
                AllowanceChargeReason = new List<JsonModels.AllowanceChargeReason>
        {
            new JsonModels.AllowanceChargeReason { _ = string.IsNullOrEmpty(ac.Reason) ? "No reason provided" : ac.Reason }
        },
                Amount = new List<JsonModels.Amount>
        {
            new JsonModels.Amount
            {
                _ = Math.Round(ac.Amount != 0 ? ac.Amount : 0.00m, 2),  // Ensure default 0.00 if null or 0
                currencyID = header.Currency ?? "MYR"
            }
        }
            }).ToList();
        }




        private List<JsonModels.InvoiceLine> MapInvoiceLines(List<InputModel.InvoiceLine> lines)
        {
            return lines.Select((line, index) => MapInvoiceLine(line, index + 1)).ToList();
        }

        private JsonModels.InvoiceLine MapInvoiceLine(InputModel.InvoiceLine line, int index)
        {
            decimal subtotal = (line.Quantity ?? 0) * (line.UnitPrice ?? 0);

            Log.Debug("Mapping invoice line {Index}: {Description}, Subtotal={Subtotal}", index, line.ItemDescription, subtotal);

            var taxTotal = MapLineTaxTotals(line);
            var totalTaxAmount = taxTotal?.FirstOrDefault()?.TaxAmount?.FirstOrDefault()?._ ?? 0;
            Log.Debug("Invoice line {Description} tax total: {TaxAmount}", line.ItemDescription, totalTaxAmount);

            return new JsonModels.InvoiceLine
            {
                // Unique Line ID (formatted as "001", "002", etc.)
                ID = new List<JsonModels.ID> { new JsonModels.ID { _ = index.ToString("D3") } },

                // Invoiced Quantity
                InvoicedQuantity = new List<JsonModels.InvoicedQuantity>
                {
                    new JsonModels.InvoicedQuantity
                    {
                        _ = (int)(line.Quantity ?? 0),  // Default to 0 if null
                        unitCode = line.UnitOfMeasure ?? "XUN"  // Default unit code if null
                    }
                },

                // Item Price Extension (Subtotal)
                ItemPriceExtension = new List<JsonModels.ItemPriceExtension>
                {
                    new JsonModels.ItemPriceExtension
                    {
                        Amount = new List<JsonModels.Amount>
                        {
                            new JsonModels.Amount
                            {
                                _ = Math.Round(subtotal, 2),
                                currencyID = line.InvoiceHeader?.Currency ?? "MYR"  // Default to MYR if null
                            }
                        }
                    }
                },

                // Line Extension Amount (Total before tax/discounts)
                LineExtensionAmount = new List<JsonModels.LineExtensionAmount>
                {
                    new JsonModels.LineExtensionAmount
                    {
                        _ = Math.Round(subtotal, 2),
                        currencyID = line.InvoiceHeader?.Currency ?? "MYR"
                    }
                },

                 // Price per Unit
                Price = new List<JsonModels.Price>
                {
                    new JsonModels.Price
                    {
                        PriceAmount = new List<JsonModels.PriceAmount>
                        {
                            new JsonModels.PriceAmount
                            {
                                _ = line.UnitPrice ?? 0.0m,  // Default to 0.0 if null
                                currencyID = line.InvoiceHeader?.Currency ?? "MYR"
                            }
                        }
                    }
                },

                // Item Details (Classification, Description, Origin Country)
                Item = new List<JsonModels.Item>
                {
                    new JsonModels.Item
                    {
                        CommodityClassification = new List<JsonModels.CommodityClassification>
                        {
                            new JsonModels.CommodityClassification
                            {
                                ItemClassificationCode = new List<JsonModels.ItemClassificationCode>
                                {
                                    new JsonModels.ItemClassificationCode
                                    {
                                        _ = line.ClassificationCode ?? "00000",
                                        listID = "CLASS"
                                    }
                                }
                            }
                        },
                        Description = new List<JsonModels.Description>
                        {
                            new JsonModels.Description { _ = line.ItemDescription ?? "No Description" }
                        },
                        OriginCountry = new List<JsonModels.OriginCountry>
                        {
                            new JsonModels.OriginCountry
                            {
                                IdentificationCode = new List<JsonModels.IdentificationCode>
                                {
                                    new JsonModels.IdentificationCode { _ = "MYS" }
                                }
                            }
                        }
                    }
                },

                // Tax Total for the Line Item
                TaxTotal = taxTotal, // Assign corrected TaxTotal here

                // Allowance Charge (Discounts/Charges)
                AllowanceCharge = MapLineAllowanceCharges(line)
            };
        }


        private List<JsonModels.TaxTotal> MapLineTaxTotals(InputModel.InvoiceLine line)
        {
            if (line.InvoiceTaxes == null || !line.InvoiceTaxes.Any())
            {
                Log.Debug("No taxes for invoice line: {Description}", line.ItemDescription);
                return new List<JsonModels.TaxTotal>();
            }

            decimal taxableAmount = (line.Quantity ?? 0) * (line.UnitPrice ?? 0);

            decimal totalTaxAmount = 0; // Initialize total tax amount

            var taxSubtotals = line.InvoiceTaxes.Select(t =>
            {
                // Handle missing TaxAmount or TaxPercentage
                if (t.TaxAmount == null && t.TaxPercentage != null)
                {
                    t.TaxAmount = (t.TaxPercentage.Value / 100) * taxableAmount;
                }
                else if (t.TaxAmount == null)
                {
                    t.TaxAmount = 0; // Default to 0 if not calculable
                }

                totalTaxAmount += t.TaxAmount ?? 0; // Accumulate tax amounts

                Log.Debug("Tax: Category={TaxCategory}, Percentage={TaxPercentage}, Amount={TaxAmount}", t.TaxCategory, t.TaxPercentage, t.TaxAmount);

                return new JsonModels.TaxSubtotal
                {
                    TaxableAmount = new List<JsonModels.TaxableAmount>
            {
                new JsonModels.TaxableAmount
                {
                    _ = Math.Round(taxableAmount, 2),
                    currencyID = line.InvoiceHeader?.Currency ?? "MYR"
                }
            },
                    TaxAmount = new List<JsonModels.TaxAmount>
            {
                new JsonModels.TaxAmount
                {
                    _ = Math.Round(t.TaxAmount ?? 0, 2),
                    currencyID = line.InvoiceHeader?.Currency ?? "MYR"
                }
            },
                    Percent = new List<JsonModels.Percent>
            {
                new JsonModels.Percent
                {
                    _ = t.TaxPercentage ?? 0
                }
            },
                    TaxCategory = new List<JsonModels.TaxCategory>
            {
                new JsonModels.TaxCategory
                {
                    ID = new List<JsonModels.ID>
                    {
                        new JsonModels.ID
                        {
                            _ = t.TaxCategory ?? "01"
                        }
                    },
                    TaxScheme = new List<JsonModels.TaxScheme>
                    {
                        new JsonModels.TaxScheme
                        {
                            ID = new List<JsonModels.ID>
                            {
                                new JsonModels.ID
                                {
                                    _ = "OTH",
                                    schemeID = "UN/ECE 5153",
                                    schemeAgencyID = "6"
                                }
                            }
                        }
                    },
                    TaxExemptionReason = new List<JsonModels.TaxExemptionReason>
                        { new JsonModels.TaxExemptionReason { _ = t.TaxExemptionReason ?? "" } }
                }
            }
                };
            }).ToList();

            return new List<JsonModels.TaxTotal>
        {
        new JsonModels.TaxTotal
        {
            TaxAmount = new List<JsonModels.TaxAmount>
            {
                new JsonModels.TaxAmount
                {
                    _ = Math.Round(totalTaxAmount, 2),
                    currencyID = line.InvoiceHeader?.Currency ?? "MYR"
                }
            },
                TaxSubtotal = taxSubtotals
            }
            };
        }




        private List<JsonModels.AllowanceCharge> MapLineAllowanceCharges(InputModel.InvoiceLine line)
        {
            if (line.AllowanceCharge == null || !line.AllowanceCharge.Any())
            {
                // Always return a properly structured empty AllowanceCharge if data is missing
                return new List<JsonModels.AllowanceCharge>
            {
            new JsonModels.AllowanceCharge
            {
                ChargeIndicator = new List<JsonModels.ChargeIndicator>
                {
                    new JsonModels.ChargeIndicator { _ = false }
                },
                AllowanceChargeReason = new List<JsonModels.AllowanceChargeReason>
                {
                    new JsonModels.AllowanceChargeReason { _ = "" }
                },
                MultiplierFactorNumeric = new List<JsonModels.MultiplierFactorNumeric>
                {
                    new JsonModels.MultiplierFactorNumeric { _ = 0.0m }
                },
                Amount = new List<JsonModels.Amount>
                {
                    new JsonModels.Amount { _ = 0.0m, currencyID = line.InvoiceHeader.Currency ?? "MYR" }
                }
            }
            };
            }

            // Map provided AllowanceCharge data
            return line.AllowanceCharge.Select(ac => new JsonModels.AllowanceCharge
            {
                ChargeIndicator = new List<JsonModels.ChargeIndicator>
            {
                new JsonModels.ChargeIndicator { _ = ac.IsCharge }
            },
                    AllowanceChargeReason = new List<JsonModels.AllowanceChargeReason>
            {
                new JsonModels.AllowanceChargeReason { _ = ac.Reason ?? "" }
            },
                    MultiplierFactorNumeric = new List<JsonModels.MultiplierFactorNumeric>
            {
                new JsonModels.MultiplierFactorNumeric { _ = 0.0m }
            },
                    Amount = new List<JsonModels.Amount>
            {
                new JsonModels.Amount { _ = 0.0m, currencyID = line.InvoiceHeader.Currency ?? "MYR" }
            }
                }).ToList();
            }




        private List<JsonModels.TaxTotal> MapTaxTotals(InputModel.InvoiceHeader header)
        {
            decimal totalTaxAmount = header.InvoiceLines
                .SelectMany(line => line.InvoiceTaxes ?? new List<InputModel.InvoiceTax>())
                .Sum(tax => tax.TaxAmount ?? 0);

            decimal totalTaxableAmount = header.InvoiceLines
                .Sum(line => (line.Quantity ?? 0) * (line.UnitPrice ?? 0));

            return new List<JsonModels.TaxTotal>
        {
        new JsonModels.TaxTotal
        {
            TaxAmount = new List<JsonModels.TaxAmount>
            {
                new JsonModels.TaxAmount { _ = Math.Round(totalTaxAmount, 2), currencyID = header.Currency }
            },
            TaxSubtotal = header.InvoiceLines.SelectMany(line => line.InvoiceTaxes ?? new List<InputModel.InvoiceTax>())
            .Select(tax => new JsonModels.TaxSubtotal
            {
                TaxableAmount = new List<JsonModels.TaxableAmount>
                {
                    new JsonModels.TaxableAmount { _ = Math.Round(totalTaxableAmount, 2), currencyID = header.Currency }
                },
                TaxAmount = new List<JsonModels.TaxAmount>
                {
                    new JsonModels.TaxAmount { _ = Math.Round(tax.TaxAmount ?? 0, 2), currencyID = header.Currency }
                },
                TaxCategory = new List<JsonModels.TaxCategory>
                {
                    new JsonModels.TaxCategory
                    {
                        ID = new List<JsonModels.ID> { new JsonModels.ID { _ = tax.TaxCategory ?? "01" } },
                        TaxScheme = new List<JsonModels.TaxScheme>
                        {
                            new JsonModels.TaxScheme
                            {
                                ID = new List<JsonModels.ID>
                                {
                                    new JsonModels.ID { _ = "OTH", schemeID = "UN/ECE 5153", schemeAgencyID = "6" }
                                }
                            }
                        },
                        TaxExemptionReason = new List<JsonModels.TaxExemptionReason>
                            { new JsonModels.TaxExemptionReason { _ = tax.TaxExemptionReason ?? "" } }
                                    }
                                }
                            }).ToList()
                        }
                    };
        }



        private List<JsonModels.LegalMonetaryTotal> MapLegalMonetaryTotal(InputModel.InvoiceHeader header)
        {
            decimal totalLineExtensionAmount = header.InvoiceLines.Sum(line =>
                (line.Quantity ?? 0) * (line.UnitPrice ?? 0)
            );

            decimal totalTaxAmount = header.InvoiceLines
                .SelectMany(line => line.InvoiceTaxes ?? new List<InputModel.InvoiceTax>())
                .Sum(tax => tax.TaxAmount ?? 0);

            decimal totalAllowanceCharge = Math.Round(
                 header.InvoiceLines
                     .SelectMany(line => line.AllowanceCharge ?? new List<InputModel.AllowanceCharge>())
                     .Sum(ac => ac.Amount), 2);  // ✅ Rounded to 2 decimal places

            decimal taxInclusiveAmount = Math.Round(totalLineExtensionAmount + totalTaxAmount, 2);
            decimal totalPayableAmount = Math.Round(taxInclusiveAmount - totalAllowanceCharge, 2);

            return new List<JsonModels.LegalMonetaryTotal>
        {
        new JsonModels.LegalMonetaryTotal
        {
            LineExtensionAmount = new List<JsonModels.Amount>
            {
                new JsonModels.Amount { _ = totalLineExtensionAmount, currencyID = header.Currency }
            },
            TaxExclusiveAmount = new List<JsonModels.Amount>
            {
                new JsonModels.Amount { _ = totalLineExtensionAmount, currencyID = header.Currency }
            },
            TaxInclusiveAmount = new List<JsonModels.Amount>
            {
                new JsonModels.Amount { _ = taxInclusiveAmount, currencyID = header.Currency }
            },
            AllowanceTotalAmount = new List<JsonModels.Amount>
            {
                new JsonModels.Amount { _ = totalAllowanceCharge, currencyID = header.Currency }
            },
            ChargeTotalAmount = new List<JsonModels.Amount>
            {
                new JsonModels.Amount { _ = totalPayableAmount, currencyID = header.Currency }
            },
            PayableAmount = new List<JsonModels.Amount>
            {
                new JsonModels.Amount { _ = totalPayableAmount, currencyID = header.Currency }
            },
            PayableRoundingAmount = new List<JsonModels.PayableRoundingAmount>
            {
                new JsonModels.PayableRoundingAmount { _ = 0.0m, currencyID = header.Currency }
            }
        }
        };
        }


        private List<JsonModels.Delivery> MapDelivery(DeliveryParty delivery, InputModel.InvoiceHeader header)
        {
            return new List<JsonModels.Delivery>
            {
                new JsonModels.Delivery
                {
                    DeliveryParty = new List<JsonModels.DeliveryParty>
                    {
                        new JsonModels.DeliveryParty
                        {
                            PartyLegalEntity = new List<JsonModels.PartyLegalEntity>
                            {
                                new JsonModels.PartyLegalEntity
                                {
                                    RegistrationName = new List<JsonModels.RegistrationName>
                                    {
                                        new JsonModels.RegistrationName { _ = "" }
                                    }
                                }
                            },
                            PostalAddress = new List<JsonModels.PostalAddress>
                            {
                                new JsonModels.PostalAddress
                                {
                                    CityName = new List<JsonModels.CityName> { new JsonModels.CityName { _ = "" } },
                                    PostalZone = new List<JsonModels.PostalZone> { new JsonModels.PostalZone { _ = "" } },
                                    CountrySubentityCode = new List<JsonModels.CountrySubentityCode>
                                    {
                                        new JsonModels.CountrySubentityCode { _ = "" }
                                    },
                                    AddressLine = new List<JsonModels.AddressLine>
                                    {
                                        new JsonModels.AddressLine
                                        {
                                            Line = new List<JsonModels.Line>
                                            {
                                                new JsonModels.Line { _ = "" }
                                            }
                                        },
                                        new JsonModels.AddressLine
                                        {
                                            Line = new List<JsonModels.Line>
                                            {
                                                new JsonModels.Line { _ = "" }
                                            }
                                        },
                                        new JsonModels.AddressLine
                                        {
                                            Line = new List<JsonModels.Line>
                                            {
                                                new JsonModels.Line { _ = "" }
                                            }
                                        }
                                    },
                                    Country = new List<JsonModels.Country>
                                    {
                                        new JsonModels.Country
                                        {
                                            IdentificationCode = new List<JsonModels.IdentificationCode>
                                            {
                                                new JsonModels.IdentificationCode
                                                {
                                                    _ = "",
                                                    listID = "",
                                                    listAgencyID = ""
                                                }
                                            }
                                        }
                                    }
                                }
                            },
                            PartyIdentification = new List<JsonModels.PartyIdentification>
                            {
                                new JsonModels.PartyIdentification
                                {
                                    ID = new List<JsonModels.ID>
                                    {
                                        new JsonModels.ID { _ = "", schemeID = "" }
                                    }
                                }
                            }
                        }
                    },
                    Shipment = new List<JsonModels.Shipment>
                    {
                        new JsonModels.Shipment
                        {
                            ID = new List<JsonModels.ID>
                            {
                                new JsonModels.ID { _ = header.Incoterms ?? "" }
                            },
                        FreightAllowanceCharge = new List<JsonModels.FreightAllowanceCharge>
                        {
                            new JsonModels.FreightAllowanceCharge
                            {
                                ChargeIndicator = new List<JsonModels.ChargeIndicator>
                                {
                                    new JsonModels.ChargeIndicator { _ = true }
                                },
                                AllowanceChargeReason = new List<JsonModels.AllowanceChargeReason>
                                {
                                    new JsonModels.AllowanceChargeReason { _ = "" }
                                },
                                Amount = new List<JsonModels.Amount>
                                {
                                    new JsonModels.Amount
                                    {
                                        _ =  0,
                                        currencyID = header.Currency ?? "MYR"
                                    }
                                }
                            }
                        }
                    }
                }

                }
            };
        }

        private List<JsonModels.PaymentTerms> MapPaymentTerms(InputModel.InvoiceHeader header)
        {
            return new List<JsonModels.PaymentTerms>
            {
                new JsonModels.PaymentTerms
                {
                    Note = new List<JsonModels.Note>
                    {
                        new JsonModels.Note { _ = header.PaymentTerms ?? string.Empty }
                    }
                }
            };
        }

        private List<JsonModels.PrepaidPayment> MapPrepaidPayment(InputModel.InvoiceHeader header)
        {
            return new List<JsonModels.PrepaidPayment>
            {
                new JsonModels.PrepaidPayment
                {
                    ID = new List<JsonModels.ID>
                    {
                        new JsonModels.ID { _ = header.PrepaymentReferenceNumber ?? "" }
                    },
                    PaidAmount = new List<JsonModels.PaidAmount>
                    {
                        new JsonModels.PaidAmount { _ = 0, currencyID = header.Currency ?? "MYR" }
                    },
                    PaidDate = new List<JsonModels.PaidDate>
                    {
                        new JsonModels.PaidDate { _ = DateTime.UtcNow.ToString("yyyy-MM-dd") }
                    },
                    PaidTime = new List<JsonModels.PaidTime>
                    {
                        new JsonModels.PaidTime { _ = DateTime.UtcNow.ToString("HH:mm:ssZ") }
                    }
                }
            };
        }

        // ✅ ADD THIS STATIC WRAPPER METHOD AT THE END:
        public static string GenerateJson(InvoiceHeader header)
        {
            var mapper = new InvoiceMapper();
            return mapper.MapToJsonModel(header);
        }


        private void ValidatePublicCustomer(InputModel.PublicCustomer customer, string role)
        {
            if (customer == null)
                throw new ArgumentNullException(nameof(customer), $"{role} information is missing.");

            var errors = new List<string>();

            if (string.IsNullOrEmpty(customer.IndustryClassificationCode)) errors.Add($"{role} IndustryClassificationCode is required.");
            if (string.IsNullOrEmpty(customer.BizDescription)) errors.Add($"{role} Business Description is required.");
            if (string.IsNullOrEmpty(customer.CompanyName)) errors.Add($"{role} Company Name is required.");
            if (string.IsNullOrEmpty(customer.TIN)) errors.Add($"{role} TIN is required.");
            if (string.IsNullOrEmpty(customer.RegNo)) errors.Add($"{role} Registration Number is required.");
            if (string.IsNullOrEmpty(customer.Addr1)) errors.Add($"{role} Address Line 1 is required.");
            if (string.IsNullOrEmpty(customer.CityName)) errors.Add($"{role} City Name is required.");
            if (string.IsNullOrEmpty(customer.PostalCode)) errors.Add($"{role} Postal Code is required.");
            if (string.IsNullOrEmpty(customer.StateCode)) errors.Add($"{role} State Code is required.");
            if (string.IsNullOrEmpty(customer.CountryCode)) errors.Add($"{role} Country Code is required.");
            if (string.IsNullOrEmpty(customer.PhoneNo)) errors.Add($"{role} Phone Number is required.");

            // Ensure email is not null (matching original side-effect logic)
            customer.Email ??= "";

            if (errors.Any())
            {
                var errorMessage = $"Validation failed for {role}: {string.Join(", ", errors)}";
                Log.Warning("InvoiceMapper validation: {ErrorMessage}", errorMessage);
                throw new InvalidOperationException(errorMessage);
            }
        }

        // ✅ 2. Mapping Logic for PublicCustomer (Mirrors MapCustomer)
        private List<JsonModels.AccountingCustomerParty> MapPublicCustomer(InputModel.PublicCustomer publicCustomer, InputModel.InvoiceHeader header)
        {
            ValidatePublicCustomer(publicCustomer, "Public Customer");

            return new List<JsonModels.AccountingCustomerParty>
    {
        new JsonModels.AccountingCustomerParty
        {
            Party = new List<JsonModels.Party>
            {
                new JsonModels.Party
                {
                    // Use the overload method below
                    PartyIdentification = BuildPartyIdentifications(publicCustomer, header),

                    PostalAddress = new List<JsonModels.PostalAddress>
                    {
                        new JsonModels.PostalAddress
                        {
                            CityName = new List<JsonModels.CityName> { new JsonModels.CityName { _ = publicCustomer.CityName } },
                            PostalZone = new List<JsonModels.PostalZone> { new JsonModels.PostalZone { _ = publicCustomer.PostalCode ?? string.Empty } },
                            CountrySubentityCode = new List<JsonModels.CountrySubentityCode> { new JsonModels.CountrySubentityCode { _ = publicCustomer.StateCode } },
                            AddressLine = new List<JsonModels.AddressLine>
                            {
                                new JsonModels.AddressLine { Line = new List<JsonModels.Line> { new JsonModels.Line { _ = publicCustomer.Addr1 ?? "" } } },
                                new JsonModels.AddressLine { Line = new List<JsonModels.Line> { new JsonModels.Line { _ = publicCustomer.Addr2 ?? "" } } },
                                new JsonModels.AddressLine { Line = new List<JsonModels.Line> { new JsonModels.Line { _ = publicCustomer.Addr3 ?? "" } } }
                            },
                            Country = new List<JsonModels.Country>
                            {
                                new JsonModels.Country
                                {
                                    IdentificationCode = new List<JsonModels.IdentificationCode>
                                    {
                                        new JsonModels.IdentificationCode
                                        {
                                            _ = publicCustomer.CountryCode,
                                            listID = "3166-1",
                                            listAgencyID = "ISO"
                                        }
                                    }
                                }
                            }
                        }
                    },
                    PartyLegalEntity = new List<JsonModels.PartyLegalEntity>
                    {
                        new JsonModels.PartyLegalEntity
                        {
                            RegistrationName = new List<JsonModels.RegistrationName>
                            {
                                new JsonModels.RegistrationName { _ = publicCustomer.CompanyName }
                            }
                        }
                    },
                    Contact = new List<JsonModels.Contact>
                    {
                        new JsonModels.Contact
                        {
                            Telephone = new List<JsonModels.Telephone> { new JsonModels.Telephone { _ = publicCustomer.PhoneNo } },
                            ElectronicMail = new List<JsonModels.ElectronicMail> { new JsonModels.ElectronicMail { _ = publicCustomer.Email ?? string.Empty } }
                        }
                    }
                }
            }
        }
    };
        }

        // ✅ 3. Helper Overload for PublicCustomer IDs (Mirrors BuildPartyIdentifications)
        private List<JsonModels.PartyIdentification> BuildPartyIdentifications(InputModel.PublicCustomer party, InputModel.InvoiceHeader header)
        {
            var ids = new List<JsonModels.PartyIdentification>();

            // 1. TIN is Mandatory
            if (!string.IsNullOrWhiteSpace(party.TIN))
            {
                ids.Add(new JsonModels.PartyIdentification
                {
                    ID = new List<JsonModels.ID> { new JsonModels.ID { _ = party.TIN, schemeID = "TIN" } }
                });
            }

            // 2. Registration Number
            if (!string.IsNullOrWhiteSpace(party.RegNo))
            {
                ids.Add(new JsonModels.PartyIdentification
                {
                    ID = new List<JsonModels.ID>
            {
                new JsonModels.ID
                {
                    _ = party.RegNo,
                    schemeID = party.RegTypeCode
                }
            }
                });
            }

            // 3. SST
            if (!string.IsNullOrWhiteSpace(party.SST) && party.SST.ToUpper() != "NA")
            {
                ids.Add(new JsonModels.PartyIdentification
                {
                    ID = new List<JsonModels.ID> { new JsonModels.ID { _ = party.SST, schemeID = "SST" } }
                });
            }

            // 4. TTX
            if (!string.IsNullOrWhiteSpace(party.TTX) && party.TTX.ToUpper() != "NA")
            {
                ids.Add(new JsonModels.PartyIdentification
                {
                    ID = new List<JsonModels.ID> { new JsonModels.ID { _ = party.TTX, schemeID = "TTX" } }
                });
            }

            // 5. Authorisation Number
            if (!string.IsNullOrWhiteSpace(party.AuthorisationNumber))
            {
                ids.Add(new JsonModels.PartyIdentification
                {
                    ID = new List<JsonModels.ID> { new JsonModels.ID { _ = party.AuthorisationNumber, schemeID = "PASSPORT" } }
                });
            }

            return ids;
        }
    }
}
