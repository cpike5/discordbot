using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="PriceEntry"/> entity.
/// </summary>
public class PriceEntryConfiguration : IEntityTypeConfiguration<PriceEntry>
{
    public void Configure(EntityTypeBuilder<PriceEntry> builder)
    {
        builder.ToTable("PriceEntries");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.FeatureKey)
            .IsRequired()
            .HasMaxLength(128);

        // Null means the price applies everywhere the feature is used.
        builder.Property(p => p.GuildId)
            .HasConversion<long?>();

        builder.Property(p => p.Amount)
            .IsRequired();

        // List<ulong> stored as JSON with ulong to long conversion, as CommandRoleRestriction does.
        builder.Property(p => p.ExemptRoleIds)
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v.Select(id => (long)id).ToList(), (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<List<long>>(v, (System.Text.Json.JsonSerializerOptions?)null)!
                    .Select(id => (ulong)id).ToList())
            .IsRequired()
            .Metadata.SetValueComparer(new ValueComparer<List<ulong>>(
                (left, right) => left != null && right != null && left.SequenceEqual(right),
                value => value.Aggregate(0, (hash, id) => HashCode.Combine(hash, id)),
                value => value.ToList()));

        builder.Property(p => p.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(p => p.UpdatedById)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .IsRequired();

        // At most one price per feature per guild, so a feature costs one currency, not a menu.
        // Both providers treat NULLs in a unique index as distinct, so the global (null guild)
        // entries are kept unique by ICurrencyService.SetPriceAsync instead.
        builder.HasIndex(p => new { p.FeatureKey, p.GuildId })
            .IsUnique()
            .HasDatabaseName("IX_PriceEntries_FeatureKey_GuildId");

        builder.HasIndex(p => p.GuildId)
            .HasDatabaseName("IX_PriceEntries_GuildId");

        builder.HasOne(p => p.Currency)
            .WithMany()
            .HasForeignKey(p => p.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
