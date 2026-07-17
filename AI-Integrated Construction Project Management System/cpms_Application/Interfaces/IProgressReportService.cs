using cpms_Application.Request.ProgressReport;
using cpms_Application.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Interfaces
{
    public interface IProgressReportService
    {
        Task<ApiResponse> SubmitReportAsync(SubmitProgressReportRequest request);
        Task<ApiResponse> GetReportsByTaskIdAsync(int taskId);
        Task<ApiResponse> ApproveReportAsync(int reportId, ReviewProgressReportRequest request);
        Task<ApiResponse> RejectReportAsync(int reportId, ReviewProgressReportRequest request);
        Task<ApiResponse> CorrectReportAsync(int reportId, CorrectProgressReportRequest request);
        Task<ApiResponse> ReverseReportAsync(int reportId, ReviewProgressReportRequest request);
    }
}
