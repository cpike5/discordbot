using Discord;
using Discord.WebSocket;
using DiscordBot.Bot.Services.FeatureRequests;

namespace DiscordBot.Bot.Handlers;

/// <summary>
/// Intercepts DM messages from users who have an active feature-request conversation session.
/// Must be subscribed to <c>DiscordSocketClient.MessageReceived</c> BEFORE
/// <see cref="DmAssistantMessageHandler"/> so that active sessions are handled exclusively here.
/// </summary>
public class FeatureRequestDmHandler
{
    private readonly FeatureRequestConversationService _conversationService;
    private readonly ILogger<FeatureRequestDmHandler> _logger;

    public FeatureRequestDmHandler(
        FeatureRequestConversationService conversationService,
        ILogger<FeatureRequestDmHandler> logger)
    {
        _conversationService = conversationService;
        _logger = logger;
    }

    /// <summary>
    /// Handles the MessageReceived event from DiscordSocketClient.
    /// Only processes DM messages from users with an active feature-request session.
    /// </summary>
    public async Task HandleMessageReceivedAsync(SocketMessage message)
    {
        if (message is not SocketUserMessage)
            return;

        if (message.Author.IsBot)
            return;

        if (message.Channel is not IDMChannel)
            return;

        var userId = message.Author.Id;

        // Only handle if there's an active feature-request session
        if (!_conversationService.TryGetSessionCorrelationId(userId, out _))
            return;

        _logger.LogDebug(
            "FeatureRequestDmHandler processing DM from user {UserId}", userId);

        await _conversationService.HandleAnswerAsync(message);
    }
}
