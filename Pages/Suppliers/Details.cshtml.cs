using eInvWorld.Data;
using eInvWorld.Helpers;
using eInvWorld.Models;
using eInvWorld.Models.InputModel;
using EINVWORLD.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace eInvWorld.Pages.Suppliers
{
    [Authorize(Roles = "Admin,Supplier")]
    public class DetailsModel : SupplierBasePage
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private new readonly ApplicationDbContext _context;
        private readonly ILogger<DetailsModel> _logger;

        public DetailsModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager, ILogger<DetailsModel> logger)
            : base(context)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        public PartyInfo PartyInfo { get; set; } = default!;
        public string StateName { get; set; } = "";
        public string CountryName { get; set; } = "";

        public List<ApplicationUser> AssignedUsers { get; set; } = new();
        public List<ApplicationUser> AvailableUsers { get; set; } = new();
        public List<PartyInfo> AssignedSuppliers { get; set; } = new();
        public List<PartyInfo> AssignedBuyers { get; set; } = new();
        public Dictionary<string, string> UserRoles { get; set; } = new();
        public List<PartyInfo> AvailableSuppliers { get; set; } = new(); // List of Buyers to Assign
        public List<PartyInfo> AvailableBuyers { get; set; } = new(); // List of Buyers to Assign
        public List<UserCompany> AssignedUserCompanies { get; set; } = new(); // Fetch primary status

        [BindProperty(SupportsGet = true)]
        public string? From { get; set; } // example: "lead"

        [BindProperty]
        public int SelectedUserId { get; set; } // Holds the selected user ID

        [BindProperty]
        public List<int> SelectedBuyerIds { get; set; } = new(); // Holds the selected buyer IDs

        public List<eInvWorld.Models.InputModel.PublicCustomer> AvailablePublicBuyers { get; set; } = new();

        [BindProperty]
        public List<int> SelectedPublicBuyerIds { get; set; } = new();
        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
                return NotFound();

            PartyInfo = await _context.PartyInfos
                .FirstOrDefaultAsync(m => m.PartyInfoId == id) ?? null!;

            if (PartyInfo == null)
                return NotFound();
            var currentUserId = _userManager.GetUserId(User);

            StateName = await _context.StateCodes
                .Where(s => s.Code == PartyInfo.StateCode)
                .Select(s => s.State)
                .FirstOrDefaultAsync() ?? "Unknown State";

            CountryName = await _context.CountryCodes
                .Where(c => c.Code == PartyInfo.CountryCode)
                .Select(c => c.Country)
                .FirstOrDefaultAsync() ?? "Unknown Country";

            AssignedUsers = await _context.UserCompanies
                   .Where(uc => uc.PartyInfoId == id && uc.UserId != currentUserId)
                   .Include(uc => uc.User)
                   .Select(uc => uc.User)
                   .ToListAsync();
            foreach (var u in AssignedUsers)
            {
                var roles = await _userManager.GetRolesAsync(u);
                UserRoles[u.Id] = roles.FirstOrDefault() ?? "User";
            }
            AssignedUserCompanies = await _context.UserCompanies
                   .Where(uc => uc.PartyInfoId == id && uc.UserId != currentUserId)
                   .ToListAsync();

            AvailableUsers = await _context.Users
                .Where(u => !AssignedUsers.Select(au => au.Id).Contains(u.Id))
                .Select(u => new ApplicationUser
                {
                    Id = u.Id,
                    FullName = u.FullName, // Ensure this property exists in ApplicationUser
                    Position = u.Position,
                    ProfilePicture = u.ProfilePicture
                })
                .ToListAsync();

            // Step 2: Fetch all General TINs dynamically (General TINs start with "EI0000000000")
            var generalTINs = await _context.PartyInfos
                      .Where(p => p.TIN.StartsWith("EI0000000000"))
                      .ToListAsync();

            // Step 1: Fetch assigned buyers from SupplierBuyers
            var assignedStandardBuyers = await _context.SupplierBuyers
                 .Where(sb => sb.SupplierId == id && sb.BuyerId != null)
                 .Include(sb => sb.Buyer)
                 .Select(sb => sb.Buyer)
                 .Where(b => b != null && b.TIN != "EI0000000030")
                 .ToListAsync();

            // Step 1b: Fetch assigned Public Buyers (PublicCustomer) and MAP them to PartyInfo
            var assignedPublicLinks = await _context.SupplierBuyers
                 .Where(sb => sb.SupplierId == id && sb.PublicCustomerId != null)
                 .Include(sb => sb.PublicCustomer)
                 .Select(sb => sb.PublicCustomer)
                 .ToListAsync();

            var mappedPublicBuyers = assignedPublicLinks
                .Where(pc => pc != null)
                .Select(pc => new PartyInfo
                {
                    PartyInfoId = pc!.PublicCustomerId, // Use PublicCustomerId as ID so Unassign works
                    CompanyName = pc.CompanyName + " (Buyers) ", // Add suffix to distinguish
                    TIN = pc.TIN,
                    Email = pc.Email,
                    PhoneNo = pc.PhoneNo,
                    CityName = pc.CityName,
                    StateCode = pc.StateCode,
                    CountryCode = pc.CountryCode,
                    // Map other essential fields to prevent null errors in View
                    Addr1 = pc.Addr1 ?? "",
                    RegNo = pc.RegNo ?? ""
                }).ToList();

            // Step 1c: Combine them into one list
            var assignedBuyers = assignedStandardBuyers.Concat(mappedPublicBuyers).ToList();

            var assignedSuppliers = await _context.SupplierBuyers
                .Where(sb => sb.BuyerId == id)
                .Include(sb => sb.Supplier)
                .Select(sb => sb.Supplier)
                .Where(s => s.TIN != "EI0000000020")
                .ToListAsync();



            //  Group by TIN to avoid ID collisions between PartyInfo and PublicCustomer tables
            AssignedBuyers = assignedBuyers.Where(x => x != null).Cast<PartyInfo>().Concat(generalTINs)
                .GroupBy(b => b.TIN)
                .Select(g => g.First())
                .ToList();

            AssignedSuppliers = assignedSuppliers.Concat(generalTINs)
                .GroupBy(s => s.PartyInfoId)
                .Select(g => g.First())
                .ToList();


            // Define the IDs that should be permanently hidden from the available buyers list
            var excludedBuyerIds = new List<int> { 2, 3, 4, 5, id.Value };

            // Step 2: Fetch all regular available buyers
            var regularBuyers = await _context.PartyInfos
                .Where(p => !_context.SupplierBuyers.Any(sb => sb.SupplierId == id && sb.BuyerId == p.PartyInfoId)
                            && !excludedBuyerIds.Contains(p.PartyInfoId))
                .ToListAsync();

            // Step 3: Merge General TINs and Regular Buyers (Excluding the mandatory ones)
            var availableBuyers = await _context.PartyInfos
                .Where(p => !_context.SupplierBuyers.Any(sb => sb.SupplierId == id && sb.BuyerId == p.PartyInfoId)
                            && p.TIN != "EI0000000030"
                            && !excludedBuyerIds.Contains(p.PartyInfoId))
                .ToListAsync();

            var availableSuppliers = await _context.PartyInfos
                .Where(p => !_context.SupplierBuyers.Any(sb => sb.BuyerId == id && sb.SupplierId == p.PartyInfoId) && p.TIN != "EI0000000020")
                .ToListAsync();

            AvailableBuyers = generalTINs.Concat(availableBuyers).ToList();
            AvailableSuppliers = generalTINs.Concat(availableSuppliers).ToList();

            // New Public Buyers
            var assignedTINs = AssignedBuyers.Select(b => b.TIN).ToHashSet();

            // Fetch PublicCustomers created by this specific Supplier (Company)
            // And exclude those who are already assigned (checked by TIN)
            AvailablePublicBuyers = await _context.PublicCustomers
                .Where(pc => pc.CreatedByCompanyId == id && !assignedTINs.Contains(pc.TIN))
                .ToListAsync();
            return Page();
        }

        // Handler: Mutually Exclusive Supplier Permissions
        public async Task<IActionResult> OnPostSetSupplierPermissionAsync(string userId, int companyId, string type, bool isChecked)
        {
            var assignment = await _context.UserCompanies
                .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.PartyInfoId == companyId);

            if (assignment == null)
                return new JsonResult(new { success = false, message = "User not assigned." });

            // ✅ ENFORCE MUTUALLY EXCLUSIVE ROLES: One must always be true, the other false
            if (type == "access")
            {
                assignment.HasCompanyAccess = isChecked;
                assignment.IsViewOnly = !isChecked;
            }
            else if (type == "view")
            {
                assignment.IsViewOnly = isChecked;
                assignment.HasCompanyAccess = !isChecked;
            }

            await _context.SaveChangesAsync();
            return new JsonResult(new { success = true });
        }

        // Replace your existing OnPostToggleViewOnlyAsync with this:
        public async Task<IActionResult> OnPostToggleViewOnlyAsync(string userId, int companyId, bool isViewOnly)
        {
            var assignment = await _context.UserCompanies.FirstOrDefaultAsync(uc => uc.UserId == userId && uc.PartyInfoId == companyId);
            if (assignment == null) return new JsonResult(new { success = false, message = "Assignment not found." });

            // ENFORCE MUTUALLY EXCLUSIVE ROLES
            assignment.IsViewOnly = isViewOnly;
            assignment.HasCompanyAccess = !isViewOnly;

            await _context.SaveChangesAsync();

            return new JsonResult(new { success = true });
        }

        public async Task<IActionResult> OnPostSetPrimaryCompanyAsync([FromForm] string userId, [FromForm] int companyId, [FromForm] bool isPrimary)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return new JsonResult(new { success = false, message = "Invalid user ID." });
            }

            var userCompany = await _context.UserCompanies
                .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.PartyInfoId == companyId);

            if (userCompany == null)
            {
                return new JsonResult(new { success = false, message = "User is not assigned to this company." });
            }

            // If setting as primary, remove primary from other companies
            if (isPrimary)
            {
                var existingPrimary = await _context.UserCompanies
                    .Where(uc => uc.UserId == userId && uc.IsPrimaryCompany)
                    .ToListAsync();

                foreach (var uc in existingPrimary)
                {
                    uc.IsPrimaryCompany = false;
                }
            }

            // Update primary status
            userCompany.IsPrimaryCompany = isPrimary;
            await _context.SaveChangesAsync();

            return new JsonResult(new { success = true, message = "Primary company updated successfully." });
        }

        public async Task<IActionResult> OnPostCreateUserAsync([FromForm] int supplierId, [FromForm] string NewUserFullName, [FromForm] string NewUserEmail, [FromForm] string NewUserPassword)
        {
            // 1. Check existing
            var existingUser = await _userManager.FindByEmailAsync(NewUserEmail);
            if (existingUser != null)
            {
                return new JsonResult(new { success = false, message = "Email already registered. Use 'Assign Existing'." });
            }

            // 2. Create User
            var newUser = new ApplicationUser
            {
                UserName = NewUserEmail,
                Email = NewUserEmail,
                FullName = NewUserFullName,
                EmailConfirmed = true,
                IsApproved = true, // Immediately approve the user so they can log in
                UserType = "Supplier", // Sync custom UserType field

                // ADD AUDIT FIELDS HERE
                UpdatedBy = User.Identity?.Name ?? "System",
                UpdatedDate = DateTime.Now
            };

            var result = await _userManager.CreateAsync(newUser, NewUserPassword);

            if (result.Succeeded)
            {
                // THIS IS THE MISSING LINE! Assign the actual Identity Role
                await _userManager.AddToRoleAsync(newUser, "Supplier");

                // 3. Auto-Assign
                var newAssignment = new UserCompany
                {
                    UserId = newUser.Id,
                    PartyInfoId = supplierId,
                    HasCompanyAccess = true, // Grant access immediately
                    IsViewOnly = false,
                    IsPrimaryCompany = true  // Set to true so this becomes their primary company
                };

                _context.UserCompanies.Add(newAssignment);
                await _context.SaveChangesAsync();

                return new JsonResult(new { success = true, message = "User created, approved, and assigned as primary!" });
            }

            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return new JsonResult(new { success = false, message = "Error: " + errors });
        }
        public async Task<IActionResult> OnPostChangeUserRoleAsync([FromForm] string userId, [FromForm] string roleName)
        {
            if (roleName != "Supplier" && roleName != "Buyer")
            {
                return new JsonResult(new { success = false, message = "Invalid role selection." });
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return new JsonResult(new { success = false, message = "User not found." });
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            if (currentRoles.Any())
            {
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
            }

            await _userManager.AddToRoleAsync(user, roleName);
            user.UserType = roleName;

            // ADD AUDIT FIELDS HERE
            user.UpdatedBy = User.Identity?.Name ?? "System";
            user.UpdatedDate = DateTime.Now;

            await _userManager.UpdateAsync(user);

            return new JsonResult(new { success = true, message = $"Role successfully updated to {roleName}." });
        }
        public async Task<IActionResult> OnPostAssignUserAsync([FromForm] int supplierId, [FromForm] string? SelectedUserId, [FromForm] string? UserEmail)
        {
            _logger.LogDebug("Assign user: SupplierId={SupplierId}, SelectedUserId={SelectedUserId}, UserEmail={UserEmail}", supplierId, SelectedUserId, UserEmail);

            bool isAdmin = User.IsInRole("Admin");
            ApplicationUser? user = null;

            // Logic for Admin (Dropdown ID) vs Supplier (Email Search)
            if (isAdmin)
            {
                if (string.IsNullOrEmpty(SelectedUserId))
                {
                    return new JsonResult(new { success = false, message = "Please select a user." });
                }

                user = await _context.Users.FirstOrDefaultAsync(u => u.Id == SelectedUserId);
            }
            else
            {
                if (string.IsNullOrEmpty(UserEmail))
                {
                    return new JsonResult(new { success = false, message = "Please enter an email address." });
                }

                user = await _userManager.FindByEmailAsync(UserEmail);
            }

            // Check if a user was successfully found
            if (user == null)
            {
                return new JsonResult(new
                {
                    success = false,
                    message = isAdmin ? "User not found." : "No existing user found with this email address."
                });
            }

            // Check if the user is already assigned to this company
            var existingAssignment = await _context.UserCompanies
                .FirstOrDefaultAsync(uc => uc.UserId == user.Id && uc.PartyInfoId == supplierId);

            if (existingAssignment != null)
            {
                return new JsonResult(new { success = false, message = "User is already assigned to this company." });
            }

            // Assign user to the company
            var newAssignment = new UserCompany
            {
                UserId = user.Id,
                PartyInfoId = supplierId,
                HasCompanyAccess = true, // Optionally grant standard access by default
                IsViewOnly = false
            };

            _context.UserCompanies.Add(newAssignment);
            await _context.SaveChangesAsync();

            return new JsonResult(new { success = true, message = "User assigned successfully." });
        }


        public async Task<IActionResult> OnPostUnassignBuyerAsync(
            int supplierId,
            int buyerId,
            string buyerType)
        {
            SupplierBuyer? assignment = null;

            if (buyerType == "PI")
            {
                assignment = await _context.SupplierBuyers
                    .FirstOrDefaultAsync(sb =>
                        sb.SupplierId == supplierId &&
                        sb.BuyerId == buyerId);
            }
            else if (buyerType == "PC")
            {
                assignment = await _context.SupplierBuyers
                    .FirstOrDefaultAsync(sb =>
                        sb.SupplierId == supplierId &&
                        sb.PublicCustomerId == buyerId);
            }
            else
            {
                return new JsonResult(new { success = false, message = "Invalid buyer type." });
            }

            if (assignment == null)
                return new JsonResult(new { success = false, message = "Assignment not found." });

            _context.SupplierBuyers.Remove(assignment);
            await _context.SaveChangesAsync();

            return new JsonResult(new { success = true });
        }

        public async Task<IActionResult> OnPostAssignBuyerAsync(int supplierId)
        {
            if (SelectedBuyerIds == null || !SelectedBuyerIds.Any())
            {
                return RedirectToPage(new { id = supplierId });
            }

            foreach (var buyerId in SelectedBuyerIds)
            {
                var existingAssignment = await _context.SupplierBuyers
                    .FirstOrDefaultAsync(sb => sb.SupplierId == supplierId && sb.BuyerId == buyerId);

                if (existingAssignment == null)
                {
                    var newAssignment = new SupplierBuyer
                    {
                        SupplierId = supplierId,
                        BuyerId = buyerId,
                        PublicCustomerId = null // Explicitly null
                    };

                    _context.SupplierBuyers.Add(newAssignment);
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToPage(new { id = supplierId });
        }

        public async Task<IActionResult> OnPostUnassignUserAsync([FromForm] int supplierId, [FromForm] string userId)
        {
            _logger.LogDebug("Unassign user: SupplierId={SupplierId}, UserId={UserId}", supplierId, userId);

            if (string.IsNullOrEmpty(userId))
            {
                return new JsonResult(new { success = false, message = "Invalid user ID." });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return new JsonResult(new { success = false, message = "User not found." });
            }

            // ✅ Remove specific assignment instead of clearing PartyInfoId
            var assignment = await _context.UserCompanies
                .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.PartyInfoId == supplierId);

            if (assignment == null)
            {
                return new JsonResult(new { success = false, message = "User is not assigned to this company." });
            }

            _context.UserCompanies.Remove(assignment);
            await _context.SaveChangesAsync();

            return new JsonResult(new { success = true, message = "User unassigned successfully." });
        }

        public async Task<IActionResult> OnPostAssignPublicBuyerAsync([FromForm] int supplierId)
        {
            // --- SECURITY CHECK START ---
            bool isAdmin = User.IsInRole("Admin");
            if (!isAdmin)
            {
                string? currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                bool hasAccess = await _context.UserCompanies
                    .AnyAsync(uc => uc.UserId == currentUserId && uc.PartyInfoId == supplierId);

                if (!hasAccess)
                {
                    return new JsonResult(new { success = false, message = "Access Denied." });
                }
            }
            // --- SECURITY CHECK END ---

            if (SelectedPublicBuyerIds == null || !SelectedPublicBuyerIds.Any())
            {
                return new JsonResult(new { success = false, message = "Please select at least one buyer from the list." });
            }

            int addedCount = 0;

            foreach (var publicBuyerId in SelectedPublicBuyerIds)
            {
                var publicBuyerTin = await _context.PublicCustomers
                    .Where(p => p.PublicCustomerId == publicBuyerId)
                    .Select(p => p.TIN)
                    .FirstOrDefaultAsync();

                var linkExists = await _context.SupplierBuyers
                    .AnyAsync(sb => sb.SupplierId == supplierId &&
                                   (sb.PublicCustomerId == publicBuyerId ||
                                   (sb.Buyer != null && sb.Buyer.TIN == publicBuyerTin)));

                if (!linkExists)
                {
                    var newAssignment = new SupplierBuyer
                    {
                        SupplierId = supplierId,
                        BuyerId = null,
                        PublicCustomerId = publicBuyerId
                    };
                    _context.SupplierBuyers.Add(newAssignment);
                    addedCount++;
                }
            }

            if (addedCount > 0)
            {
                await _context.SaveChangesAsync();
                return new JsonResult(new { success = true, message = $"{addedCount} buyer(s) assigned successfully!" });
            }

            return new JsonResult(new { success = false, message = "Selected buyers are already assigned to this supplier." });
        }
    }
}