using cpms_Application.Repository;
using cpms_Domain.Models;

namespace cpms_Infrastructure.Repositories
{
    public class WarehouseTransferRepository : GenericRepository<WarehouseTransfer>, IWarehouseTransferRepository
    {
        public WarehouseTransferRepository(AppDbContext context) : base(context) { }
    }

    public class WarehouseTransferItemRepository : GenericRepository<WarehouseTransferItem>, IWarehouseTransferItemRepository
    {
        public WarehouseTransferItemRepository(AppDbContext context) : base(context) { }
    }
}
