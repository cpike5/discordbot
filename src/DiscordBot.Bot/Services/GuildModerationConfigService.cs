using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using System.Text.Json;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service implementation for managing guild moderation configuration.
/// Handles configuration CRUD, preset application, and JSON serialization of config sections.
/// </summary>
public class GuildModerationConfigService : IGuildModerationConfigService
{
    private readonly IGuildModerationConfigRepository _configRepository;
    private readonly ILogger<GuildModerationConfigService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public GuildModerationConfigService(
        IGuildModerationConfigRepository configRepository,
        ILogger<GuildModerationConfigService> logger)
    {
        _configRepository = configRepository;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<GuildModerationConfigDto> GetConfigAsync(ulong guildId, CancellationToken ct = default)
    {
        _logger.LogDebug("Retrieving moderation configuration for guild {GuildId}", guildId);

        var config = await _configRepository.GetByGuildIdAsync(guildId, ct);

        if (config == null)
        {
            _logger.LogInformation("No moderation configuration found for guild {GuildId}, returning default", guildId);
            return GetDefaultConfig();
        }

        return MapToDto(config);
    }

    /// <inheritdoc/>
    public async Task<GuildModerationConfigDto> UpdateConfigAsync(ulong guildId, GuildModerationConfigDto configDto, CancellationToken ct = default)
    {
        _logger.LogInformation("Updating moderation configuration for guild {GuildId}", guildId);

        var config = await _configRepository.GetByGuildIdAsync(guildId, ct);

        if (config == null)
        {
            // Create new configuration
            config = new GuildModerationConfig
            {
                GuildId = guildId
            };
        }

        // Update fields
        config.Mode = configDto.Mode;
        config.SimplePreset = configDto.SimplePreset;
        config.SpamConfig = JsonSerializer.Serialize(configDto.SpamConfig, JsonOptions);
        config.ContentFilterConfig = JsonSerializer.Serialize(configDto.ContentFilterConfig, JsonOptions);
        config.RaidProtectionConfig = JsonSerializer.Serialize(configDto.RaidProtectionConfig, JsonOptions);
        config.UpdatedAt = DateTime.UtcNow;

        if (await _configRepository.GetByGuildIdAsync(guildId, ct) == null)
        {
            await _configRepository.AddAsync(config, ct);
        }
        else
        {
            await _configRepository.UpdateAsync(config, ct);
        }

        _logger.LogInformation("Moderation configuration updated successfully for guild {GuildId}", guildId);

        return MapToDto(config);
    }

    /// <inheritdoc/>
    public async Task<GuildModerationConfigDto> ApplyPresetAsync(ulong guildId, string presetName, CancellationToken ct = default)
    {
        _logger.LogInformation("Applying preset '{PresetName}' to guild {GuildId}", presetName, guildId);

        var configDto = presetName.ToLowerInvariant() switch
        {
            "relaxed" => GetRelaxedPreset(),
            "moderate" => GetModeratePreset(),
            "strict" => GetStrictPreset(),
            _ => throw new ArgumentException($"Unknown preset: {presetName}", nameof(presetName))
        };

        configDto.SimplePreset = presetName;
        configDto.Mode = ConfigMode.Simple;

        var result = await UpdateConfigAsync(guildId, configDto, ct);

        _logger.LogInformation("Preset '{PresetName}' applied successfully to guild {GuildId}", presetName, guildId);

        return result;
    }

    /// <inheritdoc/>
    public GuildModerationConfigDto GetDefaultConfig()
    {
        return new GuildModerationConfigDto
        {
            GuildId = 0,
            Mode = ConfigMode.Simple,
            SimplePreset = "Moderate",
            SpamConfig = new SpamDetectionConfigDto
            {
                Enabled = true,
                MaxMessagesPerWindow = 5,
                WindowSeconds = 5,
                MaxMentionsPerMessage = 5,
                DuplicateMessageThreshold = 0.8,
                AutoAction = AutoAction.Delete
            },
            ContentFilterConfig = new ContentFilterConfigDto
            {
                Enabled = true,
                ProhibitedWords = new List<string>(),
                AllowedLinkDomains = new List<string>(),
                BlockUnlistedLinks = false,
                BlockInviteLinks = false,
                AutoAction = AutoAction.Delete
            },
            RaidProtectionConfig = new RaidProtectionConfigDto
            {
                Enabled = true,
                MaxJoinsPerWindow = 10,
                WindowSeconds = 10,
                MinAccountAgeHours = 0,
                AutoAction = RaidAutoAction.AlertOnly
            },
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <inheritdoc/>
    public async Task<SpamDetectionConfigDto> GetSpamConfigAsync(ulong guildId, CancellationToken ct = default)
    {
        _logger.LogDebug("Retrieving spam detection configuration for guild {GuildId}", guildId);

        var config = await GetConfigAsync(guildId, ct);
        return config.SpamConfig;
    }

    /// <inheritdoc/>
    public async Task<ContentFilterConfigDto> GetContentFilterConfigAsync(ulong guildId, CancellationToken ct = default)
    {
        _logger.LogDebug("Retrieving content filter configuration for guild {GuildId}", guildId);

        var config = await GetConfigAsync(guildId, ct);
        return config.ContentFilterConfig;
    }

    /// <inheritdoc/>
    public async Task<RaidProtectionConfigDto> GetRaidProtectionConfigAsync(ulong guildId, CancellationToken ct = default)
    {
        _logger.LogDebug("Retrieving raid protection configuration for guild {GuildId}", guildId);

        var config = await GetConfigAsync(guildId, ct);
        return config.RaidProtectionConfig;
    }

    /// <summary>
    /// Maps a GuildModerationConfig entity to a DTO by deserializing JSON config sections.
    /// </summary>
    private GuildModerationConfigDto MapToDto(GuildModerationConfig config)
    {
        SpamDetectionConfigDto? spamConfig = null;
        ContentFilterConfigDto? contentFilterConfig = null;
        RaidProtectionConfigDto? raidProtectionConfig = null;

        try
        {
            spamConfig = string.IsNullOrEmpty(config.SpamConfig)
                ? new SpamDetectionConfigDto()
                : JsonSerializer.Deserialize<SpamDetectionConfigDto>(config.SpamConfig, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize spam config for guild {GuildId}, using default", config.GuildId);
            spamConfig = new SpamDetectionConfigDto();
        }

        try
        {
            contentFilterConfig = string.IsNullOrEmpty(config.ContentFilterConfig)
                ? new ContentFilterConfigDto()
                : JsonSerializer.Deserialize<ContentFilterConfigDto>(config.ContentFilterConfig, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize content filter config for guild {GuildId}, using default", config.GuildId);
            contentFilterConfig = new ContentFilterConfigDto();
        }

        try
        {
            raidProtectionConfig = string.IsNullOrEmpty(config.RaidProtectionConfig)
                ? new RaidProtectionConfigDto()
                : JsonSerializer.Deserialize<RaidProtectionConfigDto>(config.RaidProtectionConfig, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize raid protection config for guild {GuildId}, using default", config.GuildId);
            raidProtectionConfig = new RaidProtectionConfigDto();
        }

        return new GuildModerationConfigDto
        {
            GuildId = config.GuildId,
            Mode = config.Mode,
            SimplePreset = config.SimplePreset,
            SpamConfig = spamConfig ?? new SpamDetectionConfigDto(),
            ContentFilterConfig = contentFilterConfig ?? new ContentFilterConfigDto(),
            RaidProtectionConfig = raidProtectionConfig ?? new RaidProtectionConfigDto(),
            UpdatedAt = config.UpdatedAt
        };
    }

    /// <summary>
    /// Gets the "Relaxed" preset configuration with lenient settings.
    /// </summary>
    private GuildModerationConfigDto GetRelaxedPreset()
    {
        return new GuildModerationConfigDto
        {
            GuildId = 0,
            Mode = ConfigMode.Simple,
            SimplePreset = "Relaxed",
            SpamConfig = new SpamDetectionConfigDto
            {
                Enabled = true,
                MaxMessagesPerWindow = 10,
                WindowSeconds = 5,
                MaxMentionsPerMessage = 10,
                DuplicateMessageThreshold = 0.95,
                AutoAction = AutoAction.None
            },
            ContentFilterConfig = new ContentFilterConfigDto
            {
                Enabled = false,
                ProhibitedWords = new List<string>(),
                AllowedLinkDomains = new List<string>(),
                BlockUnlistedLinks = false,
                BlockInviteLinks = false,
                AutoAction = AutoAction.None
            },
            RaidProtectionConfig = new RaidProtectionConfigDto
            {
                Enabled = true,
                MaxJoinsPerWindow = 20,
                WindowSeconds = 10,
                MinAccountAgeHours = 0,
                AutoAction = RaidAutoAction.AlertOnly
            },
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Gets the "Moderate" preset configuration with balanced settings.
    /// </summary>
    private GuildModerationConfigDto GetModeratePreset()
    {
        return new GuildModerationConfigDto
        {
            GuildId = 0,
            Mode = ConfigMode.Simple,
            SimplePreset = "Moderate",
            SpamConfig = new SpamDetectionConfigDto
            {
                Enabled = true,
                MaxMessagesPerWindow = 5,
                WindowSeconds = 5,
                MaxMentionsPerMessage = 5,
                DuplicateMessageThreshold = 0.8,
                AutoAction = AutoAction.Delete
            },
            ContentFilterConfig = new ContentFilterConfigDto
            {
                Enabled = true,
                ProhibitedWords = new List<string>(),
                AllowedLinkDomains = new List<string>(),
                BlockUnlistedLinks = false,
                BlockInviteLinks = false,
                AutoAction = AutoAction.Delete
            },
            RaidProtectionConfig = new RaidProtectionConfigDto
            {
                Enabled = true,
                MaxJoinsPerWindow = 10,
                WindowSeconds = 10,
                MinAccountAgeHours = 0,
                AutoAction = RaidAutoAction.AlertOnly
            },
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Gets the "Strict" preset configuration with aggressive settings.
    /// </summary>
    private GuildModerationConfigDto GetStrictPreset()
    {
        return new GuildModerationConfigDto
        {
            GuildId = 0,
            Mode = ConfigMode.Simple,
            SimplePreset = "Strict",
            SpamConfig = new SpamDetectionConfigDto
            {
                Enabled = true,
                MaxMessagesPerWindow = 3,
                WindowSeconds = 5,
                MaxMentionsPerMessage = 3,
                DuplicateMessageThreshold = 0.7,
                AutoAction = AutoAction.Mute
            },
            ContentFilterConfig = new ContentFilterConfigDto
            {
                Enabled = true,
                ProhibitedWords = new List<string>(),
                AllowedLinkDomains = new List<string>(),
                BlockUnlistedLinks = true,
                BlockInviteLinks = true,
                AutoAction = AutoAction.Delete
            },
            RaidProtectionConfig = new RaidProtectionConfigDto
            {
                Enabled = true,
                MaxJoinsPerWindow = 5,
                WindowSeconds = 10,
                MinAccountAgeHours = 24,
                AutoAction = RaidAutoAction.LockServer
            },
            UpdatedAt = DateTime.UtcNow
        };
    }
}
