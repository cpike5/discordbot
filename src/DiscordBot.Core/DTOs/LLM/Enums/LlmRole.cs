namespace DiscordBot.Core.DTOs.LLM.Enums;

/// <summary>
/// Defines the role of a message in an LLM conversation.
/// </summary>
public enum LlmRole
{
    /// <summary>
    /// Message from the user (human).
    /// </summary>
    User,

    /// <summary>
    /// Message from the AI assistant.
    /// </summary>
    Assistant,

    /// <summary>
    /// System message that sets context and behavior.
    /// </summary>
    System
}
