namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for the /feature-request command and its associated services.
/// </summary>
public class FeatureRequestsOptions
{
    /// <summary>
    /// The configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "FeatureRequests";

    /// <summary>
    /// Whether the /feature-request command is enabled globally.
    /// Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Minimum character length for a valid feature request description.
    /// Default: 20
    /// </summary>
    public int MinDescriptionLength { get; set; } = 20;

    /// <summary>
    /// Maximum character length for a feature request description.
    /// Default: 500
    /// </summary>
    public int MaxDescriptionLength { get; set; } = 500;

    /// <summary>
    /// Character count at or above which the description is considered detailed enough
    /// to bypass the multi-step conversation flow and submit directly.
    /// Default: 100
    /// </summary>
    public int DirectSubmitThreshold { get; set; } = 100;

    /// <summary>
    /// Number of minutes before an in-progress DM conversation expires.
    /// Default: 30
    /// </summary>
    public int ConversationTimeoutMinutes { get; set; } = 30;

    /// <summary>
    /// Model to use for the AI-powered requirements gathering conversation.
    /// Default: claude-sonnet-4-20250514
    /// </summary>
    public string RequirementsGatheringModel { get; set; } = "claude-sonnet-4-20250514";

    /// <summary>
    /// Maximum number of conversation turns (user messages) before forcing the session to end.
    /// Default: 10
    /// </summary>
    public int MaxConversationTurns { get; set; } = 10;

    /// <summary>
    /// Regex patterns used to detect prompt-injection attempts in user input.
    /// Checked case-insensitively against submitted text.
    /// </summary>
    public string[] InjectionPatterns { get; set; } =
    [
        "ignore previous instructions",
        "you are now",
        "system:",
        "\\[INST\\]",
        "new instructions:",
        "</s>",
        "[/INST]"
    ];
}
