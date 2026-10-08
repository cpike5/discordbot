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
    /// Marks TTS as playing in the guild with <paramref name="displayMessage"/> (truncated to
    /// <see cref="MaxDisplayMessageLength"/>) and registers a playback token, linked to
    /// <paramref name="requestToken"/>, that the stop endpoint can cancel. A playback already
    /// registered for the guild is cancelled. Pass the returned source to
    /// <see cref="EndPlayback"/> when playback ends, whichever way it ends.
    /// </summary>
    Task<CancellationTokenSource> BeginPlaybackAsync(ulong guildId, string displayMessage, CancellationToken requestToken);

    /// <summary>
    /// Ends a playback started by <see cref="BeginPlaybackAsync"/>: removes and disposes
    /// <paramref name="playbackCts"/> and clears the guild's playing state, but only while that
    /// token is still the guild's registered one. A newer request or the stop endpoint that has
    /// taken over keeps its state.
    /// </summary>
    void EndPlayback(ulong guildId, CancellationTokenSource playbackCts);

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
