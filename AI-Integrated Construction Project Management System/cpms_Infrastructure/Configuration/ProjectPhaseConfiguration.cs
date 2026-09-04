using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cpms_Infrastructure.Configuration
{
    public class ProjectPhaseConfiguration : IEntityTypeConfiguration<ProjectPhase>
    {
        public void Configure(EntityTypeBuilder<ProjectPhase> builder)
        {
            builder.ToTable("ProjectPhases");
            builder.HasKey(p => p.ProjectPhaseId);

            builder.Property(p => p.PhaseName).IsRequired().HasMaxLength(100);
            builder.Property(p => p.Description).HasMaxLength(500);
            builder.Property(p => p.StartDate).IsRequired();
            builder.Property(p => p.EndDate).IsRequired();

            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_ProjectPhases_Dates", "[EndDate] >= [StartDate]");
            });

            builder.Property(p => p.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(p => p.IsDeleted).HasDefaultValue(false);
            builder.HasQueryFilter(p => !p.IsDeleted);

            builder.HasOne(p => p.Project)
                   .WithMany()
                   .HasForeignKey(p => p.ProjectId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
