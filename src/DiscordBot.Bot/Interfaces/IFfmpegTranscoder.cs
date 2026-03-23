using DiscordBot.Core.Enums;

namespace DiscordBot.Bot.Interfaces;

/// <summary>
/// Manages FFmpeg process lifecycle for audio transcoding.
/// Responsible for building arguments, starting the process, and reading output.
/// </summary>
public interface IFfmpegTranscoder
{
    /// <summary>
    /// Builds FFmpeg command line arguments for audio transcoding with optional filter.
    /// Output format is always 48kHz stereo 16-bit PCM (s16le) to stdout.
    /// </summary>
    /// <param name="filePath">Path to the input audio file.</param>
    /// <param name="filter">The audio filter to apply.</param>
    /// <returns>FFmpeg arguments string.</returns>
    string BuildArguments(string filePath, AudioFilter filter);

    /// <summary>
    /// Resolves the FFmpeg executable path from configuration or system PATH.
    /// </summary>
    /// <returns>The path to the FFmpeg executable.</returns>
    string ResolveFfmpegPath();

    /// <summary>
    /// Starts an FFmpeg transcoding process and returns the result.
    /// The caller is responsible for reading from the output stream and killing the process.
    /// </summary>
    /// <param name="filePath">Path to the input audio file.</param>
    /// <param name="filter">The audio filter to apply.</param>
    /// <returns>A transcoding session containing the process and output stream.</returns>
    /// <exception cref="InvalidOperationException">Thrown if FFmpeg fails to start.</exception>
    FfmpegTranscodeSession StartTranscode(string filePath, AudioFilter filter);
}

/// <summary>
/// Represents an active FFmpeg transcoding session.
/// The caller must dispose this when done to ensure the FFmpeg process is cleaned up.
/// </summary>
public sealed class FfmpegTranscodeSession : IDisposable
{
    /// <summary>
    /// Gets the FFmpeg process.
    /// </summary>
    public required System.Diagnostics.Process Process { get; init; }

    /// <summary>
    /// Gets the PCM audio output stream (FFmpeg stdout).
    /// </summary>
    public required Stream OutputStream { get; init; }

    /// <summary>
    /// Gets the FFmpeg arguments used for this session.
    /// </summary>
    public required string Arguments { get; init; }

    /// <summary>
    /// Reads any error output from FFmpeg stderr.
    /// Should only be called after the process has exited or output stream is fully read.
    /// </summary>
    /// <returns>The stderr output.</returns>
    public Task<string> ReadErrorOutputAsync() => Process.StandardError.ReadToEndAsync();

    /// <summary>
    /// Gets the FFmpeg process exit code.
    /// </summary>
    public int ExitCode => Process.ExitCode;

    /// <summary>
    /// Gets whether the FFmpeg process has exited.
    /// </summary>
    public bool HasExited => Process.HasExited;

    /// <summary>
    /// Gets the FFmpeg process ID.
    /// </summary>
    public int ProcessId => Process.Id;

    /// <summary>
    /// Kills the FFmpeg process if it is still running.
    /// </summary>
    public void KillIfRunning()
    {
        if (!Process.HasExited)
        {
            Process.Kill();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        KillIfRunning();
        Process.Dispose();
    }
}
