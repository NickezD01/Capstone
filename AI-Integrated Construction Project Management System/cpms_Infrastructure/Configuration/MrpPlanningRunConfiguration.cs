using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cpms_Infrastructure.Configuration;

public class MrpPlanningRunConfiguration : IEntityTypeConfiguration<MrpPlanningRun>
{
    public void Configure(EntityTypeBuilder<MrpPlanningRun> builder)
    {
        builder.ToTable("MrpPlanningRuns");
        builder.HasKey(x => x.RunId);
        builder.Property(x => x.CalculatedAt).HasDefaultValueSql("GETUTCDATE()");
        builder.Property(x => x.SnapshotJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.TransferRecommendationsJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.HasIndex(x => new { x.ProjectId, x.WarehouseId, x.Version }).IsUnique();
        builder.HasIndex(x => x.CalculatedAt);
        builder.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CalculatedBy).WithMany().HasForeignKey(x => x.CalculatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
