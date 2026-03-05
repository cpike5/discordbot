using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for DM-based AI assistant operations.
/// Handles owner detection, conversation history, and LLM interactions.
/// </summary>
public interface IDmAssistantService
{
    /// <summary>
    /// Processes a DM message and returns an AI response.
    /// Owner users get full AI responses; non-owners receive a placeholder message.
    /// </summary>
    /// <param name="userId">The Discord user ID of the message sender.</param>
    /// <param name="message">The message content.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The assistant response including success status, response text, and metrics.</returns>
    Task<DmAssistantResponse> ProcessMessageAsync(ulong userId, string message, CancellationToken ct = default);
}
