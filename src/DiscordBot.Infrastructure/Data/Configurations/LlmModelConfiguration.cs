using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the LlmModel entity - the local OpenRouter model catalog.
/// </summary>
public class LlmModelConfiguration : IEntityTypeConfiguration<LlmModel>
{
    public void Configure(EntityTypeBuilder<LlmModel> builder)
    {
        builder.ToTable("LlmModels");

        // Primary key is the OpenRouter slug itself.
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(m => m.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(m => m.Description)
            .HasMaxLength(1000);

        builder.Property(m => m.Vendor)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(m => m.ContextLength)
            .IsRequired()
            .HasDefaultValue(0);

        // Per-million-token USD prices, nullable when the catalog reports unknown ("-1").
        builder.Property(m => m.PromptPricePerMillion)
            .HasColumnType("decimal(18,8)");

        builder.Property(m => m.CompletionPricePerMillion)
            .HasColumnType("decimal(18,8)");

        builder.Property(m => m.CacheReadPricePerMillion)
            .HasColumnType("decimal(18,8)");

        builder.Property(m => m.CacheWritePricePerMillion)
            .HasColumnType("decimal(18,8)");

        builder.Property(m => m.SupportsTools)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(m => m.SupportsImages)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(m => m.ReleasedAt);

        builder.Property(m => m.FirstSeenAt)
            .IsRequired();

        builder.Property(m => m.LastSeenAt)
            .IsRequired();

        builder.Property(m => m.IsAvailable)
            .IsRequired()
            .HasDefaultValue(true);

        // The admin allowlist flag - default false, only ever set by an explicit admin action
        // (or the one-time bootstrap on the first refresh).
        builder.Property(m => m.IsEnabled)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(m => m.EnabledAt);

        builder.Property(m => m.EnabledBy)
            .HasMaxLength(450);

        // Vendor grouping/filtering, and the enabled-only picker/catalog queries.
        builder.HasIndex(m => m.Vendor)
            .HasDatabaseName("IX_LlmModels_Vendor");

        builder.HasIndex(m => m.IsEnabled)
            .HasDatabaseName("IX_LlmModels_IsEnabled");
    }
}
