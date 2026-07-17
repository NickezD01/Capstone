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
                // 2. Kiểm tra đầu việc (TaskItem) có tồn tại không
                var task = await _uow.TaskItems.GetAsync(t => t.TaskId == request.TaskId);
                if (task == null) return response.SetNotFound("Đầu việc không tồn tại.");
                var project = await _uow.Projects.GetAsync(p => p.ProjectId == task.ProjectId,
                    query => query.Include(p => p.Tasks));
                if (project == null)
                    return response.SetNotFound("Project not found.");
                var isOwningPm = string.Equals(currentUser.Role, Role.PM.ToString(), StringComparison.OrdinalIgnoreCase) &&
                                 project.PMUserID == currentUserId;
                var isAssignee = task.AssignedToUserID == currentUserId;
                if (!isOwningPm && !isAssignee)
                    return response.SetApiResponse(System.Net.HttpStatusCode.Forbidden, false, "Only the project manager or assigned user may report task progress.");

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
                task.ActualCost += request.ActualCostIncrement;

                // 6. TỰ ĐỘNG ĐỔI TRẠNG THÁI TASK KHI CHẠM 100%
                if (task.ActualProgressPct >= 100)
                {
                    task.Status = DomainTaskStatus.COMPLETED; // 👈 Đổi thành COMPLETE khi đủ 100
                }
                else if (task.ActualProgressPct > 0)
                {
                    task.Status = DomainTaskStatus.ACTIVE;
                }

                if (project.Tasks.Count > 0 && project.Tasks.All(x => x.Status == DomainTaskStatus.COMPLETED))
                    project.Status = ProjectStatus.COMPLETED;
                else if (DateTime.UtcNow > project.BaselineEnd)
                    project.Status = ProjectStatus.DELAYED;
                else
                    project.Status = ProjectStatus.IN_PROGRESS;

                // 7. Lưu tất cả thay đổi xuống Database và commit transaction
                await _uow.SaveChangeAsync();
                await _uow.CommitTransactionAsync();
                transactionStarted = false;

                return response.SetOk($"Progress updated by {progressIncrement}%. Current progress is {task.ActualProgressPct}% ({task.Status}); actual cost is {task.ActualCost}.");
            }
            catch (ArgumentNullException)
            {
                return response.SetApiResponse(System.Net.HttpStatusCode.Unauthorized, false, "Authenticated user claims are missing.");
            }
            catch (DbUpdateConcurrencyException)
            {
                if (transactionStarted) await _uow.RollbackTransactionAsync();
                return response.SetConflict(message: "Task progress changed while this report was being saved. Reload and retry.");
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
                var isAssignee = task.AssignedToUserID == currentUser.Id;
                if (!isAdmin && !isOwner && !isAssignee)
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
