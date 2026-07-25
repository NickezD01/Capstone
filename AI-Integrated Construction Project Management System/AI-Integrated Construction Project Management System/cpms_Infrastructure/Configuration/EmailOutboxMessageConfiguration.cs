using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cpms_Infrastructure.Configuration;

public sealed class EmailOutboxMessageConfiguration : IEntityTypeConfiguration<EmailOutboxMessage>
{
    public void Configure(EntityTypeBuilder<EmailOutboxMessage> builder)
    {
        builder.ToTable("EmailOutboxMessages");
        builder.HasKey(x => x.MessageId);
        builder.Property(x => x.Recipient).IsRequired().HasMaxLength(320);
        builder.Property(x => x.ProtectedHtmlBody).IsRequired().HasMaxLength(8000);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.ProcessedAt, x.NextAttemptAt });
        builder.ToTable(t => t.HasCheckConstraint("CK_EmailOutboxMessages_AttemptCount", "[AttemptCount] >= 0 AND [AttemptCount] <= 10"));
    }
}
