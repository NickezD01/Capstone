using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cpms_Infrastructure.Configuration
{
    public class MeetingParticipantConfiguration : IEntityTypeConfiguration<MeetingParticipant>
    {
        public void Configure(EntityTypeBuilder<MeetingParticipant> builder)
        {
            builder.HasKey(x => x.ParticipantId);
            builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
            builder.Property(x => x.DisplayName).HasMaxLength(200);
            builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(50);

            builder.HasOne(x => x.Meeting)
                .WithMany(x => x.Participants)
                .HasForeignKey(x => x.MeetingId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
