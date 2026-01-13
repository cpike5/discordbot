using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the CommandModuleConfiguration entity.
/// </summary>
public class CommandModuleConfigurationConfiguration : IEntityTypeConfiguration<CommandModuleConfiguration>
{
    public void Configure(EntityTypeBuilder<CommandModuleConfiguration> builder)
    {
        builder.ToTable("CommandModuleConfigurations");

        // Primary key is ModuleName
        builder.HasKey(c => c.ModuleName);

        builder.Property(c => c.ModuleName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.IsEnabled)
            .IsRequired();

        builder.Property(c => c.DisplayName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.Description)
            .HasMaxLength(500);

        builder.Property(c => c.Category)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.RequiresRestart)
            .IsRequired();

        builder.Property(c => c.LastModifiedAt)
            .IsRequired();

        builder.Property(c => c.LastModifiedBy)
            .HasMaxLength(450); // Match ASP.NET Identity user ID length

        // Index for efficient category queries
        builder.HasIndex(c => c.Category);

        // Index for efficient enabled state queries
        builder.HasIndex(c => c.IsEnabled);

        // Index for efficient timestamp queries (audit)
        builder.HasIndex(c => c.LastModifiedAt);
    }
}
