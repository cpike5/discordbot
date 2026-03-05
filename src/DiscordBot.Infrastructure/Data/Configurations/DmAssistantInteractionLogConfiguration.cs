using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

public class DmAssistantInteractionLogConfiguration : IEntityTypeConfiguration<DmAssistantInteractionLog>
{
    public void Configure(EntityTypeBuilder<DmAssistantInteractionLog> builder)
    {
        builder.ToTable("DmAssistantInteractionLogs");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedOnAdd();

        builder.Property(l => l.UserId)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(l => l.Timestamp).IsRequired();

        builder.Property(l => l.Message)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(l => l.Response)
            .HasMaxLength(2000);

        builder.Property(l => l.ErrorMessage)
            .HasMaxLength(1000);

        builder.Property(l => l.InputTokens).IsRequired().HasDefaultValue(0);
        builder.Property(l => l.OutputTokens).IsRequired().HasDefaultValue(0);
        builder.Property(l => l.CachedTokens).IsRequired().HasDefaultValue(0);
        builder.Property(l => l.LatencyMs).IsRequired().HasDefaultValue(0);
        builder.Property(l => l.Success).IsRequired().HasDefaultValue(true);

        builder.Property(l => l.EstimatedCostUsd)
            .HasColumnType("decimal(18,8)")
            .IsRequired()
            .HasDefaultValue(0m);

        builder.HasIndex(l => new { l.UserId, l.Timestamp })
            .HasDatabaseName("IX_DmAssistantInteractionLogs_UserId_Timestamp");

        builder.HasIndex(l => l.Timestamp)
            .HasDatabaseName("IX_DmAssistantInteractionLogs_Timestamp");

        builder.HasOne(l => l.User)
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
