using eInvWorld.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace EINVWORLD.Helpers
{
    public static class EInvoiceTypeHelper
    {
        public static async Task<string?> GetCodeFromDescriptionAsync(string typeName, ApplicationDbContext context)
        {
            if (string.IsNullOrWhiteSpace(typeName)) return null;

            var normalizedTypeName = typeName.Trim().ToLower();

            return await context.EInvoiceTypes
                .Where(t => t.IsActive && t.Description.ToLower() == normalizedTypeName)
                .Select(t => t.Code)
                .FirstOrDefaultAsync();
        }

        public static async Task<string?> GetDescriptionFromCodeAsync(string code, ApplicationDbContext context)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;

            var normalizedCode = code.Trim().ToLower();

            return await context.EInvoiceTypes
                .Where(t => t.IsActive && t.Code.ToLower() == normalizedCode)
                .Select(t => t.Description)
                .FirstOrDefaultAsync();
        }
    }
}