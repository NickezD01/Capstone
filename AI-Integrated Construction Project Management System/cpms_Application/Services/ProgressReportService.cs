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
        private readonly IClaimService _claimService; // 🚀 BỔ SUNG: Tiêm ClaimService

        public ProgressReportService(IUnitOfWork uow, IMapper mapper, IClaimService claimService)
        {
            _uow = uow;
            _mapper = mapper;
            _claimService = claimService;
        }

        public async Task<ApiResponse> SubmitReportAsync(SubmitProgressReportRequest request)
        {
            var response = new ApiResponse();
            try
            {
                // 🚀 CLAIM ĐƯỢC Ở ĐÂY: Lấy thẳng ID của người dùng đang đăng nhập từ Token
                var currentUser = _claimService.GetUserClaim();
                int currentEngineerId = currentUser.Id;

                // 1. Kiểm tra đầu việc (TaskItem) có tồn tại không
                var task = await _uow.TaskItems.GetAsync(t => t.TaskId == request.TaskId);
                if (task == null) return response.SetNotFound("Đầu việc không tồn tại.");

                // 2. Sử dụng Database Transaction
                await _uow.BeginTransactionAsync();

                // 3. Map dữ liệu và gán ID kỹ sư bảo mật lấy từ Claim
                var report = _mapper.Map<ProgressReport>(request);
                report.ReportDate = DateTime.UtcNow;
                report.EngineerId = currentEngineerId; // 🚀 Gán trực tiếp ID từ Claim, chấp Client truyền bậy từ ngoài vào

                await _uow.ProgressReports.AddAsync(report);

                // 4. LOGIC NGHIỆP VỤ: Cộng dồn tiến độ tích lũy
                task.ActualProgressPct += request.ProgressIncrement;
                if (task.ActualProgressPct > 100) task.ActualProgressPct = 100;

                // 5. Cập nhật trạng thái
                if (task.ActualProgressPct >= 100)
                {
                    task.Status = DomainTaskStatus.COMPLETED;
                }
                else if (task.ActualProgressPct > 0)
                {
                    task.Status = DomainTaskStatus.ACTIVE;
                }

                await _uow.SaveChangeAsync();
                await _uow.CommitTransactionAsync();

                return response.SetOk("Gửi báo cáo tiến độ và cập nhật đầu việc thành công!");
            }
            catch (ArgumentNullException ex)
            {
                // Bắt lỗi nếu Token thiếu Claim "UserId"
                return response.SetBadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                await _uow.RollbackTransactionAsync();
                return response.SetBadRequest("Lỗi xử lý báo cáo: " + ex.Message);
            }
        }

        public async Task<ApiResponse> GetReportsByTaskIdAsync(int taskId)
        {
            var response = new ApiResponse();
            try
            {
                var reports = await _uow.ProgressReports.GetAllAsync(
                    filter: r => r.TaskId == taskId,
                    include: query => query.Include(r => r.Engineer).Include(r => r.Task)
                );

                var result = _mapper.Map<IEnumerable<ProgressReportResponse>>(reports);
                return response.SetOk(result);
            }
            catch (Exception ex)
            {
                return response.SetBadRequest("Lỗi lấy lịch sử báo cáo: " + ex.Message);
            }
        }
    }
}