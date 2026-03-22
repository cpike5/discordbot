using System.Diagnostics;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Constants;
using DiscordBot.Core.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services.Audio;

/// <summary>
/// Manages FFmpeg process lifecycle for audio transcoding.
/// Builds command-line arguments, resolves the FFmpeg executable path,
/// and starts transcoding processes that output 48kHz stereo 16-bit PCM.
/// </summary>
public class FfmpegTranscoder : IFfmpegTranscoder
{
    private readonly ILogger<FfmpegTranscoder> _logger;
    private readonly SoundboardOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="FfmpegTranscoder"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="options">Soundboard configuration options containing the FFmpeg path.</param>
    public FfmpegTranscoder(
        ILogger<FfmpegTranscoder> logger,
        IOptions<SoundboardOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc/>
    public string BuildArguments(string filePath, AudioFilter filter)
    {
        var filterString = AudioFilters.GetFfmpegFilter(filter);

        if (string.IsNullOrEmpty(filterString))
        {
            // No filter - standard transcoding
            return $"-hide_banner -loglevel warning -i \"{filePath}\" -ac 2 -f s16le -ar 48000 pipe:1";
        }

        // With filter - insert -af between input and output format
        return $"-hide_banner -loglevel warning -i \"{filePath}\" -af \"{filterString}\" -ac 2 -f s16le -ar 48000 pipe:1";
    }

    /// <inheritdoc/>
    public string ResolveFfmpegPath()
    {
        if (!string.IsNullOrWhiteSpace(_options.FfmpegPath))
        {
            return _options.FfmpegPath;
        }

        var localFfmpeg = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
        return File.Exists(localFfmpeg) ? localFfmpeg : "ffmpeg";
    }

    /// <inheritdoc/>
    public FfmpegTranscodeSession StartTranscode(string filePath, AudioFilter filter)
    {
        var ffmpegPath = ResolveFfmpegPath();
        var arguments = BuildArguments(filePath, filter);

        _logger.LogDebug("Starting FFmpeg from path '{FfmpegPath}' for file '{FilePath}'", ffmpegPath, filePath);
        _logger.LogDebug("FFmpeg arguments: {Arguments}", arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start FFmpeg process from '{ffmpegPath}'");

        _logger.LogDebug("FFmpeg process started (PID: {ProcessId})", process.Id);

        return new FfmpegTranscodeSession
        {
            Process = process,
            OutputStream = process.StandardOutput.BaseStream,
            Arguments = arguments
        };
    }
}
