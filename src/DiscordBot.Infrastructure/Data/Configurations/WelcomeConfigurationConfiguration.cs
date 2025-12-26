using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the WelcomeConfiguration entity.
/// </summary>
public class WelcomeConfigurationConfiguration : IEntityTypeConfiguration<WelcomeConfiguration>
{
    public void Configure(EntityTypeBuilder<WelcomeConfiguration> builder)
    {
        builder.ToTable("WelcomeConfigurations");

        // GuildId is the primary key
        builder.HasKey(w => w.GuildId);

        // ulong is not natively supported, store as long and convert
        builder.Property(w => w.GuildId)
            .HasConversion<long>()
            .ValueGeneratedNever();

        builder.Property(w => w.WelcomeChannelId)
            .HasConversion<long?>();

        builder.Property(w => w.IsEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(w => w.WelcomeMessage)
            .IsRequired()
            .HasMaxLength(2000) // Discord message limit
            .HasDefaultValue(string.Empty);

        builder.Property(w => w.IncludeAvatar)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(w => w.UseEmbed)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(w => w.EmbedColor)
            .HasMaxLength(7); // #RRGGBB format

        builder.Property(w => w.CreatedAt)
            .IsRequired();

        builder.Property(w => w.UpdatedAt)
            .IsRequired();

        // Relationship with Guild
        builder.HasOne(w => w.Guild)
            .WithMany()
            .HasForeignKey(w => w.GuildId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index for enabled guilds
        builder.HasIndex(w => w.IsEnabled);
    }
}
