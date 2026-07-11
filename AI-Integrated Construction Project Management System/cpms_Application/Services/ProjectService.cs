using AutoMapper;
using cpms_Application.Interfaces;
using cpms_Application.Request.Project;
using cpms_Application.Response;
using cpms_Application.Response.Project;
using cpms_Domain.Models;
using Microsoft.AspNetCore.Http;
using DocumentFormat.OpenXml.Packaging;
// 🚀 Đổi hẳn cách dùng Paragraph để tránh xung đột namespace
using WordXml = DocumentFormat.OpenXml.Wordprocessing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Services
{
    public class ProjectService : IProjectService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IClaimService _claimService; // 🚀 Bước 1: Khai báo ClaimService

        // Inject IClaimService vào constructor
        public ProjectService(IUnitOfWork unitOfWork, IMapper mapper, IClaimService claimService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _claimService = claimService;
        }

        public async Task<ApiResponse> CreateProjectAsync(CreateProjectRequest request)
        {
            var apiResponse = new ApiResponse();
            try
            {
                var project = _mapper.Map<Project>(request);
                project.Status = ProjectStatus.PLANNING;

                await _unitOfWork.Projects.AddAsync(project);
                await _unitOfWork.SaveChangeAsync();

                return apiResponse.SetOk("Project created successfully");
            }
            catch (Exception ex)
            {
                return apiResponse.SetBadRequest(ex.Message);
            }
        }

        public async Task<ApiResponse> GetAllProjectsAsync()
        {
            var apiResponse = new ApiResponse();
            try
            {
                var projects = await _unitOfWork.Projects.GetAllAsync(null);
                var response = _mapper.Map<List<ProjectResponse>>(projects);
                return apiResponse.SetOk(response);
            }
            catch (Exception ex)
            {
                return apiResponse.SetBadRequest(ex.Message);
            }
            }

        public async Task<ApiResponse> GetProjectByIdAsync(int id)
        {
            var apiResponse = new ApiResponse();
            try
            {
                var project = await _unitOfWork.Projects.GetByIdAsync(id);

                if (project == null)
                {
                    return apiResponse.SetNotFound("Project not found or has been deleted.");
                }

                var response = _mapper.Map<ProjectResponse>(project);
                return apiResponse.SetOk(response);
            }
            catch (Exception ex)
            {
                return apiResponse.SetBadRequest(ex.Message);
            }
        }

        // 🚀 Bước 2: Bỏ tham số pmUserId ở đây đi vì đã lấy tự động từ Token qua ClaimService
        public async Task<ApiResponse> ImportProjectFromWordAsync(IFormFile file)
        {
            var apiResponse = new ApiResponse();
            try
            {
                if (file == null || file.Length == 0)
                    return apiResponse.SetBadRequest("Vui lòng tải lên một file Word (.docx) hợp lệ.");

                // 🚀 Bước 3: Tự động lấy pmUserId từ ClaimService
                var currentUser = _claimService.GetUserClaim();
                int pmUserId = currentUser.Id;

                var pmAccount = await _unitOfWork.UserAccounts.GetAsync(u => u.Id == pmUserId);
                if (pmAccount == null)
                    return apiResponse.SetNotFound("Tài khoản quản lý dự án (PMUserID) không tồn tại hoặc không hợp lệ.");

                string projectName = "";
                string address = "";
                decimal totalBudget = 0;
                string currency = "VND";
                DateTime startDate = DateTime.UtcNow;
                DateTime baselineStart = DateTime.UtcNow;
                DateTime baselineEnd = DateTime.UtcNow.AddMonths(6);

                using (var stream = file.OpenReadStream())
                {
                    using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(stream, false))
                    {
                        var body = wordDoc.MainDocumentPart.Document.Body;
                        var paragraphs = body.Descendants<WordXml.Paragraph>();

                        foreach (var para in paragraphs)
                        {
                            string text = para.InnerText.Trim();
                            if (string.IsNullOrEmpty(text)) continue;

                            if (text.StartsWith("Tên dự án:", StringComparison.OrdinalIgnoreCase))
                                projectName = text.Replace("Tên dự án:", "", StringComparison.OrdinalIgnoreCase).Trim();

                            else if (text.StartsWith("Địa điểm:", StringComparison.OrdinalIgnoreCase))
                                address = text.Replace("Địa điểm:", "", StringComparison.OrdinalIgnoreCase).Trim();

                            else if (text.StartsWith("Ngân sách tổng:", StringComparison.OrdinalIgnoreCase))
                                decimal.TryParse(text.Replace("Ngân sách tổng:", "", StringComparison.OrdinalIgnoreCase).Trim(), out totalBudget);

                            else if (text.StartsWith("Tiền tệ:", StringComparison.OrdinalIgnoreCase))
                                currency = text.Replace("Tiền tệ:", "", StringComparison.OrdinalIgnoreCase).Trim();

                            else if (text.StartsWith("Ngày thực tế bắt đầu:", StringComparison.OrdinalIgnoreCase))
                                DateTime.TryParse(text.Replace("Ngày thực tế bắt đầu:", "", StringComparison.OrdinalIgnoreCase).Trim(), out startDate);

                            else if (text.StartsWith("Ngày kế hoạch bắt đầu:", StringComparison.OrdinalIgnoreCase))
                                DateTime.TryParse(text.Replace("Ngày kế hoạch bắt đầu:", "", StringComparison.OrdinalIgnoreCase).Trim(), out baselineStart);

                            else if (text.StartsWith("Ngày kế hoạch kết thúc:", StringComparison.OrdinalIgnoreCase))
                                DateTime.TryParse(text.Replace("Ngày kế hoạch kết thúc:", "", StringComparison.OrdinalIgnoreCase).Trim(), out baselineEnd);
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(projectName))
                    return apiResponse.SetBadRequest("File văn bản sai cấu trúc hoặc trống. Không tìm thấy dòng chứa tiêu đề 'Tên dự án:'.");

                var project = new Project
                {
                    ProjectName = projectName,
                    Address = string.IsNullOrWhiteSpace(address) ? null : address,
                    Status = ProjectStatus.PLANNING,
                    StartDate = startDate,
                    BaselineStart = baselineStart,
                    BaselineEnd = baselineEnd,
                    TotalProjectBudget = totalBudget,
                    Currency = string.IsNullOrWhiteSpace(currency) ? "VND" : currency,
                    PMUserID = pmUserId
                };

                await _unitOfWork.Projects.AddAsync(project);
                await _unitOfWork.SaveChangeAsync();

                var resultResponse = _mapper.Map<ProjectResponse>(project);

                return apiResponse.SetOk(resultResponse);
            }
            catch (ArgumentNullException ex)
            {
                // Bắt riêng lỗi nếu không tìm thấy UserId trong Token (lỗi từ ClaimService)
                return apiResponse.SetBadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return apiResponse.SetBadRequest("Đã xảy ra lỗi bất ngờ khi bóc tách file Word: " + ex.Message);
            }
        }
    }
}