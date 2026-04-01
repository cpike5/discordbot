namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for the Mogwai feature — Claude Code CLI integration for coding tasks.
/// </summary>
public class MogwaiOptions
{
    public const string SectionName = "Mogwai";

    /// <summary>
    /// Gets or sets whether the Mogwai feature is enabled.
    /// Default is false (disabled until explicitly enabled).
    /// </summary>
    public bool Enabled { get; set; } = false;

    #region CLI Configuration

    /// <summary>
    /// Gets or sets the path to the Claude CLI binary.
    /// Default is "claude".
    /// </summary>
    public string ClaudeCliPath { get; set; } = "claude";

    /// <summary>
    /// Gets or sets the working directory for Claude Code sessions.
    /// Default is ".".
    /// </summary>
    public string WorkingDirectory { get; set; } = ".";

    /// <summary>
    /// Gets or sets the comma-separated list of allowed tools for --allowedTools.
    /// Default is "Bash,Read,Glob,Grep,Write,Edit".
    /// </summary>
    public string AllowedTools { get; set; } = "Bash,Read,Glob,Grep,Write,Edit";

    #endregion

    #region Limits

    /// <summary>
    /// Gets or sets the maximum budget in USD per invocation (--max-budget-usd).
    /// Default is 5.00.
    /// </summary>
    public decimal MaxBudgetUsd { get; set; } = 5.00m;

    /// <summary>
    /// Gets or sets the maximum number of turns per invocation (--max-turns).
    /// Default is 10.
    /// </summary>
    public int MaxTurns { get; set; } = 10;

    /// <summary>
    /// Gets or sets the process timeout in seconds.
    /// Default is 300 (5 minutes).
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Gets or sets the maximum CLI output length in characters before truncation.
    /// Default is 50000.
    /// </summary>
    public int MaxOutputLength { get; set; } = 50000;

    #endregion

    #region Behavior

    /// <summary>
    /// Gets or sets an optional system prompt appended via --append-system-prompt.
    /// Default is null (no extra instructions).
    /// </summary>
    public string? AppendSystemPrompt { get; set; } = null;

    /// <summary>
    /// Gets or sets whether to use the --bare flag for machine-readable output.
    /// Default is true.
    /// </summary>
    public bool UseBareMode { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to use --dangerously-skip-permissions.
    /// Default is false.
    /// </summary>
    public bool SkipPermissions { get; set; } = false;

    #endregion
}
