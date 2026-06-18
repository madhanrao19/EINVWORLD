using System.Collections.Generic;
using System.Threading.Tasks;
using eInvWorld.Models.InputModel;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace eInvWorld.Services
{
    public interface IBuyerService
    {
        Task AddBuyersAsync(List<Buyer> buyers);
        Task<List<SelectListItem>> GetCombinedBuyersBySupplierAsync(int supplierId);
    }
}