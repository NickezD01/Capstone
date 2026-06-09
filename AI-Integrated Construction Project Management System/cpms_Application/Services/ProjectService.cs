using AutoMapper;
using cpms_Application.Interfaces;
using cpms_Application.Request.Project;
using cpms_Application.Response;
using cpms_Application.Response.Project;
using cpms_Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Services
{
    public class ProjectService : IProjectService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public ProjectService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<ApiResponse> CreateProjectAsync(CreateProjectRequest request)
        {
            var apiResponse = new ApiResponse();
            try
            {
                var project = _mapper.Map<Project>(request);
                project.Status = ProjectStatus.PLANNING; // Gán mặc định

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
                // Sử dụng GetByIdAsync từ repository (bạn nên kiểm tra lại repo của mình đã hỗ trợ chưa)
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
    }
}
