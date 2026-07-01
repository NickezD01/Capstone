using AutoMapper;
using cpms_Application.Interfaces;
using cpms_Application.Request.Category;
using cpms_Application.Response;
using cpms_Application.Response.Category;
using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Services
{
    public class CategoryService : ICategoryService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public CategoryService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<ApiResponse> CreateCategoryAsync(CreateCategoryRequest request)
        {
            var apiResponse = new ApiResponse();
            try
            {
                var category = _mapper.Map<Category>(request);
                await _unitOfWork.Categories.AddAsync(category);
                await _unitOfWork.SaveChangeAsync();

                return apiResponse.SetOk("Category created successfully");
            }
            catch (Exception ex)
            {
                return apiResponse.SetBadRequest(ex.InnerException?.Message ?? ex.Message);
            }
        }

        public async Task<ApiResponse> GetAllCategoriesAsync()
        {
            var apiResponse = new ApiResponse();
            try
            {
                // 🛠️ TRUYỀN ĐÚNG THEO REPOSITORY CỦA BẠN:
                // Tham số 1 (filter): null -> không lọc
                // Tham số 2 (include): query => query.Include(...) -> Kéo bảng Materials lên để đếm số lượng
                var categories = await _unitOfWork.Categories.GetAllAsync(
                    filter: null,
                    include: query => query.Include(c => c.Materials)
                );

                var response = _mapper.Map<List<CategoryResponse>>(categories);
                return apiResponse.SetOk(response);
            }
            catch (Exception ex)
            {
                return apiResponse.SetBadRequest(ex.Message);
            }
        }

        public async Task<ApiResponse> GetCategoryByIdAsync(int id)
        {
            var apiResponse = new ApiResponse();
            try
            {
                var category = await _unitOfWork.Categories.GetByIdAsync(id);
                if (category == null) return apiResponse.SetNotFound("Category not found.");

                var response = _mapper.Map<CategoryResponse>(category);
                return apiResponse.SetOk(response);
            }
            catch (Exception ex)
            {
                return apiResponse.SetBadRequest(ex.Message);
            }
        }

        public async Task<ApiResponse> UpdateCategoryAsync(int id, UpdateCategoryRequest request)
        {
            var apiResponse = new ApiResponse();
            try
            {
                var category = await _unitOfWork.Categories.GetByIdAsync(id);
                if (category == null) return apiResponse.SetNotFound("Category not found.");

                // Map đè dữ liệu thay đổi từ request vào thực thể cũ
                _mapper.Map(request, category);

                _unitOfWork.Categories.Update(category);
                await _unitOfWork.SaveChangeAsync();

                return apiResponse.SetOk("Category updated successfully");
            }
            catch (Exception ex)
            {
                return apiResponse.SetBadRequest(ex.InnerException?.Message ?? ex.Message);
            }
        }

        public async Task<ApiResponse> DeleteCategoryAsync(int id)
        {
            var apiResponse = new ApiResponse();
            try
            {
                var category = await _unitOfWork.Categories.GetByIdAsync(id);
                if (category == null) return apiResponse.SetNotFound("Category not found.");

                _unitOfWork.Categories.Remove(category);
                await _unitOfWork.SaveChangeAsync();

                return apiResponse.SetOk("Category deleted successfully");
            }
            catch (Exception ex)
            {
                return apiResponse.SetBadRequest("Cannot delete this category. It might contain materials.");
            }
        }
    }
}
