using AutoMapper;
using cpms_Application.Interfaces;
using cpms_Application.Request.MaterialRequest;
using cpms_Application.Response;
using cpms_Application.Response.MaterialRequest;
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
                var currentUser = _claimService.GetUserClaim();

                if (request.Items == null || request.Items.Count == 0)
                    return response.SetBadRequest("Danh sách vật tư yêu cầu không được để trống.");

                await _uow.BeginTransactionAsync();

                var materialRequest = new MaterialRequest
                {
                    ProjectId = request.ProjectId,
                    TaskId = request.TaskId,
                    RequestedBy = currentUser.Id,
                    RequestDate = DateTime.UtcNow,
                    Status = "PENDING"
                };

                await _uow.MaterialRequests.AddAsync(materialRequest);
                await _uow.SaveChangeAsync();

                var requestedMaterialIds = request.Items.Select(item => item.MaterialId).Distinct().ToList();
                var allInventories = await _uow.Inventories.GetAllAsync(inv => requestedMaterialIds.Contains(inv.MaterialId));
                var allMaterials = await _uow.Materials.GetAllAsync(m => requestedMaterialIds.Contains(m.MaterialId));

                foreach (var item in request.Items)
                {
                    var inventory = allInventories.FirstOrDefault(inv => inv.MaterialId == item.MaterialId);

                    if (inventory == null)
                    {
                        var material = allMaterials.FirstOrDefault(m => m.MaterialId == item.MaterialId);
                        string matName = material?.MaterialName ?? $"ID {item.MaterialId}";
                        await _uow.RollbackTransactionAsync();
                        return response.SetBadRequest($"Vật tư [{matName}] không tồn tại trong bất kỳ kho nào.");
                    }

                    decimal availableQty = inventory.QuantityOnHand - inventory.ReservedQuantity;

                    if (availableQty < item.Quantity)
                    {
                        var material = allMaterials.FirstOrDefault(m => m.MaterialId == item.MaterialId);
                        string matName = material?.MaterialName ?? $"ID {item.MaterialId}";
                        await _uow.RollbackTransactionAsync();
                        return response.SetBadRequest($"Kho không đủ hàng cho [{matName}]. Cần: {item.Quantity}, Khả dụng trong kho: {availableQty}");
                    }

                    inventory.ReservedQuantity += item.Quantity;
                    inventory.UpdatedAt = DateTime.UtcNow;
                    _uow.Inventories.Update(inventory);

                    var requisition = new MaterialRequisition
                    {
                        RequestId = materialRequest.RequestId,
                        MaterialId = item.MaterialId,
                        Quantity = item.Quantity,
                        NeededByDate = item.NeededByDate
                    };
                    await _uow.MaterialRequisitions.AddAsync(requisition);
                }

                await _uow.SaveChangeAsync();
                await _uow.CommitTransactionAsync();

                // Nạp đầy đủ thông tin quan hệ sau khi lưu thành công
                var savedRequest = await _uow.MaterialRequests.GetAsync(
                    filter: r => r.RequestId == materialRequest.RequestId,
                    include: query => query
                        .Include(r => r.Requester)
                        .Include(r => r.Requisitions)
                            .ThenInclude(req => req.Material)
                );

                var result = _mapper.Map<MaterialRequestResponse>(savedRequest);
                return response.SetOk(result);
            }
            catch (Exception ex)
            {
                await _uow.RollbackTransactionAsync();
                return response.SetBadRequest("Lỗi xử lý yêu cầu vật tư: " + ex.Message);
            }
        }

        public async Task<ApiResponse> CreateRequestByTaskIdAsync(int taskId)
        {
            var response = new ApiResponse();
            try
            {
                var currentUser = _claimService.GetUserClaim();

                var taskItem = await _uow.TaskItems.GetAsync(
                    filter: t => t.TaskId == taskId,
                    include: query => query
                        .Include(t => t.MaterialRequirements)
                            .ThenInclude(r => r.Material)
                );

                if (taskItem == null)
                    return response.SetNotFound($"Không tìm thấy đầu việc (Task) với ID = {taskId}");

                if (taskItem.MaterialRequirements == null || !taskItem.MaterialRequirements.Any())
                    return response.SetBadRequest($"Đầu việc [{taskItem.TaskName}] này chưa được cấu hình danh mục định mức vật tư để xin cấp.");

                await _uow.BeginTransactionAsync();

                var materialRequest = new MaterialRequest
                {
                    ProjectId = taskItem.ProjectId,
                    TaskId = taskItem.TaskId,
                    RequestedBy = currentUser.Id,
                    RequestDate = DateTime.UtcNow,
                    Status = "PENDING"
                };

                await _uow.MaterialRequests.AddAsync(materialRequest);
                await _uow.SaveChangeAsync();

                var requestedMaterialIds = taskItem.MaterialRequirements.Select(r => r.MaterialId).Distinct().ToList();
                var allInventories = await _uow.Inventories.GetAllAsync(inv => requestedMaterialIds.Contains(inv.MaterialId));

                foreach (var requirement in taskItem.MaterialRequirements)
                {
                    var inventory = allInventories.FirstOrDefault(inv => inv.MaterialId == requirement.MaterialId);

                    if (inventory == null)
                    {
                        await _uow.RollbackTransactionAsync();
                        return response.SetBadRequest($"Vật tư [{requirement.Material?.MaterialName ?? $"ID {requirement.MaterialId}"}] chưa được thiết lập trong kho.");
                    }

                    decimal availableQty = inventory.QuantityOnHand - inventory.ReservedQuantity;

                    if (availableQty < requirement.GrossQuantityRequired)
                    {
                        await _uow.RollbackTransactionAsync();
                        return response.SetBadRequest($"Kho không đủ hàng cho [{requirement.Material?.MaterialName}]. Định mức cần: {requirement.GrossQuantityRequired}, Khả dụng thực tế: {availableQty}");
                    }

                    inventory.ReservedQuantity += requirement.GrossQuantityRequired;
                    inventory.UpdatedAt = DateTime.UtcNow;
                    _uow.Inventories.Update(inventory);

                    var requisition = new MaterialRequisition
                    {
                        RequestId = materialRequest.RequestId,
                        MaterialId = requirement.MaterialId,
                        Quantity = requirement.GrossQuantityRequired,
                        NeededByDate = taskItem.BaselineStart
                    };
                    await _uow.MaterialRequisitions.AddAsync(requisition);
                }

                await _uow.SaveChangeAsync();
                await _uow.CommitTransactionAsync();

                // Nạp đầy đủ thông tin quan hệ sau khi bốc định mức thành công
                var savedRequest = await _uow.MaterialRequests.GetAsync(
                    filter: r => r.RequestId == materialRequest.RequestId,
                    include: query => query
                        .Include(r => r.Requester)
                        .Include(r => r.Requisitions)
                            .ThenInclude(req => req.Material)
                );

                var result = _mapper.Map<MaterialRequestResponse>(savedRequest);
                return response.SetOk(result);
            }
            catch (Exception ex)
            {
                await _uow.RollbackTransactionAsync();
                return response.SetBadRequest("Lỗi trong quá trình xử lý tạo yêu cầu từ TaskId: " + ex.Message);
            }
        }

        public async Task<ApiResponse> ApproveRequestAsync(int requestId)
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
                    return response.SetNotFound("Không tìm thấy phiếu yêu cầu vật tư tương ứng.");

                if (materialRequest.Status != "PENDING")
                    return response.SetBadRequest($"Phiếu này đã được xử lý trước đó (Trạng thái hiện tại: {materialRequest.Status}).");

                materialRequest.Status = "APPROVED";
                _uow.MaterialRequests.Update(materialRequest);

                foreach (var item in materialRequest.Requisitions)
                {
                    var inventory = await _uow.Inventories.GetAsync(inv => inv.MaterialId == item.MaterialId);
                    if (inventory != null)
                    {
                        inventory.QuantityOnHand -= item.Quantity;
                        inventory.ReservedQuantity -= item.Quantity;
                        inventory.UpdatedAt = DateTime.UtcNow;

                        _uow.Inventories.Update(inventory);
                    }
                }

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

                materialRequest.Status = "REJECTED";
                _uow.MaterialRequests.Update(materialRequest);

                foreach (var item in materialRequest.Requisitions)
                {
                    var inventory = await _uow.Inventories.GetAsync(inv => inv.MaterialId == item.MaterialId);
                    if (inventory != null)
                    {
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

        public async Task<ApiResponse> GetRequestByIdAsync(int requestId)
        {
            var response = new ApiResponse();
            try
            {
                var materialRequest = await _uow.MaterialRequests.GetAsync(
                    filter: r => r.RequestId == requestId,
                    include: query => query
                        .Include(r => r.Requester)
                        .Include(r => r.Requisitions)
                            .ThenInclude((MaterialRequisition req) => req.Material)
                );

                if (materialRequest == null)
                    return response.SetNotFound("Không tìm thấy phiếu yêu cầu vật tư này.");

                var result = _mapper.Map<MaterialRequestResponse>(materialRequest);
                return response.SetOk(result);
            }
            catch (Exception ex)
            {
                return response.SetBadRequest("Lỗi khi lấy chi tiết phiếu yêu cầu: " + ex.Message);
            }
        }

        public async Task<ApiResponse> GetAllRequestsAsync()
        {
            var response = new ApiResponse();
            try
            {
                var requests = await _uow.MaterialRequests.GetAllAsync(
                    null,
                    include: (IQueryable<MaterialRequest> query) => query
                        .Include(r => r.Requester)
                        .Include(r => r.Requisitions)
                            .ThenInclude(req => req.Material)
                );

                var result = _mapper.Map<IEnumerable<MaterialRequestResponse>>(requests);
                return response.SetOk(result);
            }
            catch (Exception ex)
            {
                return response.SetBadRequest("Lỗi khi lấy danh sách phiếu yêu cầu: " + ex.Message);
            }
        }

        public async Task<ApiResponse> GetRequestsByProjectAsync(int projectId)
        {
            var response = new ApiResponse();
            try
            {
                var requests = await _uow.MaterialRequests.GetAllAsync(
                    filter: r => r.ProjectId == projectId,
                    include: query => query
                        .Include(r => r.Requester)
                        .Include(r => r.Requisitions)
                            .ThenInclude((MaterialRequisition req) => req.Material)
                );

                var result = _mapper.Map<IEnumerable<MaterialRequestResponse>>(requests);
                return response.SetOk(result);
            }
            catch (Exception ex)
            {
                return response.SetBadRequest($"Lỗi khi lấy danh sách phiếu yêu cầu của dự án {projectId}: " + ex.Message);
            }
        }
    }
}