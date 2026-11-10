using System.Text.Json;
using cpike.Agent;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Services.LLM.Tooling;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Services.LLM.Tools;

/// <summary>
/// <c>clear_conversation</c> — wipes the caller's DM conversation history.
/// </summary>
[cpike.Agent.Tools.NotDelegable]
public sealed class ClearConversationTool : IAgentTool
{
    private static readonly AgentToolDefinition ToolDefinition = AgentTools.Define(
        "clear_conversation",
        "Clears the entire DM conversation history for the current user. This removes all stored messages and resets the conversation context. Use this when the user explicitly asks to start fresh or clear their history. This action cannot be undone.",
        JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {},
                "required": []
            }
            """).RootElement.Clone(),
        requiresEdit: true);

    private readonly IDmConversationMessageRepository _messageRepository;
    private readonly DiscordRunContext _run;
    private readonly ILogger<ClearConversationTool> _logger;

    /// <summary>Creates the tool.</summary>
    public ClearConversationTool(
        IDmConversationMessageRepository messageRepository,
        DiscordRunContext run,
        ILogger<ClearConversationTool> logger)
    {
        _messageRepository = messageRepository ?? throw new ArgumentNullException(nameof(messageRepository));
        _run = run ?? throw new ArgumentNullException(nameof(run));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public AgentToolDefinition Definition => ToolDefinition;

    /// <inheritdoc />
    public async Task<string> InvokeAsync(
        IReadOnlyDictionary<string, JsonElement> arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get a reasonable count before clearing (max conversation window is typically ~20)
            var messages = await _messageRepository.GetRecentByUserAsync(
                _run.UserId, 1000, cancellationToken);
            var count = messages.Count();

            // Delete all messages by keeping 0
            await _messageRepository.DeleteOldestByUserAsync(_run.UserId, 0, cancellationToken);

            // The pipeline reads this so it does not re-save the turn that just cleared the history.
            _run.ConversationCleared = true;

            _logger.LogDebug("Cleared {Count} conversation messages for user {UserId}", count, _run.UserId);

            return ToolResults.Json(new
            {
                success = true,
                messages_cleared = count,
                message = $"Conversation history cleared. {count} message(s) removed."
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error executing conversation tool {ToolName}", "clear_conversation");
            return ToolResults.Error($"Error executing tool: {ex.Message}");
        }
    }
}
