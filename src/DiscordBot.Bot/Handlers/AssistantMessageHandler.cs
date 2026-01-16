using Discord;
using Discord.WebSocket;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Handlers;

/// <summary>
/// Handles Discord message events to detect bot mentions and process AI assistant requests.
/// Acts as a thin wrapper around the AssistantService to bridge Discord.NET events.
/// </summary>
public class AssistantMessageHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DiscordSocketClient _client;
    private readonly AssistantOptions _options;
    private readonly ILogger<AssistantMessageHandler> _logger;

    public AssistantMessageHandler(
        IServiceScopeFactory scopeFactory,
        DiscordSocketClient client,
        IOptions<AssistantOptions> options,
        ILogger<AssistantMessageHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Handles the MessageReceived event from DiscordSocketClient.
    /// Detects bot mentions and delegates to the AssistantService for processing.
    /// </summary>
    /// <param name="message">The received message.</param>
    public async Task HandleMessageReceivedAsync(SocketMessage message)
    {
        // Ignore bot messages
        if (message.Author.IsBot) return;

        // Ignore DMs (guild-only feature)
        if (message.Channel is not SocketGuildChannel guildChannel) return;

        // Check if bot is mentioned
        if (!message.MentionedUsers.Any(u => u.Id == _client.CurrentUser.Id)) return;

        // Check if globally enabled
        if (!_options.GloballyEnabled)
        {
            _logger.LogDebug("Assistant feature is globally disabled, ignoring mention in guild {GuildId}",
                guildChannel.Guild.Id);
            return;
        }

        var guildId = guildChannel.Guild.Id;
        var channelId = message.Channel.Id;
        var userId = message.Author.Id;
        var messageId = message.Id;

        using var activity = BotActivitySource.StartEventActivity(
            "assistant.message.process",
            guildId: guildId,
            userId: userId);

        try
        {
            // Extract question (remove bot mention)
            var question = ExtractQuestion(message.Content);

            if (string.IsNullOrWhiteSpace(question))
            {
                _logger.LogDebug("Empty question after removing mention in message {MessageId}", messageId);
                return;
            }

            _logger.LogDebug("Processing assistant request from user {UserId} in guild {GuildId}: {Question}",
                userId, guildId, question.Length > 100 ? question[..100] + "..." : question);

            // Create scope to access scoped services from singleton
            using var scope = _scopeFactory.CreateScope();
            var assistantService = scope.ServiceProvider.GetRequiredService<IAssistantService>();
            var consentRepository = scope.ServiceProvider.GetRequiredService<IUserConsentRepository>();

            // Check if enabled for guild
            if (!await assistantService.IsEnabledForGuildAsync(guildId))
            {
                _logger.LogDebug("Assistant is disabled for guild {GuildId}", guildId);
                activity?.SetTag("assistant.guild_enabled", false);
                BotActivitySource.SetSuccess(activity);
                return;
            }

            activity?.SetTag("assistant.guild_enabled", true);

            // Check if allowed in channel
            if (!await assistantService.IsAllowedInChannelAsync(guildId, channelId))
            {
                _logger.LogDebug("Assistant is not allowed in channel {ChannelId} of guild {GuildId}",
                    channelId, guildId);
                activity?.SetTag("assistant.channel_allowed", false);
                BotActivitySource.SetSuccess(activity);
                return;
            }

            activity?.SetTag("assistant.channel_allowed", true);

            // Check user consent if required
            if (_options.RequireExplicitConsent)
            {
                var hasConsent = await consentRepository.GetActiveConsentAsync(userId, ConsentType.AssistantUsage);
                if (hasConsent == null)
                {
                    _logger.LogDebug("User {UserId} has not granted AssistantUsage consent", userId);
                    activity?.SetTag("assistant.consent_granted", false);

                    // Send consent required message
                    await SendConsentRequiredMessageAsync(message);
                    BotActivitySource.SetSuccess(activity);
                    return;
                }
            }

            activity?.SetTag("assistant.consent_granted", true);

            // Check rate limit
            var rateLimitCheck = await assistantService.CheckRateLimitAsync(guildId, userId);
            if (!rateLimitCheck.IsAllowed)
            {
                _logger.LogDebug("User {UserId} is rate limited in guild {GuildId}: {Message}",
                    userId, guildId, rateLimitCheck.Message);
                activity?.SetTag("assistant.rate_limited", true);

                await message.Channel.SendMessageAsync(
                    rateLimitCheck.Message ?? "You've reached your question limit. Please try again later.",
                    messageReference: new MessageReference(messageId));

                BotActivitySource.SetSuccess(activity);
                return;
            }

            activity?.SetTag("assistant.rate_limited", false);

            // Show typing indicator
            IDisposable? typingState = null;
            if (_options.ShowTypingIndicator)
            {
                typingState = message.Channel.EnterTypingState();
            }

            try
            {
                // Process question
                var result = await assistantService.AskQuestionAsync(
                    guildId, channelId, userId, messageId, question);

                activity?.SetTag("assistant.success", result.Success);
                activity?.SetTag("assistant.input_tokens", result.InputTokens);
                activity?.SetTag("assistant.output_tokens", result.OutputTokens);
                activity?.SetTag("assistant.cached_tokens", result.CachedTokens);
                activity?.SetTag("assistant.tool_calls", result.ToolCalls);
                activity?.SetTag("assistant.latency_ms", result.LatencyMs);
                activity?.SetTag("assistant.cost_usd", result.EstimatedCostUsd);

                if (result.Success && !string.IsNullOrWhiteSpace(result.Response))
                {
                    // Send response as reply
                    await message.Channel.SendMessageAsync(
                        result.Response,
                        messageReference: new MessageReference(messageId));

                    _logger.LogInformation(
                        "Sent assistant response to user {UserId} in guild {GuildId}. " +
                        "Tokens: {InputTokens} in / {OutputTokens} out / {CachedTokens} cached. " +
                        "Cost: ${Cost:F4}. Latency: {LatencyMs}ms",
                        userId, guildId,
                        result.InputTokens, result.OutputTokens, result.CachedTokens,
                        result.EstimatedCostUsd, result.LatencyMs);
                }
                else
                {
                    // Send error message
                    await message.Channel.SendMessageAsync(
                        _options.ErrorMessage,
                        messageReference: new MessageReference(messageId));

                    _logger.LogWarning(
                        "Assistant request failed for user {UserId} in guild {GuildId}: {Error}",
                        userId, guildId, result.ErrorMessage ?? "Unknown error");
                }

                BotActivitySource.SetSuccess(activity);
            }
            finally
            {
                typingState?.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process assistant request for user {UserId} in guild {GuildId}",
                userId, guildId);

            BotActivitySource.RecordException(activity, ex);

            // Send friendly error message
            try
            {
                await message.Channel.SendMessageAsync(
                    _options.ErrorMessage,
                    messageReference: new MessageReference(messageId));
            }
            catch (Exception sendEx)
            {
                _logger.LogError(sendEx,
                    "Failed to send error message for assistant request in guild {GuildId}",
                    guildId);
            }
        }
    }

    /// <summary>
    /// Extracts the question from the message content by removing bot mentions.
    /// </summary>
    /// <param name="content">The message content.</param>
    /// <returns>The extracted question text.</returns>
    private string ExtractQuestion(string content)
    {
        // Remove both forms of bot mention: <@ID> and <@!ID>
        var question = content
            .Replace($"<@{_client.CurrentUser.Id}>", "")
            .Replace($"<@!{_client.CurrentUser.Id}>", "")
            .Trim();

        return question;
    }

    /// <summary>
    /// Sends a message to the user explaining that consent is required.
    /// </summary>
    /// <param name="message">The original message.</param>
    private async Task SendConsentRequiredMessageAsync(SocketMessage message)
    {
        var embed = new EmbedBuilder()
            .WithTitle("Consent Required")
            .WithDescription(
                "To use the AI assistant feature, you need to grant consent first.\n\n" +
                "Run `/consent grant AssistantUsage` to enable this feature.\n\n" +
                "Your questions and responses will be processed by Claude AI and logged for quality purposes.")
            .WithColor(Color.Orange)
            .WithCurrentTimestamp()
            .Build();

        await message.Channel.SendMessageAsync(
            embed: embed,
            messageReference: new MessageReference(message.Id));
    }
}
