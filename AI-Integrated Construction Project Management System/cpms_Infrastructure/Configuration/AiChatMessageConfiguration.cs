using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cpms_Infrastructure.Configuration
{
    public class AiChatMessageConfiguration : IEntityTypeConfiguration<AiChatMessage>
    {
        public void Configure(EntityTypeBuilder<AiChatMessage> builder)
        {
            builder.HasKey(x => x.MessageId);
            builder.Property(x => x.Content).HasMaxLength(8000).IsRequired();
            builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);

            builder.HasOne(x => x.Session)
                .WithMany(x => x.Messages)
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
