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
        Task<ApiResponse> CreateMaterialAsync(CreateMaterialRequest request);
        Task<ApiResponse> GetAllMaterialsAsync();
    }
}
