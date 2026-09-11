using DiscordBot.Agents.Abstractions;

namespace DiscordBot.Agents.Contracts;

/// <summary>
/// Context for running an agent, including tools and configuration.
/// </summary>
public class AgentContext
{
    /// <summary>
    /// The system prompt defining agent behavior.
    /// </summary>
    public string SystemPrompt { get; set; } = string.Empty;

    /// <summary>
    /// Tool registry providing access to enabled tools.
    /// </summary>
    public IToolRegistry? ToolRegistry { get; set; }

    /// <summary>
    /// Execution context with user/guild/channel information.
    /// </summary>
    public ToolContext ExecutionContext { get; set; } = new();

    /// <summary>
    /// The model identifier to use for this agent run.
    /// If null, falls back to provider default.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Maximum tokens to generate.
    /// </summary>
    public int MaxTokens { get; set; } = 2048;

    /// <summary>
    /// Temperature for generation.
    /// </summary>
    public double Temperature { get; set; } = 0.7;

    /// <summary>
    /// Maximum number of tool call iterations allowed.
    /// </summary>
    public int MaxToolCallIterations { get; set; } = 10;

    /// <summary>
    /// Hard ceiling on the characters of a single tool result entering conversation history.
    /// A longer result is replaced by a truncation envelope so the model knows it is reading a
    /// fragment. Default 8000 (~2,000 tokens). 0 disables the cap.
    /// </summary>
    /// <remarks>
    /// A tool result is re-sent on every subsequent iteration of the loop, so one oversized read
    /// is paid for again on every following turn. This is the engine's backstop: a tool that
    /// returns too much is capped here whether or not it knows how to limit itself.
    /// </remarks>
    public int MaxToolResultChars { get; set; } = 8000;

    /// <summary>
    /// Deadline for a single tool execution, in milliseconds. On expiry the tool's result is
    /// replaced by a directive timeout message and the loop continues. Default 10000. 0 disables
    /// the deadline.
    /// </summary>
    /// <remarks>
    /// Only the tool's own deadline is laundered into a result: a cancellation coming from the
    /// caller still propagates out of the run as cancellation.
    /// </remarks>
    public int ToolExecutionTimeoutMs { get; set; } = 10000;

    /// <summary>
    /// How many times one tool may be called with identical arguments in a single run before
    /// further identical calls are refused with a directive result instead of being executed.
    /// Default 3. 0 disables the guard.
    /// </summary>
    public int DuplicateToolCallLimit { get; set; } = 3;

    /// <summary>
    /// Optional pre-existing conversation history.
    /// When set, the agent runner initializes from this history + appends the user message,
    /// instead of starting with only the user message. Used by the DM assistant to carry
    /// multi-turn conversation context through the agentic loop.
    /// Null = default behavior (guild assistant unaffected).
    /// </summary>
    public List<LlmMessage>? ConversationHistory { get; set; }

    /// <summary>
    /// A free-form label for what kind of run this is, carried purely for correlation in logs and
    /// telemetry. The engine never switches on it; the hosting application decides the vocabulary.
    /// </summary>
    public string? RunKind { get; set; }
}
