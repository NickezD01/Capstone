using cpms_Application.Interfaces;
using cpms_Application.Request.WorkCategory;
using cpms_Application.Response;
using cpms_Application.Response.WorkCategory;
using cpms_Domain.Models;
using System.Net;

namespace cpms_Application.Services;

public sealed class WorkCategoryService : IWorkCategoryService
{
    private readonly IUnitOfWork _unitOfWork;

    public WorkCategoryService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse> GetAllAsync()
    {
        var categories = await _unitOfWork.WorkCategories.GetAllAsync(null);
        return new ApiResponse().SetOk(categories
            .OrderBy(c => c.Name)
            .Select(ToResponse)
            .ToList());
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var category = await _unitOfWork.WorkCategories.GetByIdAsync(id);
        if (category == null)
            return new ApiResponse().SetNotFound("Work category not found.");
        return new ApiResponse().SetOk(ToResponse(category));
    }

    public async Task<ApiResponse> CreateAsync(CreateWorkCategoryRequest request)
    {
        var name = (request?.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            return new ApiResponse().SetBadRequest("Work category name is required and must not exceed 200 characters.");
        if (request!.Description?.Trim().Length > 2000)
            return new ApiResponse().SetBadRequest("Work category description must not exceed 2000 characters.");
        var duplicate = await _unitOfWork.WorkCategories.GetAsync(c => c.Name.ToLower() == name.ToLower());
        if (duplicate != null)
            return new ApiResponse().SetConflict("A work category with this name already exists.");

        var category = new WorkCategory
        {
            Name = name,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim()
        };
        await _unitOfWork.WorkCategories.AddAsync(category);
        await _unitOfWork.SaveChangeAsync();
        return new ApiResponse().SetApiResponse(HttpStatusCode.Created, true, result: ToResponse(category));
    }

    public async Task<ApiResponse> UpdateAsync(int id, UpdateWorkCategoryRequest request)
    {
        var category = await _unitOfWork.WorkCategories.GetByIdAsync(id);
        if (category == null)
            return new ApiResponse().SetNotFound("Work category not found.");
        var name = (request?.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            return new ApiResponse().SetBadRequest("Work category name is required and must not exceed 200 characters.");
        if (request!.Description?.Trim().Length > 2000)
            return new ApiResponse().SetBadRequest("Work category description must not exceed 2000 characters.");
        var duplicate = await _unitOfWork.WorkCategories.GetAsync(c =>
            c.WorkCategoryId != id && c.Name.ToLower() == name.ToLower());
        if (duplicate != null)
            return new ApiResponse().SetConflict("A work category with this name already exists.");

        category.Name = name;
        category.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        await _unitOfWork.SaveChangeAsync();
        return new ApiResponse().SetOk(ToResponse(category));
    }

    public async Task<ApiResponse> DeleteAsync(int id)
    {
        var category = await _unitOfWork.WorkCategories.GetByIdAsync(id);
        if (category == null)
            return new ApiResponse().SetNotFound("Work category not found.");
        var inUse = await _unitOfWork.Phases.GetAsync(p => p.WorkCategoryId == id);
        if (inUse != null)
            return new ApiResponse().SetConflict("Cannot delete a work category that phases still reference.");

        _unitOfWork.WorkCategories.Remove(category);
        await _unitOfWork.SaveChangeAsync();
        return new ApiResponse().SetOk("Work category deleted successfully.");
    }

    private static WorkCategoryResponse ToResponse(WorkCategory category) => new()
    {
        WorkCategoryId = category.WorkCategoryId,
        Name = category.Name,
        Description = category.Description
    };
}
