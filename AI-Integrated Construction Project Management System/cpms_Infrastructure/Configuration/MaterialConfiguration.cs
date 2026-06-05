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
            builder.Property(m => m.Unit).HasMaxLength(50);
            builder.Property(m => m.Category).HasMaxLength(100);

            builder.Property(m => m.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(m => m.IsDeleted).HasDefaultValue(false);
            builder.HasQueryFilter(m => !m.IsDeleted);
        }
    }
}
