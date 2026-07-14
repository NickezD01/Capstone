using cpms_Application.Request.Material;
using cpms_Application.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Interfaces
{
    public interface IMaterialService
    {
        Task<ApiResponse> CreateMaterialAsync(MaterialRequest request);
        Task<ApiResponse> GetAllMaterialsAsync();
        Task<ApiResponse> GetMaterialByIdAsync(int id);
        Task<ApiResponse> UpdateMaterialAsync(int id, UpdateMaterialRequest request);
        Task<ApiResponse> DeleteMaterialAsync(int id);
        Task<ApiResponse> CreateVariantAsync(MaterialVariantRequest request);
        Task<ApiResponse> GetVariantsByMaterialAsync(int materialId);
        Task<ApiResponse> UpdateVariantAsync(int variantId, MaterialVariantRequest request);
        Task<ApiResponse> DeleteVariantAsync(int variantId);
    }
}
