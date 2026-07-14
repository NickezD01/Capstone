using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cpms_Infrastructure.Configuration
{
    public class MaterialVariantConfiguration : IEntityTypeConfiguration<MaterialVariant>
    {
        public void Configure(EntityTypeBuilder<MaterialVariant> builder)
        {
            builder.ToTable("MaterialVariants");
            builder.HasKey(v => v.VariantId);
            builder.Property(v => v.VariantName).IsRequired().HasMaxLength(250);
            builder.Property(v => v.SKU).HasMaxLength(100);
            builder.Property(v => v.Brand).HasMaxLength(150);
            builder.Property(v => v.Grade).HasMaxLength(100);
            builder.Property(v => v.Size).HasMaxLength(100);
            builder.Property(v => v.Color).HasMaxLength(100);
            builder.Property(v => v.Specification).HasMaxLength(1000);
            builder.Property(v => v.Packaging).HasMaxLength(200);
            builder.Property(v => v.Unit).IsRequired().HasMaxLength(50);
            builder.Property(v => v.IsActive).HasDefaultValue(true);
            builder.Property(v => v.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(v => v.IsDeleted).HasDefaultValue(false);
            builder.HasIndex(v => v.SKU).IsUnique().HasFilter("[SKU] IS NOT NULL AND [IsDeleted] = 0");
            builder.HasQueryFilter(v => !v.IsDeleted);
        }
    }
}
