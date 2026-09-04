namespace DiscordBot.Core.Configuration.Assistant;

/// <summary>
/// Message length constraints and error/retry behavior for the guild AI assistant.
/// Binds under "Assistant:Messages" (flat legacy keys under "Assistant" remain supported).
/// </summary>
public class AssistantMessageOptions
{
    /// <summary>
    /// Gets or sets the maximum length of a user's question in characters.
    /// Questions longer than this will be rejected.
    /// Default is 500 characters.
    /// </summary>
    public int MaxQuestionLength { get; set; } = 500;

    /// <summary>
    /// Gets or sets the maximum length of Claude's response in characters.
    /// Responses exceeding this will be truncated with a suffix.
    /// Default is 1800 (leaves buffer for Discord's 2000 char limit).
    /// </summary>
    public int MaxResponseLength { get; set; } = 1800;

    /// <summary>
    /// Gets or sets the suffix appended when responses are truncated.
    /// Default is "\n\n... *(response truncated)*".
    /// </summary>
    public string TruncationSuffix { get; set; } = "\n\n... *(response truncated)*";

    /// <summary>
    /// Gets or sets the friendly error message shown to users when the API fails.
    /// Default is "Oops, I'm having trouble thinking right now. Please try again in a moment."
    /// </summary>
    public string ErrorMessage { get; set; } = "Oops, I'm having trouble thinking right now. Please try again in a moment.";

    /// <summary>
    /// Gets or sets the maximum number of retry attempts for failed API calls.
    /// Default is 2 (1 initial attempt + 2 retries = 3 total attempts).
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 2;

    /// <summary>
    /// Gets or sets the delay between retry attempts in milliseconds.
    /// Default is 1000 (1 second).
    /// </summary>
    public int RetryDelayMs { get; set; } = 1000;
}
