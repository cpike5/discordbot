using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

public class DmConversationMessageConfiguration : IEntityTypeConfiguration<DmConversationMessage>
{
    public void Configure(EntityTypeBuilder<DmConversationMessage> builder)
    {
        builder.ToTable("DmConversationMessages");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedOnAdd();

        builder.Property(m => m.UserId)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(m => m.Role)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(m => m.Content)
            .IsRequired()
            .HasMaxLength(4096);

        builder.Property(m => m.Timestamp)
            .IsRequired();

        builder.HasIndex(m => new { m.UserId, m.Timestamp })
            .HasDatabaseName("IX_DmConversationMessages_UserId_Timestamp");

        builder.HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
