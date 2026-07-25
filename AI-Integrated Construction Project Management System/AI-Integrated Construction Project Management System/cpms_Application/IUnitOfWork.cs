using cpms_Application.Repository;
using cpms_Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application
{
    public interface IUnitOfWork : IDisposable
    {
        // ========================================================
        // USER MANAGEMENT
        // ========================================================
        IUserAccountRepository UserAccounts { get; }
        IRefreshTokenRepository RefreshTokens { get; }
        IEmailVerificationRepository EmailVerifications { get; }
        IGenericRepository<EmailOutboxMessage> EmailOutboxMessages { get; }

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
        IGenericRepository<MaterialVariant> MaterialVariants { get; }
        ISupplierRepository Suppliers { get; }
        ISupplierCatalogRepository SupplierCatalogs { get; }
        ISupplierMetricRepository SupplierMetrics { get; }
        ICategoryRepository Categories { get; }
        IMaterialRequestRepository MaterialRequests { get; }
        IMaterialRequisitionRepository MaterialRequisitions { get; }
        IGenericRepository<MaterialReturn> MaterialReturns { get; }
        IProjectBudgetHistoryRepository ProjectBudgetHistories { get; }
        IGenericRepository<MrpPlanningRun> MrpPlanningRuns { get; }

        // ========================================================
        // PURCHASING & ORDERS
        // ========================================================
        IPurchaseOrderRepository PurchaseOrders { get; }
        IOrderLineItemRepository OrderLineItems { get; }
        IWarehouseRepository Warehouses { get; }
        IInventoryRepository Inventories { get; }
        IGenericRepository<InventoryReservation> InventoryReservations { get; }
        IGenericRepository<InventoryTransaction> InventoryTransactions { get; }
        IGenericRepository<InventoryAdjustment> InventoryAdjustments { get; }
        IGenericRepository<PhysicalCountSession> PhysicalCountSessions { get; }
        IGenericRepository<PhysicalCountLine> PhysicalCountLines { get; }
        ITaskMaterialRequirementRepository TaskMaterialRequirements { get; }
        IWarehouseTransferRepository WarehouseTransfers { get; }
        IWarehouseTransferItemRepository WarehouseTransferItems { get; }
        IGenericRepository<TransferInventoryReservation> TransferInventoryReservations { get; }

        // ========================================================
        // CORE METHODS
        // ========================================================
        Task SaveChangeAsync();

        // Hỗ trợ quản lý Transaction đồng bộ dữ liệu phức tạp
        Task BeginTransactionAsync();
        Task BeginTransactionAsync(System.Data.IsolationLevel isolationLevel);
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();

        // Các hàm thực thi SQL thuần (Raw SQL) từ code gốc của bạn
        Task<T> ExecuteScalarAsync<T>(string sql);
        Task ExecuteRawSqlAsync(string sql);
    }
}
