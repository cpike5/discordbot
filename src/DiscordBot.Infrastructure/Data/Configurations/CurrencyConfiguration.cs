using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="Currency"/> entity.
/// </summary>
public class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
{
    public void Configure(EntityTypeBuilder<Currency> builder)
    {
        builder.ToTable("Currencies");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Scope)
            .IsRequired()
            .HasConversion<int>();

        // ulong is not natively supported, store as long and convert
        builder.Property(c => c.GuildId)
            .HasConversion<long?>();

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(c => c.Symbol)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(c => c.IsTransferable)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(c => c.AllowNegative)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(c => c.IncomeInterval)
            .HasConversion<int?>();

        builder.Property(c => c.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(c => c.CreatedById)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        // Unique within the scope: one "Coins" per guild, one "Bot Credit" among the globals.
        // Both providers treat NULLs in a unique index as distinct, so this constrains guild
        // currencies only; ICurrencyService.CreateAsync/UpdateAsync does the case-insensitive
        // check that also covers globals.
        builder.HasIndex(c => new { c.Scope, c.GuildId, c.Name })
            .IsUnique()
            .HasDatabaseName("IX_Currencies_Scope_GuildId_Name");

        builder.HasIndex(c => c.GuildId)
            .HasDatabaseName("IX_Currencies_GuildId");
    }
}
