namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for the unified search results page.
/// Contains categorized search results from Guilds, Command Logs, and Users.
/// </summary>
public class SearchResultsViewModel
{
    /// <summary>
    /// Gets or sets the search term used for the query.
    /// </summary>
    public string SearchTerm { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the current user has permission to see user results.
    /// </summary>
    public bool CanViewUsers { get; set; }

    /// <summary>
    /// Gets or sets the guild search results.
    /// </summary>
    public IReadOnlyList<GuildSearchResultItem> GuildResults { get; set; } = Array.Empty<GuildSearchResultItem>();

    /// <summary>
    /// Gets or sets the total number of guilds matching the search (for "View all" links).
    /// </summary>
    public int TotalGuildResults { get; set; }

    /// <summary>
    /// Gets or sets the command log search results.
    /// </summary>
    public IReadOnlyList<CommandLogSearchResultItem> CommandLogResults { get; set; } = Array.Empty<CommandLogSearchResultItem>();

    /// <summary>
    /// Gets or sets the total number of command logs matching the search (for "View all" links).
    /// </summary>
    public int TotalCommandLogResults { get; set; }

    /// <summary>
    /// Gets or sets the user search results (only populated for Admin+ users).
    /// </summary>
    public IReadOnlyList<UserSearchResultItem> UserResults { get; set; } = Array.Empty<UserSearchResultItem>();

    /// <summary>
    /// Gets or sets the total number of users matching the search (for "View all" links).
    /// </summary>
    public int TotalUserResults { get; set; }

    /// <summary>
    /// Gets whether there are any search results across all categories.
    /// </summary>
    public bool HasResults => GuildResults.Any() || CommandLogResults.Any() || UserResults.Any();
}

/// <summary>
/// Search result item for a guild.
/// </summary>
public class GuildSearchResultItem
{
    /// <summary>
    /// Gets or sets the guild's Discord snowflake ID.
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    /// Gets or sets the guild name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the guild icon URL from live Discord data.
    /// </summary>
    public string? IconUrl { get; set; }

    /// <summary>
    /// Gets or sets the member count from live Discord data.
    /// </summary>
    public int? MemberCount { get; set; }

    /// <summary>
    /// Gets or sets whether the bot is currently connected to this guild.
    /// </summary>
    public bool IsActive { get; set; }
}

/// <summary>
/// Search result item for a command log entry.
/// </summary>
public class CommandLogSearchResultItem
{
    /// <summary>
    /// Gets or sets the unique identifier for the command log entry.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the command that was executed.
    /// </summary>
    public string CommandName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the command was executed.
    /// </summary>
    public DateTime ExecutedAt { get; set; }

    /// <summary>
    /// Gets the executed at timestamp in ISO 8601 format for client-side timezone conversion.
    /// </summary>
    public string ExecutedAtUtcIso => ExecutedAt.ToString("o");

    /// <summary>
    /// Gets or sets the guild name where the command was executed.
    /// </summary>
    public string? GuildName { get; set; }

    /// <summary>
    /// Gets or sets the user identifier (username or user ID).
    /// </summary>
    public string UserIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the command executed successfully.
    /// </summary>
    public bool Success { get; set; }
}

/// <summary>
/// Search result item for a user.
/// </summary>
public class UserSearchResultItem
{
    /// <summary>
    /// Gets or sets the user's ID.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's email address.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the user's highest role.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Discord avatar URL if Discord is linked.
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Gets or sets whether the user is currently active.
    /// </summary>
    public bool IsActive { get; set; }
}
