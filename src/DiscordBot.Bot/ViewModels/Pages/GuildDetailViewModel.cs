using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for displaying detailed guild information.
/// </summary>
public record GuildDetailViewModel
{
    /// <summary>
    /// Gets the guild's Discord snowflake ID.
    /// </summary>
    public ulong Id { get; init; }

    /// <summary>
    /// Gets the guild name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the member count.
    /// </summary>
    public int MemberCount { get; init; }

    /// <summary>
    /// Gets the guild icon URL.
    /// </summary>
    public string? IconUrl { get; init; }

    /// <summary>
    /// Gets whether the guild is active.
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Gets the date when the bot joined the guild.
    /// </summary>
    public DateTime JoinedAt { get; init; }

    /// <summary>
    /// Gets the custom command prefix for the guild.
    /// </summary>
    public string? Prefix { get; init; }

    /// <summary>
    /// Gets the guild settings as JSON.
    /// </summary>
    public string? Settings { get; init; }

    /// <summary>
    /// Gets the list of recent command logs for this guild.
    /// </summary>
    public IReadOnlyList<RecentCommandLogItem> RecentCommandLogs { get; init; } = Array.Empty<RecentCommandLogItem>();

    /// <summary>
    /// Creates a <see cref="GuildDetailViewModel"/> from a <see cref="GuildDto"/>.
    /// </summary>
    /// <param name="dto">The guild DTO to map from.</param>
    /// <param name="recentLogs">Optional collection of recent command logs for the guild.</param>
    /// <returns>A new <see cref="GuildDetailViewModel"/> instance.</returns>
    public static GuildDetailViewModel FromDto(GuildDto dto, IEnumerable<CommandLogDto>? recentLogs = null)
    {
        return new GuildDetailViewModel
        {
            Id = dto.Id,
            Name = dto.Name,
            MemberCount = dto.MemberCount ?? 0,
            IconUrl = dto.IconUrl,
            IsActive = dto.IsActive,
            JoinedAt = dto.JoinedAt,
            Prefix = dto.Prefix,
            Settings = dto.Settings,
            RecentCommandLogs = recentLogs?.Select(RecentCommandLogItem.FromDto).ToList() ?? (IReadOnlyList<RecentCommandLogItem>)Array.Empty<RecentCommandLogItem>()
        };
    }
}

/// <summary>
/// Represents a recent command log entry for guild detail display.
/// </summary>
public record RecentCommandLogItem
{
    /// <summary>
    /// Gets the unique identifier for the command log entry.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets the username of the user who executed the command.
    /// </summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// Gets the name of the command that was executed.
    /// </summary>
    public string CommandName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the timestamp when the command was executed.
    /// </summary>
    public DateTime ExecutedAt { get; init; }

    /// <summary>
    /// Gets the response time in milliseconds.
    /// </summary>
    public int ResponseTimeMs { get; init; }

    /// <summary>
    /// Gets whether the command executed successfully.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the error message if the command failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a <see cref="RecentCommandLogItem"/> from a <see cref="CommandLogDto"/>.
    /// </summary>
    /// <param name="dto">The command log DTO to map from.</param>
    /// <returns>A new <see cref="RecentCommandLogItem"/> instance.</returns>
    public static RecentCommandLogItem FromDto(CommandLogDto dto)
    {
        return new RecentCommandLogItem
        {
            Id = dto.Id,
            Username = dto.Username ?? "Unknown",
            CommandName = dto.CommandName,
            ExecutedAt = dto.ExecutedAt,
            ResponseTimeMs = dto.ResponseTimeMs,
            Success = dto.Success,
            ErrorMessage = dto.ErrorMessage
        };
    }
}
