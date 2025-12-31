using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service for filtering message content against blocklists and patterns.
/// </summary>
public interface IContentFilterService
{
    /// <summary>
    /// Analyzes message content for prohibited words or patterns.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="content">The message content to analyze.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="messageId">The message ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A detection result DTO if prohibited content is found, null otherwise.</returns>
    Task<DetectionResultDto?> AnalyzeMessageAsync(ulong guildId, string content, ulong userId, ulong channelId, ulong messageId, CancellationToken ct = default);

    /// <summary>
    /// Loads and caches the content filters for a guild.
    /// This should be called when configuration changes to refresh the cache.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="ct">Cancellation token.</param>
    Task LoadGuildFiltersAsync(ulong guildId, CancellationToken ct = default);

    /// <summary>
    /// Invalidates the cached filters for a guild.
    /// This forces the next analysis to reload the configuration.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    void InvalidateCache(ulong guildId);
}
