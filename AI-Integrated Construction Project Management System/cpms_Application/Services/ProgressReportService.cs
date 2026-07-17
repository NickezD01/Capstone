using AutoMapper;
using cpms_Application.Interfaces;
using cpms_Application.Request.ProgressReport;
using cpms_Application.Response;
using cpms_Application.Response.ProgressReport;
using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DomainTaskStatus = cpms_Domain.Models.TaskStatus;

namespace cpms_Application.Services
{
    public class ProgressReportService : IProgressReportService
    {
        private readonly IUnitOfWork _uow;
        private readonly IMapper _mapper;
        private readonly IClaimService _claimService;

        public ProgressReportService(IUnitOfWork uow, IMapper mapper, IClaimService claimService)
        {
            _uow = uow;
            _mapper = mapper;
            _claimService = claimService;
        }

        public async Task<ApiResponse> SubmitReportAsync(SubmitProgressReportRequest request)
        {
            var response = new ApiResponse();
            var transactionStarted = false;
            try
            {
                // 1. Lấy ID của kỹ sư đăng nhập từ Token bảo mật
                var currentUser = _claimService.GetUserClaim();
                int currentUserId = currentUser.Id;
                if (!string.Equals(currentUser.Role, Role.PM.ToString(), StringComparison.OrdinalIgnoreCase))
                    return response.SetApiResponse(System.Net.HttpStatusCode.Forbidden, false, "Only project managers may submit progress reports.");

                // 2. Kiểm tra đầu việc (TaskItem) có tồn tại không
                var task = await _uow.TaskItems.GetAsync(t => t.TaskId == request.TaskId);
                if (task == null) return response.SetNotFound("Đầu việc không tồn tại.");
                var project = await _uow.Projects.GetByIdAsync(task.ProjectId);
                if (project == null || project.PMUserID != currentUserId)
                    return response.SetApiResponse(System.Net.HttpStatusCode.Forbidden, false, "You may only report progress for a project you manage.");

                // 🛑 CHECK VALIDATION: Lượng tăng thêm phải lớn hơn 0 (Kiểu int)
                decimal progressIncrement = request.ProgressIncrement;
                if (progressIncrement <= 0)
                {
                    return response.SetBadRequest("Giá trị tiến độ tăng thêm phải lớn hơn 0%.");
                }

                // Chặn nếu tiến độ hiện tại của Task đã là 100% rồi (không cho báo cáo tiếp)
                if (task.ActualProgressPct >= 100)
                {
                    return response.SetBadRequest("Đầu việc này đã hoàn thành 100%, không thể báo cáo thêm tiến độ.");
                }
                if (task.ActualProgressPct + progressIncrement > 100)
                {
                    return response.SetBadRequest($"Progress increment exceeds the remaining {100 - task.ActualProgressPct}%.");
                }

                // 3. Bắt đầu Database Transaction
                await _uow.BeginTransactionAsync();
                transactionStarted = true;

                // 4. Map dữ liệu báo cáo tiến độ và lưu vào DB
                var report = _mapper.Map<ProgressReport>(request);
                report.ReportDate = DateTime.UtcNow;
                report.ReportedByUserId = currentUserId;

                await _uow.ProgressReports.AddAsync(report);

                // 5. LOGIC CỘNG DỒN: Tiến độ cũ (decimal/int) + Lượng nhập mới tăng thêm (int)
                task.ActualProgressPct += progressIncrement;

                // 6. TỰ ĐỘNG ĐỔI TRẠNG THÁI TASK KHI CHẠM 100%
                if (task.ActualProgressPct >= 100)
                {
                    task.Status = DomainTaskStatus.COMPLETED; // 👈 Đổi thành COMPLETE khi đủ 100
                }
                else if (task.ActualProgressPct > 0)
                {
                    task.Status = DomainTaskStatus.ACTIVE;
                }

                // 7. Lưu tất cả thay đổi xuống Database và commit transaction
                await _uow.SaveChangeAsync();
                await _uow.CommitTransactionAsync();
                transactionStarted = false;

                return response.SetOk($"Gửi báo cáo thành công! Đã cộng thêm {progressIncrement}%. Tiến độ hiện tại của đầu việc đạt {task.ActualProgressPct}% ({task.Status}).");
            }
            catch (ArgumentNullException)
            {
                return response.SetApiResponse(System.Net.HttpStatusCode.Unauthorized, false, "Authenticated user claims are missing.");
            }
            catch (Exception)
            {
                if (transactionStarted) await _uow.RollbackTransactionAsync();
                return response.SetApiResponse(System.Net.HttpStatusCode.InternalServerError, false, "Unable to process the progress report.");
            }
        }

        public async Task<ApiResponse> GetReportsByTaskIdAsync(int taskId)
        {
            var response = new ApiResponse();
            try
            {
                var task = await _uow.TaskItems.GetByIdAsync(taskId);
                if (task == null) return response.SetNotFound("Task not found.");
                var project = await _uow.Projects.GetByIdAsync(task.ProjectId);
                if (project == null) return response.SetNotFound("Project not found.");
                var currentUser = _claimService.GetUserClaim();
                var isAdmin = string.Equals(currentUser.Role, Role.ADMIN.ToString(), StringComparison.OrdinalIgnoreCase);
                var isOwner = string.Equals(currentUser.Role, Role.PM.ToString(), StringComparison.OrdinalIgnoreCase) &&
                              project.PMUserID == currentUser.Id;
                if (!isAdmin && !isOwner)
                    return response.SetApiResponse(System.Net.HttpStatusCode.Forbidden, false, "You do not have access to this task's progress reports.");
                var reports = await _uow.ProgressReports.GetAllAsync(
                    filter: r => r.TaskId == taskId,
                    include: query => query.Include(r => r.Reporter).Include(r => r.Task)
                );

                var result = _mapper.Map<IEnumerable<ProgressReportResponse>>(reports);
                return response.SetOk(result);
            }
            catch (Exception)
            {
                return response.SetApiResponse(System.Net.HttpStatusCode.InternalServerError, false, "Unable to retrieve progress reports.");
            }
        }
    }
}
