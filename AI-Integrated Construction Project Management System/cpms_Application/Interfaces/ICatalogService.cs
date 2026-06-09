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
    }
}
