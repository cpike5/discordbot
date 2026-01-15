using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordBot.Infrastructure.Services;

/// <summary>
/// Service implementation for managing assistant guild settings.
/// Handles configuration, enable/disable operations, and default settings creation.
/// </summary>
public class AssistantGuildSettingsService : IAssistantGuildSettingsService
{
    private readonly ILogger<AssistantGuildSettingsService> _logger;
    private readonly AssistantGuildSettingsRepository _repository;
    private readonly IOptions<AssistantOptions> _assistantOptions;

    /// <summary>
    /// Initializes a new instance of the AssistantGuildSettingsService.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="repository">Repository for guild settings data access.</param>
    /// <param name="assistantOptions">Assistant configuration options.</param>
    public AssistantGuildSettingsService(
        ILogger<AssistantGuildSettingsService> logger,
        AssistantGuildSettingsRepository repository,
        IOptions<AssistantOptions> assistantOptions)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _assistantOptions = assistantOptions ?? throw new ArgumentNullException(nameof(assistantOptions));
    }

    /// <inheritdoc />
    public async Task<AssistantGuildSettings> GetOrCreateSettingsAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting or creating assistant settings for guild {GuildId}", guildId);

        var settings = await _repository.GetByGuildIdAsync(guildId, cancellationToken);

        if (settings != null)
        {
            return settings;
        }

        // Create new settings with defaults from options
        _logger.LogInformation("Creating default assistant settings for guild {GuildId}", guildId);

        var now = DateTime.UtcNow;
        var newSettings = new AssistantGuildSettings
        {
            GuildId = guildId,
            IsEnabled = _assistantOptions.Value.EnabledByDefaultForNewGuilds,
            AllowedChannelIds = "[]",
            RateLimitOverride = null,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _repository.AddAsync(newSettings, cancellationToken);

        _logger.LogInformation(
            "Created default assistant settings for guild {GuildId}. Enabled: {IsEnabled}",
            guildId, newSettings.IsEnabled);

        return newSettings;
    }

    /// <inheritdoc />
    public async Task UpdateSettingsAsync(
        AssistantGuildSettings settings,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Updating assistant settings for guild {GuildId}", settings.GuildId);

        settings.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(settings, cancellationToken);

        _logger.LogInformation(
            "Updated assistant settings for guild {GuildId}. Enabled: {IsEnabled}, RateLimit: {RateLimit}",
            settings.GuildId, settings.IsEnabled, settings.RateLimitOverride);
    }

    /// <inheritdoc />
    public async Task EnableAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Enabling assistant for guild {GuildId}", guildId);

        var settings = await GetOrCreateSettingsAsync(guildId, cancellationToken);

        if (settings.IsEnabled)
        {
            _logger.LogDebug("Assistant already enabled for guild {GuildId}", guildId);
            return;
        }

        settings.IsEnabled = true;
        settings.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(settings, cancellationToken);

        _logger.LogInformation("Enabled assistant for guild {GuildId}", guildId);
    }

    /// <inheritdoc />
    public async Task DisableAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Disabling assistant for guild {GuildId}", guildId);

        var settings = await _repository.GetByGuildIdAsync(guildId, cancellationToken);

        if (settings == null)
        {
            _logger.LogDebug("No settings found for guild {GuildId}, nothing to disable", guildId);
            return;
        }

        if (!settings.IsEnabled)
        {
            _logger.LogDebug("Assistant already disabled for guild {GuildId}", guildId);
            return;
        }

        settings.IsEnabled = false;
        settings.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(settings, cancellationToken);

        _logger.LogInformation("Disabled assistant for guild {GuildId}", guildId);
    }

    /// <summary>
    /// Checks if the assistant is enabled for a guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if enabled globally and for the guild.</returns>
    public async Task<bool> IsEnabledAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        // Check global setting first
        if (!_assistantOptions.Value.GloballyEnabled)
        {
            return false;
        }

        var settings = await _repository.GetByGuildIdAsync(guildId, cancellationToken);
        return settings?.IsEnabled ?? false;
    }

    /// <summary>
    /// Checks if a channel is allowed for the assistant in a guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="channelId">Discord channel ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the channel is allowed (or no restrictions are set).</returns>
    public async Task<bool> IsChannelAllowedAsync(
        ulong guildId,
        ulong channelId,
        CancellationToken cancellationToken = default)
    {
        var settings = await _repository.GetByGuildIdAsync(guildId, cancellationToken);

        if (settings == null)
        {
            return false; // No settings means not enabled
        }

        var allowedChannels = settings.GetAllowedChannelIdsList();

        // Empty list means all channels are allowed
        if (allowedChannels.Count == 0)
        {
            return true;
        }

        return allowedChannels.Contains(channelId);
    }

    /// <summary>
    /// Gets the rate limit for a guild (guild override or global default).
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rate limit value.</returns>
    public async Task<int> GetRateLimitAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        var settings = await _repository.GetByGuildIdAsync(guildId, cancellationToken);

        return settings?.RateLimitOverride ?? _assistantOptions.Value.DefaultRateLimit;
    }
}
