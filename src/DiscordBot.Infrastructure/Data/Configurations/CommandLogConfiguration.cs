using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the CommandLog entity.
/// </summary>
public class CommandLogConfiguration : IEntityTypeConfiguration<CommandLog>
{
    public void Configure(EntityTypeBuilder<CommandLog> builder)
    {
        builder.ToTable("CommandLogs");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .ValueGeneratedOnAdd();

        // ulong foreign keys converted to long
        builder.Property(c => c.GuildId)
            .HasConversion<long?>();

        builder.Property(c => c.UserId)
            .HasConversion<long>();

        builder.Property(c => c.CommandName)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.Parameters)
            .HasColumnType("TEXT");

        builder.Property(c => c.ExecutedAt)
            .IsRequired();

        builder.Property(c => c.ResponseTimeMs)
            .IsRequired();

        builder.Property(c => c.Success)
            .IsRequired();

        builder.Property(c => c.ErrorMessage)
            .HasMaxLength(2000);

        // Relationships
        builder.HasOne(c => c.Guild)
            .WithMany(g => g.CommandLogs)
            .HasForeignKey(c => c.GuildId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.User)
            .WithMany(u => u.CommandLogs)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for common queries
        builder.HasIndex(c => c.GuildId);
        builder.HasIndex(c => c.UserId);
        builder.HasIndex(c => c.CommandName);
        builder.HasIndex(c => c.ExecutedAt);
        builder.HasIndex(c => c.Success);
    }
}
