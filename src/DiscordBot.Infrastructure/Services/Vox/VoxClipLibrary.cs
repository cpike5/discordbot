using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs.Vox;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces.Vox;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordBot.Infrastructure.Services.Vox;

/// <summary>
/// Service implementation for the VOX clip library that manages and provides access to audio clips.
/// Scans directories on initialization and provides fast lookup and search capabilities.
/// </summary>
public class VoxClipLibrary : IVoxClipLibrary
{
    private readonly ILogger<VoxClipLibrary> _logger;
    private readonly VoxOptions _options;
    private readonly Dictionary<VoxClipGroup, Dictionary<string, VoxClipInfo>> _clipInventory;
    private readonly SemaphoreSlim _initLock;
    private readonly TaskCompletionSource _initializationTcs;
    private bool _initialized;

    public VoxClipLibrary(
        ILogger<VoxClipLibrary> logger,
        IOptions<VoxOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        _clipInventory = new Dictionary<VoxClipGroup, Dictionary<string, VoxClipInfo>>();
        _initLock = new SemaphoreSlim(1, 1);
        _initializationTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <inheritdoc/>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                _logger.LogWarning("VoxClipLibrary already initialized. Skipping re-initialization");
                return;
            }

            _logger.LogInformation("Initializing VOX clip library from base path: {BasePath}", _options.BasePath);

            var groups = new[] { VoxClipGroup.Vox, VoxClipGroup.Fvox, VoxClipGroup.Hgrunt };

            foreach (var group in groups)
            {
                _clipInventory[group] = new Dictionary<string, VoxClipInfo>(StringComparer.OrdinalIgnoreCase);
            }

            // Scan each group's directory in parallel
            var scanTasks = groups.Select(group => ScanGroupDirectoryAsync(group, cancellationToken));
            await Task.WhenAll(scanTasks);

            // Log summary
            foreach (var group in groups)
            {
                var count = _clipInventory[group].Count;
                _logger.LogInformation("VOX clip library initialized: {Group} has {Count} clips", group, count);
            }

            _initialized = true;
            _initializationTcs.TrySetResult();
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <inheritdoc/>
    public bool IsInitialized => _initialized;

    /// <inheritdoc/>
    public async Task<bool> WaitForInitializationAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (_initialized)
            return true;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            await _initializationTcs.Task.WaitAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<VoxClipInfo> GetClips(VoxClipGroup group)
    {
        EnsureInitialized();
        return _clipInventory[group].Values.ToList();
    }

    /// <inheritdoc/>
    public VoxClipInfo? GetClip(VoxClipGroup group, string clipName)
    {
        EnsureInitialized();

        if (string.IsNullOrWhiteSpace(clipName))
            return null;

        var normalizedName = clipName.ToLowerInvariant();
        return _clipInventory[group].TryGetValue(normalizedName, out var clip) ? clip : null;
    }

    /// <inheritdoc/>
    public IReadOnlyList<VoxClipInfo> SearchClips(VoxClipGroup group, string query, int maxResults = 25)
    {
        EnsureInitialized();

        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<VoxClipInfo>();

        var normalizedQuery = query.ToLowerInvariant();
        var clips = _clipInventory[group].Values;

        // Separate prefix matches from substring matches
        var prefixMatches = clips
            .Where(c => c.Name.StartsWith(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Name)
            .ToList();

        var substringMatches = clips
            .Where(c => !c.Name.StartsWith(normalizedQuery, StringComparison.OrdinalIgnoreCase) &&
                        c.Name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Name)
            .ToList();

        // Combine: prefix matches first, then substring matches
        var results = new List<VoxClipInfo>(maxResults);
        results.AddRange(prefixMatches.Take(maxResults));

        var remaining = maxResults - results.Count;
        if (remaining > 0)
        {
            results.AddRange(substringMatches.Take(remaining));
        }

        return results;
    }

    /// <inheritdoc/>
    public string GetClipFilePath(VoxClipGroup group, string clipName)
    {
        EnsureInitialized();
        var groupPath = GetGroupDirectoryPath(group);
        var normalizedName = clipName.ToLowerInvariant();

        // Look up the clip to get the original filename (which may differ from normalized name)
        if (_clipInventory[group].TryGetValue(normalizedName, out var clip))
        {
            return Path.Combine(groupPath, $"{clip.FileName}.mp3");
        }

        // Fallback to using the provided name if clip not found
        return Path.Combine(groupPath, $"{normalizedName}.mp3");
    }

    /// <inheritdoc/>
    public int GetClipCount(VoxClipGroup group)
    {
        EnsureInitialized();
        return _clipInventory[group].Count;
    }

    private async Task ScanGroupDirectoryAsync(VoxClipGroup group, CancellationToken cancellationToken)
    {
        var groupPath = GetGroupDirectoryPath(group);

        if (!Directory.Exists(groupPath))
        {
            _logger.LogWarning("VOX directory not found for group {Group}: {Path}", group, groupPath);
            return;
        }

        var mp3Files = Directory.GetFiles(groupPath, "*.mp3", SearchOption.TopDirectoryOnly);

        if (mp3Files.Length == 0)
        {
            _logger.LogInformation("No MP3 files found in {Group} directory: {Path}", group, groupPath);
            return;
        }

        _logger.LogDebug("Scanning {Count} MP3 files in {Group} directory", mp3Files.Length, group);

        // Process files in parallel for performance
        var clips = new List<VoxClipInfo>(mp3Files.Length);
        var lockObject = new object();

        await Parallel.ForEachAsync(
            mp3Files,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = cancellationToken },
            async (filePath, ct) =>
            {
                var clipInfo = await ExtractClipInfoAsync(filePath, group, ct);
                if (clipInfo != null)
                {
                    lock (lockObject)
                    {
                        clips.Add(clipInfo);
                    }
                }
            });

        // Add all clips to the inventory
        foreach (var clip in clips)
        {
            _clipInventory[group][clip.Name] = clip;
        }
    }

    private async Task<VoxClipInfo?> ExtractClipInfoAsync(string filePath, VoxClipGroup group, CancellationToken cancellationToken)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            var fileName = Path.GetFileNameWithoutExtension(filePath).ToLowerInvariant();

            // Extract duration using FFprobe
            var duration = await GetAudioDurationAsync(filePath, cancellationToken);

            return new VoxClipInfo
            {
                Name = fileName,
                FileName = fileName,
                Group = group,
                FileSizeBytes = fileInfo.Length,
                DurationSeconds = duration
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract clip info from file: {FilePath}", filePath);
            return null;
        }
    }

    private async Task<double> GetAudioDurationAsync(string filePath, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "ffprobe",
            Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{filePath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            _logger.LogWarning(ex, "FFprobe not found. Duration will be set to 0. Install FFmpeg and ensure ffprobe is in PATH");
            return 0.0;
        }

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            _logger.LogWarning("FFprobe exited with code {ExitCode} for file '{FilePath}': {Error}",
                process.ExitCode, filePath, error);
            return 0.0;
        }

        if (double.TryParse(output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var duration))
        {
            return duration;
        }

        _logger.LogWarning("Failed to parse duration from FFprobe output for file '{FilePath}': {Output}",
            filePath, output);
        return 0.0;
    }

    private string GetGroupDirectoryPath(VoxClipGroup group)
    {
        var groupName = group.ToString().ToLowerInvariant();
        return Path.Combine(_options.BasePath, groupName);
    }

    private void EnsureInitialized()
    {
        if (_initialized)
            return;

        // Wait briefly for initialization to complete (background service may still be initializing)
        var waitTask = WaitForInitializationAsync(TimeSpan.FromSeconds(30));
        if (!waitTask.GetAwaiter().GetResult())
        {
            throw new InvalidOperationException(
                "VoxClipLibrary initialization timed out. The library may still be loading clips in the background.");
        }
    }
}
