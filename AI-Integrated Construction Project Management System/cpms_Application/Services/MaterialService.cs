using AutoMapper;
using cpms_Application.Interfaces;
using cpms_Application.Request.Material;
using cpms_Application.Response;
using cpms_Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace cpms_Application.Services
{
    public class MaterialService : IMaterialService
    {
        private readonly IUnitOfWork _uow;
        private readonly IMapper _mapper;

        public MaterialService(IUnitOfWork uow, IMapper mapper)
        {
            _uow = uow;
            _mapper = mapper;
        }

        // 1. Tạo mới vật tư
        public async Task<ApiResponse> CreateMaterialAsync(CreateMaterialRequest request)
        {
            var response = new ApiResponse();
            try
            {
                var material = _mapper.Map<Material>(request);
                await _uow.Materials.AddAsync(material);
                await _uow.SaveChangeAsync();
                return response.SetOk("Material created successfully");
            }
            catch (Exception ex)
            {
                var errorMsg = ex.InnerException?.InnerException != null
                    ? ex.InnerException.InnerException.Message
                    : (ex.InnerException != null ? ex.InnerException.Message : ex.Message);
                return response.SetBadRequest("Error creating material: " + errorMsg);
            }
        }

        // 2. Lấy toàn bộ danh sách vật tư
        public async Task<ApiResponse> GetAllMaterialsAsync()
        {
            var response = new ApiResponse();
            try
            {
                var data = await _uow.Materials.GetAllAsync(null);
                return response.SetOk(data);
            }
            catch (Exception ex)
            {
                return response.SetBadRequest("Error fetching materials: " + ex.Message);
            }
        }

        // 3. Lấy chi tiết vật tư theo ID
        public async Task<ApiResponse> GetMaterialByIdAsync(int id)
        {
            var response = new ApiResponse();
            try
            {
                var material = await _uow.Materials.GetByIdAsync(id);
                if (material == null)
                    return response.SetNotFound($"Material with ID {id} not found.");

                return response.SetOk(material);
            }
            catch (Exception ex)
            {
                return response.SetBadRequest("Error fetching material detail: " + ex.Message);
            }
        }

        // 4. Cập nhật thông tin vật tư
        public async Task<ApiResponse> UpdateMaterialAsync(int id, UpdateMaterialRequest request)
        {
            var response = new ApiResponse();
            try
            {
                // Tìm xem vật tư có tồn tại không
                var material = await _uow.Materials.GetByIdAsync(id);
                if (material == null)
                    return response.SetNotFound($"Material with ID {id} not found.");

                // Map đè dữ liệu thay đổi từ request vào thực thể gốc
                _mapper.Map(request, material);

                // Cập nhật trạng thái theo dõi của EF Core (nếu repo của bạn có hàm Update)
                _uow.Materials.Update(material);
                await _uow.SaveChangeAsync();

                return response.SetOk("Material updated successfully");
            }
            catch (Exception ex)
            {
                var errorMsg = ex.InnerException?.InnerException != null
                    ? ex.InnerException.InnerException.Message
                    : (ex.InnerException != null ? ex.InnerException.Message : ex.Message);
                return response.SetBadRequest("Error updating material: " + errorMsg);
            }
        }

        // 5. Xóa vật tư
        public async Task<ApiResponse> DeleteMaterialAsync(int id)
        {
            var response = new ApiResponse();
            try
            {
                var material = await _uow.Materials.GetByIdAsync(id);
                if (material == null)
                    return response.SetNotFound($"Material with ID {id} not found.");

                // Tùy thuộc vào dự án của bạn là xóa cứng (Hard Delete) hay xóa mềm (Soft Delete)
                // Dưới đây áp dụng xóa cứng thông qua Repository:
                _uow.Materials.Remove(material);
                await _uow.SaveChangeAsync();

                return response.SetOk("Material deleted successfully");
            }
            catch (Exception ex)
            {
                var errorMsg = ex.InnerException?.InnerException != null
                    ? ex.InnerException.InnerException.Message
                    : (ex.InnerException != null ? ex.InnerException.Message : ex.Message);
                return response.SetBadRequest("Error deleting material: " + errorMsg);
            }
        }
    }
}