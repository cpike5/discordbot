using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service implementation for managing sound file storage on disk.
/// Handles file I/O operations, path resolution, and audio file validation.
/// </summary>
public class SoundFileService : ISoundFileService
{
    private readonly ILogger<SoundFileService> _logger;
    private readonly SoundboardOptions _options;

    public SoundFileService(
        ILogger<SoundFileService> logger,
        IOptions<SoundboardOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc/>
    public async Task SaveSoundFileAsync(ulong guildId, string fileName, Stream fileStream, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "sound_file",
            "save",
            guildId: guildId);

        try
        {
            _logger.LogInformation("Saving sound file '{FileName}' for guild {GuildId}", fileName, guildId);

            await EnsureGuildDirectoryExistsAsync(guildId, ct);

            var filePath = GetSoundFilePath(guildId, fileName);

            // Use FileStream to write the file to disk
            using (var fileStreamOutput = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await fileStream.CopyToAsync(fileStreamOutput, ct);
            }

            _logger.LogInformation("Sound file '{FileName}' saved successfully for guild {GuildId} at '{FilePath}'",
                fileName, guildId, filePath);

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save sound file '{FileName}' for guild {GuildId}", fileName, guildId);
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteSoundFileAsync(ulong guildId, string fileName, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "sound_file",
            "delete",
            guildId: guildId);

        try
        {
            _logger.LogInformation("Deleting sound file '{FileName}' for guild {GuildId}", fileName, guildId);

            var filePath = GetSoundFilePath(guildId, fileName);

            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Sound file '{FileName}' not found at '{FilePath}' for guild {GuildId}",
                    fileName, filePath, guildId);
                BotActivitySource.SetSuccess(activity);
                return false;
            }

            await Task.Run(() => File.Delete(filePath), ct);

            _logger.LogInformation("Sound file '{FileName}' deleted successfully for guild {GuildId}",
                fileName, guildId);

            BotActivitySource.SetSuccess(activity);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete sound file '{FileName}' for guild {GuildId}", fileName, guildId);
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public string GetSoundFilePath(ulong guildId, string fileName)
    {
        return Path.Combine(_options.BasePath, guildId.ToString(), fileName);
    }

    /// <inheritdoc/>
    public bool SoundFileExists(ulong guildId, string fileName)
    {
        var filePath = GetSoundFilePath(guildId, fileName);
        return File.Exists(filePath);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> DiscoverSoundFilesAsync(ulong guildId, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "sound_file",
            "discover",
            guildId: guildId);

        try
        {
            _logger.LogDebug("Discovering sound files for guild {GuildId}", guildId);

            var guildDirectory = Path.Combine(_options.BasePath, guildId.ToString());

            if (!Directory.Exists(guildDirectory))
            {
                _logger.LogDebug("Guild directory does not exist for guild {GuildId}, returning empty list", guildId);
                BotActivitySource.SetRecordsReturned(activity, 0);
                BotActivitySource.SetSuccess(activity);
                return Array.Empty<string>();
            }

            // Discover all files with supported audio formats
            var files = await Task.Run(() =>
            {
                return Directory.EnumerateFiles(guildDirectory)
                    .Select(Path.GetFileName)
                    .Where(f => f != null && IsValidAudioFormat(f))
                    .Cast<string>()
                    .ToList();
            }, ct);

            _logger.LogInformation("Discovered {Count} sound files for guild {GuildId}", files.Count, guildId);

            BotActivitySource.SetRecordsReturned(activity, files.Count);
            BotActivitySource.SetSuccess(activity);
            return files;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover sound files for guild {GuildId}", guildId);
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<double> GetAudioDurationAsync(string filePath, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "sound_file",
            "get_duration");

        try
        {
            _logger.LogDebug("Getting audio duration for file '{FilePath}'", filePath);

            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Cannot get duration - file does not exist: '{FilePath}'", filePath);
                BotActivitySource.SetSuccess(activity);
                return 0.0;
            }

            var ffprobePath = _options.FfprobePath ?? "ffprobe";

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ffprobePath,
                Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{filePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new System.Diagnostics.Process { StartInfo = startInfo };

            try
            {
                process.Start();
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                _logger.LogWarning(ex, "FFprobe not found at '{FfprobePath}'. Duration will be 0. Install FFmpeg or configure Soundboard:FfprobePath", ffprobePath);
                BotActivitySource.SetSuccess(activity);
                return 0.0;
            }

            var output = await process.StandardOutput.ReadToEndAsync(ct);
            var error = await process.StandardError.ReadToEndAsync(ct);

            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                _logger.LogWarning("FFprobe exited with code {ExitCode} for file '{FilePath}': {Error}",
                    process.ExitCode, filePath, error);
                BotActivitySource.SetSuccess(activity);
                return 0.0;
            }

            if (double.TryParse(output.Trim(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var duration))
            {
                _logger.LogDebug("Audio duration for '{FilePath}': {Duration}s", filePath, duration);
                BotActivitySource.SetSuccess(activity);
                return duration;
            }

            _logger.LogWarning("Could not parse FFprobe output '{Output}' for file '{FilePath}'", output, filePath);
            BotActivitySource.SetSuccess(activity);
            return 0.0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to get audio duration for file '{FilePath}'", filePath);
            BotActivitySource.RecordException(activity, ex);
            return 0.0;
        }
    }

    /// <inheritdoc/>
    public bool IsValidAudioFormat(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return false;
        }

        // Remove leading dot from extension for comparison (e.g., ".mp3" -> "mp3")
        var extensionWithoutDot = extension.TrimStart('.');

        // Case-insensitive comparison against supported formats
        return _options.SupportedFormats.Any(format =>
            format.Equals(extensionWithoutDot, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc/>
    public async Task EnsureGuildDirectoryExistsAsync(ulong guildId, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "sound_file",
            "ensure_directory",
            guildId: guildId);

        try
        {
            var guildDirectory = Path.Combine(_options.BasePath, guildId.ToString());

            if (!Directory.Exists(guildDirectory))
            {
                _logger.LogInformation("Creating sound directory for guild {GuildId} at '{Directory}'",
                    guildId, guildDirectory);

                await Task.Run(() => Directory.CreateDirectory(guildDirectory), ct);

                _logger.LogInformation("Sound directory created for guild {GuildId}", guildId);
            }
            else
            {
                _logger.LogDebug("Sound directory already exists for guild {GuildId}", guildId);
            }

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure directory exists for guild {GuildId}", guildId);
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }
}
