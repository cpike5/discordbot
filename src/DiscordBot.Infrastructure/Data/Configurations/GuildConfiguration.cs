using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the Guild entity.
/// </summary>
public class GuildConfiguration : IEntityTypeConfiguration<Guild>
{
    public void Configure(EntityTypeBuilder<Guild> builder)
    {
        builder.ToTable("Guilds");

        builder.HasKey(g => g.Id);

        // ulong is not natively supported, store as long and convert
        builder.Property(g => g.Id)
            .HasConversion<long>()
            .ValueGeneratedNever();

        builder.Property(g => g.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(g => g.JoinedAt)
            .IsRequired();

        builder.Property(g => g.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(g => g.Prefix)
            .HasMaxLength(10);

        builder.Property(g => g.Settings)
            .HasColumnType("TEXT");

        // Index for active guild queries
        builder.HasIndex(g => g.IsActive);
    }
}
