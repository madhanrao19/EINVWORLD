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

        // Method to populate unit of measure options
        public List<SelectListItem> GetUnitOptions()
        {
            return _context.UnitTypes
                .Where(u => u.IsActive)
                .OrderBy(u => u.Code)
                .Select(u => new SelectListItem
                {
                    Value = u.Code,
                    Text = u.Name
                })
                .ToList();
        }


    }
}
