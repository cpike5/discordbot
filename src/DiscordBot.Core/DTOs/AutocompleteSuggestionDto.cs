namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for autocomplete suggestions.
/// </summary>
public class AutocompleteSuggestionDto
{
    /// <summary>
    /// Gets or sets the unique identifier for the suggestion.
    /// </summary>
    /// <remarks>
    /// For users, this is the Discord user ID.
    /// For guilds, this is the Discord guild ID.
    /// For commands, this is the full command name.
    /// </remarks>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display text shown to the user.
    /// </summary>
    /// <remarks>
    /// For users, this is the username.
    /// For guilds, this is the guild name.
    /// For commands, this is the full command name with description.
    /// </remarks>
    public string DisplayText { get; set; } = string.Empty;
}

/// <summary>
/// Data transfer object for channel autocomplete suggestions with additional channel metadata.
/// </summary>
public class ChannelSuggestionDto : AutocompleteSuggestionDto
{
    /// <summary>
    /// Gets or sets the channel type.
    /// </summary>
    /// <remarks>
    /// Examples: Text, Voice, Category, News, Thread
    /// </remarks>
    public string ChannelType { get; set; } = string.Empty;
}
