using cpms_Application.Request.SupplierCatalog;
using cpms_Application.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Interfaces
{
    public interface ICatalogService
    {
        Task<ApiResponse> AddMaterialToCatalogAsync(CreateCatalogRequest request);
        Task<ApiResponse> GetCatalogOffersAsync(int? supplierId, int? variantId, bool availableOnly = true);
        Task<ApiResponse> GetCatalogOfferByIdAsync(int catalogId);
        Task<ApiResponse> UpdateCatalogOfferAsync(int catalogId, UpdateCatalogRequest request);
        Task<ApiResponse> DeactivateCatalogOfferAsync(int catalogId);
    }
}
