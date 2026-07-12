using AutoMapper;
using cpms_Application.Interfaces;
using cpms_Application.Request.PurchaseOrder;
using cpms_Application.Response;
using cpms_Application.Response.PurchaseOrder;
using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
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

        // Hàm hỗ trợ Eager Loading đầy đủ thông tin để phục vụ cấu trúc Response mới
        private async Task<PurchaseOrder?> GetSavedPurchaseOrderWithDetailsAsync(int poId)
        {
            return await _uow.PurchaseOrders.GetAsync(
                filter: p => p.PoId == poId,
                include: query => query
                    .Include(p => p.Project)
                    .Include(p => p.Supplier)
                    .Include(p => p.OrderLineItems)
                        .ThenInclude(line => line.Material)
            );
        }

        public async Task<ApiResponse> CreatePurchaseOrderAsync(CreatePurchaseOrderRequest request)
        {
            var response = new ApiResponse();
            await _uow.BeginTransactionAsync();
            try
            {
                var projectExists = await _uow.Projects.GetByIdAsync(request.ProjectId);
                if (projectExists == null) return response.SetBadRequest($"ProjectId {request.ProjectId} không tồn tại.");

                var supplierExists = await _uow.Suppliers.GetByIdAsync(request.SupplierId);
                if (supplierExists == null) return response.SetBadRequest($"SupplierId {request.SupplierId} không tồn tại.");

                var po = _mapper.Map<PurchaseOrder>(request);
                var userClaim = _claimService.GetUserClaim();
                po.UserAccountId = userClaim.Id;
                po.OrderDate = DateTime.UtcNow;
                po.Status = PurchaseOrderStatus.PENDING;

                decimal total = 0;

                foreach (var item in request.Items)
                {
                    var materialExists = await _uow.Materials.GetByIdAsync(item.MaterialId);
                    if (materialExists == null)
                    {
                        await _uow.RollbackTransactionAsync();
                        return response.SetBadRequest($"MaterialId {item.MaterialId} không tồn tại.");
                    }

                    var lineItem = _mapper.Map<OrderLineItem>(item);
                    total += (lineItem.Quantity * lineItem.UnitPrice);
                    po.OrderLineItems.Add(lineItem);
                }

                po.TotalAmount = total;

                await _uow.PurchaseOrders.AddAsync(po);
                await _uow.SaveChangeAsync();
                await _uow.CommitTransactionAsync();

                var savedPo = await GetSavedPurchaseOrderWithDetailsAsync(po.PoId);
                return response.SetOk(_mapper.Map<PurchaseOrderResponse>(savedPo));
            }
            catch (Exception ex)
            {
                await _uow.RollbackTransactionAsync();
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
                var pos = await _uow.PurchaseOrders.GetAllAsync(
                    filter: null,
                    include: query => query
                        .Include(p => p.Project)
                        .Include(p => p.Supplier)
                        .Include(p => p.OrderLineItems)
                            .ThenInclude(line => line.Material)
                );
                return response.SetOk(_mapper.Map<List<PurchaseOrderResponse>>(pos));
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
                _uow.PurchaseOrders.Update(po);
                await _uow.SaveChangeAsync();

                var updatedPo = await GetSavedPurchaseOrderWithDetailsAsync(poId);
                return response.SetOk(_mapper.Map<PurchaseOrderResponse>(updatedPo));
            }
            catch (Exception ex)
            {
                return response.SetBadRequest(ex.Message);
            }
        }

        public async Task<ApiResponse> RejectPurchaseOrderAsync(int poId)
        {
            var response = new ApiResponse();
            try
            {
                var purchaseOrder = await _uow.PurchaseOrders.GetAsync(po => po.PoId == poId);
                if (purchaseOrder == null)
                    return response.SetNotFound($"Đơn mua hàng với ID = {poId} không tồn tại.");

                if (purchaseOrder.Status != PurchaseOrderStatus.PENDING)
                    return response.SetBadRequest("Chỉ có thể từ chối đơn mua hàng đang ở trạng thái chờ duyệt (Pending).");

                purchaseOrder.Status = PurchaseOrderStatus.REJECTED;
                _uow.PurchaseOrders.Update(purchaseOrder);
                await _uow.SaveChangeAsync();

                var updatedPo = await GetSavedPurchaseOrderWithDetailsAsync(poId);
                return response.SetOk(_mapper.Map<PurchaseOrderResponse>(updatedPo));
            }
            catch (Exception ex)
            {
                var deepErrorMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return response.SetBadRequest("Lỗi khi từ chối đơn hàng: " + deepErrorMessage);
            }
        }

        public async Task<ApiResponse> ImportToWarehouseAsync(int poId, int warehouseId)
        {
            var po = await _uow.PurchaseOrders.GetWithItemsAsync(poId);

            if (po == null)
                return new ApiResponse().SetNotFound("Đơn hàng không tồn tại!");

            if (po.Status == PurchaseOrderStatus.DELIVERED)
                return new ApiResponse().SetBadRequest("Đơn hàng này đã được nhập kho trước đó rồi!");

            if (po.Status != PurchaseOrderStatus.APPROVED)
                return new ApiResponse().SetBadRequest("Chỉ đơn hàng đã được phê duyệt (Approved) mới có thể nhập kho!");

            await _uow.BeginTransactionAsync();
            try
            {
                foreach (var item in po.OrderLineItems)
                {
                    var inventory = await _uow.Inventories.GetAsync(x =>
                        x.WarehouseId == warehouseId && x.MaterialId == item.MaterialId);

                    if (inventory == null)
                    {
                        await _uow.Inventories.AddAsync(new InventoryRecord
                        {
                            WarehouseId = warehouseId,
                            MaterialId = item.MaterialId,
                            QuantityOnHand = item.Quantity,
                            ReservedQuantity = 0,
                            ReorderLevel = 10,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        inventory.QuantityOnHand += item.Quantity;
                        inventory.UpdatedAt = DateTime.UtcNow;
                        _uow.Inventories.Update(inventory);
                    }
                }

                po.Status = PurchaseOrderStatus.DELIVERED;
                po.DeliveryDate = DateTime.UtcNow;
                _uow.PurchaseOrders.Update(po);

                await _uow.SaveChangeAsync();
                await _uow.CommitTransactionAsync();

                var updatedPo = await GetSavedPurchaseOrderWithDetailsAsync(poId);
                return new ApiResponse().SetOk(_mapper.Map<PurchaseOrderResponse>(updatedPo));
            }
            catch (Exception ex)
            {
                await _uow.RollbackTransactionAsync();
                return new ApiResponse().SetBadRequest("Lỗi hệ thống khi nhập kho: " + ex.Message);
            }
        }
    }
}