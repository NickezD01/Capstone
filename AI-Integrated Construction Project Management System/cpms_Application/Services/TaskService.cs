using AutoMapper;
using cpms_Application.Interfaces;
using cpms_Application.Request.Tasks;
using cpms_Application.Response;
using cpms_Application.Response.Tasks;
using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// 🚀 KHẮC PHỤC LỖI AMBIGUOUS: Đặt bí danh cho Enum của Domain
using DomainTaskStatus = cpms_Domain.Models.TaskStatus;

namespace cpms_Application.Services
{
    public class TaskService : ITaskService
    {
        private readonly IUnitOfWork _uow;
        private readonly IMapper _mapper;

        public TaskService(IUnitOfWork uow, IMapper mapper)
        {
            _uow = uow;
            _mapper = mapper;
        }

        public async Task<ApiResponse> CreateTaskAsync(CreateTaskRequest request)
        {
            var response = new ApiResponse();
            try
            {
                var user = await _uow.UserAccounts.GetAsync(u => u.Id == request.AssignedToUserID);
                if (user == null) return response.SetNotFound("Nhân sự được giao việc không tồn tại.");

                var taskItem = _mapper.Map<TaskItem>(request);
                taskItem.ActualCost = 0;
                taskItem.ActualProgressPct = 0;

                // 🚀 Gán trạng thái thông qua Alias mới đặt
                taskItem.Status = DomainTaskStatus.PENDING;

                await _uow.TaskItems.AddAsync(taskItem);
                await _uow.SaveChangeAsync();

                return response.SetOk("Khởi tạo đầu việc dự án thành công!");
            }
            catch (Exception ex)
            {
                return response.SetBadRequest("Lỗi tạo đầu việc: " + ex.Message);
            }
        }

        public async Task<ApiResponse> GetTasksByProjectAsync(int projectId)
        {
            var response = new ApiResponse();
            try
            {
                var tasks = await _uow.TaskItems.GetAllAsync(
                    filter: t => t.ProjectId == projectId,
                    include: query => query.Include(t => t.AssignedToUser)
                );

                var result = _mapper.Map<IEnumerable<TaskResponse>>(tasks);
                return response.SetOk(result);
            }
            catch (Exception ex)
            {
                return response.SetBadRequest("Lỗi lấy danh sách đầu việc: " + ex.Message);
            }
        }
    }
}