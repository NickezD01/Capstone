using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cpms_Infrastructure.Configuration
{
    public class AiChatSessionConfiguration : IEntityTypeConfiguration<AiChatSession>
    {
        public void Configure(EntityTypeBuilder<AiChatSession> builder)
        {
            builder.HasKey(x => x.SessionId);
            builder.Property(x => x.Title).HasMaxLength(200).IsRequired();

            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Project)
                .WithMany()
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
