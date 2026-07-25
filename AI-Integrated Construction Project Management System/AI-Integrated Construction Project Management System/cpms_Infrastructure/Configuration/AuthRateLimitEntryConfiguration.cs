using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cpms_Infrastructure.Configuration;

public sealed class AuthRateLimitEntryConfiguration : IEntityTypeConfiguration<AuthRateLimitEntry>
{
    public void Configure(EntityTypeBuilder<AuthRateLimitEntry> builder)
    {
        builder.ToTable("AuthRateLimitEntries", table =>
            table.HasCheckConstraint("CK_AuthRateLimitEntries_RequestCount", "[RequestCount] >= 0"));
        builder.HasKey(x => x.PartitionKey);
        builder.Property(x => x.PartitionKey).HasMaxLength(64);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => x.WindowStart);
    }
}
