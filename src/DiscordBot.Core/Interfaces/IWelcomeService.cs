using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for welcome configuration management and message sending.
/// </summary>
public interface IWelcomeService
{
    /// <summary>
    /// Gets the welcome configuration for a specific guild.
    /// </summary>
    /// <param name="guildId">The Discord guild snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The welcome configuration DTO, or null if not found.</returns>
    Task<WelcomeConfigurationDto?> GetConfigurationAsync(ulong guildId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the welcome configuration for a specific guild.
    /// Creates a new configuration if one doesn't exist.
    /// </summary>
    /// <param name="guildId">The Discord guild snowflake ID.</param>
    /// <param name="updateDto">The update DTO containing the fields to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated welcome configuration DTO, or null if the guild was not found.</returns>
    Task<WelcomeConfigurationDto?> UpdateConfigurationAsync(
        ulong guildId,
        WelcomeConfigurationUpdateDto updateDto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a welcome message to a user who joined a guild.
    /// Uses the guild's welcome configuration to determine message content and format.
    /// </summary>
    /// <param name="guildId">The Discord guild snowflake ID.</param>
    /// <param name="userId">The Discord user snowflake ID of the new member.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the message was sent successfully, false otherwise.</returns>
    Task<bool> SendWelcomeMessageAsync(ulong guildId, ulong userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a preview of the welcome message for a specific guild.
    /// Useful for testing message templates before enabling welcome messages.
    /// </summary>
    /// <param name="guildId">The Discord guild snowflake ID.</param>
    /// <param name="previewUserId">The Discord user snowflake ID to use for template preview.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The preview message string with template variables replaced, or null if configuration not found.</returns>
    Task<string?> PreviewWelcomeMessageAsync(ulong guildId, ulong previewUserId, CancellationToken cancellationToken = default);
}
