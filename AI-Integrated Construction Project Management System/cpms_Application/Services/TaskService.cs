using AutoMapper;
using cpms_Application.Interfaces;
using cpms_Application.Request.Tasks;
using cpms_Application.Response;
using cpms_Application.Response.Tasks;
using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// Tránh lỗi Ambiguous (trùng tên) giữa Task của hệ thống và Entity TaskStatus của Domain
using DomainTaskStatus = cpms_Domain.Models.TaskStatus;

namespace cpms_Application.Services
{
    public class TaskService : ITaskService
    {
        private readonly IUnitOfWork _uow;
        private readonly IMapper _mapper;

        public TaskService(IUnitOfWork uow, IMapper mapper)
        {
            _uow = uow;
            _mapper = mapper;
        }

        public async Task<ApiResponse> CreateTaskAsync(CreateTaskRequest request)
        {
            var response = new ApiResponse();
            try
            {
                // 1. Kiểm tra xem dự án (Project) có tồn tại thực tế hay không
                // Thay thế GetAsync bằng hàm tìm kiếm theo ID tương ứng trong cấu trúc của bạn (ví dụ: GetByIdAsync hoặc GetAsync)
                var project = await _uow.Projects.GetAsync(p => p.ProjectId == request.ProjectId);
                if (project == null)
                    return response.SetNotFound($"Dự án với ID = {request.ProjectId} không tồn tại trong hệ thống.");

                // 2. Kiểm tra nhân sự được giao việc có tồn tại hay không
                var user = await _uow.UserAccounts.GetAsync(u => u.Id == request.AssignedToUserID);
                if (user == null)
                    return response.SetNotFound($"Nhân sự được giao việc với ID = {request.AssignedToUserID} không tồn tại.");

                // 3. Map dữ liệu cơ bản và cấu hình mặc định cho Task mới
                var taskItem = _mapper.Map<TaskItem>(request);
                taskItem.ActualCost = 0;
                taskItem.ActualProgressPct = 0;
                taskItem.Status = DomainTaskStatus.PENDING;

                await _uow.TaskItems.AddAsync(taskItem);

                // 🚀 LƯU LẦN 1: Tạo bản ghi TaskItem để DB sinh mã `taskItem.TaskId` (Tự tăng)
                await _uow.SaveChangeAsync();

                // 4. Xử lý lưu định mức vật tư đi kèm đầu việc (Nếu có dữ liệu truyền lên)
                if (request.Materials != null && request.Materials.Any())
                {
                    // Tối ưu hóa: Thu thập toàn bộ MaterialId cần kiểm tra để truy vấn DB một lần duy nhất
                    var requestedMaterialIds = request.Materials.Select(m => m.MaterialId).Distinct().ToList();
                    var existingMaterials = await _uow.Materials.GetAllAsync(m => requestedMaterialIds.Contains(m.MaterialId));

                    foreach (var matRequest in request.Materials)
                    {
                        // Kiểm tra xem vật tư có thực sự hợp lệ không
                        if (!existingMaterials.Any(m => m.MaterialId == matRequest.MaterialId))
                        {
                            return response.SetBadRequest($"Mã định mức lỗi: Vật tư với ID = {matRequest.MaterialId} không tồn tại.");
                        }

                        // Khởi tạo thực thể liên kết Task và Vật tư
                        var requirement = new TaskMaterialRequirement
                        {
                            TaskId = taskItem.TaskId, // Sử dụng mã TaskId vừa sinh tự động ở trên
                            MaterialId = matRequest.MaterialId,
                            GrossQuantityRequired = matRequest.GrossQuantityRequired
                        };

                        await _uow.TaskMaterialRequirements.AddAsync(requirement);
                    }

                    // 🚀 LƯU LẦN 2: Lưu toàn bộ danh sách định mức vật tư phụ thuộc vào Database
                    await _uow.SaveChangeAsync();
                }

                return response.SetOk("Khởi tạo đầu việc dự án và định mức vật tư thành công!");
            }
            catch (Exception ex)
            {
                // 🔍 TRÍCH XUẤT LỖI TẬN GỐC: Trả ra chính xác InnerException để hiển thị lên Swagger (ví dụ: lỗi trùng khóa, sai kiểu dữ liệu,...)
                var deepErrorMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                if (ex.InnerException?.InnerException != null)
                {
                    deepErrorMessage += " -> " + ex.InnerException.InnerException.Message;
                }
                return response.SetBadRequest("Lỗi tạo đầu việc (Database Error): " + deepErrorMessage);
            }
        }

        public async Task<ApiResponse> GetTasksByProjectAsync(int projectId)
        {
            var response = new ApiResponse();
            try
            {
                // Tối ưu hóa truy vấn: Lôi kèm User gánh vác, danh sách định mức và thuộc tính của Vật tư để map sang DTO
                var tasks = await _uow.TaskItems.GetAllAsync(
                    filter: t => t.ProjectId == projectId,
                    include: query => query
                        .Include(t => t.AssignedToUser)
                        .Include(t => t.MaterialRequirements)
                            .ThenInclude(mr => mr.Material)
                );

                var result = _mapper.Map<IEnumerable<TaskResponse>>(tasks);
                return response.SetOk(result);
            }
            catch (Exception ex)
            {
                return response.SetBadRequest("Lỗi lấy danh sách đầu việc: " + ex.Message);
            }
        }

        public async Task<ApiResponse> GetMaterialRequirementsByProjectIdAsync(int projectId)
        {
            var apiResponse = new ApiResponse();
            try
            {
                // 1. Kiểm tra xem dự án (Project) có tồn tại không
                var project = await _uow.Projects.GetAsync(p => p.ProjectId == projectId);
                if (project == null)
                    return apiResponse.SetNotFound("Dự án không tồn tại.");

                // 2. 🚀 CẢI TIẾN HIỆU NĂNG: Lọc trực tiếp từ DB bằng Include thay vì bốc toàn bộ bảng định mức lên RAM (In-Memory Filtering)
                // Lấy các định mức vật tư mà có Task thuộc về ProjectId này
                var projectRequirements = await _uow.TaskMaterialRequirements.GetAllAsync(
                    filter: r => r.TaskItem.ProjectId == projectId,
                    include: query => query
                        .Include(r => r.Material)
                        .Include(r => r.TaskItem)
                );

                if (!projectRequirements.Any())
                {
                    return apiResponse.SetOk(new List<TaskMaterialResponse>());
                }

                // 3. Sử dụng AutoMapper để map trực tiếp sang DTO phẳng (Vì đã include đầy đủ dữ liệu cha ở trên)
                var responseData = _mapper.Map<List<TaskMaterialResponse>>(projectRequirements);

                return apiResponse.SetOk(responseData);
            }
            catch (Exception ex)
            {
                return apiResponse.SetBadRequest("Đã xảy ra lỗi khi lấy định mức vật tư: " + ex.Message);
            }
        }
    }
}