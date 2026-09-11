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
    /// A tool that overruns it is abandoned and the model is told so; the loop continues.
    /// Default is 10000 (10 seconds).
    /// </summary>
    /// <remarks>
    /// Raised from 5000: that was tight for a documentation read plus a database round trip on a
    /// cold SQLite file, and the deadline was not actually enforced until it was.
    /// </remarks>
    public int ToolExecutionTimeoutMs { get; set; } = 10000;

    /// <summary>
    /// Gets or sets the ceiling, in characters, on a single tool result entering conversation
    /// history. A longer result is replaced by a truncation envelope telling the model it is
    /// reading a fragment. Default is 8000 (~2,000 tokens). 0 disables the cap.
    /// </summary>
    /// <remarks>
    /// A tool result is re-sent on every later iteration of the agentic loop, so one oversized
    /// read is paid for again on each following turn.
    /// </remarks>
    public int MaxToolResultChars { get; set; } = 8000;

    /// <summary>
    /// Gets or sets how many times one tool may be called with identical arguments in a single
    /// run before further identical calls are refused without executing the tool.
    /// Default is 3. 0 disables the guard.
    /// </summary>
    public int DuplicateToolCallLimit { get; set; } = 3;

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
