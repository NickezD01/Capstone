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

// 🚀 KHẮC PHỤC LỖI AMBIGUOUS: Đặt bí danh cho Enum của Domain
using DomainTaskStatus = cpms_Domain.Models.TaskStatus;

namespace cpms_Application.Services
{
    public class ProgressReportService : IProgressReportService
    {
        private readonly IUnitOfWork _uow;
        private readonly IMapper _mapper;

        public ProgressReportService(IUnitOfWork uow, IMapper mapper)
        {
            _uow = uow;
            _mapper = mapper;
        }

        public async Task<ApiResponse> SubmitReportAsync(SubmitProgressReportRequest request)
        {
            var response = new ApiResponse();
            try
            {
                var task = await _uow.TaskItems.GetAsync(t => t.TaskId == request.TaskId);
                if (task == null) return response.SetNotFound("Đầu việc không tồn tại.");

                var engineer = await _uow.UserAccounts.GetAsync(u => u.Id == request.EngineerId);
                if (engineer == null) return response.SetNotFound("Kỹ sư báo cáo không tồn tại trong hệ thống.");

                await _uow.BeginTransactionAsync();

                var report = _mapper.Map<ProgressReport>(request);
                report.ReportDate = DateTime.UtcNow;

                await _uow.ProgressReports.AddAsync(report);

                // Cộng dồn tiến độ tích lũy
                task.ActualProgressPct += request.ProgressIncrement;
                if (task.ActualProgressPct > 100) task.ActualProgressPct = 100;

                // 🚀 Cập nhật trạng thái thông qua Alias
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
                    include: query => query.Include(r => r.Engineer).Include(r => r.Task) // Gọi đúng thuộc tính .Task
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