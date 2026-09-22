using cpms_Domain.Models;

namespace cpms_Application.Interfaces
{
    // Centralizes resolution of the single active operational warehouse.
    public interface IWarehouseContext
    {
        Task<Warehouse?> GetActiveWarehouseAsync();
        Task<int?> GetActiveWarehouseIdAsync();
    }
}
