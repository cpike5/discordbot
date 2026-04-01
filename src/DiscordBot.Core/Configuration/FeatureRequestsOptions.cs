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
    /// Doc generation sub-process configuration.
    /// </summary>
    public DocGenOptions DocGen { get; set; } = new();

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

    /// <summary>
    /// Nested configuration for the automated documentation generation subprocess.
    /// </summary>
    public class DocGenOptions
    {
        /// <summary>
        /// Whether the doc generation background worker is enabled.
        /// When false, requests are accepted but no documentation is auto-generated.
        /// Default: true
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Path or executable name of the Claude CLI binary.
        /// Defaults to "claude" (expected to be on PATH).
        /// </summary>
        public string ClaudeCodeBinaryPath { get; set; } = "claude";

        /// <summary>
        /// Maximum minutes to wait for the claude subprocess to complete.
        /// Default: 5
        /// </summary>
        public int TimeoutMinutes { get; set; } = 5;

        /// <summary>
        /// Git branch that feature-proposal branches are created from.
        /// Default: "main"
        /// </summary>
        public string BaseBranch { get; set; } = "main";

        /// <summary>
        /// Prefix applied to generated branch names.
        /// Default: "feature-proposal/"
        /// </summary>
        public string BranchPrefix { get; set; } = "feature-proposal/";

        /// <summary>
        /// Repository-relative path where generated documentation directories are created.
        /// Default: "docs/feature-proposals/"
        /// </summary>
        public string DocsBasePath { get; set; } = "docs/feature-proposals/";
    }
}
