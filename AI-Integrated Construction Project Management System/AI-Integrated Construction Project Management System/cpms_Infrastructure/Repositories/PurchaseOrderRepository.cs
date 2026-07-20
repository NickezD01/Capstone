using cpms_Application.Repository;
using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Infrastructure.Repositories
{
    public class PurchaseOrderRepository : GenericRepository<PurchaseOrder>, IPurchaseOrderRepository
    {
        public PurchaseOrderRepository(AppDbContext context) : base(context)
        {
        }
        public async Task<PurchaseOrder?> GetWithItemsAsync(int poId)
        {
            return await _context.PurchaseOrders
                .Include(po => po.OrderLineItems) // Load danh sách vật liệu kèm theo
                .FirstOrDefaultAsync(po => po.PoId == poId);
        }
    }
}
