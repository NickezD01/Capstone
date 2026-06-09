using cpms_Application.Request.Supplier;
using cpms_Application.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Interfaces
{
    public interface ISupplierService
    {
        Task<ApiResponse> CreateSupplierAsync(CreateSupplierRequest request);
        Task<ApiResponse> GetAllSuppliersAsync();
    }
}
