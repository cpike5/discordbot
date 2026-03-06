using System.Text.Json;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Infrastructure.Services.LLM.Implementations;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Services.LLM.Providers;

/// <summary>
/// Tool provider for DM conversation management.
/// Provides tools for clearing and summarizing conversation history.
/// </summary>
public class ConversationToolProvider : IDmToolProvider
{
    private readonly ILogger<ConversationToolProvider> _logger;
    private readonly IDmConversationMessageRepository _messageRepository;

    /// <inheritdoc />
    public string Name => "Conversation";

    /// <inheritdoc />
    public string Description => "Manage DM conversation history - clear or summarize conversations";

    public ConversationToolProvider(
        ILogger<ConversationToolProvider> logger,
        IDmConversationMessageRepository messageRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _messageRepository = messageRepository ?? throw new ArgumentNullException(nameof(messageRepository));
    }

    /// <inheritdoc />
    public IEnumerable<LlmToolDefinition> GetTools()
    {
        return ConversationTools.GetAllTools();
    }

    /// <inheritdoc />
    public async Task<ToolExecutionResult> ExecuteToolAsync(
        string toolName,
        JsonElement input,
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Executing conversation tool {ToolName}", toolName);

        try
        {
            return toolName.ToLowerInvariant() switch
            {
                ConversationTools.ClearConversation => await ExecuteClearConversationAsync(context, cancellationToken),
                ConversationTools.SummarizeConversation => await ExecuteSummarizeConversationAsync(context, cancellationToken),
                _ => throw new NotSupportedException($"Tool '{toolName}' is not supported by this provider")
            };
        }
        catch (NotSupportedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing conversation tool {ToolName}", toolName);
            return ToolExecutionResult.CreateError($"Error executing tool: {ex.Message}");
        }
    }

    private async Task<ToolExecutionResult> ExecuteClearConversationAsync(
        ToolContext context, CancellationToken cancellationToken)
    {
        // Get a reasonable count before clearing (max conversation window is typically ~20)
        var messages = await _messageRepository.GetRecentByUserAsync(
            context.UserId, 1000, cancellationToken);
        var count = messages.Count();

        // Delete all messages by keeping 0
        await _messageRepository.DeleteOldestByUserAsync(context.UserId, 0, cancellationToken);

        _logger.LogDebug("Cleared {Count} conversation messages for user {UserId}", count, context.UserId);

        return CreateJsonResult(new
        {
            success = true,
            messages_cleared = count,
            message = $"Conversation history cleared. {count} message(s) removed."
        });
    }

    private async Task<ToolExecutionResult> ExecuteSummarizeConversationAsync(
        ToolContext context, CancellationToken cancellationToken)
    {
        // Fetch with a reasonable limit — only need timestamps, not content
        var messages = await _messageRepository.GetRecentByUserAsync(
            context.UserId, 1000, cancellationToken);
        var messageList = messages.ToList();

        if (messageList.Count == 0)
        {
            return CreateJsonResult(new
            {
                message_count = 0,
                message = "No conversation history found."
            });
        }

        return CreateJsonResult(new
        {
            message_count = messageList.Count,
            oldest_message_date = messageList.First().Timestamp.ToString("o"),
            newest_message_date = messageList.Last().Timestamp.ToString("o")
        });
    }

    private static ToolExecutionResult CreateJsonResult(object data)
    {
        var jsonString = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        });
        var jsonElement = JsonDocument.Parse(jsonString).RootElement.Clone();
        return ToolExecutionResult.CreateSuccess(jsonElement);
    }
}
