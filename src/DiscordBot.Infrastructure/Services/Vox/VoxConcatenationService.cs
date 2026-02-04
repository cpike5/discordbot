using System.ComponentModel;
using System.Diagnostics;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces.Vox;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordBot.Infrastructure.Services.Vox;

/// <summary>
/// Service implementation for concatenating multiple VOX audio clips into a single output file.
/// Uses FFmpeg to decode clips to PCM and programmatically inserts silence between segments.
/// </summary>
public class VoxConcatenationService : IVoxConcatenationService
{
    private readonly ILogger<VoxConcatenationService> _logger;
    private readonly VoxOptions _options;
    private static readonly ActivitySource ActivitySource = new("DiscordBot.Vox");

    public VoxConcatenationService(
        ILogger<VoxConcatenationService> logger,
        IOptions<VoxOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc/>
    public async Task<VoxConcatenationResult> ConcatenateAsync(
        IReadOnlyList<string> clipFilePaths,
        int wordGapMs,
        CancellationToken cancellationToken = default)
    {
        if (clipFilePaths == null || clipFilePaths.Count == 0)
        {
            throw new ArgumentException("At least one clip file path must be provided.", nameof(clipFilePaths));
        }

        if (wordGapMs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(wordGapMs), "Word gap must be non-negative.");
        }

        // Validate all input files exist
        foreach (var path in clipFilePaths)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Clip file not found: {path}", path);
            }
        }

        using var activity = ActivitySource.StartActivity("Concatenation");
        activity?.SetTag("clip_count", clipFilePaths.Count);
        activity?.SetTag("word_gap_ms", wordGapMs);

        var stopwatch = Stopwatch.StartNew();

        _logger.LogDebug("Concatenating {Count} clips with {GapMs}ms word gap", clipFilePaths.Count, wordGapMs);

        // Single clip optimization - no concatenation needed
        if (clipFilePaths.Count == 1)
        {
            _logger.LogDebug("Single clip detected, decoding to PCM without concatenation");
            var singleResult = await DecodeSingleClipToPcmAsync(clipFilePaths[0], cancellationToken);
            stopwatch.Stop();

            var singleBytes = new FileInfo(singleResult).Length;
            var singleDurationMs = stopwatch.Elapsed.TotalMilliseconds;

            _logger.LogInformation(
                "VOX_CONCATENATION_COMPLETED: Concatenated {ClipCount} clips ({AudioBytes} bytes) in {ConcatenationMs}ms",
                clipFilePaths.Count, singleBytes, singleDurationMs);

            activity?.SetTag("duration_ms", singleDurationMs);
            activity?.SetTag("output_bytes", singleBytes);
            activity?.SetStatus(ActivityStatusCode.Ok);

            return new VoxConcatenationResult(singleResult, singleBytes, singleDurationMs);
        }

        // Generate output path in temp directory
        var outputPath = Path.Combine(Path.GetTempPath(), $"vox_concat_{Guid.NewGuid():N}.pcm");

        // Register cleanup on cancellation
        using var cancellationRegistration = cancellationToken.Register(() =>
        {
            try
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                    _logger.LogDebug("Cleaned up temporary file on cancellation: {FilePath}", outputPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up temporary file: {FilePath}", outputPath);
            }
        });

        try
        {
            // Calculate silence bytes needed: gapMs * 48000 * 2 (bytes/sample) * 2 (channels) / 1000 = gapMs * 192
            var silenceBytes = wordGapMs * 192;
            var silenceBuffer = new byte[silenceBytes];
            Array.Fill<byte>(silenceBuffer, 0); // PCM silence is zero-filled

            // Open output file for writing
            await using var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920);

            // Process each clip
            for (int i = 0; i < clipFilePaths.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var clipPath = clipFilePaths[i];
                _logger.LogDebug("Processing clip {Index}/{Total}: {FilePath}", i + 1, clipFilePaths.Count, clipPath);

                // Decode clip to PCM and append to output
                await DecodeToPcmStreamAsync(clipPath, outputStream, cancellationToken);

                // Insert silence gap after clip (except after the last clip)
                if (i < clipFilePaths.Count - 1 && silenceBytes > 0)
                {
                    await outputStream.WriteAsync(silenceBuffer, cancellationToken);
                    _logger.LogTrace("Inserted {Bytes} bytes of silence ({Ms}ms) after clip {Index}", silenceBytes, wordGapMs, i + 1);
                }
            }

            stopwatch.Stop();
            var audioBytes = outputStream.Length;
            var durationMs = stopwatch.Elapsed.TotalMilliseconds;

            _logger.LogInformation(
                "VOX_CONCATENATION_COMPLETED: Concatenated {ClipCount} clips ({AudioBytes} bytes) in {ConcatenationMs}ms",
                clipFilePaths.Count, audioBytes, durationMs);

            activity?.SetTag("duration_ms", durationMs);
            activity?.SetTag("output_bytes", audioBytes);
            activity?.SetStatus(ActivityStatusCode.Ok);

            return new VoxConcatenationResult(outputPath, audioBytes, durationMs);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            _logger.LogError(ex, "Failed to concatenate clips after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

            // Clean up output file on error
            try
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
            catch (Exception cleanupEx)
            {
                _logger.LogWarning(cleanupEx, "Failed to clean up temporary file after error: {FilePath}", outputPath);
            }

            throw;
        }
    }

    /// <summary>
    /// Decodes a single audio file to PCM format (48kHz, stereo, s16le) and returns the temp file path.
    /// </summary>
    private async Task<string> DecodeSingleClipToPcmAsync(string inputPath, CancellationToken cancellationToken)
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"vox_single_{Guid.NewGuid():N}.pcm");

        // Register cleanup on cancellation
        using var cancellationRegistration = cancellationToken.Register(() =>
        {
            try
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                    _logger.LogDebug("Cleaned up temporary file on cancellation: {FilePath}", outputPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up temporary file: {FilePath}", outputPath);
            }
        });

        var startInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(inputPath);
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add("s16le");
        startInfo.ArgumentList.Add("-ar");
        startInfo.ArgumentList.Add("48000");
        startInfo.ArgumentList.Add("-ac");
        startInfo.ArgumentList.Add("2");
        startInfo.ArgumentList.Add(outputPath);

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException("FFmpeg not found. Install FFmpeg and ensure it is in PATH.", ex);
        }

        try
        {
            var errorOutput = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                _logger.LogError("FFmpeg exited with code {ExitCode}: {Error}", process.ExitCode, errorOutput);

                // Clean up output file if created
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }

                throw new InvalidOperationException($"FFmpeg failed to decode clip: {inputPath}. Exit code: {process.ExitCode}");
            }
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill();
                _logger.LogDebug("Killed FFmpeg process due to cancellation or early exit");
            }
        }

        _logger.LogDebug("Decoded single clip to PCM: {OutputPath}", outputPath);
        return outputPath;
    }

    /// <summary>
    /// Decodes an audio file to PCM and writes the output directly to a stream.
    /// Output format: PCM s16le, 48kHz, stereo (compatible with PlaybackService).
    /// </summary>
    private async Task DecodeToPcmStreamAsync(string inputPath, Stream outputStream, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(inputPath);
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add("s16le");
        startInfo.ArgumentList.Add("-ar");
        startInfo.ArgumentList.Add("48000");
        startInfo.ArgumentList.Add("-ac");
        startInfo.ArgumentList.Add("2");
        startInfo.ArgumentList.Add("-");

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException("FFmpeg not found. Install FFmpeg and ensure it is in PATH.", ex);
        }

        try
        {
            // Read FFmpeg's stdout (PCM data) and write to output stream
            var copyTask = process.StandardOutput.BaseStream.CopyToAsync(outputStream, cancellationToken);

            // Read stderr for logging (but don't block on it)
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await copyTask;
            await process.WaitForExitAsync(cancellationToken);
            var errorOutput = await errorTask;

            if (process.ExitCode != 0)
            {
                _logger.LogError("FFmpeg exited with code {ExitCode}: {Error}", process.ExitCode, errorOutput);
                throw new InvalidOperationException($"FFmpeg failed to decode clip: {inputPath}. Exit code: {process.ExitCode}");
            }
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill();
                _logger.LogDebug("Killed FFmpeg process due to cancellation or early exit");
            }
        }
    }
}
