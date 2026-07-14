using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Infrastructure.Configuration
{
    public class MaterialConfiguration : IEntityTypeConfiguration<Material>
    {
        public void Configure(EntityTypeBuilder<Material> builder)
        {
            builder.ToTable("Materials");
            builder.HasKey(m => m.MaterialId);

            builder.Property(m => m.MaterialName).IsRequired().HasMaxLength(200);
            builder.Property(m => m.DefaultUnit).IsRequired().HasMaxLength(50);
            builder.Property(m => m.Description).HasMaxLength(1000);
            builder.Property(m => m.IsActive).HasDefaultValue(true);

            // Cấu hình quan hệ với Category
            builder.HasOne(m => m.Category)
       .WithMany(c => c.Materials)
       .HasForeignKey(m => m.CategoryId)
       .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(m => m.Variants)
                   .WithOne(v => v.Material)
                   .HasForeignKey(v => v.MaterialId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.Property(m => m.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(m => m.IsDeleted).HasDefaultValue(false);
            builder.HasQueryFilter(m => !m.IsDeleted);
        }
    }
}
