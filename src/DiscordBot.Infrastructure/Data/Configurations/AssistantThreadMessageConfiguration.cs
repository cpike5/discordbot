using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>EF Core configuration for <see cref="AssistantThreadMessage"/>.</summary>
public class AssistantThreadMessageConfiguration : IEntityTypeConfiguration<AssistantThreadMessage>
{
    public void Configure(EntityTypeBuilder<AssistantThreadMessage> builder)
    {
        builder.ToTable("AssistantThreadMessages");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedOnAdd();

        builder.Property(m => m.ThreadId)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(m => m.UserId)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(m => m.Role)
            .IsRequired()
            .HasMaxLength(20);

        // Same cap as the DM history: a question is capped well below this and a reply at 2000
        builder.Property(m => m.Content)
            .IsRequired()
            .HasMaxLength(4096);

        builder.Property(m => m.Timestamp).IsRequired();

        // The window read: newest rows of one thread
        builder.HasIndex(m => new { m.ThreadId, m.Id })
            .HasDatabaseName("IX_AssistantThreadMessages_ThreadId_Id");

        // The purge path
        builder.HasIndex(m => m.UserId)
            .HasDatabaseName("IX_AssistantThreadMessages_UserId");

        builder.HasOne(m => m.Thread)
            .WithMany()
            .HasForeignKey(m => m.ThreadId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
