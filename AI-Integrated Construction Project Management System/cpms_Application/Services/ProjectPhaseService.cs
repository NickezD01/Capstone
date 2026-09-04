using AutoMapper;
using cpms_Application.Interfaces;
using cpms_Application.Request.ProjectPhase;
using cpms_Application.Response;
using cpms_Application.Response.ProjectPhase;
using cpms_Domain.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace cpms_Application.Services
{
    public class ProjectPhaseService : IProjectPhaseService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public ProjectPhaseService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<ApiResponse> CreateAsync(CreateProjectPhaseRequest request)
        {
            var apiResponse = new ApiResponse();
            try
            {
                var phase = _mapper.Map<ProjectPhase>(request);
                await _unitOfWork.ProjectPhases.AddAsync(phase);
                await _unitOfWork.SaveChangeAsync();
                return apiResponse.SetOk(_mapper.Map<ProjectPhaseResponse>(phase));
            }
            catch (Exception)
            {
                return apiResponse.SetApiResponse(System.Net.HttpStatusCode.InternalServerError, false, "Unable to create phase.");
            }
        }

        public async Task<ApiResponse> GetByIdAsync(int id)
        {
            var apiResponse = new ApiResponse();
            try
            {
                var phase = await _unitOfWork.ProjectPhases.GetByIdAsync(id);
                if (phase == null) return apiResponse.SetNotFound("Phase not found.");
                return apiResponse.SetOk(_mapper.Map<ProjectPhaseResponse>(phase));
            }
            catch (Exception)
            {
                return apiResponse.SetApiResponse(System.Net.HttpStatusCode.InternalServerError, false, "Unable to retrieve phase.");
            }
        }

        public async Task<ApiResponse> GetByProjectIdAsync(int projectId)
        {
            var apiResponse = new ApiResponse();
            try
            {
                var phases = await _unitOfWork.ProjectPhases.GetAllAsync(p => p.ProjectId == projectId);
                return apiResponse.SetOk(_mapper.Map<IEnumerable<ProjectPhaseResponse>>(phases));
            }
            catch (Exception)
            {
                return apiResponse.SetApiResponse(System.Net.HttpStatusCode.InternalServerError, false, "Unable to retrieve project phases.");
            }
        }

        public async Task<ApiResponse> UpdateAsync(UpdateProjectPhaseRequest request)
        {
            var apiResponse = new ApiResponse();
            try
            {
                var phase = await _unitOfWork.ProjectPhases.GetByIdAsync(request.ProjectPhaseId);
                if (phase == null) return apiResponse.SetNotFound("Phase not found.");
                _mapper.Map(request, phase);
                _unitOfWork.ProjectPhases.Update(phase);
                await _unitOfWork.SaveChangeAsync();
                return apiResponse.SetOk(_mapper.Map<ProjectPhaseResponse>(phase));
            }
            catch (Exception)
            {
                return apiResponse.SetApiResponse(System.Net.HttpStatusCode.InternalServerError, false, "Unable to update phase.");
            }
        }

        public async Task<ApiResponse> DeleteAsync(int id)
        {
            var apiResponse = new ApiResponse();
            try
            {
                await _unitOfWork.ProjectPhases.RemoveByIdAsync(id);
                await _unitOfWork.SaveChangeAsync();
                return apiResponse.SetOk(true);
            }
            catch (Exception)
            {
                return apiResponse.SetApiResponse(System.Net.HttpStatusCode.InternalServerError, false, "Unable to delete phase.");
            }
        }
    }
}
