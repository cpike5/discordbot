using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the GuildMember entity.
/// </summary>
public class GuildMemberConfiguration : IEntityTypeConfiguration<GuildMember>
{
    public void Configure(EntityTypeBuilder<GuildMember> builder)
    {
        builder.ToTable("GuildMembers");

        // Composite primary key (GuildId, UserId)
        builder.HasKey(gm => new { gm.GuildId, gm.UserId });

        // ulong is not natively supported, store as long and convert
        builder.Property(gm => gm.GuildId)
            .HasConversion<long>();

        builder.Property(gm => gm.UserId)
            .HasConversion<long>();

        builder.Property(gm => gm.JoinedAt)
            .IsRequired();

        builder.Property(gm => gm.Nickname)
            .HasMaxLength(32);

        builder.Property(gm => gm.CachedRolesJson)
            .HasColumnType("TEXT");

        builder.Property(gm => gm.LastCachedAt)
            .IsRequired();

        builder.Property(gm => gm.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // Navigation properties
        builder.HasOne(gm => gm.Guild)
            .WithMany(g => g.Members)
            .HasForeignKey(gm => gm.GuildId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(gm => gm.User)
            .WithMany(u => u.GuildMemberships)
            .HasForeignKey(gm => gm.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for performance-critical queries
        builder.HasIndex(gm => gm.IsActive)
            .HasDatabaseName("IX_GuildMembers_IsActive");

        builder.HasIndex(gm => gm.LastActiveAt)
            .HasDatabaseName("IX_GuildMembers_LastActiveAt");

        builder.HasIndex(gm => gm.GuildId)
            .HasDatabaseName("IX_GuildMembers_GuildId");

        // Composite index for guild-wide activity queries
        builder.HasIndex(gm => new { gm.GuildId, gm.LastActiveAt })
            .HasDatabaseName("IX_GuildMembers_GuildId_LastActiveAt");

        // Composite index for guild-wide join date queries
        builder.HasIndex(gm => new { gm.GuildId, gm.JoinedAt })
            .HasDatabaseName("IX_GuildMembers_GuildId_JoinedAt");
    }
}
