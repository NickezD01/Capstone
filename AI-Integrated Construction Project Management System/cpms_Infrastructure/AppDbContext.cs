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

        // ==========================================
        // CÁC DBSET ĐÃ ĐƯỢC ĐỒNG BỘ THEO SCHEMA MỚI
        // ==========================================
        public DbSet<UserAccount> UserAccounts { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<TaskItem> TaskItems { get; set; }
        public DbSet<ProgressReport> ProgressReports { get; set; }
        public DbSet<Material> Materials { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<SupplierCatalog> SupplierCatalogs { get; set; }
        public DbSet<SupplierMetric> SupplierMetrics { get; set; }
        public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
        public DbSet<OrderLineItem> OrderLineItems { get; set; }
        public DbSet<EmailVerification> EmailVerifications { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<Category> Categories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Tự động quét và nạp trọn bộ 10 file Configuration không trùng lặp 
            // nằm trong cùng Assembly (thư mục Infrastructure) này.
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }
    }
}
