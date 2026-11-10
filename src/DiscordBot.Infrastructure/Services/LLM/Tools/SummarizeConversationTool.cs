using System.Text.Json;
using cpike.Agent;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Services.LLM.Tooling;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Services.LLM.Tools;

/// <summary>
/// <c>summarize_conversation</c> — message count and date range of the caller's DM history.
/// </summary>
public sealed class SummarizeConversationTool : IAgentTool
{
    private static readonly AgentToolDefinition ToolDefinition = AgentTools.Define(
        "summarize_conversation",
        "Returns metadata about the current conversation including message count and date range. The conversation content is already in your context - use this tool to get statistics, then generate a summary yourself from the messages you can see.",
        JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {},
                "required": []
            }
            """).RootElement.Clone());

    private readonly IDmConversationMessageRepository _messageRepository;
    private readonly DiscordRunContext _run;
    private readonly ILogger<SummarizeConversationTool> _logger;

    /// <summary>Creates the tool.</summary>
    public SummarizeConversationTool(
        IDmConversationMessageRepository messageRepository,
        DiscordRunContext run,
        ILogger<SummarizeConversationTool> logger)
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
            // Fetch with a reasonable limit — only need timestamps, not content
            var messages = await _messageRepository.GetRecentByUserAsync(
                _run.UserId, 1000, cancellationToken);
            var messageList = messages.ToList();

            if (messageList.Count == 0)
            {
                return ToolResults.Json(new
                {
                    message_count = 0,
                    message = "No conversation history found."
                });
            }

            return ToolResults.Json(new
            {
                message_count = messageList.Count,
                oldest_message_date = messageList.First().Timestamp.ToString("o"),
                newest_message_date = messageList.Last().Timestamp.ToString("o")
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error executing conversation tool {ToolName}", "summarize_conversation");
            return ToolResults.Error($"Error executing tool: {ex.Message}");
        }
    }
}
