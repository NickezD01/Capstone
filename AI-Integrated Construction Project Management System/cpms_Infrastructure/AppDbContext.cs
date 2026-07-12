using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Infrastructure
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Để trống hoặc cấu hình thêm nếu bạn dùng Lazy Loading, Log...
        }

        // ========================================================
        // AUTH, SYSTEM LOGS & SECURITY (Bổ sung theo ERD & Auth Flow)
        // ========================================================
        public DbSet<UserAccount> UserAccounts { get; set; }
        public DbSet<EmailVerification> EmailVerifications { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<ActivityLog> Activities { get; set; }
        public DbSet<AIAlert> AIAlerts { get; set; }
        public DbSet<SystemReport> SystemReports { get; set; }

        // ========================================================
        // CORE PROJECT MANAGEMENT & PROGRESS
        // ========================================================
        public DbSet<Project> Projects { get; set; }
        public DbSet<TaskItem> TaskItems { get; set; }
        public DbSet<ProgressReport> ProgressReports { get; set; }
        public DbSet<ProjectBudgetHistory> ProjectBudgetHistories { get; set; }
        // ========================================================
        // WAREHOUSE & MATERIAL INVENTORY (Đã đồng bộ chuẩn ERD)
        // ========================================================
        public DbSet<Warehouse> Warehouses { get; set; }
        public DbSet<InventoryRecord> InventoryRecords { get; set; } // Thay thế cho Inventories cũ
        public DbSet<Material> Materials { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<MaterialRequest> MaterialRequests { get; set; }
        public DbSet<MaterialRequisition> MaterialRequisitions { get; set; }

        // ========================================================
        // SUPPLIERS & PURCHASING ORDERS
        // ========================================================
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<SupplierCatalog> SupplierCatalogs { get; set; }
        public DbSet<SupplierMetric> SupplierMetrics { get; set; }
        public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
        public DbSet<OrderLineItem> OrderLineItems { get; set; }
        public DbSet<TaskMaterialRequirement> TaskMaterialRequirements { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Tự động quét và nạp trọn bộ các file Configuration không trùng lặp 
            // nằm trong cùng Assembly (thư mục Infrastructure) này.
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }
    }
}
