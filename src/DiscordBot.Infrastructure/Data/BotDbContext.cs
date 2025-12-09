using DiscordBot.Core.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DiscordBot.Infrastructure.Data;

/// <summary>
/// Entity Framework Core database context for the Discord bot with ASP.NET Core Identity support.
/// </summary>
public class BotDbContext : IdentityDbContext<ApplicationUser>
{
    public BotDbContext(DbContextOptions<BotDbContext> options) : base(options)
    {
    }

    public DbSet<Guild> Guilds => Set<Guild>();
    public new DbSet<User> Users => Set<User>();
    public DbSet<CommandLog> CommandLogs => Set<CommandLog>();
    public DbSet<UserGuildAccess> UserGuildAccess => Set<UserGuildAccess>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // IMPORTANT: Call base first to configure Identity tables
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from the current assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BotDbContext).Assembly);

        // Configure ApplicationUser entity
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            // Configure DiscordUserId to handle SQLite compatibility (ulong -> long conversion)
            entity.Property(e => e.DiscordUserId)
                .HasConversion(
                    v => v.HasValue ? (long)v.Value : (long?)null,
                    v => v.HasValue ? (ulong)v.Value : (ulong?)null);

            // Configure string properties with appropriate max lengths
            entity.Property(e => e.DiscordUsername).HasMaxLength(100);
            entity.Property(e => e.DiscordAvatarUrl).HasMaxLength(500);
            entity.Property(e => e.DisplayName).HasMaxLength(100);

            // Configure indexes for performance
            entity.HasIndex(e => e.DiscordUserId).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.IsActive);

            // Configure default values
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // Configure UserGuildAccess entity
        modelBuilder.Entity<UserGuildAccess>(entity =>
        {
            // Composite primary key
            entity.HasKey(e => new { e.ApplicationUserId, e.GuildId });

            // Configure GuildId to handle SQLite compatibility (ulong -> long conversion)
            entity.Property(e => e.GuildId)
                .HasConversion(
                    v => (long)v,
                    v => (ulong)v);

            // Configure AccessLevel as int for storage
            entity.Property(e => e.AccessLevel)
                .HasConversion<int>();

            // Configure default values
            entity.Property(e => e.GrantedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Configure foreign key relationships
            entity.HasOne(e => e.ApplicationUser)
                .WithMany()
                .HasForeignKey(e => e.ApplicationUserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Guild)
                .WithMany()
                .HasForeignKey(e => e.GuildId)
                .OnDelete(DeleteBehavior.Cascade);

            // Index for efficient guild lookups
            entity.HasIndex(e => e.GuildId);
        });
    }
}
