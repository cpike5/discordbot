using System.Collections.Concurrent;
using DiscordBot.Core.DTOs.Portal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Interfaces;

/// <summary>
/// Owns the member-portal TTS "send" pipeline: validating guild/rate-limit state,
/// synthesizing audio for a <see cref="SendTtsRequest"/>, and playing it in the guild's
/// connected voice channel. Also owns the per-guild playback-tracking state (current
/// message, playing flag, active playback cancellation) shared across the split
/// PortalTts* controllers, so it must be registered as a singleton.
/// Extracted from <c>PortalTtsControllerBase</c> so each PortalTts* controller only
/// depends on what it actually uses.
/// </summary>
public interface ITtsSendPipeline
{
    /// <summary>Current TTS message being played, keyed by guild ID.</summary>
    ConcurrentDictionary<ulong, string> CurrentMessages { get; }

    /// <summary>Whether TTS is currently playing, keyed by guild ID.</summary>
    ConcurrentDictionary<ulong, bool> PlaybackState { get; }

    /// <summary>Active playback cancellation token sources, keyed by guild ID.</summary>
    ConcurrentDictionary<ulong, CancellationTokenSource> PlaybackCancellationTokens { get; }

    /// <summary>Maximum length of a tracked "current message" display string.</summary>
    int MaxDisplayMessageLength { get; }

    /// <summary>
    /// Checks if audio features are globally enabled at the bot level.
    /// </summary>
    Task<bool> IsAudioGloballyEnabledAsync();

    /// <summary>
    /// Returns a Bad Request result if TTS is not enabled for the guild, or null if it is.
    /// </summary>
    Task<IActionResult?> CheckTtsEnabledAsync(HttpContext httpContext, ulong guildId, CancellationToken cancellationToken);

    /// <summary>
    /// Synthesizes audio for a send/preview request based on SSML, style, or plain message content.
    /// </summary>
    Task<Stream> SynthesizeFromRequestAsync(SendTtsRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Core send pipeline shared by the "send" endpoint and history "replay": validates the
    /// guild/rate-limit state, synthesizes audio for <paramref name="request"/>, and plays it
    /// in the guild's connected voice channel.
    /// </summary>
    Task<IActionResult> SendTtsCoreAsync(HttpContext httpContext, ulong guildId, SendTtsRequest request, CancellationToken cancellationToken);
}
