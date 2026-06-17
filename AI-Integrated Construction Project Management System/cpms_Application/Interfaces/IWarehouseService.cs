using cpms_Application.Request.Warehouse;
using cpms_Application.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Interfaces
{
    public interface IWarehouseService
    {
        Task<ApiResponse> CreateWarehouseAsync(CreateWarehouseRequest request);
        Task<ApiResponse> GetAllWarehousesAsync();
    }
}
