using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using cpms_Domain.Models;

namespace cpms_Infrastructure;

public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<EmailVerification> EmailVerifications { get; set; }

    public virtual DbSet<GoodsReceipt> GoodsReceipts { get; set; }

    public virtual DbSet<GoodsReceiptDetail> GoodsReceiptDetails { get; set; }

    public virtual DbSet<Material> Materials { get; set; }

    public virtual DbSet<MaterialCategory> MaterialCategories { get; set; }

    public virtual DbSet<MaterialInventory> MaterialInventories { get; set; }

    public virtual DbSet<MaterialIssue> MaterialIssues { get; set; }

    public virtual DbSet<MaterialIssueDetail> MaterialIssueDetails { get; set; }

    public virtual DbSet<MaterialRequest> MaterialRequests { get; set; }

    public virtual DbSet<MaterialRequestDetail> MaterialRequestDetails { get; set; }

    public virtual DbSet<ProgressReport> ProgressReports { get; set; }

    public virtual DbSet<Project> Projects { get; set; }

    public virtual DbSet<PurchaseOrder> PurchaseOrders { get; set; }

    public virtual DbSet<PurchaseOrderDetail> PurchaseOrderDetails { get; set; }

    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }

    public virtual DbSet<Supplier> Suppliers { get; set; }

    public virtual DbSet<cpms_Domain.Models.Task> Tasks { get; set; }

    public virtual DbSet<Unit> Units { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<Warehouse> Warehouses { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EmailVerification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__EmailVer__3214EC0771DD61E1");

            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.ModifiedBy).HasMaxLength(100);
            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.VerificationCode).HasMaxLength(20);

            entity.HasOne(d => d.User).WithMany(p => p.EmailVerifications)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__EmailVeri__UserI__797309D9");
        });

        modelBuilder.Entity<GoodsReceipt>(entity =>
        {
            entity.HasKey(e => e.ReceiptId).HasName("PK__GoodsRec__CC08C4000B7F8F5B");

            entity.Property(e => e.ReceiptId).HasColumnName("ReceiptID");
            entity.Property(e => e.Poid).HasColumnName("POID");
            entity.Property(e => e.ReceiptDate).HasColumnType("datetime");
            entity.Property(e => e.WarehouseId).HasColumnName("WarehouseID");

            entity.HasOne(d => d.Po).WithMany(p => p.GoodsReceipts)
                .HasForeignKey(d => d.Poid)
                .HasConstraintName("FK__GoodsRecei__POID__59FA5E80");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.GoodsReceipts)
                .HasForeignKey(d => d.WarehouseId)
                .HasConstraintName("FK__GoodsRece__Wareh__5AEE82B9");
        });

        modelBuilder.Entity<GoodsReceiptDetail>(entity =>
        {
            entity.HasKey(e => e.ReceiptDetailId).HasName("PK__GoodsRec__82FADEDBEF71C994");

            entity.Property(e => e.ReceiptDetailId).HasColumnName("ReceiptDetailID");
            entity.Property(e => e.MaterialId).HasColumnName("MaterialID");
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ReceiptId).HasColumnName("ReceiptID");

            entity.HasOne(d => d.Material).WithMany(p => p.GoodsReceiptDetails)
                .HasForeignKey(d => d.MaterialId)
                .HasConstraintName("FK__GoodsRece__Mater__5EBF139D");

            entity.HasOne(d => d.Receipt).WithMany(p => p.GoodsReceiptDetails)
                .HasForeignKey(d => d.ReceiptId)
                .HasConstraintName("FK__GoodsRece__Recei__5DCAEF64");
        });

        modelBuilder.Entity<Material>(entity =>
        {
            entity.HasKey(e => e.MaterialId).HasName("PK__Material__C5061317E51803D2");

            entity.HasIndex(e => e.MaterialCode, "UQ__Material__170C54BAEDCE75E5").IsUnique();

            entity.Property(e => e.MaterialId).HasColumnName("MaterialID");
            entity.Property(e => e.CategoryId).HasColumnName("CategoryID");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.MaterialCode).HasMaxLength(50);
            entity.Property(e => e.MaterialName).HasMaxLength(255);
            entity.Property(e => e.MinStock).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Specification).HasMaxLength(500);
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.UnitId).HasColumnName("UnitID");

            entity.HasOne(d => d.Category).WithMany(p => p.Materials)
                .HasForeignKey(d => d.CategoryId)
                .HasConstraintName("FK__Materials__Categ__47DBAE45");

            entity.HasOne(d => d.Unit).WithMany(p => p.Materials)
                .HasForeignKey(d => d.UnitId)
                .HasConstraintName("FK__Materials__UnitI__48CFD27E");
        });

        modelBuilder.Entity<MaterialCategory>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("PK__Material__19093A2B95624C95");

            entity.Property(e => e.CategoryId).HasColumnName("CategoryID");
            entity.Property(e => e.CategoryName).HasMaxLength(255);
            entity.Property(e => e.Description).HasMaxLength(500);
        });

        modelBuilder.Entity<MaterialInventory>(entity =>
        {
            entity.HasKey(e => e.InventoryId).HasName("PK__Material__F5FDE6D30A0A5460");

            entity.Property(e => e.InventoryId).HasColumnName("InventoryID");
            entity.Property(e => e.LastUpdated).HasColumnType("datetime");
            entity.Property(e => e.MaterialId).HasColumnName("MaterialID");
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.WarehouseId).HasColumnName("WarehouseID");

            entity.HasOne(d => d.Material).WithMany(p => p.MaterialInventories)
                .HasForeignKey(d => d.MaterialId)
                .HasConstraintName("FK__MaterialI__Mater__4F7CD00D");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.MaterialInventories)
                .HasForeignKey(d => d.WarehouseId)
                .HasConstraintName("FK__MaterialI__Wareh__5070F446");
        });

        modelBuilder.Entity<MaterialIssue>(entity =>
        {
            entity.HasKey(e => e.IssueId).HasName("PK__Material__6C861624CBD57CE9");

            entity.Property(e => e.IssueId).HasColumnName("IssueID");
            entity.Property(e => e.IssueDate).HasColumnType("datetime");
            entity.Property(e => e.ProjectId).HasColumnName("ProjectID");
            entity.Property(e => e.WarehouseId).HasColumnName("WarehouseID");

            entity.HasOne(d => d.Project).WithMany(p => p.MaterialIssues)
                .HasForeignKey(d => d.ProjectId)
                .HasConstraintName("FK__MaterialI__Proje__6A30C649");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.MaterialIssues)
                .HasForeignKey(d => d.WarehouseId)
                .HasConstraintName("FK__MaterialI__Wareh__693CA210");
        });

        modelBuilder.Entity<MaterialIssueDetail>(entity =>
        {
            entity.HasKey(e => e.IssueDetailId).HasName("PK__Material__68ADB57EA7D8289B");

            entity.Property(e => e.IssueDetailId).HasColumnName("IssueDetailID");
            entity.Property(e => e.IssueId).HasColumnName("IssueID");
            entity.Property(e => e.MaterialId).HasColumnName("MaterialID");
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Issue).WithMany(p => p.MaterialIssueDetails)
                .HasForeignKey(d => d.IssueId)
                .HasConstraintName("FK__MaterialI__Issue__6D0D32F4");

            entity.HasOne(d => d.Material).WithMany(p => p.MaterialIssueDetails)
                .HasForeignKey(d => d.MaterialId)
                .HasConstraintName("FK__MaterialI__Mater__6E01572D");
        });

        modelBuilder.Entity<MaterialRequest>(entity =>
        {
            entity.HasKey(e => e.RequestId).HasName("PK__Material__33A8519A89458BA4");

            entity.Property(e => e.RequestId).HasColumnName("RequestID");
            entity.Property(e => e.ProjectId).HasColumnName("ProjectID");
            entity.Property(e => e.RequestDate).HasColumnType("datetime");
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.Project).WithMany(p => p.MaterialRequests)
                .HasForeignKey(d => d.ProjectId)
                .HasConstraintName("FK__MaterialR__Proje__619B8048");

            entity.HasOne(d => d.User).WithMany(p => p.MaterialRequests)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__MaterialR__UserI__628FA481");
        });

        modelBuilder.Entity<MaterialRequestDetail>(entity =>
        {
            entity.HasKey(e => e.RequestDetailId).HasName("PK__Material__DC528B7019124DFF");

            entity.Property(e => e.RequestDetailId).HasColumnName("RequestDetailID");
            entity.Property(e => e.MaterialId).HasColumnName("MaterialID");
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.RequestId).HasColumnName("RequestID");

            entity.HasOne(d => d.Material).WithMany(p => p.MaterialRequestDetails)
                .HasForeignKey(d => d.MaterialId)
                .HasConstraintName("FK__MaterialR__Mater__66603565");

            entity.HasOne(d => d.Request).WithMany(p => p.MaterialRequestDetails)
                .HasForeignKey(d => d.RequestId)
                .HasConstraintName("FK__MaterialR__Reque__656C112C");
        });

        modelBuilder.Entity<ProgressReport>(entity =>
        {
            entity.HasKey(e => e.ReportId).HasName("PK__Progress__D5BD48E5365C38AC");

            entity.Property(e => e.ReportId).HasColumnName("ReportID");
            entity.Property(e => e.ReportDate).HasColumnType("datetime");
            entity.Property(e => e.SitePhotoUrl)
                .HasMaxLength(255)
                .HasColumnName("SitePhotoURL");
            entity.Property(e => e.TaskId).HasColumnName("TaskID");
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.Task).WithMany(p => p.ProgressReports)
                .HasForeignKey(d => d.TaskId)
                .HasConstraintName("FK__ProgressR__TaskI__3F466844");

            entity.HasOne(d => d.User).WithMany(p => p.ProgressReports)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__ProgressR__UserI__403A8C7D");
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.ProjectId).HasName("PK__Projects__761ABED0E533B368");

            entity.Property(e => e.ProjectId).HasColumnName("ProjectID");
            entity.Property(e => e.Address).HasMaxLength(255);
            entity.Property(e => e.ProjectName).HasMaxLength(255);
            entity.Property(e => e.Status).HasMaxLength(255);
            entity.Property(e => e.ProjectManagerId).HasColumnName("ProjectManagerID");
            entity.Property(e => e.CustomerId).HasColumnName("CustomerID");
            entity.Property(e => e.CreatedDate).HasColumnType("datetime").HasDefaultValueSql("GETUTCDATE()");
        });

        modelBuilder.Entity<PurchaseOrder>(entity =>
        {
            entity.HasKey(e => e.Poid).HasName("PK__Purchase__5F02A2F467519381");

            entity.Property(e => e.Poid).HasColumnName("POID");
            entity.Property(e => e.OrderDate).HasColumnType("datetime");
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.SupplierId).HasColumnName("SupplierID");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Supplier).WithMany(p => p.PurchaseOrders)
                .HasForeignKey(d => d.SupplierId)
                .HasConstraintName("FK__PurchaseO__Suppl__534D60F1");
        });

        modelBuilder.Entity<PurchaseOrderDetail>(entity =>
        {
            entity.HasKey(e => e.PodetailId).HasName("PK__Purchase__4EB47B5EB05567BD");

            entity.Property(e => e.PodetailId).HasColumnName("PODetailID");
            entity.Property(e => e.MaterialId).HasColumnName("MaterialID");
            entity.Property(e => e.Poid).HasColumnName("POID");
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Material).WithMany(p => p.PurchaseOrderDetails)
                .HasForeignKey(d => d.MaterialId)
                .HasConstraintName("FK__PurchaseO__Mater__571DF1D5");

            entity.HasOne(d => d.Po).WithMany(p => p.PurchaseOrderDetails)
                .HasForeignKey(d => d.Poid)
                .HasConstraintName("FK__PurchaseOr__POID__5629CD9C");
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.TokenId).HasName("PK__RefreshT__658FEEEABBF7EA3D");

            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.ModifiedBy).HasMaxLength(100);
            entity.Property(e => e.Token).HasMaxLength(500);
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.User).WithMany(p => p.RefreshTokens)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RefreshTo__UserI__73BA3083");
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasKey(e => e.SupplierId).HasName("PK__Supplier__4BE66694FE50ECF4");

            entity.Property(e => e.SupplierId).HasColumnName("SupplierID");
            entity.Property(e => e.Address).HasMaxLength(255);
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.Phone).HasMaxLength(50);
            entity.Property(e => e.SupplierName).HasMaxLength(255);
            entity.Property(e => e.TaxCode).HasMaxLength(100);
        });

        modelBuilder.Entity<cpms_Domain.Models.Task>(entity =>
        {
            entity.HasKey(e => e.TaskId).HasName("PK__Tasks__7C6949D1C9E04D8E");

            entity.Property(e => e.TaskId).HasColumnName("TaskID");
            entity.Property(e => e.PhaseName).HasMaxLength(255);
            entity.Property(e => e.ProjectId).HasColumnName("ProjectID");
            entity.Property(e => e.Status).HasMaxLength(255);
            entity.Property(e => e.TaskName).HasMaxLength(255);

            entity.HasOne(d => d.Project).WithMany(p => p.Tasks)
                .HasForeignKey(d => d.ProjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Tasks__ProjectID__3C69FB99");
        });

        modelBuilder.Entity<Unit>(entity =>
        {
            entity.HasKey(e => e.UnitId).HasName("PK__Units__44F5EC956D13E6CB");

            entity.Property(e => e.UnitId).HasColumnName("UnitID");
            entity.Property(e => e.UnitName).HasMaxLength(100);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Users__1788CCAC10D74689");

            entity.HasIndex(e => e.Email, "UQ__Users__A9D10534488F1B66").IsUnique();

            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.FullName).HasMaxLength(255);
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.Role).HasMaxLength(255);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<Warehouse>(entity =>
        {
            entity.HasKey(e => e.WarehouseId).HasName("PK__Warehous__2608AFD9EBBE1A51");

            entity.Property(e => e.WarehouseId).HasColumnName("WarehouseID");
            entity.Property(e => e.Location).HasMaxLength(255);
            entity.Property(e => e.WarehouseName).HasMaxLength(255);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
