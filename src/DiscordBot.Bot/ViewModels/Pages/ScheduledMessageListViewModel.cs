using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for the scheduled messages list (index) page.
/// </summary>
public class ScheduledMessageListViewModel
{
    /// <summary>
    /// Gets or sets the guild's Discord snowflake ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the guild name for display.
    /// </summary>
    public string GuildName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the guild icon URL for display.
    /// </summary>
    public string? GuildIconUrl { get; set; }

    /// <summary>
    /// Gets or sets the list of scheduled messages with display information.
    /// </summary>
    public List<ScheduledMessageListItem> Messages { get; set; } = new();

    /// <summary>
    /// Gets or sets the current page number (1-based).
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Gets or sets the page size (number of items per page).
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Gets or sets the total count of scheduled messages for this guild.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Gets the total number of pages.
    /// </summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

    /// <summary>
    /// Gets whether there is a previous page.
    /// </summary>
    public bool HasPreviousPage => Page > 1;

    /// <summary>
    /// Gets whether there is a next page.
    /// </summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>
    /// Gets the 1-based index of the first item on the current page.
    /// </summary>
    public int FirstItemIndex => TotalCount > 0 ? ((Page - 1) * PageSize) + 1 : 0;

    /// <summary>
    /// Gets the 1-based index of the last item on the current page.
    /// </summary>
    public int LastItemIndex => Math.Min(Page * PageSize, TotalCount);

    /// <summary>
    /// Creates a ScheduledMessageListViewModel from service data.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="guildName">The guild name.</param>
    /// <param name="guildIconUrl">The guild icon URL.</param>
    /// <param name="messages">The list of scheduled message DTOs.</param>
    /// <param name="channelNameResolver">Function to resolve channel ID to channel name.</param>
    /// <param name="page">The current page number (1-based).</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="totalCount">The total count of scheduled messages.</param>
    /// <returns>A new ScheduledMessageListViewModel instance.</returns>
    public static ScheduledMessageListViewModel Create(
        ulong guildId,
        string guildName,
        string? guildIconUrl,
        IEnumerable<ScheduledMessageDto> messages,
        Func<ulong, string> channelNameResolver,
        int page,
        int pageSize,
        int totalCount)
    {
        return new ScheduledMessageListViewModel
        {
            GuildId = guildId,
            GuildName = guildName,
            GuildIconUrl = guildIconUrl,
            Messages = messages.Select(m => ScheduledMessageListItem.FromDto(m, channelNameResolver)).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}

/// <summary>
/// Represents a single scheduled message in the list view with display information.
/// </summary>
public class ScheduledMessageListItem
{
    /// <summary>
    /// Gets or sets the unique identifier for this scheduled message.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the Discord channel snowflake ID.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    /// Gets or sets the channel name resolved from Discord.
    /// </summary>
    public string ChannelName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the title of the scheduled message.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the content of the scheduled message.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the schedule frequency.
    /// </summary>
    public ScheduleFrequency Frequency { get; set; }

    /// <summary>
    /// Gets or sets the cron expression (for custom schedules).
    /// </summary>
    public string? CronExpression { get; set; }

    /// <summary>
    /// Gets or sets whether this scheduled message is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this message was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets the created at UTC time in ISO format for JavaScript conversion.
    /// </summary>
    public string CreatedAtUtcIso => DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc).ToString("o");

    /// <summary>
    /// Gets or sets the timestamp when this message was last executed.
    /// </summary>
    public DateTime? LastExecutedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this message will next be executed.
    /// </summary>
    public DateTime? NextExecutionAt { get; set; }

    /// <summary>
    /// Gets the status display text (Active, Paused, or Expired).
    /// </summary>
    public string StatusDisplay
    {
        get
        {
            if (!IsEnabled)
                return "Paused";

            if (Frequency == ScheduleFrequency.Once && LastExecutedAt.HasValue)
                return "Expired";

            if (Frequency == ScheduleFrequency.Once && NextExecutionAt.HasValue && NextExecutionAt.Value < DateTime.UtcNow)
                return "Expired";

            return "Active";
        }
    }

    /// <summary>
    /// Gets the badge variant for the status.
    /// </summary>
    public string StatusBadgeVariant
    {
        get
        {
            return StatusDisplay switch
            {
                "Active" => "Success",
                "Paused" => "Warning",
                "Expired" => "Default",
                _ => "Default"
            };
        }
    }

    /// <summary>
    /// Gets the message preview (truncated content or title).
    /// </summary>
    public string MessagePreview
    {
        get
        {
            var preview = !string.IsNullOrWhiteSpace(Title) ? Title : Content;
            return preview.Length > 50 ? preview[..50] + "..." : preview;
        }
    }

    /// <summary>
    /// Gets the schedule description for display.
    /// </summary>
    public string ScheduleDescription
    {
        get
        {
            return Frequency switch
            {
                ScheduleFrequency.Once => "One-time",
                ScheduleFrequency.Hourly => "Every hour",
                ScheduleFrequency.Daily => "Daily",
                ScheduleFrequency.Weekly => "Weekly",
                ScheduleFrequency.Monthly => "Monthly",
                ScheduleFrequency.Custom => "Custom schedule",
                _ => "Unknown"
            };
        }
    }

    /// <summary>
    /// Gets the next run UTC time in ISO format for JavaScript conversion.
    /// Returns the ISO 8601 format string for use with data-utc attribute.
    /// </summary>
    public string? NextRunUtcIso
    {
        get
        {
            if (!IsEnabled || !NextExecutionAt.HasValue)
                return null;

            if (Frequency == ScheduleFrequency.Once && LastExecutedAt.HasValue)
                return null;

            // Ensure DateTime is marked as UTC so ToString("o") includes the Z suffix
            // Without this, JavaScript treats the datetime as local time instead of UTC
            var utcTime = DateTime.SpecifyKind(NextExecutionAt.Value, DateTimeKind.Utc);
            return utcTime.ToString("o");
        }
    }

    /// <summary>
    /// Gets the next run display text (fallback for non-JavaScript scenarios).
    /// This will be replaced by JavaScript conversion using the data-utc attribute.
    /// </summary>
    public string NextRunDisplay
    {
        get
        {
            if (!IsEnabled || !NextExecutionAt.HasValue)
                return "--";

            if (Frequency == ScheduleFrequency.Once && LastExecutedAt.HasValue)
                return "--";

            // Keep UTC time here - JavaScript will convert to local
            return NextExecutionAt.Value.ToString("MMM d, yyyy h:mm tt") + " UTC";
        }
    }

    /// <summary>
    /// Creates a ScheduledMessageListItem from a ScheduledMessageDto.
    /// </summary>
    /// <param name="dto">The scheduled message DTO.</param>
    /// <param name="channelNameResolver">Function to resolve channel ID to channel name.</param>
    /// <returns>A new ScheduledMessageListItem instance.</returns>
    public static ScheduledMessageListItem FromDto(ScheduledMessageDto dto, Func<ulong, string> channelNameResolver)
    {
        return new ScheduledMessageListItem
        {
            Id = dto.Id,
            ChannelId = dto.ChannelId,
            ChannelName = channelNameResolver(dto.ChannelId),
            Title = dto.Title,
            Content = dto.Content,
            Frequency = dto.Frequency,
            CronExpression = dto.CronExpression,
            IsEnabled = dto.IsEnabled,
            CreatedAt = dto.CreatedAt,
            LastExecutedAt = dto.LastExecutedAt,
            NextExecutionAt = dto.NextExecutionAt
        };
    }
}
