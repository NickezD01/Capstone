using cpms_Application;
using cpms_Application.Repository;
using cpms_Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Infrastructure
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;

        // =========================================
        // PROPERTIES (Chỉ đọc bên ngoài, gán bên trong)
        // =========================================
        public IUserAccountRepository UserAccounts { get; }
        public IProjectRepository Projects { get; }
        public ITaskItemRepository TaskItems { get; }
        public IProgressReportRepository ProgressReports { get; }
        public IMaterialRepository Materials { get; }
        public ISupplierRepository Suppliers { get; }
        public ISupplierCatalogRepository SupplierCatalogs { get; }
        public ISupplierMetricRepository SupplierMetrics { get; }
        public IPurchaseOrderRepository PurchaseOrders { get; }
        public IOrderLineItemRepository OrderLineItems { get; }
        public IEmailVerificationRepository EmailVerifications { get; }
        public IRefreshTokenRepository RefreshTokens { get; }
        public ICategoryRepository Categories { get; }
        public IWarehouseRepository Warehouses { get; }
        public IInventoryRepository Inventories { get; }


        public IMaterialRequestRepository MaterialRequests { get; }
        public IMaterialRequisitionRepository MaterialRequisitions { get; }


        // =========================================
        // CONSTRUCTOR (Khởi tạo toàn bộ Repo bằng context)
        // =========================================
        public UnitOfWork(AppDbContext context)
        {
            _context = context;

            UserAccounts = new UserAccountRepository(context);
            Projects = new ProjectRepository(context);
            TaskItems = new TaskItemRepository(context);
            ProgressReports = new ProgressReportRepository(context);
            Materials = new MaterialRepository(context);
            Suppliers = new SupplierRepository(context);
            SupplierCatalogs = new SupplierCatalogRepository(context);
            SupplierMetrics = new SupplierMetricRepository(context);
            PurchaseOrders = new PurchaseOrderRepository(context);
            OrderLineItems = new OrderLineItemRepository(context);
            EmailVerifications = new EmailVerificationRepository(context);
            RefreshTokens = new RefreshTokenRepository(context);
            Categories = new CategoryRepository(context);
            Warehouses = new WarehouseRepository(context);
            Inventories = new InventoryRepository(context);

 
            MaterialRequests = new MaterialRequestRepository(context);
            MaterialRequisitions = new MaterialRequisitionRepository(context);
        }

        // =========================================
        // SAVE CHANGES
        // =========================================
        public async Task SaveChangeAsync()
        {
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        // =========================================
        // TRANSACTION MANAGEMENT
        // =========================================
        public async Task BeginTransactionAsync() => await _context.Database.BeginTransactionAsync();
        public async Task CommitTransactionAsync() => await _context.Database.CommitTransactionAsync();
        public async Task RollbackTransactionAsync() => await _context.Database.RollbackTransactionAsync();

        // =========================================
        // EXECUTE SCALAR
        // =========================================
        public async Task<T> ExecuteScalarAsync<T>(string sql)
        {
            using var command = _context.Database.GetDbConnection().CreateCommand();
            try
            {
                if (command.Connection!.State != ConnectionState.Open)
                {
                    await command.Connection.OpenAsync();
                }
                command.CommandText = sql;
                var result = await command.ExecuteScalarAsync();
                return (T)Convert.ChangeType(result!, typeof(T));
            }
            finally
            {
                if (command.Connection!.State == ConnectionState.Open)
                {
                    await command.Connection.CloseAsync();
                }
            }
        }

        // =========================================
        // EXECUTE RAW SQL
        // =========================================
        public async Task ExecuteRawSqlAsync(string sql)
        {
            using var command = _context.Database.GetDbConnection().CreateCommand();
            try
            {
                if (command.Connection!.State != ConnectionState.Open)
                {
                    await command.Connection.OpenAsync();
                }
                command.CommandText = sql;
                await command.ExecuteNonQueryAsync();
            }
            finally
            {
                if (command.Connection!.State == ConnectionState.Open)
                {
                    await command.Connection.CloseAsync();
                }
            }
        }

        // =========================================
        // DISPOSE
        // =========================================
        public void Dispose()
        {
            _context.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}