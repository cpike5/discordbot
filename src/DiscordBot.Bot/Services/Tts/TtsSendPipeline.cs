using System.Collections.Concurrent;
using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.DTOs.Portal;
using DiscordBot.Core.DTOs.Tts;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Exceptions;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Services.Tts;

/// <summary>
/// Singleton implementation of <see cref="ITtsSendPipeline"/>. Extracted from the former
/// <c>PortalTtsControllerBase</c> so the send pipeline and its playback-tracking state are a
/// standalone, independently testable concern, and each PortalTts* controller depends only
/// on what it uses. Must remain a singleton — the playback-tracking dictionaries are shared,
/// per-process state across all guilds and all controllers.
///
/// <see cref="ITtsSettingsService"/>, <see cref="ISsmlBuilder"/>, <see cref="IAudioModerationLogService"/>
/// and <see cref="ITtsPlaybackService"/> are registered scoped/transient (per request), so — being
/// a singleton — this class resolves them from a fresh <see cref="IServiceScope"/> per call rather
/// than capturing them in the constructor, avoiding captive-dependency bugs (in particular
/// <see cref="ISsmlBuilder"/>'s mutable fluent-builder state, which is not safe to share across
/// concurrent requests).
/// </summary>
public class TtsSendPipeline : ITtsSendPipeline
{
    protected const int DisplayMessageLength = 50;

    private readonly ITtsService _ttsService;
    private readonly ISettingsService _settingsService;
    private readonly IAudioService _audioService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TtsSendPipeline> _logger;

    public ConcurrentDictionary<ulong, string> CurrentMessages { get; } = new();
    public ConcurrentDictionary<ulong, bool> PlaybackState { get; } = new();
    public ConcurrentDictionary<ulong, CancellationTokenSource> PlaybackCancellationTokens { get; } = new();
    public int MaxDisplayMessageLength => DisplayMessageLength;

    public TtsSendPipeline(
        ITtsService ttsService,
        ISettingsService settingsService,
        IAudioService audioService,
        IServiceScopeFactory scopeFactory,
        ILogger<TtsSendPipeline> logger)
    {
        _ttsService = ttsService;
        _settingsService = settingsService;
        _audioService = audioService;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> IsAudioGloballyEnabledAsync()
    {
        return await _settingsService.GetSettingValueAsync<bool?>("Features:AudioEnabled") ?? true;
    }

    /// <inheritdoc />
    public async Task<IActionResult?> CheckTtsEnabledAsync(HttpContext httpContext, ulong guildId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var ttsSettingsService = scope.ServiceProvider.GetRequiredService<ITtsSettingsService>();

        var settings = await ttsSettingsService.GetOrCreateSettingsAsync(guildId, cancellationToken);
        if (!settings.TtsEnabled)
        {
            _logger.LogWarning("TTS not enabled for guild {GuildId}", guildId);
            return new BadRequestObjectResult(new ApiErrorDto
            {
                Message = "TTS is not enabled for this guild",
                Detail = "Contact a server administrator to enable TTS in guild settings.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = httpContext.GetCorrelationId(),
                ErrorCode = "tts_not_enabled"
            });
        }

        return null;
    }

    /// <inheritdoc />
    public Task<Stream> SynthesizeFromRequestAsync(SendTtsRequest request, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var ssmlBuilder = scope.ServiceProvider.GetRequiredService<ISsmlBuilder>();
        return SynthesizeFromRequestAsync(request, ssmlBuilder, cancellationToken);
    }

    private async Task<Stream> SynthesizeFromRequestAsync(SendTtsRequest request, ISsmlBuilder ssmlBuilder, CancellationToken cancellationToken)
    {
        // If SSML is provided, use SSML synthesis directly
        if (!string.IsNullOrWhiteSpace(request.Ssml))
        {
            _logger.LogDebug("Using SSML synthesis");
            return await _ttsService.SynthesizeSpeechAsync(request.Ssml, null, SynthesisMode.Ssml, cancellationToken);
        }

        // If Style is provided, use SSML builder to wrap message with style
        if (!string.IsNullOrWhiteSpace(request.Style))
        {
            _logger.LogDebug("Using style '{Style}' with intensity {Intensity}",
                request.Style, request.StyleIntensity ?? 1.0m);

            var styleIntensity = request.StyleIntensity ?? 1.0m;
            var builder = ssmlBuilder.Reset()
                .BeginDocument("en-US")
                .WithVoice(request.Voice)
                .WithStyle(request.Style, (double)styleIntensity);

            // Apply prosody adjustments (speed/pitch) if different from defaults
            if (Math.Abs(request.Speed - 1.0) > 0.01 || Math.Abs(request.Pitch - 1.0) > 0.01)
            {
                builder.WithProsody(rate: request.Speed, pitch: request.Pitch);
                builder.AddText(request.Message);
                builder.EndProsody();
            }
            else
            {
                builder.AddText(request.Message);
            }

            builder.EndStyle().EndVoice();
            var ssml = builder.Build();

            _logger.LogDebug("Built SSML with style: {SsmlLength} characters", ssml.Length);
            return await _ttsService.SynthesizeSpeechAsync(ssml, null, SynthesisMode.Ssml, cancellationToken);
        }

        // Otherwise, use standard TTS synthesis
        _logger.LogDebug("Using standard TTS synthesis");
        var options = new TtsOptions
        {
            Voice = request.Voice,
            Speed = request.Speed,
            Pitch = request.Pitch
        };
        return await _ttsService.SynthesizeSpeechAsync(request.Message, options, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IActionResult> SendTtsCoreAsync(HttpContext httpContext, ulong guildId, SendTtsRequest request, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var ttsSettingsService = scope.ServiceProvider.GetRequiredService<ITtsSettingsService>();
        var ttsPlaybackService = scope.ServiceProvider.GetRequiredService<ITtsPlaybackService>();
        var audioModerationLogService = scope.ServiceProvider.GetRequiredService<IAudioModerationLogService>();
        var ssmlBuilder = scope.ServiceProvider.GetRequiredService<ISsmlBuilder>();

        var user = httpContext.User;
        _logger.LogInformation("Send TTS request for guild {GuildId}, voice {Voice}", guildId, request.Voice);

        // Check if audio is globally enabled at the bot level
        if (!await IsAudioGloballyEnabledAsync())
        {
            _logger.LogWarning("Audio features globally disabled - rejecting SendTts for guild {GuildId}", guildId);
            return new BadRequestObjectResult(new ApiErrorDto
            {
                Message = "Audio features disabled",
                Detail = "Audio features have been disabled by an administrator.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = httpContext.GetCorrelationId(),
                ErrorCode = "audio_disabled"
            });
        }

        // Check if TTS is enabled for this guild
        var settings = await ttsSettingsService.GetOrCreateSettingsAsync(guildId, cancellationToken);
        if (!settings.TtsEnabled)
        {
            _logger.LogWarning("TTS not enabled for guild {GuildId}", guildId);
            return new BadRequestObjectResult(new ApiErrorDto
            {
                Message = "TTS is not enabled for this guild",
                Detail = "Contact a server administrator to enable TTS in guild settings.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = httpContext.GetCorrelationId(),
                ErrorCode = "tts_not_enabled"
            });
        }

        // Check if bot is connected to voice
        if (!_audioService.IsConnected(guildId))
        {
            _logger.LogWarning("Bot not connected to voice channel in guild {GuildId}", guildId);
            return new BadRequestObjectResult(new ApiErrorDto
            {
                Message = "Not connected to voice channel",
                Detail = "The bot must be connected to a voice channel before sending TTS messages.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = httpContext.GetCorrelationId(),
                ErrorCode = "not_connected"
            });
        }

        // Validate message is not empty
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            _logger.LogWarning("Empty TTS message provided for guild {GuildId}", guildId);
            return new BadRequestObjectResult(new ApiErrorDto
            {
                Message = "Message cannot be empty",
                Detail = "Please provide a message to synthesize.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = httpContext.GetCorrelationId(),
                ErrorCode = "empty_message"
            });
        }

        // Validate message length against guild settings
        if (request.Message.Length > settings.MaxMessageLength)
        {
            _logger.LogWarning("TTS message too long for guild {GuildId} (length: {Length}, max: {Max})",
                guildId, request.Message.Length, settings.MaxMessageLength);
            return new BadRequestObjectResult(new ApiErrorDto
            {
                Message = "Message too long",
                Detail = $"Message length ({request.Message.Length}) exceeds the maximum allowed ({settings.MaxMessageLength}).",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = httpContext.GetCorrelationId(),
                ErrorCode = "message_too_long"
            });
        }

        // Check rate limit
        var userId = user.GetDiscordUserId();

        // Enrich APM transaction for Kibana visibility
        try
        {
            var transaction = Elastic.Apm.Agent.Tracer.CurrentTransaction;
            if (transaction != null)
            {
                transaction.Name = "portal.tts.send";
                transaction.SetLabel("guild_id", guildId.ToString());
                transaction.SetLabel("voice", request.Voice ?? "default");
                transaction.SetLabel("text_length", request.Message?.Length ?? 0);
                transaction.SetLabel("synthesis_mode", !string.IsNullOrWhiteSpace(request.Ssml) ? "ssml" : !string.IsNullOrWhiteSpace(request.Style) ? "style" : "plain");
                transaction.SetLabel("user_id", userId.ToString());
            }
        }
        catch { /* APM not available */ }

        if (await ttsSettingsService.IsUserRateLimitedAsync(guildId, userId, cancellationToken))
        {
            _logger.LogWarning("User {UserId} rate limited for TTS in guild {GuildId}", userId, guildId);
            return new ObjectResult(new ApiErrorDto
            {
                Message = "Rate limit exceeded",
                Detail = $"You have exceeded the rate limit of {settings.RateLimitPerMinute} messages per minute. Please wait before sending more messages.",
                StatusCode = StatusCodes.Status429TooManyRequests,
                TraceId = httpContext.GetCorrelationId(),
                ErrorCode = "rate_limited"
            })
            { StatusCode = StatusCodes.Status429TooManyRequests };
        }

        // Synthesize speech based on request parameters
        Stream audioStream;
        try
        {
            audioStream = await SynthesizeFromRequestAsync(request, ssmlBuilder, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "TTS service not configured for guild {GuildId}", guildId);
            return new BadRequestObjectResult(new ApiErrorDto
            {
                Message = "TTS service not available",
                Detail = "The text-to-speech service is not properly configured.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = httpContext.GetCorrelationId(),
                ErrorCode = "tts_not_configured"
            });
        }
        catch (SsmlValidationException ex)
        {
            _logger.LogWarning(ex, "SSML validation failed for guild {GuildId}", guildId);
            return new BadRequestObjectResult(new ApiErrorDto
            {
                Message = "SSML validation failed",
                Detail = string.Join("; ", ex.Errors),
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = httpContext.GetCorrelationId(),
                ErrorCode = "ssml_validation_failed"
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid TTS request for guild {GuildId}", guildId);
            return new BadRequestObjectResult(new ApiErrorDto
            {
                Message = "Invalid TTS request",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = httpContext.GetCorrelationId(),
                ErrorCode = "invalid_request"
            });
        }

        // Update current message tracking (truncate to MaxDisplayMessageLength characters)
        var truncatedMessage = request.Message.Length > MaxDisplayMessageLength
            ? request.Message.Substring(0, MaxDisplayMessageLength)
            : request.Message;
        CurrentMessages.AddOrUpdate(guildId, truncatedMessage, (k, v) => truncatedMessage);

        // Mark TTS as playing
        PlaybackState.AddOrUpdate(guildId, true, (k, v) => true);

        // Create a cancellation token that can be triggered by the stop endpoint
        // Link it with the request token so both HTTP disconnect and stop button work
        // Do NOT use 'using' — lifetime is managed explicitly via TryRemove in finally/StopPlayback
        var playbackCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Atomically swap in the new CTS, capturing any previous one for disposal
        CancellationTokenSource? previousCts = null;
        PlaybackCancellationTokens.AddOrUpdate(guildId, playbackCts, (_, existing) =>
        {
            previousCts = existing;
            return playbackCts;
        });
        if (previousCts != null)
        {
            await previousCts.CancelAsync();
            previousCts.Dispose();
        }

        // Play the audio using the TTS playback service
        TtsPlaybackResult playbackResult;
        try
        {
            playbackResult = await ttsPlaybackService.PlayAsync(
                guildId,
                userId,
                user.FindFirst("discord:username")?.Value ?? "Portal User",
                request.Message,
                request.Voice,
                audioStream,
                playbackCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Cancelled by the stop endpoint, not by HTTP disconnect — return success
            _logger.LogInformation("TTS playback was stopped by user for guild {GuildId}", guildId);
            return new OkObjectResult(new { Message = "Playback stopped" });
        }
        finally
        {
            // Whoever wins TryRemove owns disposal — prevents double-dispose with StopPlayback
            if (PlaybackCancellationTokens.TryRemove(guildId, out var removedCts))
                removedCts.Dispose();
            PlaybackState.TryRemove(guildId, out _);
            CurrentMessages.TryRemove(guildId, out _);
        }

        if (!playbackResult.Success)
        {
            _logger.LogWarning("TTS playback failed for guild {GuildId}: {ErrorMessage}", guildId, playbackResult.ErrorMessage);
            return new BadRequestObjectResult(new ApiErrorDto
            {
                Message = "Failed to play TTS",
                Detail = playbackResult.ErrorMessage ?? "An error occurred while streaming audio to Discord.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = httpContext.GetCorrelationId(),
                ErrorCode = "play_failed"
            });
        }

        _logger.LogInformation("Successfully sent TTS message for guild {GuildId}", guildId);

        // Log to audio moderation log (fire-and-forget)
        audioModerationLogService.LogPlayback(guildId, userId, AudioFeatureType.Tts, request.Message, channelId: null);

        return new OkObjectResult(new { Message = "TTS message sent successfully", DurationSeconds = playbackResult.DurationSeconds });
    }
}
