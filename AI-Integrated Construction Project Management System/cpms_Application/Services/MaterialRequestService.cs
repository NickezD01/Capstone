using AutoMapper;
using cpms_Application.Interfaces;
using cpms_Application.Request.MaterialRequest;
using cpms_Application.Response;
using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Services
{
    public class MaterialRequestService : IMaterialRequestService
    {
        private readonly IUnitOfWork _uow;
        private readonly IMapper _mapper;
        private readonly IClaimService _claimService;

        public MaterialRequestService(IUnitOfWork uow, IMapper mapper, IClaimService claimService)
        {
            _uow = uow;
            _mapper = mapper;
            _claimService = claimService;
        }

        public async Task<ApiResponse> CreateRequestAsync(CreateMaterialRequest request)
        {
            var response = new ApiResponse();
            try
            {
                // 1. Lấy thông tin người tạo yêu cầu từ ClaimService
                var currentUser = _claimService.GetUserClaim();

                if (request.Items == null || request.Items.Count == 0)
                    return response.SetBadRequest("Danh sách vật tư yêu cầu không được để trống.");

                // 2. Kích hoạt Transaction để đảm bảo an toàn dữ liệu khi kiểm tra và trừ kho hàng loạt
                await _uow.BeginTransactionAsync();

                // 3. Khởi tạo Master Record (MaterialRequest)
                var materialRequest = new MaterialRequest
                {
                    ProjectId = request.ProjectId,
                    RequestedBy = currentUser.Id,
                    RequestDate = DateTime.UtcNow,
                    Status = "PENDING"
                };

                await _uow.MaterialRequests.AddAsync(materialRequest);
                await _uow.SaveChangeAsync(); // Lưu trước để sinh ra RequestId cho các Detail Items

                // 4. Lặp qua từng Item để kiểm tra tồn kho (Inventory Check)
                foreach (var item in request.Items)
                {
                    // Lấy bản ghi kho của vật tư này (Lưu ý: Có thể lọc thêm theo WarehouseId nếu dự án chỉ định kho cụ thể)
                    var inventory = await _uow.Inventories.GetAsync(inv => inv.MaterialId == item.MaterialId);

                    if (inventory == null)
                    {
                        var material = await _uow.Materials.GetAsync(m => m.MaterialId == item.MaterialId);
                        string matName = material?.MaterialName ?? $"ID {item.MaterialId}";
                        await _uow.RollbackTransactionAsync();
                        return response.SetBadRequest($"Vật tư [{matName}] không tồn tại trong bất kỳ kho nào.");
                    }

                    // Tính số lượng khả dụng thực tế: Available = Thật có - Đã giữ chỗ trước đó
                    decimal availableQty = inventory.QuantityOnHand - inventory.ReservedQuantity;

                    if (availableQty < item.Quantity)
                    {
                        var material = await _uow.Materials.GetAsync(m => m.MaterialId == item.MaterialId);
                        string matName = material?.MaterialName ?? $"ID {item.MaterialId}";
                        await _uow.RollbackTransactionAsync();
                        return response.SetBadRequest($"Kho không đủ hàng cho [{matName}]. Cần: {item.Quantity}, Khả dụng trong kho: {availableQty}");
                    }

                    // 5. LOGIC GIỮ HÀNG (Mẹo ERD): Tăng số lượng Reserved để giữ chỗ tạm thời cho phiếu PENDING này
                    inventory.ReservedQuantity += item.Quantity;
                    inventory.UpdatedAt = DateTime.UtcNow;
                    _uow.Inventories.Update(inventory);

                    // 6. Tạo dòng chi tiết Detail Record (MaterialRequisition)
                    var requisition = new MaterialRequisition
                    {
                        RequestId = materialRequest.RequestId,
                        MaterialId = item.MaterialId,
                        Quantity = item.Quantity,
                        NeededByDate = item.NeededByDate
                    };
                    await _uow.MaterialRequisitions.AddAsync(requisition);
                }

                // 7. Lưu tổng thể và Commit toàn bộ tiến trình
                await _uow.SaveChangeAsync();
                await _uow.CommitTransactionAsync();

                return response.SetOk("Tạo yêu cầu vật tư thành công, hệ thống đã tạm giữ số lượng trong kho!");
            }
            catch (Exception ex)
            {
                await _uow.RollbackTransactionAsync();
                return response.SetBadRequest("Lỗi xử lý yêu cầu vật tư: " + ex.Message);
            }
        }
        public async Task<ApiResponse> ApproveRequestAsync(int requestId)
        {
            var response = new ApiResponse();
            try
            {
                // 1. Kích hoạt Transaction để đảm bảo an toàn dữ liệu kho
                await _uow.BeginTransactionAsync();

                // 2. Lấy thông tin phiếu yêu cầu kèm danh sách vật tư chi tiết bên dưới
                var materialRequest = await _uow.MaterialRequests.GetAsync(
                    filter: r => r.RequestId == requestId,
                    include: query => query.Include(r => r.Requisitions)
                );

                if (materialRequest == null)
                    return response.SetNotFound("Không tìm thấy phiếu yêu cầu vật tư tương ứng.");

                if (materialRequest.Status != "PENDING")
                    return response.SetBadRequest($"Phiếu này đã được xử lý trước đó (Trạng thái hiện tại: {materialRequest.Status}).");

                // 3. Chuyển trạng thái phiếu sang APPROVED
                materialRequest.Status = "APPROVED";
                _uow.MaterialRequests.Update(materialRequest);

                // 4. Duyệt qua từng món vật tư để trừ kho thực tế và hạ lượng giữ chỗ (Reserved)
                foreach (var item in materialRequest.Requisitions)
                {
                    var inventory = await _uow.Inventories.GetAsync(inv => inv.MaterialId == item.MaterialId);
                    if (inventory != null)
                    {
                        // 🚀 LOGIC KHO CHUẨN:
                        inventory.QuantityOnHand -= item.Quantity;    // Hàng thực tế đã xuất đi
                        inventory.ReservedQuantity -= item.Quantity;  // Trả lại lượng giữ chỗ tạm thời
                        inventory.UpdatedAt = DateTime.UtcNow;

                        _uow.Inventories.Update(inventory);
                    }
                }

                // 5. Lưu tất cả thay đổi và xác nhận Transaction thành công
                await _uow.SaveChangeAsync();
                await _uow.CommitTransactionAsync();

                return response.SetOk("Duyệt phiếu yêu cầu và xuất kho thành công!");
            }
            catch (Exception ex)
            {
                await _uow.RollbackTransactionAsync();
                return response.SetBadRequest("Lỗi trong quá trình duyệt phiếu: " + ex.Message);
            }
        }

        public async Task<ApiResponse> RejectRequestAsync(int requestId)
        {
            var response = new ApiResponse();
            try
            {
                await _uow.BeginTransactionAsync();

                var materialRequest = await _uow.MaterialRequests.GetAsync(
                    filter: r => r.RequestId == requestId,
                    include: query => query.Include(r => r.Requisitions)
                );

                if (materialRequest == null)
                    return response.SetNotFound("Không tìm thấy phiếu yêu cầu vật tư.");

                if (materialRequest.Status != "PENDING")
                    return response.SetBadRequest($"Không thể hủy phiếu đã xử lý (Trạng thái hiện tại: {materialRequest.Status}).");

                // 3. Chuyển trạng thái phiếu sang REJECTED
                materialRequest.Status = "REJECTED";
                _uow.MaterialRequests.Update(materialRequest);

                // 4. Nhả số lượng đã giữ chỗ (Reserved) trong kho ra cho người khác xài
                foreach (var item in materialRequest.Requisitions)
                {
                    var inventory = await _uow.Inventories.GetAsync(inv => inv.MaterialId == item.MaterialId);
                    if (inventory != null)
                    {
                        // 🚀 NHẢ HÀNG GIỮ CHỖ: QuantityOnHand giữ nguyên, chỉ hạ ReservedQuantity
                        inventory.ReservedQuantity -= item.Quantity;
                        inventory.UpdatedAt = DateTime.UtcNow;

                        _uow.Inventories.Update(inventory);
                    }
                }

                await _uow.SaveChangeAsync();
                await _uow.CommitTransactionAsync();

                return response.SetOk("Đã từ chối phiếu yêu cầu và hoàn trả số lượng khả dụng cho kho!");
            }
            catch (Exception ex)
            {
                await _uow.RollbackTransactionAsync();
                return response.SetBadRequest("Lỗi trong quá trình từ chối phiếu: " + ex.Message);
            }
        }
    }
}
