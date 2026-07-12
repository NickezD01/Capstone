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
            try
            {
                // 1. Lấy ID của kỹ sư đăng nhập từ Token bảo mật
                var currentUser = _claimService.GetUserClaim();
                int currentEngineerId = currentUser.Id;

                // 2. Kiểm tra đầu việc (TaskItem) có tồn tại không
                var task = await _uow.TaskItems.GetAsync(t => t.TaskId == request.TaskId);
                if (task == null) return response.SetNotFound("Đầu việc không tồn tại.");

                // 🛑 CHECK VALIDATION: Lượng tăng thêm phải lớn hơn 0 (Kiểu int)
                int progressIncrement = request.ProgressIncrement; // 👈 Chuyển sang int theo yêu cầu của bạn
                if (progressIncrement <= 0)
                {
                    return response.SetBadRequest("Giá trị tiến độ tăng thêm phải lớn hơn 0%.");
                }

                // Chặn nếu tiến độ hiện tại của Task đã là 100% rồi (không cho báo cáo tiếp)
                if (task.ActualProgressPct >= 100)
                {
                    return response.SetBadRequest("Đầu việc này đã hoàn thành 100%, không thể báo cáo thêm tiến độ.");
                }

                // 3. Bắt đầu Database Transaction
                await _uow.BeginTransactionAsync();

                // 4. Map dữ liệu báo cáo tiến độ và lưu vào DB
                var report = _mapper.Map<ProgressReport>(request);
                report.ReportDate = DateTime.UtcNow;
                report.EngineerId = currentEngineerId;

                await _uow.ProgressReports.AddAsync(report);

                // 5. LOGIC CỘNG DỒN: Tiến độ cũ (decimal/int) + Lượng nhập mới tăng thêm (int)
                task.ActualProgressPct += progressIncrement;

                // Chốt chặn tối đa là 100%
                if (task.ActualProgressPct > 100)
                {
                    task.ActualProgressPct = 100;
                }

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

                return response.SetOk($"Gửi báo cáo thành công! Đã cộng thêm {progressIncrement}%. Tiến độ hiện tại của đầu việc đạt {task.ActualProgressPct}% ({task.Status}).");
            }
            catch (ArgumentNullException ex)
            {
                return response.SetBadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                await _uow.RollbackTransactionAsync();
                return response.SetBadRequest("Lỗi xử lý báo cáo tiến độ: " + ex.Message);
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