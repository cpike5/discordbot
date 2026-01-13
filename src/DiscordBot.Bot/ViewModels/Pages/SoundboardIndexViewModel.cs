using DiscordBot.Core.Entities;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for a single sound in the soundboard.
/// </summary>
public record SoundViewModel
{
    /// <summary>
    /// Gets the unique identifier for this sound.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets the display name for the sound.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the filename on disk.
    /// </summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the file size in bytes.
    /// </summary>
    public long FileSizeBytes { get; init; }

    /// <summary>
    /// Gets the file size formatted for display (e.g., "152.3 KB", "1.2 MB").
    /// </summary>
    public string FileSizeFormatted { get; init; } = string.Empty;

    /// <summary>
    /// Gets the audio duration in seconds.
    /// </summary>
    public double DurationSeconds { get; init; }

    /// <summary>
    /// Gets the duration formatted for display (e.g., "0:02", "1:30").
    /// </summary>
    public string DurationFormatted { get; init; } = string.Empty;

    /// <summary>
    /// Gets the number of times this sound has been played.
    /// </summary>
    public int PlayCount { get; init; }

    /// <summary>
    /// Gets the timestamp when this sound was uploaded (UTC).
    /// </summary>
    public DateTime UploadedAt { get; init; }

    /// <summary>
    /// Gets the uploaded timestamp in ISO format for client-side rendering.
    /// </summary>
    public string UploadedAtUtcIso => DateTime.SpecifyKind(UploadedAt, DateTimeKind.Utc).ToString("o");

    /// <summary>
    /// Creates a SoundViewModel from a Sound entity.
    /// </summary>
    public static SoundViewModel FromEntity(Sound sound)
    {
        return new SoundViewModel
        {
            Id = sound.Id,
            Name = sound.Name,
            FileName = sound.FileName,
            FileSizeBytes = sound.FileSizeBytes,
            FileSizeFormatted = FormatFileSize(sound.FileSizeBytes),
            DurationSeconds = sound.DurationSeconds,
            DurationFormatted = FormatDuration(sound.DurationSeconds),
            PlayCount = sound.PlayCount,
            UploadedAt = sound.UploadedAt
        };
    }

    /// <summary>
    /// Formats a file size in bytes to a human-readable string.
    /// </summary>
    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";

        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";

        if (bytes < 1024 * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024.0):F1} MB";

        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
    }

    /// <summary>
    /// Formats a duration in seconds to a human-readable string (e.g., "0:02", "1:30").
    /// </summary>
    private static string FormatDuration(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);

        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}";

        return $"{ts.Minutes}:{ts.Seconds:D2}";
    }
}

/// <summary>
/// View model for soundboard statistics.
/// </summary>
public record SoundboardStatsViewModel
{
    /// <summary>
    /// Gets the total number of sounds in this guild.
    /// </summary>
    public int TotalSounds { get; init; }

    /// <summary>
    /// Gets the number of plays today.
    /// </summary>
    public int PlaysToday { get; init; }

    /// <summary>
    /// Gets the number of plays yesterday.
    /// </summary>
    public int PlaysYesterday { get; init; }

    /// <summary>
    /// Gets the comparison text to display (e.g., "+12% vs yesterday", "No data").
    /// </summary>
    public string ComparisonText { get; init; } = string.Empty;

    /// <summary>
    /// Gets the CSS class for the comparison text color (e.g., "text-green-600", "text-red-600").
    /// </summary>
    public string ComparisonCssClass { get; init; } = string.Empty;

    /// <summary>
    /// Gets the total storage used in bytes.
    /// </summary>
    public long StorageUsedBytes { get; init; }

    /// <summary>
    /// Gets the storage limit in bytes.
    /// </summary>
    public long StorageLimitBytes { get; init; }

    /// <summary>
    /// Gets the storage used formatted for display (e.g., "48 MB").
    /// </summary>
    public string StorageUsedFormatted { get; init; } = string.Empty;

    /// <summary>
    /// Gets the storage limit formatted for display (e.g., "500 MB").
    /// </summary>
    public string StorageLimitFormatted { get; init; } = string.Empty;

    /// <summary>
    /// Gets the storage usage percentage (0-100).
    /// </summary>
    public int StoragePercentage { get; init; }

    /// <summary>
    /// Gets the name of the most played sound.
    /// Null if there are no sounds.
    /// </summary>
    public string? TopSoundName { get; init; }

    /// <summary>
    /// Gets the play count of the most played sound.
    /// </summary>
    public int TopSoundPlays { get; init; }
}

/// <summary>
/// View model for the Soundboard management page.
/// </summary>
public record SoundboardIndexViewModel
{
    /// <summary>
    /// Gets the guild's Discord snowflake ID.
    /// </summary>
    public ulong GuildId { get; init; }

    /// <summary>
    /// Gets the guild name.
    /// </summary>
    public string GuildName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the guild icon URL.
    /// </summary>
    public string? GuildIconUrl { get; init; }

    /// <summary>
    /// Gets the soundboard statistics.
    /// </summary>
    public SoundboardStatsViewModel Stats { get; init; } = new();

    /// <summary>
    /// Gets the list of sounds in this guild.
    /// </summary>
    public List<SoundViewModel> Sounds { get; init; } = new();

    /// <summary>
    /// Gets the maximum number of sounds allowed per guild.
    /// </summary>
    public int MaxSoundsPerGuild { get; init; }

    /// <summary>
    /// Gets the maximum file size allowed in bytes.
    /// </summary>
    public long MaxFileSizeBytes { get; init; }

    /// <summary>
    /// Gets the maximum duration allowed in seconds.
    /// </summary>
    public int MaxDurationSeconds { get; init; }

    /// <summary>
    /// Gets the supported audio formats (e.g., "MP3, WAV, OGG").
    /// </summary>
    public string SupportedFormats { get; init; } = string.Empty;

    /// <summary>
    /// Gets the current sort order (e.g., "name-asc", "name-desc", "newest", "oldest").
    /// </summary>
    public string CurrentSort { get; init; } = "name-asc";

    /// <summary>
    /// Gets the display label for the current sort order.
    /// </summary>
    public string CurrentSortLabel => CurrentSort switch
    {
        "name-desc" => "Name (Z-A)",
        "newest" => "Newest First",
        "oldest" => "Oldest First",
        _ => "Name (A-Z)"
    };

    /// <summary>
    /// Creates a SoundboardIndexViewModel from service data.
    /// </summary>
    public static SoundboardIndexViewModel Create(
        ulong guildId,
        string guildName,
        string? guildIconUrl,
        IReadOnlyList<Sound> sounds,
        GuildAudioSettings settings,
        int playsToday,
        int playsYesterday,
        string currentSort = "name-asc")
    {
        // Map sounds to view models
        var soundViewModels = sounds.Select(SoundViewModel.FromEntity).ToList();

        // Calculate storage used
        var storageUsedBytes = sounds.Sum(s => s.FileSizeBytes);
        var storageLimitBytes = settings.MaxStorageBytes;

        // Calculate storage percentage (avoid division by zero)
        var storagePercentage = storageLimitBytes > 0
            ? Math.Min(100, (int)((storageUsedBytes * 100) / storageLimitBytes))
            : 0;

        // Find most played sound
        var topSound = sounds.OrderByDescending(s => s.PlayCount).FirstOrDefault();

        // Calculate comparison text and CSS class
        var (comparisonText, comparisonCssClass) = CalculateComparison(playsToday, playsYesterday);

        // Build statistics
        var stats = new SoundboardStatsViewModel
        {
            TotalSounds = sounds.Count,
            PlaysToday = playsToday,
            PlaysYesterday = playsYesterday,
            ComparisonText = comparisonText,
            ComparisonCssClass = comparisonCssClass,
            StorageUsedBytes = storageUsedBytes,
            StorageLimitBytes = storageLimitBytes,
            StorageUsedFormatted = FormatFileSize(storageUsedBytes),
            StorageLimitFormatted = FormatFileSize(storageLimitBytes),
            StoragePercentage = storagePercentage,
            TopSoundName = topSound?.Name,
            TopSoundPlays = topSound?.PlayCount ?? 0
        };

        return new SoundboardIndexViewModel
        {
            GuildId = guildId,
            GuildName = guildName,
            GuildIconUrl = guildIconUrl,
            Stats = stats,
            Sounds = soundViewModels,
            MaxSoundsPerGuild = settings.MaxSoundsPerGuild,
            MaxFileSizeBytes = settings.MaxFileSizeBytes,
            MaxDurationSeconds = settings.MaxDurationSeconds,
            SupportedFormats = "MP3, WAV, OGG", // Static for now, could be made configurable
            CurrentSort = currentSort
        };
    }

    /// <summary>
    /// Calculates comparison text and CSS class for play statistics.
    /// </summary>
    /// <param name="today">Number of plays today.</param>
    /// <param name="yesterday">Number of plays yesterday.</param>
    /// <returns>A tuple containing the comparison text and CSS class.</returns>
    private static (string text, string cssClass) CalculateComparison(int today, int yesterday)
    {
        // Handle case where yesterday had no plays
        if (yesterday == 0)
        {
            if (today > 0)
            {
                return ($"+{today} (no data yesterday)", "text-green-600");
            }
            else
            {
                return ("No data", "text-gray-500");
            }
        }

        // Calculate percentage change
        var percentageChange = ((today - yesterday) / (double)yesterday) * 100;

        if (percentageChange > 0)
        {
            return ($"+{percentageChange:F0}% vs yesterday", "text-green-600");
        }
        else if (percentageChange < 0)
        {
            return ($"{percentageChange:F0}% vs yesterday", "text-red-600");
        }
        else
        {
            return ("Same as yesterday", "text-gray-500");
        }
    }

    /// <summary>
    /// Formats a file size in bytes to a human-readable string.
    /// </summary>
    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";

        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";

        if (bytes < 1024 * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024.0):F1} MB";

        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
    }
}
