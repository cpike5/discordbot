using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the ApplicationSetting entity.
/// </summary>
public class ApplicationSettingConfiguration : IEntityTypeConfiguration<ApplicationSetting>
{
    public void Configure(EntityTypeBuilder<ApplicationSetting> builder)
    {
        builder.ToTable("ApplicationSettings");

        // Primary key
        builder.HasKey(s => s.Key);

        builder.Property(s => s.Key)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.Value)
            .IsRequired()
            .HasMaxLength(4000);

        // Store enums as integers
        builder.Property(s => s.Category)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(s => s.DataType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(s => s.RequiresRestart)
            .IsRequired();

        builder.Property(s => s.LastModifiedAt)
            .IsRequired();

        builder.Property(s => s.LastModifiedBy)
            .HasMaxLength(450); // Match ASP.NET Identity user ID length

        // Index for efficient category queries
        builder.HasIndex(s => s.Category);

        // Index for efficient timestamp queries (audit)
        builder.HasIndex(s => s.LastModifiedAt);
    }
}
