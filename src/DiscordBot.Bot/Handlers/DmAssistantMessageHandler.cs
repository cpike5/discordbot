using System.Text;
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

        var userId = message.Author.Id;

        using var activity = BotActivitySource.StartEventActivity(
            "dm_assistant.message.process",
            userId: userId);

        IDisposable? typingState = null;
        try
        {
            using var scope = _scopeFactory.CreateScope();

            // Check enabled state from settings service (runtime-togglable via Settings UI),
            // falling back to IOptions config value
            var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            var isEnabled = await settingsService.GetSettingValueAsync<bool?>("DmAssistant:Enabled")
                ?? _options.Enabled;

            if (!isEnabled)
            {
                _logger.LogDebug("DM assistant is disabled, ignoring DM from {UserId}", userId);
                BotActivitySource.SetSuccess(activity);
                return;
            }

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
                await SendResponseAsync(message.Channel, response.Response);

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

    private const int DiscordMaxMessageLength = 2000;
    private const int FileAttachmentThreshold = 8000;

    /// <summary>
    /// Sends a response to the channel, handling Discord's 2000-char message limit
    /// by chunking into multiple messages or uploading as a file attachment.
    /// </summary>
    private static async Task SendResponseAsync(IMessageChannel channel, string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return;

        if (response.Length <= DiscordMaxMessageLength)
        {
            await channel.SendMessageAsync(response);
            return;
        }

        if (response.Length > FileAttachmentThreshold)
        {
            await channel.SendMessageAsync("Here's the full response:");

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(response));
            await channel.SendFileAsync(stream, "response.md", text: null);
            return;
        }

        // Split on newline boundaries into <=2000 char chunks
        var lines = response.Split('\n');
        var chunk = new StringBuilder();

        foreach (var line in lines)
        {
            // If a single line exceeds the limit, hard-split it
            if (line.Length > DiscordMaxMessageLength)
            {
                // Flush any accumulated content first
                if (chunk.Length > 0)
                {
                    await channel.SendMessageAsync(chunk.ToString());
                    chunk.Clear();
                }

                // Hard-split the long line at max-length boundaries
                for (var i = 0; i < line.Length; i += DiscordMaxMessageLength)
                {
                    var length = Math.Min(DiscordMaxMessageLength, line.Length - i);
                    await channel.SendMessageAsync(line.Substring(i, length));
                }

                continue;
            }

            // Check if adding this line (with newline separator) would exceed the limit
            var addition = chunk.Length == 0 ? line.Length : line.Length + 1;
            if (chunk.Length + addition > DiscordMaxMessageLength)
            {
                await channel.SendMessageAsync(chunk.ToString());
                chunk.Clear();
            }

            if (chunk.Length > 0)
                chunk.Append('\n');

            chunk.Append(line);
        }

        // Send any remaining content
        if (chunk.Length > 0)
        {
            await channel.SendMessageAsync(chunk.ToString());
        }
    }
}
