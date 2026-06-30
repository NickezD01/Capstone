using AutoMapper;
using cpms_Application.Interfaces;
using cpms_Application.Authorization;
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
        private readonly IClaimService _claimService;

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
                project.Status = "PLANNING"; // Gán mặc định

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
                var claim = _claimService.GetUserClaim();
                List<Project> projects;

                if (string.Equals(claim.Role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase))
                {
                    projects = await _unitOfWork.Projects.GetAllAsync(null);
                }
                else if (string.Equals(claim.Role, AppRoles.ProjectManager, StringComparison.OrdinalIgnoreCase))
                {
                    projects = await _unitOfWork.Projects.GetAllAsync(p => p.ProjectManagerId == claim.Id);
                }
                else
                {
                    return apiResponse.SetNotFound("No projects available for this role.");
                }

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
                var claim = _claimService.GetUserClaim();
                Project? project;

                if (string.Equals(claim.Role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase))
                {
                    project = await _unitOfWork.Projects.GetByIdAsync(id);
                }
                else if (string.Equals(claim.Role, AppRoles.ProjectManager, StringComparison.OrdinalIgnoreCase))
                {
                    project = (await _unitOfWork.Projects.GetAllAsync(p => p.ProjectId == id && p.ProjectManagerId == claim.Id)).FirstOrDefault();
                }
                else
                {
                    return apiResponse.SetNotFound("Project not found or access denied.");
                }

                if (project == null)
                {
                    return apiResponse.SetNotFound("Project not found or access denied.");
                }

                var response = _mapper.Map<ProjectResponse>(project);
                return apiResponse.SetOk(response);
            }
            catch (Exception ex)
            {
                return apiResponse.SetBadRequest(ex.Message);
            }
        }

        public async Task<ApiResponse> UpdateProjectStatusAsync(int id, UpdateProjectStatusRequest request)
        {
            var apiResponse = new ApiResponse();
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Status))
                {
                    return apiResponse.SetBadRequest("Project status is required.");
                }

                var claim = _claimService.GetUserClaim();
                Project? project;

                if (string.Equals(claim.Role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase))
                {
                    project = await _unitOfWork.Projects.GetByIdAsync(id);
                }
                else if (string.Equals(claim.Role, AppRoles.ProjectManager, StringComparison.OrdinalIgnoreCase))
                {
                    project = (await _unitOfWork.Projects.GetAllAsync(p => p.ProjectId == id && p.ProjectManagerId == claim.Id)).FirstOrDefault();
                }
                else
                {
                    return apiResponse.SetNotFound("Project not found or access denied.");
                }

                if (project == null)
                {
                    return apiResponse.SetNotFound("Project not found or access denied.");
                }

                project.Status = request.Status.Trim();
                _unitOfWork.Projects.Update(project);
                await _unitOfWork.SaveChangeAsync();

                return apiResponse.SetOk("Project status updated successfully");
            }
            catch (Exception ex)
            {
                return apiResponse.SetBadRequest(ex.Message);
            }
        }
    }
}
