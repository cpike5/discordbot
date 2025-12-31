using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service for compiling comprehensive user moderation profiles and investigation reports.
/// </summary>
public interface IInvestigationService
{
    /// <summary>
    /// Investigates a user and compiles a comprehensive moderation profile.
    /// This includes all moderation cases, notes, tags, flagged events, and watchlist status.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID to investigate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A comprehensive user moderation profile DTO.</returns>
    Task<UserModerationProfileDto> InvestigateUserAsync(ulong guildId, ulong userId, CancellationToken ct = default);
}
