using cpms_Application;
using cpms_Domain.Models;

namespace cpms_Application.Services;

public static class CanonicalWarehousePolicy
{
    public static async Task<Warehouse?> ResolveAsync(IUnitOfWork unitOfWork)
    {
        var warehouses = await unitOfWork.Warehouses.GetAllAsync(w => !w.IsDeleted);
        return warehouses.OrderBy(w => w.WarehouseId).FirstOrDefault();
    }

    public static async Task<int?> ResolveIdAsync(IUnitOfWork unitOfWork)
    {
        var warehouse = await ResolveAsync(unitOfWork);
        return warehouse?.WarehouseId;
    }
}
