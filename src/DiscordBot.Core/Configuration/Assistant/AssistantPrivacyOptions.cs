namespace DiscordBot.Core.Configuration.Assistant;

/// <summary>
/// Consent and audit logging configuration for the guild AI assistant.
/// Binds under "Assistant:Privacy" (flat legacy keys under "Assistant" remain supported).
/// </summary>
public class AssistantPrivacyOptions
{
    /// <summary>
    /// Gets or sets whether users must explicitly opt-in via /consent before using the assistant.
    /// Default is true (explicit consent required).
    /// </summary>
    /// <remarks>
    /// When true, users must run /consent and enable "assistant_usage" before asking questions.
    /// When false, mentioning the bot implies consent (simpler UX, but less privacy control).
    /// </remarks>
    public bool RequireExplicitConsent { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to log user questions and Claude responses for audit/debugging.
    /// Default is true.
    /// </summary>
    /// <remarks>
    /// When enabled, questions and responses are logged to the existing audit log system.
    /// Users are informed via /privacy command that questions are processed by Claude API.
    /// </remarks>
    public bool LogInteractions { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of days to retain assistant interaction logs.
    /// Aligns with message log retention policy.
    /// Default is 90 days.
    /// </summary>
    public int InteractionLogRetentionDays { get; set; } = 90;
}
