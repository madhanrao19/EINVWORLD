using eInvWorld.Data;
using eInvWorld.Models.InputModel;
using eInvWorld.Models.JsonModels;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace EINVWORLD.Helpers
{
    public static class ParsePartyInfoFromJson
    {
        // ✅ Helper to prevent NULLs and String Truncation Database Exceptions
        private static string? SafeString(string? input, int maxLength, string? fallback = null)
        {
            if (string.IsNullOrWhiteSpace(input)) return fallback;
            return input.Length > maxLength ? input.Substring(0, maxLength) : input;
        }

        public static PartyInfo? Extract(JToken? partyJson, string? fallbackTin = null)
        {
            if (partyJson == null) return null;

            string? GetIdByScheme(string scheme) =>
                partyJson["PartyIdentification"]
                    ?.Children<JToken>()
                    .FirstOrDefault(x => x["ID"]?[0]?["schemeID"]?.ToString() == scheme)?["ID"]?[0]?["_"]?.ToString();

            string? GetAddressLine(int index) =>
                partyJson["PostalAddress"]?[0]?["AddressLine"]?[index]?["Line"]?[0]?["_"]?.ToString();

            // 1. Extract RegTypeCode dynamically
            string? regNo = null;
            string? regTypeCode = null;

            var partyIds = partyJson["PartyIdentification"]?.Children<JToken>();
            if (partyIds != null)
            {
                foreach (var pId in partyIds)
                {
                    var scheme = pId["ID"]?[0]?["schemeID"]?.ToString();
                    var val = pId["ID"]?[0]?["_"]?.ToString();

                    if (!string.IsNullOrWhiteSpace(scheme) && scheme != "TIN" && scheme != "SST" && scheme != "TTX")
                    {
                        regTypeCode = scheme;
                        regNo = val;
                        break;
                    }
                }
            }

            // Provide mandatory fallback for Registration
            if (string.IsNullOrWhiteSpace(regTypeCode))
            {
                regTypeCode = "BRN";
                regNo = regNo ?? GetIdByScheme("BRN") ?? "NA";
            }

            // 2. Safely extract and format phone number to pass Regex: ^\+[0-9]{8,14}$
            string? rawPhone = partyJson["Contact"]?[0]?["Telephone"]?[0]?["_"]?.ToString();
            string cleanPhone = "+60000000000"; // Safe default
            if (!string.IsNullOrWhiteSpace(rawPhone))
            {
                var digitsOnly = new string(rawPhone.Where(char.IsDigit).ToArray());
                if (digitsOnly.Length >= 8 && digitsOnly.Length <= 14)
                {
                    cleanPhone = "+" + digitsOnly;
                }
            }

            // 3. Map with strict lengths and fallbacks matching your PartyInfo.cs attributes
            return new PartyInfo
            {
                TIN = SafeString(GetIdByScheme("TIN") ?? fallbackTin, 14) ?? string.Empty,
                RegTypeCode = SafeString(regTypeCode, 10, "BRN") ?? "BRN",
                RegNo = SafeString(regNo, 100) ?? string.Empty,
                SST = SafeString(GetIdByScheme("SST"), 35),
                TTX = SafeString(GetIdByScheme("TTX"), 17),
                CompanyName = SafeString(partyJson["PartyLegalEntity"]?[0]?["RegistrationName"]?[0]?["_"]?.ToString(), 300, "Unknown Company") ?? "Unknown Company",
                Email = SafeString(partyJson["Contact"]?[0]?["ElectronicMail"]?[0]?["_"]?.ToString(), 320),
                PhoneNo = cleanPhone,
                Addr1 = SafeString(GetAddressLine(0), 150) ?? string.Empty,
                Addr2 = SafeString(GetAddressLine(1), 150),
                Addr3 = SafeString(GetAddressLine(2), 150),
                PostalCode = SafeString(partyJson["PostalAddress"]?[0]?["PostalZone"]?[0]?["_"]?.ToString(), 50),
                CityName = SafeString(partyJson["PostalAddress"]?[0]?["CityName"]?[0]?["_"]?.ToString(), 50) ?? string.Empty,
                StateCode = SafeString(partyJson["PostalAddress"]?[0]?["CountrySubentityCode"]?[0]?["_"]?.ToString(), 50, "00") ?? "00",
                CountryCode = SafeString(partyJson["PostalAddress"]?[0]?["Country"]?[0]?["IdentificationCode"]?[0]?["_"]?.ToString(), 10, "MYS") ?? "MYS",
                IndustryClassificationCode = SafeString(partyJson["IndustryClassificationCode"]?.ToString(), 50, "00000") ?? "00000",
                BizDescription = SafeString(partyJson["IndustryClassificationCode"]?[0]?["name"]?.ToString(), 300, "N/A") ?? "N/A",

                CreatedDate = DateTime.Now,
                CreatedBy = "SystemSync",
                IsActive = true,
                IsAdminCreated = false,
                IsApproved = true
            };
        }

        public static async Task<PartyInfo?> GetOrCreatePartyInfoAsync(ApplicationDbContext dbContext, JToken? partyJson)
        {
            if (partyJson == null) return null;

            var tin = partyJson["PartyIdentification"]?.Children<JToken>()
                .FirstOrDefault(x => x["ID"]?[0]?["schemeID"]?.ToString() == "TIN")?["ID"]?[0]?["_"]?.ToString();

            if (string.IsNullOrWhiteSpace(tin))
            {
                tin = partyJson["PartyIdentification"]?[0]?["ID"]?[0]?["_"]?.ToString();
            }

            if (string.IsNullOrWhiteSpace(tin))
                return null;

            var existing = await dbContext.PartyInfos.FirstOrDefaultAsync(p => p.TIN == tin);
            if (existing != null)
                return existing;

            var newParty = Extract(partyJson, tin); // Pass TIN to fallback mechanism
            if (newParty != null)
            {
                dbContext.PartyInfos.Add(newParty);
                await dbContext.SaveChangesAsync();
            }

            return newParty;
        }

        public static PublicCustomer? ExtractPublicCustomer(JToken? partyJson, string? fallbackTin, int ownerCompanyId)
        {
            if (partyJson == null) return null;

            string? GetIdByScheme(string scheme) =>
                partyJson["PartyIdentification"]
                    ?.Children<JToken>()
                    .FirstOrDefault(x => x["ID"]?[0]?["schemeID"]?.ToString() == scheme)?["ID"]?[0]?["_"]?.ToString();

            string? GetAddressLine(int index) =>
                partyJson["PostalAddress"]?[0]?["AddressLine"]?[index]?["Line"]?[0]?["_"]?.ToString();

            // 1. Extract RegTypeCode dynamically
            string? regNo = null;
            string? regTypeCode = null;

            var partyIds = partyJson["PartyIdentification"]?.Children<JToken>();
            if (partyIds != null)
            {
                foreach (var pId in partyIds)
                {
                    var scheme = pId["ID"]?[0]?["schemeID"]?.ToString();
                    var val = pId["ID"]?[0]?["_"]?.ToString();

                    if (!string.IsNullOrWhiteSpace(scheme) && scheme != "TIN" && scheme != "SST" && scheme != "TTX")
                    {
                        regTypeCode = scheme;
                        regNo = val;
                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(regTypeCode))
            {
                regTypeCode = "BRN";
                regNo = regNo ?? GetIdByScheme("BRN") ?? "NA";
            }

            string? rawPhone = partyJson["Contact"]?[0]?["Telephone"]?[0]?["_"]?.ToString();
            string cleanPhone = "+60000000000";
            if (!string.IsNullOrWhiteSpace(rawPhone))
            {
                var digitsOnly = new string(rawPhone.Where(char.IsDigit).ToArray());
                if (digitsOnly.Length >= 8 && digitsOnly.Length <= 14)
                {
                    cleanPhone = "+" + digitsOnly;
                }
            }

            // Map to PublicCustomer instead of PartyInfo
            return new PublicCustomer
            {
                TIN = SafeString(GetIdByScheme("TIN") ?? fallbackTin, 14) ?? string.Empty,
                RegTypeCode = SafeString(regTypeCode, 10, "BRN") ?? "BRN",
                RegNo = SafeString(regNo, 100) ?? string.Empty,
                SST = SafeString(GetIdByScheme("SST"), 35),
                TTX = SafeString(GetIdByScheme("TTX"), 17),
                CompanyName = SafeString(partyJson["PartyLegalEntity"]?[0]?["RegistrationName"]?[0]?["_"]?.ToString(), 300, "Unknown Company") ?? "Unknown Company",
                Email = SafeString(partyJson["Contact"]?[0]?["ElectronicMail"]?[0]?["_"]?.ToString(), 320),
                PhoneNo = cleanPhone,
                Addr1 = SafeString(GetAddressLine(0), 150) ?? string.Empty,
                Addr2 = SafeString(GetAddressLine(1), 150),
                Addr3 = SafeString(GetAddressLine(2), 150),
                PostalCode = SafeString(partyJson["PostalAddress"]?[0]?["PostalZone"]?[0]?["_"]?.ToString(), 50),
                CityName = SafeString(partyJson["PostalAddress"]?[0]?["CityName"]?[0]?["_"]?.ToString(), 50) ?? string.Empty,
                StateCode = SafeString(partyJson["PostalAddress"]?[0]?["CountrySubentityCode"]?[0]?["_"]?.ToString(), 50, "00") ?? "00",
                CountryCode = SafeString(partyJson["PostalAddress"]?[0]?["Country"]?[0]?["IdentificationCode"]?[0]?["_"]?.ToString(), 10, "MYS") ?? "MYS",
                IndustryClassificationCode = SafeString(partyJson["IndustryClassificationCode"]?.ToString(), 50, "00000") ?? "00000",
                BizDescription = SafeString(partyJson["IndustryClassificationCode"]?[0]?["name"]?.ToString(), 300, "N/A") ?? "N/A",

                CreatedByCompanyId = ownerCompanyId, // Link exclusively to your supplier company!
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "SystemSync",
                IsActive = true,
                IsAdminCreated = true, // Bypasses InviteCode validation
                IsApproved = true
            };
        }
    }

}
