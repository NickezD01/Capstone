using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cpms_Infrastructure.Configuration
{
    public class ChatConversationConfiguration : IEntityTypeConfiguration<ChatConversation>
    {
        public void Configure(EntityTypeBuilder<ChatConversation> builder)
        {
            builder.HasKey(x => x.ConversationId);
            builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
            builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(50);

            builder.HasOne(x => x.Project)
                .WithMany()
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Task)
                .WithMany()
                .HasForeignKey(x => x.TaskId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
