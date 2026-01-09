using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the CommandRoleRestriction entity.
/// </summary>
public class CommandRoleRestrictionConfiguration : IEntityTypeConfiguration<CommandRoleRestriction>
{
    public void Configure(EntityTypeBuilder<CommandRoleRestriction> builder)
    {
        builder.ToTable("CommandRoleRestrictions");

        // Primary key on Id (auto-generated)
        builder.HasKey(r => r.Id);

        // ulong property converted to long for SQLite compatibility
        builder.Property(r => r.GuildId)
            .HasConversion<long>()
            .IsRequired();

        // String property
        builder.Property(r => r.CommandName)
            .IsRequired()
            .HasMaxLength(50);

        // List<ulong> stored as JSON with ulong to long conversion
        builder.Property(r => r.AllowedRoleIds)
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v.Select(id => (long)id).ToList(), (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<List<long>>(v, (System.Text.Json.JsonSerializerOptions?)null)!
                    .Select(id => (ulong)id).ToList()
            )
            .IsRequired();

        // Index on (GuildId, CommandName) for efficient lookups
        builder.HasIndex(r => new { r.GuildId, r.CommandName });

        // Relationship with GuildAudioSettings
        builder.HasOne(r => r.GuildAudioSettings)
            .WithMany(s => s.CommandRoleRestrictions)
            .HasForeignKey(r => r.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
