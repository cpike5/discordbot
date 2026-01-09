using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service implementation for managing guild audio settings.
/// Handles volume levels, storage limits, and command role restrictions.
/// </summary>
public class GuildAudioSettingsService : IGuildAudioSettingsService
{
    private readonly IGuildAudioSettingsRepository _settingsRepository;
    private readonly ILogger<GuildAudioSettingsService> _logger;

    public GuildAudioSettingsService(
        IGuildAudioSettingsRepository settingsRepository,
        ILogger<GuildAudioSettingsService> logger)
    {
        _settingsRepository = settingsRepository;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<GuildAudioSettings> GetSettingsAsync(ulong guildId, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "guild_audio_settings",
            "get_settings",
            guildId: guildId);

        try
        {
            _logger.LogDebug("Getting audio settings for guild {GuildId}", guildId);

            var settings = await _settingsRepository.GetOrCreateAsync(guildId, ct);

            _logger.LogDebug("Retrieved audio settings for guild {GuildId}: AudioEnabled={AudioEnabled}, MaxSounds={MaxSounds}",
                guildId, settings.AudioEnabled, settings.MaxSoundsPerGuild);

            BotActivitySource.SetSuccess(activity);
            return settings;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<GuildAudioSettings> UpdateSettingsAsync(
        ulong guildId,
        Action<GuildAudioSettings> updateAction,
        CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "guild_audio_settings",
            "update_settings",
            guildId: guildId);

        try
        {
            _logger.LogInformation("Updating audio settings for guild {GuildId}", guildId);

            var settings = await _settingsRepository.GetOrCreateAsync(guildId, ct);

            // Apply the update action
            updateAction(settings);

            // Update timestamp
            settings.UpdatedAt = DateTime.UtcNow;

            await _settingsRepository.UpdateAsync(settings, ct);

            _logger.LogInformation("Audio settings updated for guild {GuildId}", guildId);

            BotActivitySource.SetSuccess(activity);
            return settings;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task AddCommandRestrictionAsync(
        ulong guildId,
        string commandName,
        ulong roleId,
        CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "guild_audio_settings",
            "add_command_restriction",
            guildId: guildId);

        try
        {
            _logger.LogInformation("Adding role restriction for command '{CommandName}' in guild {GuildId}: roleId={RoleId}",
                commandName, guildId, roleId);

            var settings = await _settingsRepository.GetOrCreateAsync(guildId, ct);

            // Find or create the command restriction
            var restriction = settings.CommandRoleRestrictions
                .FirstOrDefault(r => r.CommandName.Equals(commandName, StringComparison.OrdinalIgnoreCase));

            if (restriction == null)
            {
                // Create new restriction
                restriction = new CommandRoleRestriction
                {
                    GuildId = guildId,
                    CommandName = commandName,
                    AllowedRoleIds = new List<ulong> { roleId }
                };
                settings.CommandRoleRestrictions.Add(restriction);

                _logger.LogInformation("Created new command restriction for '{CommandName}' in guild {GuildId}",
                    commandName, guildId);
            }
            else
            {
                // Add role to existing restriction if not already present
                if (!restriction.AllowedRoleIds.Contains(roleId))
                {
                    restriction.AllowedRoleIds.Add(roleId);

                    _logger.LogInformation("Added role {RoleId} to existing restriction for '{CommandName}' in guild {GuildId}",
                        roleId, commandName, guildId);
                }
                else
                {
                    _logger.LogDebug("Role {RoleId} already in restriction for '{CommandName}' in guild {GuildId}",
                        roleId, commandName, guildId);
                }
            }

            settings.UpdatedAt = DateTime.UtcNow;
            await _settingsRepository.UpdateAsync(settings, ct);

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task RemoveCommandRestrictionAsync(
        ulong guildId,
        string commandName,
        ulong roleId,
        CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "guild_audio_settings",
            "remove_command_restriction",
            guildId: guildId);

        try
        {
            _logger.LogInformation("Removing role restriction for command '{CommandName}' in guild {GuildId}: roleId={RoleId}",
                commandName, guildId, roleId);

            var settings = await _settingsRepository.GetOrCreateAsync(guildId, ct);

            // Find the command restriction
            var restriction = settings.CommandRoleRestrictions
                .FirstOrDefault(r => r.CommandName.Equals(commandName, StringComparison.OrdinalIgnoreCase));

            if (restriction != null)
            {
                // Remove the role ID
                var removed = restriction.AllowedRoleIds.Remove(roleId);

                if (removed)
                {
                    _logger.LogInformation("Removed role {RoleId} from restriction for '{CommandName}' in guild {GuildId}",
                        roleId, commandName, guildId);

                    settings.UpdatedAt = DateTime.UtcNow;
                    await _settingsRepository.UpdateAsync(settings, ct);
                }
                else
                {
                    _logger.LogDebug("Role {RoleId} was not in restriction for '{CommandName}' in guild {GuildId}",
                        roleId, commandName, guildId);
                }
            }
            else
            {
                _logger.LogDebug("No restriction found for command '{CommandName}' in guild {GuildId}",
                    commandName, guildId);
            }

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ulong>> GetAllowedRolesForCommandAsync(
        ulong guildId,
        string commandName,
        CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "guild_audio_settings",
            "get_allowed_roles",
            guildId: guildId);

        try
        {
            _logger.LogDebug("Getting allowed roles for command '{CommandName}' in guild {GuildId}",
                commandName, guildId);

            var settings = await _settingsRepository.GetOrCreateAsync(guildId, ct);

            var restriction = settings.CommandRoleRestrictions
                .FirstOrDefault(r => r.CommandName.Equals(commandName, StringComparison.OrdinalIgnoreCase));

            var allowedRoles = restriction?.AllowedRoleIds ?? new List<ulong>();

            _logger.LogDebug("Command '{CommandName}' in guild {GuildId} has {Count} allowed roles",
                commandName, guildId, allowedRoles.Count);

            BotActivitySource.SetRecordsReturned(activity, allowedRoles.Count);
            BotActivitySource.SetSuccess(activity);
            return allowedRoles;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsCommandAllowedForRoleAsync(
        ulong guildId,
        string commandName,
        ulong roleId,
        CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "guild_audio_settings",
            "is_command_allowed",
            guildId: guildId);

        try
        {
            _logger.LogDebug("Checking if role {RoleId} is allowed to use command '{CommandName}' in guild {GuildId}",
                roleId, commandName, guildId);

            var settings = await _settingsRepository.GetOrCreateAsync(guildId, ct);

            var restriction = settings.CommandRoleRestrictions
                .FirstOrDefault(r => r.CommandName.Equals(commandName, StringComparison.OrdinalIgnoreCase));

            // If no restriction exists or AllowedRoleIds is empty, everyone is allowed
            if (restriction == null || restriction.AllowedRoleIds.Count == 0)
            {
                _logger.LogDebug("No restrictions for command '{CommandName}' in guild {GuildId}, role {RoleId} is allowed",
                    commandName, guildId, roleId);
                BotActivitySource.SetSuccess(activity);
                return true;
            }

            // Check if the role is in the allowed list
            var isAllowed = restriction.AllowedRoleIds.Contains(roleId);

            _logger.LogDebug("Role {RoleId} is {Allowed} to use command '{CommandName}' in guild {GuildId}",
                roleId, isAllowed ? "allowed" : "not allowed", commandName, guildId);

            BotActivitySource.SetSuccess(activity);
            return isAllowed;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }
}
