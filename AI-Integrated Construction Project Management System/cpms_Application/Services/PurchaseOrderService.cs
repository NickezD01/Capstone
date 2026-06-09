using AutoMapper;
using cpms_Application.Interfaces;
using cpms_Application.Request.PurchaseOrder;
using cpms_Application.Response;
using cpms_Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Services
{
    public class PurchaseOrderService : IPurchaseOrderService
    {
        private readonly IUnitOfWork _uow;
        private readonly IMapper _mapper;

        public PurchaseOrderService(IUnitOfWork uow, IMapper mapper)
        {
            _uow = uow;
            _mapper = mapper;
        }

        public async Task<ApiResponse> CreatePurchaseOrderAsync(CreatePurchaseOrderRequest request)
        {
            var response = new ApiResponse();
            await _uow.BeginTransactionAsync();
            try
            {
                var po = _mapper.Map<PurchaseOrder>(request);
                po.OrderDate = DateTime.UtcNow;
                po.Status = PurchaseOrderStatus.PENDING; // Đã sửa lỗi kiểu dữ liệu

                // Khởi tạo biến lưu tổng tiền
                decimal total = 0;

                // Lưu PO trước để lấy PoId (Database tạo khóa chính)
                await _uow.PurchaseOrders.AddAsync(po);
                await _uow.SaveChangeAsync();

                foreach (var item in request.Items)
                {
                    var lineItem = _mapper.Map<OrderLineItem>(item);
                    lineItem.PoId = po.PoId;

                    // Tính toán giá trị từng dòng
                    total += (lineItem.Quantity * lineItem.UnitPrice);

                    await _uow.OrderLineItems.AddAsync(lineItem);
                }

                // Cập nhật tổng tiền vào PO
                po.TotalAmount = total;

                await _uow.SaveChangeAsync();
                await _uow.CommitTransactionAsync();

                return response.SetOk("Order created successfully with total amount: " + total);
            }
            catch (Exception ex)
            {
                await _uow.RollbackTransactionAsync();
                return response.SetBadRequest(ex.Message);
            }
        }
        public async Task<ApiResponse> GetAllPurchaseOrdersAsync()
        {
            var response = new ApiResponse();
            try
            {
                var pos = await _uow.PurchaseOrders.GetAllAsync(null); // Lấy tất cả
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
    }
}
