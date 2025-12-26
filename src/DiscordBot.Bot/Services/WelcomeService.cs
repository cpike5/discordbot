using Discord;
using Discord.WebSocket;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for welcome configuration management and message sending.
/// </summary>
public class WelcomeService : IWelcomeService
{
    private readonly IWelcomeConfigurationRepository _repository;
    private readonly DiscordSocketClient _client;
    private readonly ILogger<WelcomeService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WelcomeService"/> class.
    /// </summary>
    /// <param name="repository">The welcome configuration repository.</param>
    /// <param name="client">The Discord socket client.</param>
    /// <param name="logger">The logger.</param>
    public WelcomeService(
        IWelcomeConfigurationRepository repository,
        DiscordSocketClient client,
        ILogger<WelcomeService> logger)
    {
        _repository = repository;
        _client = client;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<WelcomeConfigurationDto?> GetConfigurationAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving welcome configuration for guild {GuildId}", guildId);

        var config = await _repository.GetByGuildIdAsync(guildId, cancellationToken);
        if (config == null)
        {
            _logger.LogDebug("Welcome configuration not found for guild {GuildId}", guildId);
            return null;
        }

        _logger.LogInformation("Retrieved welcome configuration for guild {GuildId}: IsEnabled={IsEnabled}, ChannelId={ChannelId}, Message={Message}",
            guildId, config.IsEnabled, config.WelcomeChannelId, config.WelcomeMessage?.Substring(0, Math.Min(50, config.WelcomeMessage?.Length ?? 0)));

        return MapToDto(config);
    }

    /// <inheritdoc/>
    public async Task<WelcomeConfigurationDto?> UpdateConfigurationAsync(
        ulong guildId,
        WelcomeConfigurationUpdateDto updateDto,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating welcome configuration for guild {GuildId}", guildId);

        var config = await _repository.GetByGuildIdAsync(guildId, cancellationToken);

        // If configuration doesn't exist, create a new one
        if (config == null)
        {
            _logger.LogInformation("Creating new welcome configuration for guild {GuildId}. IsEnabled={IsEnabled}, ChannelId={ChannelId}",
                guildId, updateDto.IsEnabled, updateDto.WelcomeChannelId);

            config = new WelcomeConfiguration
            {
                GuildId = guildId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Apply all non-null fields from the update DTO
            ApplyUpdate(config, updateDto);

            _logger.LogDebug("After ApplyUpdate: IsEnabled={IsEnabled}, ChannelId={ChannelId}",
                config.IsEnabled, config.WelcomeChannelId);

            await _repository.AddAsync(config, cancellationToken);

            _logger.LogInformation("Welcome configuration created for guild {GuildId}", guildId);
        }
        else
        {
            _logger.LogInformation("Updating existing welcome configuration for guild {GuildId}. IsEnabled={IsEnabled}, ChannelId={ChannelId}",
                guildId, updateDto.IsEnabled, updateDto.WelcomeChannelId);

            // Update existing configuration
            ApplyUpdate(config, updateDto);
            config.UpdatedAt = DateTime.UtcNow;

            await _repository.UpdateAsync(config, cancellationToken);

            _logger.LogInformation("Welcome configuration updated for guild {GuildId}", guildId);
        }

        return MapToDto(config);
    }

    /// <inheritdoc/>
    public async Task<bool> SendWelcomeMessageAsync(ulong guildId, ulong userId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to send welcome message for user {UserId} in guild {GuildId}", userId, guildId);

        var config = await _repository.GetByGuildIdAsync(guildId, cancellationToken);
        if (config == null)
        {
            _logger.LogDebug("No welcome configuration found for guild {GuildId}, skipping welcome message", guildId);
            return false;
        }

        if (!config.IsEnabled)
        {
            _logger.LogDebug("Welcome messages are disabled for guild {GuildId}, skipping", guildId);
            return false;
        }

        if (!config.WelcomeChannelId.HasValue)
        {
            _logger.LogWarning("Welcome messages are enabled for guild {GuildId}, but no channel is configured", guildId);
            return false;
        }

        var guild = _client.GetGuild(guildId);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found in Discord client, cannot send welcome message", guildId);
            return false;
        }

        var channel = guild.GetTextChannel(config.WelcomeChannelId.Value);
        if (channel == null)
        {
            _logger.LogWarning("Welcome channel {ChannelId} not found in guild {GuildId}, cannot send welcome message",
                config.WelcomeChannelId.Value, guildId);
            return false;
        }

        var user = guild.GetUser(userId);
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found in guild {GuildId}, cannot send welcome message", userId, guildId);
            return false;
        }

        try
        {
            // Replace template variables in the message
            var message = ReplaceTemplateVariables(config.WelcomeMessage, guild, user);

            if (config.UseEmbed)
            {
                var embedBuilder = new EmbedBuilder()
                    .WithDescription(message)
                    .WithCurrentTimestamp();

                // Add color if specified
                if (!string.IsNullOrWhiteSpace(config.EmbedColor))
                {
                    if (TryParseHexColor(config.EmbedColor, out var color))
                    {
                        embedBuilder.WithColor(color);
                    }
                    else
                    {
                        _logger.LogWarning("Invalid embed color '{Color}' for guild {GuildId}, using default",
                            config.EmbedColor, guildId);
                    }
                }

                // Add user avatar if configured
                if (config.IncludeAvatar)
                {
                    embedBuilder.WithThumbnailUrl(user.GetDisplayAvatarUrl(size: 256));
                }

                await channel.SendMessageAsync(embed: embedBuilder.Build());
            }
            else
            {
                await channel.SendMessageAsync(message);
            }

            _logger.LogInformation("Welcome message sent for user {UserId} ({Username}) in guild {GuildId}",
                userId, user.Username, guildId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send welcome message for user {UserId} in guild {GuildId}", userId, guildId);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<string?> PreviewWelcomeMessageAsync(ulong guildId, ulong previewUserId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Generating welcome message preview for guild {GuildId} with user {UserId}", guildId, previewUserId);

        var config = await _repository.GetByGuildIdAsync(guildId, cancellationToken);
        if (config == null)
        {
            _logger.LogDebug("No welcome configuration found for guild {GuildId}, cannot generate preview", guildId);
            return null;
        }

        var guild = _client.GetGuild(guildId);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found in Discord client, cannot generate preview", guildId);
            return null;
        }

        var user = guild.GetUser(previewUserId);
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found in guild {GuildId}, cannot generate preview", previewUserId, guildId);
            return null;
        }

        var preview = ReplaceTemplateVariables(config.WelcomeMessage, guild, user);

        _logger.LogDebug("Generated welcome message preview for guild {GuildId}", guildId);

        return preview;
    }

    /// <summary>
    /// Applies updates from the update DTO to the configuration entity.
    /// </summary>
    /// <param name="config">The configuration entity to update.</param>
    /// <param name="updateDto">The update DTO containing the fields to update.</param>
    private static void ApplyUpdate(WelcomeConfiguration config, WelcomeConfigurationUpdateDto updateDto)
    {
        if (updateDto.IsEnabled.HasValue)
        {
            config.IsEnabled = updateDto.IsEnabled.Value;
        }

        if (updateDto.WelcomeChannelId.HasValue)
        {
            config.WelcomeChannelId = updateDto.WelcomeChannelId.Value;
        }

        if (updateDto.WelcomeMessage != null)
        {
            config.WelcomeMessage = updateDto.WelcomeMessage;
        }

        if (updateDto.IncludeAvatar.HasValue)
        {
            config.IncludeAvatar = updateDto.IncludeAvatar.Value;
        }

        if (updateDto.UseEmbed.HasValue)
        {
            config.UseEmbed = updateDto.UseEmbed.Value;
        }

        if (updateDto.EmbedColor != null)
        {
            config.EmbedColor = updateDto.EmbedColor;
        }
    }

    /// <summary>
    /// Maps a WelcomeConfiguration entity to a WelcomeConfigurationDto.
    /// </summary>
    /// <param name="config">The configuration entity.</param>
    /// <returns>The mapped DTO.</returns>
    private static WelcomeConfigurationDto MapToDto(WelcomeConfiguration config)
    {
        return new WelcomeConfigurationDto
        {
            GuildId = config.GuildId,
            IsEnabled = config.IsEnabled,
            WelcomeChannelId = config.WelcomeChannelId,
            WelcomeMessage = config.WelcomeMessage,
            IncludeAvatar = config.IncludeAvatar,
            UseEmbed = config.UseEmbed,
            EmbedColor = config.EmbedColor,
            CreatedAt = config.CreatedAt,
            UpdatedAt = config.UpdatedAt
        };
    }

    /// <summary>
    /// Replaces template variables in the message with actual values.
    /// Supported variables: {user}, {username}, {server}, {membercount}
    /// </summary>
    /// <param name="template">The message template with variables.</param>
    /// <param name="guild">The Discord guild.</param>
    /// <param name="user">The Discord user.</param>
    /// <returns>The message with template variables replaced.</returns>
    private static string ReplaceTemplateVariables(string template, SocketGuild guild, SocketGuildUser user)
    {
        return template
            .Replace("{user}", user.Mention, StringComparison.OrdinalIgnoreCase)
            .Replace("{username}", user.DisplayName, StringComparison.OrdinalIgnoreCase)
            .Replace("{server}", guild.Name, StringComparison.OrdinalIgnoreCase)
            .Replace("{membercount}", guild.MemberCount.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Attempts to parse a hex color string to a Discord Color.
    /// </summary>
    /// <param name="hexColor">The hex color string (e.g., "#5865F2" or "5865F2").</param>
    /// <param name="color">The parsed Discord color.</param>
    /// <returns>True if parsing was successful, false otherwise.</returns>
    private static bool TryParseHexColor(string hexColor, out Color color)
    {
        color = default;

        if (string.IsNullOrWhiteSpace(hexColor))
        {
            return false;
        }

        // Remove # prefix if present
        var hex = hexColor.TrimStart('#');

        // Validate hex format (6 characters, only 0-9 A-F)
        if (hex.Length != 6 || !hex.All(c => char.IsDigit(c) || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f')))
        {
            return false;
        }

        try
        {
            var r = Convert.ToByte(hex.Substring(0, 2), 16);
            var g = Convert.ToByte(hex.Substring(2, 2), 16);
            var b = Convert.ToByte(hex.Substring(4, 2), 16);

            color = new Color(r, g, b);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
