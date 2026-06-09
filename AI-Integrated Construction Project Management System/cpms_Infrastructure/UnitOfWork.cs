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
                // Giữ nguyên logic xử lý lỗi bọc chuỗi lỗi của bạn
                throw new Exception(ex.Message);
            }
        }

        // =========================================
        // TRANSACTION MANAGEMENT (Bổ sung cực quan trọng)
        // =========================================
        public async Task BeginTransactionAsync() => await _context.Database.BeginTransactionAsync();
        public async Task CommitTransactionAsync() => await _context.Database.CommitTransactionAsync();
        public async Task RollbackTransactionAsync() => await _context.Database.RollbackTransactionAsync();

        // =========================================
        // EXECUTE SCALAR (Giữ nguyên logic gốc của bạn)
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
        // EXECUTE RAW SQL (Giữ nguyên logic gốc của bạn)
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
        // DISPOSE (Giải phóng bộ nhớ DbContext chuẩn chỉnh)
        // =========================================
        public void Dispose()
        {
            _context.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
