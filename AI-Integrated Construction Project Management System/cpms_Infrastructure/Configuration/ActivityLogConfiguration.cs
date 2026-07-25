using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cpms_Infrastructure.Configuration
{
    public class ActivityLogConfiguration : IEntityTypeConfiguration<ActivityLog>
    {
        public void Configure(EntityTypeBuilder<ActivityLog> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.ActivityName).IsRequired().HasMaxLength(100);
            builder.Property(x => x.EntityType).IsRequired().HasMaxLength(200);
            builder.Property(x => x.EntityId).HasMaxLength(200);
            builder.Property(x => x.ChangesJson).HasMaxLength(8000);
            builder.Property(x => x.CorrelationId).HasMaxLength(100);
            builder.Property(x => x.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            builder.HasOne(x => x.User)
                .WithMany(x => x.Activities)
                .HasForeignKey(x => x.UserID)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
