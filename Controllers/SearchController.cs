using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using eInvWorld.Data;
using System.Security.Claims;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace eInvWorld.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SearchController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public SearchController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Search([FromQuery] string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
                return Ok(new { results = new List<object>() });

            var query = q.ToLower();
            var results = new List<object>();

            var isAdmin = User.IsInRole("Admin");
            var isSupplier = User.IsInRole("Supplier");
            var isBuyer = User.IsInRole("Buyer");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // ==========================================
            // 1. GET USER'S COMPANY IDs & TINs
            // ==========================================
            int? userPartyId = null;
            List<string> userTINs = new List<string>();

            if (!isAdmin)
            {
                // Get the PartyId for the page shortcuts
                userPartyId = await _context.UserCompanies
                    .Where(uc => uc.UserId == userId)
                    .Select(uc => uc.PartyInfoId)
                    .FirstOrDefaultAsync();

                // Get the TINs for accurate Invoice filtering (matching InvoiceLists.cshtml.cs)
                userTINs = await _context.UserCompanies
                    .Where(uc => uc.UserId == userId)
                    .Include(uc => uc.PartyInfo)
                    .Select(uc => uc.PartyInfo.TIN)
                    .Distinct()
                    .ToListAsync();
            }

            // ==========================================
            // 2. SEARCH PAGES (Strictly Role-Based)
            // ==========================================
            var availablePages = new List<dynamic> {
                new { Title = "My Profile", Link = "/Profile/Index", Icon = "ri-user-settings-line" }
            };

            if (isAdmin)
            {
                availablePages.AddRange(new[] {
                    new { Title = "Dashboard Overview", Link = "/Dashboard/Dashboard", Icon = "ri-dashboard-2-line" },
                    new { Title = "State Codes", Link = "/Admin/Codes/StateCodes/ListState", Icon = "ri-list-check" },
                    new { Title = "Country Codes", Link = "/Admin/Codes/CountryCodes/ListCountry", Icon = "ri-list-check" },
                    new { Title = "Currency Codes", Link = "/Admin/Codes/CurrencyCodes/ListCurrency", Icon = "ri-list-check" },
                    new { Title = "Classification Codes", Link = "/Admin/Codes/ClassificationCodes/ListClassification", Icon = "ri-list-check" },
                    new { Title = "MSIC SubCategory Codes", Link = "/Admin/Codes/MSICSubCategoryCodes/ListMSICSubCategory", Icon = "ri-list-check" },
                    new { Title = "Payment Modes", Link = "/Admin/Codes/PaymentModes/ListPaymentMode", Icon = "ri-list-check" },
                    new { Title = "Tax Types", Link = "/Admin/Codes/TaxTypes/ListTaxType", Icon = "ri-list-check" },
                    new { Title = "Unit Types", Link = "/Admin/Codes/UnitTypes/ListUnitType", Icon = "ri-list-check" },
                    new { Title = "E-Invoice Types", Link = "/Admin/Codes/EInvoiceTypes/ListEInvoiceType", Icon = "ri-list-check" },
                    new { Title = "Item Management", Link = "/Items/Index", Icon = "ri-file-list-line" },
                    new { Title = "Create Item", Link = "/Items/Create", Icon = "ri-add-box-line" },
                    new { Title = "Manage Users", Link = "/Admin/Users/ManageUser", Icon = "ri-group-line" },
                    new { Title = "Customer Submissions", Link = "/Lead/List", Icon = "ri-user-add-line" },
                    new { Title = "Create New Company", Link = "/Suppliers/Create", Icon = "ri-building-line" },
                    new { Title = "List of Companies", Link = "/Suppliers/Index", Icon = "ri-store-line" },
                    new { Title = "Create e-Invoice", Link = "/Invoices/CreateInvoice", Icon = "ri-file-list-3-line" },
                    new { Title = "Import Invoices", Link = "/Invoices/ImportCSV", Icon = "ri-file-upload-line" },
                    new { Title = "View All e-Invoices", Link = "/Invoices/InvoiceLists", Icon = "ri-file-list-3-line" },
                    new { Title = "Invoice Sync", Link = "/Admin/InvoiceSync", Icon = "ri-tools-line" },
                    new { Title = "Manage Resource", Link = "/Admin/Resources/Manage", Icon = "ri-file-paper-2-line" },
                    new { Title = "System Logs", Link = "/Admin/Logs/Index", Icon = "ri-history-line" }
                });
            }

            if (isSupplier)
            {
                availablePages.AddRange(new[] {
                    new { Title = "Dashboard Overview", Link = "/Dashboard/Dashboard", Icon = "ri-dashboard-2-line" },
                    new { Title = "Main Dashboard", Link = "/Dashboard/MainDashboard", Icon = "ri-dashboard-2-line" },
                    new { Title = "View All e-Invoices", Link = "/Invoices/InvoiceLists", Icon = "ri-file-list-2-line" },
                    new { Title = "Received Invoices", Link = "/Invoices/InvoiceLists?invoiceDirection=Received", Icon = "ri-download-2-line" },
                    new { Title = "Create e-Invoice", Link = "/Invoices/CreateInvoice", Icon = "ri-file-add-line" },
                    new { Title = "Manage Templates", Link = "/Templates/TemplateLists", Icon = "ri-file-copy-2-line" },
                    new { Title = "Item Management", Link = "/Items/Index", Icon = "ri-file-list-line" },
                    new { Title = "Create Item", Link = "/Items/Create", Icon = "ri-add-box-line" },
                    new { Title = "List of Buyers", Link = "/PublicCustomer/List", Icon = "ri-team-line" }
                });

                if (userPartyId.HasValue)
                {
                    availablePages.Add(new { Title = "My Company Details", Link = $"/Suppliers/Details?id={userPartyId.Value}", Icon = "ri-building-line" });
                }
            }

            if (isBuyer)
            {
                availablePages.AddRange(new[] {
                    new { Title = "Dashboard Overview", Link = "/Dashboard/Dashboard", Icon = "ri-dashboard-2-line" },
                    new { Title = "View All e-Invoices", Link = "/Invoices/InvoiceLists", Icon = "ri-file-list-2-line" },
                    new { Title = "Received Invoices", Link = "/Invoices/InvoiceLists?invoiceDirection=Received", Icon = "ri-download-2-line" }
                });
            }

            var matchedPages = availablePages
                .Where(p => p.Title.ToLower().Contains(query))
                .Select(p => (object)new { Type = "Page", Data = p });

            results.AddRange(matchedPages);

            // ==========================================
            // 3. SEARCH USERS (Admin Only)
            // ==========================================
            if (isAdmin)
            {
                var users = await _context.Users
                    .Where(u => u.FullName.ToLower().Contains(query) || (u.Email != null && u.Email.ToLower().Contains(query)))
                    .Take(3)
                    .Select(u => (object)new { Type = "User", Data = new { Title = u.FullName, Subtitle = u.Email, Link = $"/Admin/Users/AddEditUser?id={u.Id}" } })
                    .ToListAsync();
                results.AddRange(users);
            }

            // ==========================================
            // 4. SEARCH INVOICES (TIN-Based Matching)
            // ==========================================

            // ✅ Include the related tables exactly like InvoiceLists does
            var invoiceQuery = _context.InvoiceHeaders
                .Include(i => i.Supplier)
                .Include(i => i.Customer)
                .Include(i => i.PublicCustomer)
                .AsQueryable();

            if (isAdmin)
            {
                // Admins see all invoices without restriction
            }
            else if (userTINs.Any())
            {
                // ✅ Safe TIN-based filtering matching your list page logic
                invoiceQuery = invoiceQuery.Where(i =>
                    (i.Supplier != null && userTINs.Contains(i.Supplier.TIN)) ||
                    (i.Customer != null && userTINs.Contains(i.Customer.TIN)) ||
                    (i.PublicCustomer != null && userTINs.Contains(i.PublicCustomer.TIN))
                );
            }
            else
            {
                // Fallback for users with no companies assigned
                invoiceQuery = invoiceQuery.Where(i => false);
            }

            var invoices = await invoiceQuery
                .Where(i => i.InvoiceNo.ToLower().Contains(query) ||
                           (i.UUID != null && i.UUID.ToLower().Contains(query)))
                .Take(5) // Increased from 3 to 5 since users might have many sent/received matches
                .Select(i => (object)new
                {
                    Type = "Invoice",
                    Data = new
                    {
                        Title = i.InvoiceNo,
                        Subtitle = "Status: " + i.InternalStatusId,
                        Link = (i.UUID != null && i.UUID.Length > 15 && i.UUID != i.InvoiceNo && i.InternalStatusId != "Draft")
                                ? $"/Invoices/InvoiceDetails2/{i.UUID}"
                                : $"/Invoices/InvoiceDetails2/{i.InvoiceNo}"
                    }
                })
                .ToListAsync();

            results.AddRange(invoices);

            return Ok(new { results });
        }
    }
}