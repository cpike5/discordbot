using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for AssistantGuildSettings entities.
/// Provides data access operations for assistant configuration per guild.
/// </summary>
public class AssistantGuildSettingsRepository : Repository<AssistantGuildSettings>, IAssistantGuildSettingsRepository
{
    private readonly ILogger<AssistantGuildSettingsRepository> _logger;

    public AssistantGuildSettingsRepository(
        BotDbContext context,
        ILogger<AssistantGuildSettingsRepository> logger,
        ILogger<Repository<AssistantGuildSettings>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AssistantGuildSettings?> GetByGuildIdAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving assistant settings for guild {GuildId}", guildId);

        var settings = await DbSet
            .AsNoTracking()
            .Include(s => s.Guild)
            .FirstOrDefaultAsync(s => s.GuildId == guildId, cancellationToken);

        _logger.LogDebug("Settings found for guild {GuildId}: {Found}", guildId, settings != null);
        return settings;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<AssistantGuildSettings>> GetEnabledGuildsAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving all guilds with assistant enabled");

        var settings = await DbSet
            .AsNoTracking()
            .Include(s => s.Guild)
            .Where(s => s.IsEnabled)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Found {Count} guilds with assistant enabled", settings.Count);
        return settings;
    }

    /// <summary>
    /// Gets or creates settings for a guild.
    /// If settings don't exist, creates default settings with the assistant disabled.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The existing or newly created settings.</returns>
    public async Task<AssistantGuildSettings> GetOrCreateAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting or creating assistant settings for guild {GuildId}", guildId);

        var settings = await DbSet
            .Include(s => s.Guild)
            .FirstOrDefaultAsync(s => s.GuildId == guildId, cancellationToken);

        if (settings != null)
        {
            _logger.LogDebug("Existing settings found for guild {GuildId}", guildId);
            return settings;
        }

        _logger.LogInformation("Creating default assistant settings for guild {GuildId}", guildId);

        var now = DateTime.UtcNow;
        settings = new AssistantGuildSettings
        {
            GuildId = guildId,
            IsEnabled = false, // Disabled by default
            AllowedChannelIds = "[]",
            RateLimitOverride = null,
            CreatedAt = now,
            UpdatedAt = now
        };

        await DbSet.AddAsync(settings, cancellationToken);
        await Context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created default assistant settings for guild {GuildId}",
            guildId);

        return settings;
    }

    /// <summary>
    /// Updates settings for a guild.
    /// </summary>
    /// <param name="settings">The settings to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task UpdateSettingsAsync(
        AssistantGuildSettings settings,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Updating assistant settings for guild {GuildId}", settings.GuildId);

        settings.UpdatedAt = DateTime.UtcNow;
        DbSet.Update(settings);
        await Context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Updated assistant settings for guild {GuildId}. Enabled: {IsEnabled}",
            settings.GuildId, settings.IsEnabled);
    }
}
