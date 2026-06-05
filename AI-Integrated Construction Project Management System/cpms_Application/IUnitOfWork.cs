using cpms_Application.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application
{
    public interface IUnitOfWork 
    {
        // ========================================================
        // USER MANAGEMENT
        // ========================================================
        IUserAccountRepository UserAccounts { get; }
        IRefreshTokenRepository RefreshTokens { get; }
        IEmailVerificationRepository EmailVerifications { get; }

        // ========================================================
        // PROJECT & TASKS
        // ========================================================
        IProjectRepository Projects { get; }
        ITaskItemRepository TaskItems { get; }
        IProgressReportRepository ProgressReports { get; }

        // ========================================================
        // MATERIAL & SUPPLIERS
        // ========================================================
        IMaterialRepository Materials { get; }
        ISupplierRepository Suppliers { get; }
        ISupplierCatalogRepository SupplierCatalogs { get; }
        ISupplierMetricRepository SupplierMetrics { get; }

        // ========================================================
        // PURCHASING & ORDERS
        // ========================================================
        IPurchaseOrderRepository PurchaseOrders { get; }
        IOrderLineItemRepository OrderLineItems { get; }

        // ========================================================
        // CORE METHODS
        // ========================================================
        Task SaveChangeAsync();

        // Hỗ trợ quản lý Transaction đồng bộ dữ liệu phức tạp
        Task BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();

        // Các hàm thực thi SQL thuần (Raw SQL) từ code gốc của bạn
        Task<T> ExecuteScalarAsync<T>(string sql);
        Task ExecuteRawSqlAsync(string sql);
    }
}
