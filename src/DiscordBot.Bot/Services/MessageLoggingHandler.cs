using Discord;
using Discord.WebSocket;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Handles Discord message events and logs them to the database for users who have granted consent.
/// </summary>
public class MessageLoggingHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MessageLoggingHandler> _logger;

    public MessageLoggingHandler(
        IServiceScopeFactory scopeFactory,
        ILogger<MessageLoggingHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Handles the MessageReceived event from DiscordSocketClient.
    /// Filters messages, checks consent, and logs to database.
    /// </summary>
    /// <param name="message">The received message from Discord.</param>
    public async Task HandleMessageReceivedAsync(SocketMessage message)
    {
        var adapter = new DiscordMessageAdapter(message);
        await HandleMessageAsync(adapter);
    }

    /// <summary>
    /// Handles a Discord message by filtering, checking consent, and logging to database.
    /// This method accepts an IDiscordMessage interface to enable testability.
    /// </summary>
    /// <param name="message">The message to process.</param>
    internal async Task HandleMessageAsync(IDiscordMessage message)
    {
        try
        {
            // Filter out bot messages
            if (message.IsAuthorBot)
            {
                _logger.LogTrace("Skipping bot message {MessageId} from {AuthorId}", message.Id, message.AuthorId);
                return;
            }

            // Filter out system messages - only process user messages
            if (!message.IsUserMessage)
            {
                _logger.LogTrace("Skipping system message {MessageId}", message.Id);
                return;
            }

            // Create scope to access scoped services from singleton
            using var scope = _scopeFactory.CreateScope();
            var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();

            // Check if message logging is globally enabled
            var isEnabled = await settingsService.GetSettingValueAsync<bool>("Features:MessageLoggingEnabled");
            if (!isEnabled)
            {
                _logger.LogTrace("Message logging is disabled globally, skipping message {MessageId}", message.Id);
                return;
            }

            _logger.LogDebug("Processing message {MessageId} from user {AuthorId} in channel {ChannelId}",
                message.Id, message.AuthorId, message.ChannelId);

            var consentService = scope.ServiceProvider.GetRequiredService<IConsentService>();
            var messageLogRepository = scope.ServiceProvider.GetRequiredService<IMessageLogRepository>();

            // Check if user has granted consent for message logging
            var hasConsent = await consentService.HasConsentAsync(
                message.AuthorId,
                ConsentType.MessageLogging);

            if (!hasConsent)
            {
                _logger.LogDebug("User {AuthorId} has not granted message logging consent, skipping message {MessageId}",
                    message.AuthorId, message.Id);
                return;
            }

            // Determine message source
            var source = message.IsDirectMessage
                ? Core.Enums.MessageSource.DirectMessage
                : Core.Enums.MessageSource.ServerChannel;

            // Create MessageLog entity
            var messageLog = new MessageLog
            {
                DiscordMessageId = message.Id,
                AuthorId = message.AuthorId,
                ChannelId = message.ChannelId,
                GuildId = message.GuildId,
                Source = source,
                Content = message.Content,
                Timestamp = message.Timestamp.UtcDateTime,
                LoggedAt = DateTime.UtcNow,
                HasAttachments = message.HasAttachments,
                HasEmbeds = message.HasEmbeds,
                ReplyToMessageId = message.ReplyToMessageId
            };

            // Save to database
            await messageLogRepository.AddAsync(messageLog);

            _logger.LogDebug("Successfully logged message {MessageId} from user {AuthorId} (source: {Source}, guild: {GuildId})",
                message.Id, message.AuthorId, source, message.GuildId);
        }
        catch (Exception ex)
        {
            // Log error but don't crash the bot
            _logger.LogError(ex,
                "Failed to log message {MessageId} from user {AuthorId} in channel {ChannelId}",
                message.Id, message.AuthorId, message.ChannelId);
        }
    }
}
