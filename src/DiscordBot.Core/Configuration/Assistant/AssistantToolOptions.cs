namespace DiscordBot.Core.Configuration.Assistant;

/// <summary>
/// Tool execution and prompt/documentation path configuration for the guild AI assistant.
/// Binds under "Assistant:Tools" (flat legacy keys under "Assistant" remain supported).
/// </summary>
public class AssistantToolOptions
{
    /// <summary>
    /// Gets or sets whether documentation tools are enabled.
    /// If false, Claude will only use the agent prompt without tool access.
    /// Default is true.
    /// </summary>
    public bool EnableDocumentationTools { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of tool calls Claude can make per question.
    /// Prevents infinite loops and controls API costs.
    /// Default is 5.
    /// </summary>
    public int MaxToolCallsPerQuestion { get; set; } = 5;

    /// <summary>
    /// Gets or sets the timeout for individual tool executions in milliseconds.
    /// Default is 5000 (5 seconds).
    /// </summary>
    public int ToolExecutionTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Gets or sets the path to the agent behavior/security prompt file.
    /// Supports placeholders: {GUILD_ID}, {BASE_URL}
    /// Default is "docs/agents/assistant-agent.md".
    /// </summary>
    public string AgentPromptPath { get; set; } = "docs/agents/assistant-agent.md";

    /// <summary>
    /// Gets or sets the base directory for documentation files.
    /// Used by documentation tools to locate feature docs.
    /// Default is "docs/articles".
    /// </summary>
    public string DocumentationBasePath { get; set; } = "docs/articles";

    /// <summary>
    /// Gets or sets the path to the README file for command lists.
    /// Default is "README.md".
    /// </summary>
    public string ReadmePath { get; set; } = "README.md";
}
