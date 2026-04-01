using Discord.WebSocket;
using DiscordBot.Bot.Services.NotX;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Handlers;

/// <summary>
/// Handles Discord <c>MessageReceived</c> events for the not-X feature.
/// Scans guild messages for X/Twitter URLs and delegates to <see cref="INotXService"/>
/// to fetch and post tweet embeds. Registered as a singleton and creates a DI scope
/// per invocation to access scoped services.
/// </summary>
public class NotXMessageHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotXMessageHandler> _logger;

    public NotXMessageHandler(
        IServiceScopeFactory scopeFactory,
        ILogger<NotXMessageHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Handles a Discord MessageReceived event. Extracts tweet URLs from guild messages
    /// and triggers embed posting via <see cref="INotXService.ProcessTweetAsync"/>.
    /// Exceptions are swallowed to prevent crashing the Discord.NET event pipeline.
    /// </summary>
    /// <param name="message">The raw socket message from the gateway.</param>
    public async Task HandleMessageReceivedAsync(SocketMessage message)
    {
        try
        {
            // Only process real user messages
            if (message is not SocketUserMessage userMessage)
                return;

            if (userMessage.Author.IsBot)
                return;

            // Skip DMs — feature is guild-only
            if (userMessage.Channel is not SocketGuildChannel guildChannel)
                return;

            // Fast path: no URLs to scan
            var content = userMessage.Content;
            if (string.IsNullOrEmpty(content))
                return;

            var urlMatches = TweetUrlExtractor.Extract(content);
            if (urlMatches.Count == 0)
                return;

            var guildId = guildChannel.Guild.Id;
            var channelId = userMessage.Channel.Id;
            var messageId = userMessage.Id;

            _logger.LogDebug(
                "not-X: found {UrlCount} tweet URL(s) in message {MessageId} from guild {GuildId} channel {ChannelId}",
                urlMatches.Count, messageId, guildId, channelId);

            using var scope = _scopeFactory.CreateScope();
            var notXService = scope.ServiceProvider.GetRequiredService<INotXService>();

            // Process sequentially to avoid Discord rate-limit pressure
            foreach (var match in urlMatches)
            {
                await notXService.ProcessTweetAsync(
                    guildId,
                    channelId,
                    messageId,
                    match.FullUrl);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "not-X: unhandled exception processing message {MessageId}; skipping",
                message.Id);
        }
    }
}
