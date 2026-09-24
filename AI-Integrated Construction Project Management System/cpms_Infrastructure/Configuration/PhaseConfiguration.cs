using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cpms_Infrastructure.Configuration
{
    public class PhaseConfiguration : IEntityTypeConfiguration<Phase>
    {
        public void Configure(EntityTypeBuilder<Phase> builder)
        {
            builder.ToTable("Phases");
            builder.HasKey(p => p.PhaseId);

            builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
            builder.Property(p => p.Description).HasMaxLength(2000);
            builder.Property(p => p.SequenceOrder).IsRequired();
            builder.Property(p => p.Status)
                .IsRequired()
                .HasMaxLength(30)
                .HasConversion<string>();
            builder.Property(p => p.RowVersion).IsRowVersion();
            builder.Property(p => p.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(p => p.IsDeleted).HasDefaultValue(false);

            builder.HasIndex(p => new { p.ProjectId, p.Name })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            builder.HasIndex(p => new { p.ProjectId, p.SequenceOrder });

            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Phases_SequenceOrder", "[SequenceOrder] >= 0");
                t.HasCheckConstraint("CK_Phases_BaselineDates", "[BaselineEnd] >= [BaselineStart]");
            });

            builder.HasQueryFilter(p => !p.IsDeleted);
            builder.HasOne(p => p.Project)
                .WithMany(project => project.Phases)
                .HasForeignKey(p => p.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(p => p.WorkCategory)
                .WithMany(category => category.Phases)
                .HasForeignKey(p => p.WorkCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
