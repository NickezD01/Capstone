using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cpms_Infrastructure.Configuration
{
    public class WorkCategoryConfiguration : IEntityTypeConfiguration<WorkCategory>
    {
        public void Configure(EntityTypeBuilder<WorkCategory> builder)
        {
            builder.ToTable("WorkCategories");
            builder.HasKey(w => w.WorkCategoryId);

            builder.Property(w => w.Name).IsRequired().HasMaxLength(200);
            builder.Property(w => w.Description).HasMaxLength(2000);
            builder.Property(w => w.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(w => w.IsDeleted).HasDefaultValue(false);

            builder.HasIndex(w => w.Name)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");

            builder.HasQueryFilter(w => !w.IsDeleted);
        }
    }
}
