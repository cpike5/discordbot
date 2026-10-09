using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>EF Core configuration for <see cref="AssistantThread"/>.</summary>
public class AssistantThreadConfiguration : IEntityTypeConfiguration<AssistantThread>
{
    public void Configure(EntityTypeBuilder<AssistantThread> builder)
    {
        builder.ToTable("AssistantThreads");

        // The Discord thread id is the key; it is never generated here
        builder.HasKey(t => t.ThreadId);
        builder.Property(t => t.ThreadId)
            .HasConversion<long>()
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(t => t.GuildId)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(t => t.ParentChannelId)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(t => t.StarterUserId)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.LastActivityAt).IsRequired();
        builder.Property(t => t.TurnCount).IsRequired().HasDefaultValue(0);

        builder.Property(t => t.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(t => t.ActiveSkills)
            .IsRequired()
            .HasMaxLength(1024)
            .HasDefaultValue("[]");

        builder.HasIndex(t => t.GuildId)
            .HasDatabaseName("IX_AssistantThreads_GuildId");

        // The retention sweep deletes by last activity
        builder.HasIndex(t => t.LastActivityAt)
            .HasDatabaseName("IX_AssistantThreads_LastActivityAt");

        builder.HasOne(t => t.Guild)
            .WithMany()
            .HasForeignKey(t => t.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
