using cpms_Application.Request.SupplierRecommendation;
using cpms_Application.Response;

namespace cpms_Application.Interfaces
{
    public interface ISupplierRecommendationService
    {
        Task<ApiResponse> RecommendBalancedSuppliersAsync(BalancedSupplierRecommendationRequest request);
    }
}
