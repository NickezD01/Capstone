using cpms_Application.Interfaces;
using cpms_Domain.Models;

namespace cpms_Application.Services
{
    public sealed class WarehouseContext : IWarehouseContext
    {
        private readonly IUnitOfWork _uow;

        public WarehouseContext(IUnitOfWork uow) => _uow = uow;

        public async Task<Warehouse?> GetActiveWarehouseAsync() =>
            await _uow.Warehouses.GetAsync(w => w.IsActive);

        public async Task<int?> GetActiveWarehouseIdAsync() =>
            (await GetActiveWarehouseAsync())?.WarehouseId;
    }
}
