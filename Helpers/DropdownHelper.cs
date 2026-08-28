using eInvWorld.Data;
using eInvWorld.Models.InputModel;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace eInvWorld.Helpers
{
    public class DropdownHelper
    {
        private readonly ApplicationDbContext _context;

        public DropdownHelper(ApplicationDbContext context)
        {
            _context = context;
        }

        // Method to populate state options
        public List<SelectListItem> GetStateOptions()
        {
            return _context.StateCodes
                .Where(s => s.IsActive)
                .Select(s => new SelectListItem
                {
                    Value = s.Code,
                    Text = s.State
                })
                .ToList();
        }

        // Method to populate country options
        public List<SelectListItem> GetCountryOptions()
        {
            return _context.CountryCodes
                .Where(s => s.IsActive)
                .Select(s => new SelectListItem
                {
                    Value = s.Code,
                    Text = s.Country
                })
                .ToList();
        }

        // Method to populate MSIC options
        public List<SelectListItem> GetMSICOptions()
        {
            return _context.MSICSubCategoryCodes
                .Where(s => s.IsActive)
                .Select(s => new SelectListItem
                {
                    Value = s.Code,
                    Text = s.Description
                })
                .ToList();
        }

        // Method to populate ID types options
        public List<SelectListItem> GetIdTypesOptions()
        {
            return _context.RegistrationTypes
                .Select(rt => new SelectListItem
                {
                    Value = rt.Code,   // Stores "NRIC", "BRN" in DB
                    Text = rt.Name     // Displays "Identification Card No."
                })
                .ToList();
        }


        // Helper method to get description for IdType enum
        private string GetDescription(IdType idType)
        {
            return idType switch
            {
                IdType.NRIC => "Identification Card No.",
                IdType.PASSPORT => "Passport No.",
                IdType.BRN => "Business Registration No.",
                IdType.ARMY => "Army No.",
                _ => string.Empty,
            };
        }

        // Method to get e-Invoice type description by code
        public string GetEInvoiceTypeDescription(string code)
        {
            return _context.EInvoiceTypes
                .Where(e => e.IsActive && e.Code == code)
                .Select(e => e.Description)
                .FirstOrDefault() ?? code; // fallback to code if not found
        }

        // Resolves a state code to its display name; falls back to the raw value when it doesn't
        // match a StateCodes row (e.g. a foreign buyer's free-text state).
        public string GetStateName(string? code)
        {
            if (string.IsNullOrEmpty(code)) return "-";
            return _context.StateCodes
                .Where(s => s.Code == code)
                .Select(s => s.State)
                .FirstOrDefault() ?? code;
        }

        // Method to populate Classification Code options
        public List<SelectListItem> GetClassificationCodeOptions()
        {
            return _context.ClassificationCodes
                .Where(c => c.IsActive)
                .OrderBy(c => c.Code)
                .Select(c => new SelectListItem
                {
                    Value = c.Code,
                    Text = c.Code + " – " + c.Description
                })
                .ToList();
        }

        // Method to populate tax category dropdown
        public List<SelectListItem> GetTaxCategoryOptions()
        {
            return _context.TaxTypes
                .Where(t => t.IsActive)
                .OrderBy(t => t.Code)
                .Select(t => new SelectListItem
                {
                    Value = t.Code,
                    Text = t.Description
                })
                .ToList();
        }

        // Curated shortlist of the MyInvois/UN-ECE unit codes that cover the vast majority of
        // SME invoicing scenarios. Kept in display priority order; every other active unit
        // remains fully available (and LHDN-compliant) under the "All Units" group.
        private static readonly string[] CommonUnitCodes =
        {
            "EA", "H87", "SET", "KGM", "GRM", "LTR", "MLT", "MTR", "MTK", "MTQ",
            "HUR", "DAY", "MON", "ANN", "PR", "DZN", "XCT", "TNE", "KWH", "KT",
            "E48", "E51", "E54", "LS", "XUN"
        };

        // Method to populate unit of measure options, grouped so the ~24 units SMEs actually
        // use surface above the full UN/ECE Rec-20 list (hundreds of codes) that LHDN requires
        // as the source of truth. Select2's built-in search still covers the full list.
        public List<SelectListItem> GetUnitOptions()
        {
            var units = _context.UnitTypes
                .Where(u => u.IsActive)
                .ToDictionary(u => u.Code, u => u.Name);

            var commonGroup = new SelectListGroup { Name = "Common Units" };
            var allGroup = new SelectListGroup { Name = "All Units" };

            var common = CommonUnitCodes
                .Where(units.ContainsKey)
                .Select(code => new SelectListItem
                {
                    Value = code,
                    Text = $"{units[code]} ({code})",
                    Group = commonGroup
                });

            var others = units.Keys
                .Except(CommonUnitCodes)
                .OrderBy(code => code)
                .Select(code => new SelectListItem
                {
                    Value = code,
                    Text = $"{units[code]} ({code})",
                    Group = allGroup
                });

            return common.Concat(others).ToList();
        }


    }
}
