using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cpms_Infrastructure.Configuration
{
    public class MeetingConfiguration : IEntityTypeConfiguration<Meeting>
    {
        public void Configure(EntityTypeBuilder<Meeting> builder)
        {
            builder.HasKey(x => x.MeetingId);
            builder.Property(x => x.Subject).HasMaxLength(250).IsRequired();
            builder.Property(x => x.Agenda).HasMaxLength(4000);
            builder.Property(x => x.TimeZone).HasMaxLength(100);
            builder.Property(x => x.Provider).HasConversion<string>().HasMaxLength(50);
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);
            builder.Property(x => x.JoinUrl).HasMaxLength(1000);
            builder.Property(x => x.ExternalEventId).HasMaxLength(300);
            builder.Property(x => x.ExternalOnlineMeetingId).HasMaxLength(300);
            builder.Property(x => x.FailureReason).HasMaxLength(2000);

            builder.HasOne(x => x.Project)
                .WithMany()
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Task)
                .WithMany()
                .HasForeignKey(x => x.TaskId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Organizer)
                .WithMany()
                .HasForeignKey(x => x.OrganizerId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
