using AutoMapper;
using cpms_Application.Interfaces;
using cpms_Application.Request.PurchaseOrder;
using cpms_Application.Response;
using cpms_Domain.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace cpms_Application.Services
{
    public class PurchaseOrderService : IPurchaseOrderService
    {
        private readonly IUnitOfWork _uow;
        private readonly IMapper _mapper;
        private readonly IClaimService _claimService;

        public PurchaseOrderService(IUnitOfWork uow, IMapper mapper, IClaimService claimService)
        {
            _uow = uow;
            _mapper = mapper;
            _claimService = claimService;
        }

        public async Task<ApiResponse> CreatePurchaseOrderAsync(CreatePurchaseOrderRequest request)
        {
            var response = new ApiResponse();
            await _uow.BeginTransactionAsync();
            try
            {
                // 1. Kiểm tra nhanh xem Project và Supplier truyền lên có tồn tại thật không
                var projectExists = await _uow.Projects.GetByIdAsync(request.ProjectId);
                if (projectExists == null) return response.SetBadRequest($"ProjectId {request.ProjectId} không tồn tại.");

                var supplierExists = await _uow.Suppliers.GetByIdAsync(request.SupplierId); // Giả định uow có Suppliers
                if (supplierExists == null) return response.SetBadRequest($"SupplierId {request.SupplierId} không tồn tại.");

                // 2. Map dữ liệu cơ bản
                var po = _mapper.Map<PurchaseOrder>(request);
                var userClaim = _claimService.GetUserClaim();
                po.UserAccountId = userClaim.Id;
                po.OrderDate = DateTime.UtcNow;
                po.Status = PurchaseOrderStatus.PENDING;

                decimal total = 0;

                // 3. Duyệt danh sách items và add TRỰC TIẾP vào Navigation Property của po
                foreach (var item in request.Items)
                {
                    // Kiểm tra vật tư có tồn tại không
                    var materialExists = await _uow.Materials.GetByIdAsync(item.MaterialId);
                    if (materialExists == null) return response.SetBadRequest($"MaterialId {item.MaterialId} không tồn tại.");

                    var lineItem = _mapper.Map<OrderLineItem>(item);

                    // Tính tổng tiền
                    total += (lineItem.Quantity * lineItem.UnitPrice);

                    // Nạp vào list con của po (EF Core sẽ tự động map PoId khi lưu)
                    po.OrderLineItems.Add(lineItem);
                }

                po.TotalAmount = total;

                // 4. Lưu 1 lần duy nhất cho cả đơn hàng và chi tiết đơn hàng
                await _uow.PurchaseOrders.AddAsync(po);
                await _uow.SaveChangeAsync();

                await _uow.CommitTransactionAsync();
                return response.SetOk("Order created successfully with total amount: " + total);
            }
            catch (Exception ex)
            {
                await _uow.RollbackTransactionAsync();

                // 🚀 ĐÀO SÂU XUỐNG 3 TẦNG ĐỂ LẤY LỖI GỐC TỪ SQL SERVER/POSTGRES
                var errorMsg = ex.InnerException?.InnerException != null
                    ? ex.InnerException.InnerException.Message
                    : (ex.InnerException != null ? ex.InnerException.Message : ex.Message);

                return response.SetBadRequest("Error saving order: " + errorMsg);
            }
        }

        public async Task<ApiResponse> GetAllPurchaseOrdersAsync()
        {
            var response = new ApiResponse();
            try
            {
                var pos = await _uow.PurchaseOrders.GetAllAsync(null);
                return response.SetOk(pos);
            }
            catch (Exception ex)
            {
                return response.SetBadRequest(ex.Message);
            }
        }

        public async Task<ApiResponse> ApprovePurchaseOrderAsync(int poId)
        {
            var response = new ApiResponse();
            try
            {
                var po = await _uow.PurchaseOrders.GetAsync(p => p.PoId == poId);
                if (po == null) return response.SetNotFound("Order not found");

                if (po.Status != PurchaseOrderStatus.PENDING)
                    return response.SetBadRequest("Only PENDING orders can be approved");

                po.Status = PurchaseOrderStatus.APPROVED;

                await _uow.SaveChangeAsync();
                return response.SetOk("Order approved successfully");
            }
            catch (Exception ex)
            {
                return response.SetBadRequest(ex.Message);
            }
        }

        public async Task<ApiResponse> ImportToWarehouseAsync(int poId, int warehouseId)
        {
            var po = await _uow.PurchaseOrders.GetWithItemsAsync(poId);

            if (po == null || po.Status != PurchaseOrderStatus.APPROVED)
                return new ApiResponse().SetBadRequest("Đơn hàng không tồn tại hoặc chưa được phê duyệt!");

            await _uow.BeginTransactionAsync();
            try
            {
                foreach (var item in po.OrderLineItems)
                {
                    // 🚀 ĐỒNG BỘ ERD: Tìm bản ghi tồn kho qua InventoryRecords repo
                    var inventory = await _uow.Inventories.GetAsync(x =>
                        x.WarehouseId == warehouseId && x.MaterialId == item.MaterialId);

                    if (inventory == null)
                    {
                        // 🚀 ĐỒNG BỘ ERD: Nếu chưa có vật tư này trong kho, tạo mới bản ghi InventoryRecord
                        await _uow.Inventories.AddAsync(new InventoryRecord
                        {
                            WarehouseId = warehouseId,
                            MaterialId = item.MaterialId,
                            QuantityOnHand = item.Quantity,     // Gán số lượng nhập vào thực tế
                            ReservedQuantity = 0,               // Đơn hàng mới nhập kho chưa bị dự án nào giữ chỗ
                            ReorderLevel = 10,                   // Định mức mặc định cảnh báo sắp hết hàng
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        // 🚀 ĐỒNG BỘ ERD: Cộng dồn số lượng thực tế (QuantityOnHand) và cập nhật thời gian
                        inventory.QuantityOnHand += item.Quantity;
                        inventory.UpdatedAt = DateTime.UtcNow;

                        // Gọi hàm Update để thông báo EF Core theo dõi thay đổi thực thể
                        _uow.Inventories.Update(inventory);
                    }
                }

                po.Status = PurchaseOrderStatus.DELIVERED;
                await _uow.SaveChangeAsync();
                await _uow.CommitTransactionAsync();

                return new ApiResponse().SetOk("Nhập kho thành công!");
            }
            catch (Exception ex)
            {
                await _uow.RollbackTransactionAsync();
                return new ApiResponse().SetBadRequest("Lỗi hệ thống khi nhập kho: " + ex.Message);
            }
        }
    }
}