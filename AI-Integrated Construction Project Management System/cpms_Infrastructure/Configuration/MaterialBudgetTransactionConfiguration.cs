using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cpms_Infrastructure.Configuration
{
    public class MaterialBudgetTransactionConfiguration : IEntityTypeConfiguration<MaterialBudgetTransaction>
    {
        public void Configure(EntityTypeBuilder<MaterialBudgetTransaction> builder)
        {
            builder.ToTable("MaterialBudgetTransactions");
            builder.HasKey(t => t.Id);

            builder.Property(t => t.TransactionType).IsRequired().HasMaxLength(30);
            builder.Property(t => t.Quantity).HasColumnType("decimal(18,4)");
            builder.Property(t => t.Amount).HasColumnType("decimal(18,2)");
            builder.Property(t => t.OldActualCost).HasColumnType("decimal(18,2)");
            builder.Property(t => t.NewActualCost).HasColumnType("decimal(18,2)");
            builder.Property(t => t.DebitedBefore).HasColumnType("decimal(18,2)");
            builder.Property(t => t.DebitedAfter).HasColumnType("decimal(18,2)");
            builder.Property(t => t.Note).HasMaxLength(1000);
            builder.Property(t => t.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            builder.HasIndex(t => t.ProjectId);
            builder.HasIndex(t => t.RequestId);

            builder.HasOne(t => t.Project)
                   .WithMany()
                   .HasForeignKey(t => t.ProjectId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(t => t.MaterialRequest)
                   .WithMany()
                   .HasForeignKey(t => t.RequestId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
