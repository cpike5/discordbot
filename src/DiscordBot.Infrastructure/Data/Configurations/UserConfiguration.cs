using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the User entity.
/// </summary>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        // ulong is not natively supported, store as long and convert
        builder.Property(u => u.Id)
            .HasConversion<long>()
            .ValueGeneratedNever();

        builder.Property(u => u.Username)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(u => u.Discriminator)
            .IsRequired()
            .HasMaxLength(4)
            .HasDefaultValue("0");

        builder.Property(u => u.FirstSeenAt)
            .IsRequired();

        builder.Property(u => u.LastSeenAt)
            .IsRequired();

        builder.Property(u => u.AvatarHash)
            .HasMaxLength(64);

        builder.Property(u => u.GlobalDisplayName)
            .HasMaxLength(32);

        // Index for recently active user queries
        builder.HasIndex(u => u.LastSeenAt);
    }
}
