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
    public DbSet<MessageLog> MessageLogs => Set<MessageLog>();
    public DbSet<UserGuildAccess> UserGuildAccess => Set<UserGuildAccess>();
    public DbSet<UserActivityLog> UserActivityLogs => Set<UserActivityLog>();
    public DbSet<DiscordOAuthToken> DiscordOAuthTokens => Set<DiscordOAuthToken>();
    public DbSet<VerificationCode> VerificationCodes => Set<VerificationCode>();
    public DbSet<ApplicationSetting> ApplicationSettings => Set<ApplicationSetting>();
    public DbSet<UserConsent> UserConsents => Set<UserConsent>();
    public DbSet<WelcomeConfiguration> WelcomeConfigurations => Set<WelcomeConfiguration>();
    public DbSet<ScheduledMessage> ScheduledMessages => Set<ScheduledMessage>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RatWatch> RatWatches => Set<RatWatch>();
    public DbSet<RatVote> RatVotes => Set<RatVote>();
    public DbSet<RatRecord> RatRecords => Set<RatRecord>();
    public DbSet<GuildRatWatchSettings> GuildRatWatchSettings => Set<GuildRatWatchSettings>();
    public DbSet<GuildMember> GuildMembers => Set<GuildMember>();
    public DbSet<FlaggedEvent> FlaggedEvents => Set<FlaggedEvent>();
    public DbSet<GuildModerationConfig> GuildModerationConfigs => Set<GuildModerationConfig>();
    public DbSet<ModerationCase> ModerationCases => Set<ModerationCase>();
    public DbSet<ModNote> ModNotes => Set<ModNote>();
    public DbSet<ModTag> ModTags => Set<ModTag>();
    public DbSet<UserModTag> UserModTags => Set<UserModTag>();
    public DbSet<Watchlist> Watchlists => Set<Watchlist>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<MemberActivitySnapshot> MemberActivitySnapshots => Set<MemberActivitySnapshot>();
    public DbSet<ChannelActivitySnapshot> ChannelActivitySnapshots => Set<ChannelActivitySnapshot>();
    public DbSet<GuildMetricsSnapshot> GuildMetricsSnapshots => Set<GuildMetricsSnapshot>();
    public DbSet<PerformanceAlertConfig> PerformanceAlertConfigs => Set<PerformanceAlertConfig>();
    public DbSet<PerformanceIncident> PerformanceIncidents => Set<PerformanceIncident>();
    public DbSet<MetricSnapshot> MetricSnapshots => Set<MetricSnapshot>();
    public DbSet<Sound> Sounds => Set<Sound>();
    public DbSet<SoundPlayLog> SoundPlayLogs => Set<SoundPlayLog>();
    public DbSet<GuildAudioSettings> GuildAudioSettings => Set<GuildAudioSettings>();
    public DbSet<CommandRoleRestriction> CommandRoleRestrictions => Set<CommandRoleRestriction>();
    public DbSet<UserDiscordGuild> UserDiscordGuilds => Set<UserDiscordGuild>();
    public DbSet<TtsMessage> TtsMessages => Set<TtsMessage>();
    public DbSet<GuildTtsSettings> GuildTtsSettings => Set<GuildTtsSettings>();
    public DbSet<CommandModuleConfiguration> CommandModuleConfigurations => Set<CommandModuleConfiguration>();
    public DbSet<UserActivityEvent> UserActivityEvents => Set<UserActivityEvent>();

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

        // Configure UserActivityLog entity
        modelBuilder.Entity<UserActivityLog>(entity =>
        {
            // Configure primary key
            entity.HasKey(e => e.Id);

            // Configure string properties with appropriate max lengths
            entity.Property(e => e.ActorUserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.TargetUserId).HasMaxLength(450);
            entity.Property(e => e.IpAddress).HasMaxLength(45);

            // Configure Action as int for storage
            entity.Property(e => e.Action)
                .HasConversion<int>()
                .IsRequired();

            // Configure default values
            entity.Property(e => e.Timestamp).HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Configure foreign key relationships
            entity.HasOne(e => e.Actor)
                .WithMany()
                .HasForeignKey(e => e.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Target)
                .WithMany()
                .HasForeignKey(e => e.TargetUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Indexes for efficient queries
            entity.HasIndex(e => e.ActorUserId);
            entity.HasIndex(e => e.TargetUserId);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.Action);
        });

        // Configure UserDiscordGuild entity
        modelBuilder.Entity<UserDiscordGuild>(entity =>
        {
            // Configure primary key
            entity.HasKey(e => e.Id);

            // Configure GuildId to handle SQLite compatibility (ulong -> long conversion)
            entity.Property(e => e.GuildId)
                .HasConversion(
                    v => (long)v,
                    v => (ulong)v);

            // Configure string properties with appropriate max lengths
            entity.Property(e => e.ApplicationUserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.GuildName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.GuildIconHash).HasMaxLength(100);

            // Configure default values
            entity.Property(e => e.CapturedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.LastUpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Configure foreign key relationship
            entity.HasOne(e => e.ApplicationUser)
                .WithMany()
                .HasForeignKey(e => e.ApplicationUserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Composite unique index for user-guild membership (one record per user per guild)
            entity.HasIndex(e => new { e.ApplicationUserId, e.GuildId }).IsUnique();

            // Index for efficient lookups by guild
            entity.HasIndex(e => e.GuildId);
        });
    }
}
