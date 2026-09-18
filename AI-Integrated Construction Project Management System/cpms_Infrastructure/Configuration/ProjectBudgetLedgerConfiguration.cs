using cpms_Domain.Ledger;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cpms_Infrastructure.Configuration;

public sealed class ProjectBudgetLedgerConfiguration : IEntityTypeConfiguration<ProjectBudgetLedger>
{
    public void Configure(EntityTypeBuilder<ProjectBudgetLedger> builder)
    {
        builder.ToTable("ProjectBudgetLedgers", table =>
        {
            table.HasCheckConstraint("CK_ProjectBudgetLedgers_EntryType", "[EntryType] IN ('ISSUE','CORRECTION','RETURN')");
            table.HasCheckConstraint("CK_ProjectBudgetLedgers_ActualCost", "[ActualCost] >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EstimatedCost).HasColumnType("decimal(18,2)");
        builder.Property(x => x.ActualCost).HasColumnType("decimal(18,2)");
        builder.Property(x => x.BudgetDebitedAmount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.EntryType).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.RecordedAt).HasDefaultValueSql("GETUTCDATE()");
        builder.HasIndex(x => new { x.ProjectId, x.RecordedAt });
        builder.HasIndex(x => new { x.MaterialRequestId, x.RecordedAt });
        builder.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.MaterialRequest).WithMany().HasForeignKey(x => x.MaterialRequestId).OnDelete(DeleteBehavior.Restrict);
    }
}
