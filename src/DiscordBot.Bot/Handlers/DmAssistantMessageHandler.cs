using Discord;
using Discord.WebSocket;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Handlers;

/// <summary>
/// Handles Discord DM messages to process AI assistant requests.
/// Acts as a thin wrapper around the DmAssistantService to bridge Discord.NET events.
/// This is the DM counterpart to <see cref="AssistantMessageHandler"/> which handles guild messages.
/// </summary>
public class DmAssistantMessageHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DiscordSocketClient _client;
    private readonly DmAssistantOptions _options;
    private readonly ILogger<DmAssistantMessageHandler> _logger;

    public DmAssistantMessageHandler(
        IServiceScopeFactory scopeFactory,
        DiscordSocketClient client,
        IOptions<DmAssistantOptions> options,
        ILogger<DmAssistantMessageHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Handles the MessageReceived event from DiscordSocketClient.
    /// Processes DM messages and delegates to the DmAssistantService.
    /// </summary>
    /// <param name="message">The received message.</param>
    public async Task HandleMessageReceivedAsync(SocketMessage message)
    {
        if (message is not SocketUserMessage)
            return;

        if (message.Author.IsBot)
            return;

        if (message.Channel is not IDMChannel)
            return;

        if (!_options.Enabled)
        {
            _logger.LogDebug("DM assistant is disabled, ignoring DM from {UserId}", message.Author.Id);
            return;
        }

        var userId = message.Author.Id;
        var messageId = message.Id;

        using var activity = BotActivitySource.StartEventActivity(
            "dm_assistant.message.process",
            userId: userId);

        IDisposable? typingState = null;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dmAssistantService = scope.ServiceProvider.GetService<IDmAssistantService>();
            if (dmAssistantService is null)
            {
                _logger.LogDebug("IDmAssistantService not registered, ignoring DM from {UserId}", userId);
                BotActivitySource.SetSuccess(activity);
                return;
            }

            if (_options.ShowTypingIndicator)
            {
                typingState = message.Channel.EnterTypingState();
            }

            var response = await dmAssistantService.ProcessMessageAsync(userId, message.Content);

            activity?.SetTag("dm_assistant.success", response.Success);

            if (response.Success && !string.IsNullOrWhiteSpace(response.Response))
            {
                await message.Channel.SendMessageAsync(response.Response);

                _logger.LogInformation(
                    "Sent DM assistant response to user {UserId}",
                    userId);
            }
            else
            {
                await message.Channel.SendMessageAsync(_options.ErrorMessage);

                _logger.LogWarning(
                    "DM assistant request failed for user {UserId}: {Error}",
                    userId, response.ErrorMessage ?? "Unknown error");
            }

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process DM assistant request for user {UserId}",
                userId);

            BotActivitySource.RecordException(activity, ex);

            try
            {
                await message.Channel.SendMessageAsync(_options.ErrorMessage);
            }
            catch (Exception sendEx)
            {
                _logger.LogError(sendEx,
                    "Failed to send error message for DM assistant request from user {UserId}",
                    userId);
            }
        }
        finally
        {
            typingState?.Dispose();
        }
    }
}
