using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cpms_Infrastructure.Configuration
{
    public class ChatParticipantConfiguration : IEntityTypeConfiguration<ChatParticipant>
    {
        public void Configure(EntityTypeBuilder<ChatParticipant> builder)
        {
            builder.HasKey(x => x.ParticipantId);
            builder.HasIndex(x => new { x.ConversationId, x.UserId }).IsUnique();

            builder.HasOne(x => x.Conversation)
                .WithMany(x => x.Participants)
                .HasForeignKey(x => x.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
