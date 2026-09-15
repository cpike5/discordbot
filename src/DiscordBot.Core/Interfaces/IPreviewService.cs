using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Looks up lightweight preview data for the user/guild hover-popup used across the web UI
/// (<c>PreviewController</c> for the still-legacy Razor Pages surface via
/// <c>wwwroot/js/preview-popup.js</c>, and <c>Blazor/Shared/Overlays/UserPreview.razor</c>/
/// <c>GuildPreview.razor</c> for the ported Blazor surface). Reads only from the Discord gateway
/// cache (<c>DiscordSocketClient</c>) and the database - never a live Discord API call - so a
/// miss (user/guild not in cache, e.g. web-only mode with no gateway connection) returns
/// <see langword="null"/> rather than throwing.
/// </summary>
public interface IPreviewService
{
    /// <summary>
    /// Gets preview data for a user, optionally scoped to a guild (roles, join date, guild
    /// avatar). When <paramref name="guildId"/> is given but the guild isn't in the Discord cache,
    /// or the user isn't a member of it, falls back to a guild-less user preview instead of
    /// failing outright - the caller only cares whether the user exists at all.
    /// </summary>
    /// <param name="userId">The Discord user snowflake ID.</param>
    /// <param name="guildId">Optional guild context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user preview DTO, or <see langword="null"/> if the user isn't in the Discord cache.</returns>
    Task<UserPreviewDto?> GetUserPreviewAsync(ulong userId, ulong? guildId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets preview data for a guild.
    /// </summary>
    /// <param name="guildId">The Discord guild snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The guild preview DTO, or <see langword="null"/> if the guild isn't in the Discord cache.</returns>
    Task<GuildPreviewDto?> GetGuildPreviewAsync(ulong guildId, CancellationToken cancellationToken = default);
}
