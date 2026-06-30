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
        private readonly IClaimService _claimService; // Đã thêm

        // Cập nhật Constructor để nhận IClaimService
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
                // 1. Map request sang entity
                var po = _mapper.Map<PurchaseOrder>(request);

                // 2. Thiết lập thông tin mặc định
                po.OrderDate = DateTime.UtcNow;
                po.Status = "PENDING";

                decimal total = 0;

                // 4. Lưu PO để lấy PoId từ DB
                await _uow.PurchaseOrders.AddAsync(po);
                await _uow.SaveChangeAsync();

                // 5. Lưu các item con
                foreach (var item in request.Items)
                {
                    var lineItem = _mapper.Map<PurchaseOrderDetail>(item);
                    lineItem.Poid = po.Poid;
                    total += lineItem.Quantity.GetValueOrDefault() * lineItem.UnitPrice.GetValueOrDefault();

                    await _uow.OrderLineItems.AddAsync(lineItem);
                }

                po.TotalAmount = total;
                await _uow.SaveChangeAsync();

                await _uow.CommitTransactionAsync();
                return response.SetOk("Order created successfully with total amount: " + total);
            }
            catch (Exception ex)
            {
                await _uow.RollbackTransactionAsync();

                // CẢI TIẾN: Lấy chi tiết lỗi từ Database (InnerException)
                var errorMsg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return response.SetBadRequest("Error saving order: " + errorMsg);
            }
        }

        // ... (Các hàm GetAllPurchaseOrdersAsync, ApprovePurchaseOrderAsync, ImportToWarehouseAsync giữ nguyên)

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
                var po = await _uow.PurchaseOrders.GetAsync(p => p.Poid == poId);
                if (po == null) return response.SetNotFound("Order not found");

                if (po.Status != "PENDING")
                    return response.SetBadRequest("Only PENDING orders can be approved");

                po.Status = "APPROVED";

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
            // Lấy PO kèm các vật liệu
            var po = await _uow.PurchaseOrders.GetWithItemsAsync(poId);

            if (po == null || po.Status != "APPROVED")
                return new ApiResponse().SetBadRequest("Đơn hàng không tồn tại hoặc chưa được phê duyệt!");

            await _uow.BeginTransactionAsync();
            try
            {
                foreach (var item in po.PurchaseOrderDetails)
                {
                    // Kiểm tra vật liệu đã có trong kho này chưa
                    var inventory = await _uow.Inventories.GetAsync(x =>
                        x.WarehouseId == warehouseId && x.MaterialId == item.MaterialId);

                    if (inventory == null)
                    {
                        // Nếu chưa có thì thêm mới
                        await _uow.Inventories.AddAsync(new MaterialInventory
                        {
                            WarehouseId = warehouseId,
                            MaterialId = item.MaterialId,
                            Quantity = item.Quantity
                        });
                    }
                    else
                    {
                        // Nếu có rồi thì cộng dồn
                        inventory.Quantity += item.Quantity;
                        // CỰC KỲ QUAN TRỌNG: Nếu repo có .AsNoTracking(), 
                        // bạn phải gọi hàm Update để EF theo dõi thay đổi
                        _uow.Inventories.Update(inventory);
                    }
                }

                po.Status = "DELIVERED";
                await _uow.SaveChangeAsync(); // Lưu tất cả thay đổi vào DB
                await _uow.CommitTransactionAsync();

                return new ApiResponse().SetOk("Nhập kho thành công!");
            }
            catch (Exception ex)
            {
                await _uow.RollbackTransactionAsync();
                return new ApiResponse().SetBadRequest("Lỗi hệ thống: " + ex.Message);
            }
        }
    }
}
