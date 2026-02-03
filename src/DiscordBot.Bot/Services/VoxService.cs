using Discord.Audio;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs.Vox;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces.Vox;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service implementation for high-level VOX message playback operations.
/// Orchestrates tokenization, clip lookup, concatenation, and Discord audio streaming.
/// </summary>
public class VoxService : IVoxService
{
    private readonly ILogger<VoxService> _logger;
    private readonly VoxOptions _options;
    private readonly IVoxClipLibrary _clipLibrary;
    private readonly IVoxConcatenationService _concatenationService;
    private readonly IAudioService _audioService;

    public VoxService(
        ILogger<VoxService> logger,
        IOptions<VoxOptions> options,
        IVoxClipLibrary clipLibrary,
        IVoxConcatenationService concatenationService,
        IAudioService audioService)
    {
        _logger = logger;
        _options = options.Value;
        _clipLibrary = clipLibrary;
        _concatenationService = concatenationService;
        _audioService = audioService;
    }

    /// <inheritdoc/>
    public async Task<VoxPlaybackResult> PlayAsync(
        ulong guildId,
        string message,
        VoxClipGroup group,
        VoxPlaybackOptions options,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return new VoxPlaybackResult
            {
                Success = false,
                ErrorMessage = "Message cannot be empty."
            };
        }

        // Validate message length
        if (message.Length > _options.MaxMessageLength)
        {
            return new VoxPlaybackResult
            {
                Success = false,
                ErrorMessage = $"Message exceeds maximum length of {_options.MaxMessageLength} characters."
            };
        }

        // Validate word gap range
        if (options.WordGapMs < 20 || options.WordGapMs > 200)
        {
            return new VoxPlaybackResult
            {
                Success = false,
                ErrorMessage = $"Word gap must be between 20 and 200 milliseconds (got {options.WordGapMs}ms)."
            };
        }

        // Tokenize the message
        var tokens = TokenizeMessage(message);

        // Validate token count
        if (tokens.Count > _options.MaxMessageWords)
        {
            return new VoxPlaybackResult
            {
                Success = false,
                ErrorMessage = $"Message exceeds maximum word count of {_options.MaxMessageWords} words."
            };
        }

        _logger.LogDebug("Processing VOX message with {TokenCount} tokens for group {Group}", tokens.Count, group);

        // Look up clips for each token
        var matchedClips = new List<VoxClipInfo>();
        var matchedWords = new List<string>();
        var skippedWords = new List<string>();

        foreach (var token in tokens)
        {
            var clip = _clipLibrary.GetClip(group, token);
            if (clip != null)
            {
                matchedClips.Add(clip);
                matchedWords.Add(token);
            }
            else
            {
                skippedWords.Add(token);
                _logger.LogDebug("No clip found for token: {Token}", token);
            }
        }

        // Check if at least one clip matched
        if (matchedClips.Count == 0)
        {
            return new VoxPlaybackResult
            {
                Success = false,
                ErrorMessage = "No matching clips found for any words in the message.",
                SkippedWords = skippedWords
            };
        }

        // Check if bot is connected to voice
        var audioClient = _audioService.GetAudioClient(guildId);
        if (audioClient == null)
        {
            return new VoxPlaybackResult
            {
                Success = false,
                ErrorMessage = "Bot is not connected to a voice channel.",
                MatchedWords = matchedWords,
                SkippedWords = skippedWords
            };
        }

        // Get clip file paths
        var clipPaths = matchedClips
            .Select(c => _clipLibrary.GetClipFilePath(c.Group, c.Name))
            .ToList();

        _logger.LogInformation(
            "Playing VOX message: {MatchedCount} matched, {SkippedCount} skipped",
            matchedClips.Count,
            skippedWords.Count);

        string? concatenatedPcmPath = null;

        try
        {
            // Concatenate clips
            concatenatedPcmPath = await _concatenationService.ConcatenateAsync(
                clipPaths,
                options.WordGapMs,
                cancellationToken);

            // Calculate total duration
            var totalDuration = matchedClips.Sum(c => c.DurationSeconds);
            if (matchedClips.Count > 1)
            {
                totalDuration += (matchedClips.Count - 1) * (options.WordGapMs / 1000.0);
            }

            // Stream PCM to Discord
            await StreamPcmToDiscordAsync(audioClient, concatenatedPcmPath, guildId, cancellationToken);

            // Update last activity
            _audioService.UpdateLastActivity(guildId);

            return new VoxPlaybackResult
            {
                Success = true,
                MatchedWords = matchedWords,
                SkippedWords = skippedWords,
                EstimatedDurationSeconds = totalDuration
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("VOX playback cancelled for guild {GuildId}", guildId);
            return new VoxPlaybackResult
            {
                Success = false,
                ErrorMessage = "Playback was cancelled.",
                MatchedWords = matchedWords,
                SkippedWords = skippedWords
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to play VOX message for guild {GuildId}", guildId);
            return new VoxPlaybackResult
            {
                Success = false,
                ErrorMessage = $"Failed to play VOX message: {ex.Message}",
                MatchedWords = matchedWords,
                SkippedWords = skippedWords
            };
        }
        finally
        {
            // Clean up temporary file
            if (concatenatedPcmPath != null && File.Exists(concatenatedPcmPath))
            {
                try
                {
                    File.Delete(concatenatedPcmPath);
                    _logger.LogDebug("Cleaned up temporary PCM file: {FilePath}", concatenatedPcmPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to clean up temporary PCM file: {FilePath}", concatenatedPcmPath);
                }
            }
        }
    }

    /// <inheritdoc/>
    public VoxTokenPreview TokenizePreview(string message, VoxClipGroup group)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return new VoxTokenPreview
            {
                Tokens = new List<VoxTokenInfo>()
            };
        }

        // Tokenize the message
        var tokens = TokenizeMessage(message);

        var tokenInfos = new List<VoxTokenInfo>();
        var matchedCount = 0;
        var skippedCount = 0;
        var totalDuration = 0.0;

        foreach (var token in tokens)
        {
            var clip = _clipLibrary.GetClip(group, token);
            if (clip != null)
            {
                tokenInfos.Add(new VoxTokenInfo
                {
                    Word = token,
                    HasClip = true,
                    DurationSeconds = clip.DurationSeconds
                });
                matchedCount++;
                totalDuration += clip.DurationSeconds;
            }
            else
            {
                tokenInfos.Add(new VoxTokenInfo
                {
                    Word = token,
                    HasClip = false,
                    DurationSeconds = 0
                });
                skippedCount++;
            }
        }

        // Add word gaps to duration estimate
        if (matchedCount > 1)
        {
            totalDuration += (matchedCount - 1) * (_options.DefaultWordGapMs / 1000.0);
        }

        return new VoxTokenPreview
        {
            Tokens = tokenInfos,
            MatchedCount = matchedCount,
            SkippedCount = skippedCount,
            EstimatedDurationSeconds = totalDuration
        };
    }

    /// <summary>
    /// Tokenizes a message into individual words.
    /// Converts punctuation to timing tokens (comma -> _comma, period -> _period),
    /// then splits on whitespace and converts to lowercase.
    /// Preserves other punctuation like ! and ? that may be part of clip names (e.g., "request!").
    /// </summary>
    private List<string> TokenizeMessage(string message)
    {
        // Replace punctuation with spaced tokens to ensure they become separate tokens
        var processed = message
            .Replace(",", " _comma ")
            .Replace(".", " _period ");

        // Split on whitespace
        var rawTokens = processed.Split(
            new[] { ' ', '\t', '\n', '\r' },
            StringSplitOptions.RemoveEmptyEntries);

        var tokens = new List<string>();

        foreach (var rawToken in rawTokens)
        {
            // Convert to lowercase - preserve all characters including ! and ?
            var token = rawToken.ToLowerInvariant();

            // Only add non-empty tokens
            if (!string.IsNullOrEmpty(token))
            {
                tokens.Add(token);
            }
        }

        return tokens;
    }

    /// <summary>
    /// Streams raw PCM audio to Discord.
    /// </summary>
    private async Task StreamPcmToDiscordAsync(
        IAudioClient audioClient,
        string pcmFilePath,
        ulong guildId,
        CancellationToken cancellationToken)
    {
        const int bufferSize = 3840; // 20ms of audio at 48kHz stereo 16-bit

        var discord = _audioService.GetOrCreatePcmStream(guildId);
        if (discord == null)
        {
            throw new InvalidOperationException($"Failed to get PCM stream for guild {guildId}");
        }

        using var fileStream = File.OpenRead(pcmFilePath);

        var buffer = new byte[bufferSize];
        int bytesRead;

        while ((bytesRead = await fileStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await discord.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }

        await discord.FlushAsync(cancellationToken);

        _logger.LogDebug("Finished streaming PCM to Discord");
    }
}
