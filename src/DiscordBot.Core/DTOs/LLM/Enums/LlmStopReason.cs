namespace DiscordBot.Core.DTOs.LLM.Enums;

/// <summary>
/// Reasons why an LLM stopped generating a response.
/// </summary>
public enum LlmStopReason
{
    /// <summary>
    /// The model reached a natural conclusion (end of turn).
    /// </summary>
    EndTurn,

    /// <summary>
    /// The model wants to call a tool.
    /// </summary>
    ToolUse,

    /// <summary>
    /// The response hit the max tokens limit.
    /// </summary>
    MaxTokens,

    /// <summary>
    /// An error occurred during generation.
    /// </summary>
    Error
}
