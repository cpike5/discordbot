using Discord;
using Discord.Net;
using Discord.WebSocket;
using DiscordBot.Bot.Helpers;
using DiscordBot.Bot.Services.LLM;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.DTOs.Llm;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace DiscordBot.Bot.Handlers;

/// <summary>
/// Handles Discord message events for the guild assistant: a mention answered in the channel, a
/// mention that opens a thread (guilds in thread mode), and a message in a thread the assistant
/// holds, which is a turn of that conversation. A thin wrapper around <see cref="IAssistantService"/>
/// that bridges Discord.NET events; the decision of which kind of request a message is lives in
/// <see cref="AssistantTriggerRules"/>.
/// </summary>
public class AssistantMessageHandler
{
    /// <summary>Longest thread name Discord allows is 100; leave room.</summary>
    private const int MaxThreadNameLength = 90;

    private const string ConsentPromptedKeyPrefix = "assistant:consent_prompted:";
    private const string ThreadCreateWarnedKeyPrefix = "assistant:thread_create_warned:";
    private static readonly TimeSpan OncePerHour = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DiscordSocketClient _client;
    private readonly IMemoryCache _cache;
    private readonly AssistantOptions _options;
    private readonly ILogger<AssistantMessageHandler> _logger;

    /// <summary>
    /// One lock per thread, so two members posting at once take their turns one after the other
    /// and the second sees the first's. Entries are dropped when a thread closes; an idle entry
    /// costs a few bytes until then.
    /// </summary>
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _threadLocks = new();

    public AssistantMessageHandler(
        IServiceScopeFactory scopeFactory,
        DiscordSocketClient client,
        IMemoryCache cache,
        IOptions<AssistantOptions> options,
        ILogger<AssistantMessageHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _client = client;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Whether this caller may use assistant tools that create or change data.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately narrow: a caller may write only if Discord already lets them change the server
    /// (Manage Server or Administrator). Broadening this later is a one-line change and a decision
    /// someone can make with a specific write tool in front of them; starting broad and narrowing
    /// after a tool has shipped is not.
    /// </para>
    /// <para>
    /// Decided here because this is the only layer with a Discord client — Infrastructure has no
    /// Discord.NET reference, and the flag it carries is a plain bool for exactly that reason.
    /// </para>
    /// </remarks>
    private static bool CallerCanMutate(SocketUser author) =>
        author is IGuildUser member
        && (member.GuildPermissions.ManageGuild || member.GuildPermissions.Administrator);

    /// <summary>
    /// Handles the MessageReceived event from DiscordSocketClient.
    /// </summary>
    /// <param name="message">The received message.</param>
    public async Task HandleMessageReceivedAsync(SocketMessage message)
    {
        if (message.Author.IsBot)
        {
            return;
        }

        if (message.Channel is not SocketGuildChannel guildChannel)
        {
            return;
        }

        var thread = message.Channel as SocketThreadChannel;
        var botId = _client.CurrentUser.Id;
        var mentionsBot = message.MentionedUsers.Any(u => u.Id == botId);

        // Nothing to do without a mention or a thread, and this is the hot path for every guild
        // message, so decide that before opening a scope.
        if (!mentionsBot && thread is null)
        {
            return;
        }

        var threadOwnedByBot = thread is not null && ((IThreadChannel)thread).OwnerId == botId;
        if (!mentionsBot && !threadOwnedByBot)
        {
            return;
        }

        var guildId = guildChannel.Guild.Id;
        var userId = message.Author.Id;
        var messageId = message.Id;

        using var activity = BotActivitySource.StartEventActivity(
            "assistant.message.process",
            guildId: guildId,
            userId: userId);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var assistantService = scope.ServiceProvider.GetService<IAssistantService>();
            if (assistantService is null)
            {
                _logger.LogDebug("IAssistantService not registered (API key not configured), ignoring message in guild {GuildId}", guildId);
                BotActivitySource.SetSuccess(activity);
                return;
            }

            var consentRepository = scope.ServiceProvider.GetRequiredService<IUserConsentRepository>();
            var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            var guildSettings = scope.ServiceProvider.GetRequiredService<IAssistantGuildSettingsService>();
            var threads = scope.ServiceProvider.GetRequiredService<IAssistantThreadRepository>();

            var globallyEnabled = await settingsService.GetSettingValueAsync<bool?>("Assistant:GloballyEnabled")
                ?? _options.GloballyEnabled;
            activity?.SetTag("assistant.globally_enabled", globallyEnabled);
            if (!globallyEnabled)
            {
                BotActivitySource.SetSuccess(activity);
                return;
            }

            var guildEnabled = await assistantService.IsEnabledForGuildAsync(guildId);
            activity?.SetTag("assistant.guild_enabled", guildEnabled);
            if (!guildEnabled)
            {
                BotActivitySource.SetSuccess(activity);
                return;
            }

            var threadRow = threadOwnedByBot ? await threads.GetByThreadIdAsync(thread!.Id) : null;
            var mode = await guildSettings.GetConversationModeAsync(guildId);

            var trigger = AssistantTriggerRules.Classify(
                authorIsBot: false,
                isGuildChannel: true,
                mentionsBot,
                isThread: thread is not null,
                threadOwnedByBot,
                threadIsKnownAssistantThread: threadRow is not null,
                mode);
            activity?.SetTag("assistant.trigger", trigger.ToString());

            if (trigger == AssistantTrigger.Ignore)
            {
                BotActivitySource.SetSuccess(activity);
                return;
            }

            if (trigger == AssistantTrigger.ThreadTurn && threadRow!.Status == AssistantThreadStatus.Closed)
            {
                _logger.LogDebug("Thread {ThreadId} is closed; ignoring message {MessageId}", thread!.Id, messageId);
                BotActivitySource.SetSuccess(activity);
                return;
            }

            // The allowed-channel list is about where a conversation may start; a thread inherits
            // its parent. For a turn, the row remembers the parent; otherwise ask the channel.
            ulong? parentChannelId = trigger == AssistantTrigger.ThreadTurn
                ? threadRow!.ParentChannelId
                : thread?.ParentChannel?.Id;
            var channelAllowed = await assistantService.IsAllowedInChannelAsync(guildId, parentChannelId ?? message.Channel.Id);
            activity?.SetTag("assistant.channel_allowed", channelAllowed);
            if (!channelAllowed)
            {
                BotActivitySource.SetSuccess(activity);
                return;
            }

            var question = ExtractQuestion(message.Content);
            if (string.IsNullOrWhiteSpace(question))
            {
                BotActivitySource.SetSuccess(activity);
                return;
            }

            if (_options.Privacy.RequireExplicitConsent)
            {
                var hasConsent = await consentRepository.GetActiveConsentAsync(userId, ConsentType.AssistantUsage);
                activity?.SetTag("assistant.consent_granted", hasConsent != null);
                if (hasConsent == null)
                {
                    // In a thread, say it once per person: a bystander posting in a public thread
                    // should not get the embed on every message.
                    if (trigger != AssistantTrigger.ThreadTurn || MarkConsentPrompted(thread!.Id, userId))
                    {
                        await SendConsentRequiredMessageAsync(message);
                    }

                    BotActivitySource.SetSuccess(activity);
                    return;
                }
            }

            var rateLimitCheck = await assistantService.CheckRateLimitAsync(guildId, userId);
            activity?.SetTag("assistant.rate_limited", !rateLimitCheck.IsAllowed);
            if (!rateLimitCheck.IsAllowed)
            {
                await message.Channel.SendMessageAsync(
                    rateLimitCheck.Message ?? "You've reached your question limit. Please try again later.",
                    messageReference: new MessageReference(messageId));
                BotActivitySource.SetSuccess(activity);
                return;
            }

            // Where the answer goes, and which thread (if any) the turn belongs to
            IMessageChannel replyChannel = message.Channel;
            ulong? threadId = trigger == AssistantTrigger.ThreadTurn ? thread!.Id : null;

            if (trigger == AssistantTrigger.NewThreadQuestion)
            {
                var created = await TryCreateThreadAsync(message, guildChannel, question);
                if (created is not null)
                {
                    threadRow = new AssistantThread
                    {
                        ThreadId = created.Id,
                        GuildId = guildId,
                        ParentChannelId = message.Channel.Id,
                        StarterUserId = userId,
                        CreatedAt = DateTime.UtcNow,
                        LastActivityAt = DateTime.UtcNow
                    };
                    // Saved before the model is asked, so a second message in the thread that
                    // arrives while this answer is being written is already a turn.
                    await threads.AddAsync(threadRow);

                    replyChannel = created;
                    threadId = created.Id;
                    parentChannelId = message.Channel.Id;
                    activity?.SetTag("assistant.thread_created", true);
                }
                else
                {
                    activity?.SetTag("assistant.thread_created", false);
                }
            }

            var request = new GuildAssistantRequest(
                guildId,
                ChannelId: threadId ?? message.Channel.Id,
                ParentChannelId: threadId is null ? null : parentChannelId,
                ThreadId: threadId,
                userId,
                messageId,
                question,
                CallerCanMutate(message.Author));

            // The first answer in a new thread needs no reference: the thread hangs off the question
            var reference = threadId is not null && trigger == AssistantTrigger.NewThreadQuestion
                ? null
                : new MessageReference(messageId);

            var gate = threadId is null ? null : _threadLocks.GetOrAdd(threadId.Value, _ => new SemaphoreSlim(1, 1));
            if (gate is not null)
            {
                await gate.WaitAsync();
            }

            try
            {
                using var typingState = _options.ShowTypingIndicator ? replyChannel.EnterTypingState() : null;

                var result = await assistantService.AskQuestionAsync(request);

                activity?.SetTag("assistant.success", result.Success);
                activity?.SetTag("assistant.input_tokens", result.InputTokens);
                activity?.SetTag("assistant.output_tokens", result.OutputTokens);
                activity?.SetTag("assistant.cached_tokens", result.CachedTokens);
                activity?.SetTag("assistant.tool_calls", result.ToolCalls);
                activity?.SetTag("assistant.latency_ms", result.LatencyMs);
                activity?.SetTag("assistant.cost_usd", result.EstimatedCostUsd);

                if (result.Success && !string.IsNullOrWhiteSpace(result.Response))
                {
                    await DiscordReplyChunker.SendAsync(replyChannel, result.Response, reference);

                    _logger.LogInformation(
                        "Sent assistant response to user {UserId} in guild {GuildId} ({Trigger}). " +
                        "Tokens: {InputTokens} in / {OutputTokens} out / {CachedTokens} cached. " +
                        "Cost: ${Cost:F4}. Latency: {LatencyMs}ms",
                        userId, guildId, trigger,
                        result.InputTokens, result.OutputTokens, result.CachedTokens,
                        result.EstimatedCostUsd, result.LatencyMs);

                    if (threadId is not null)
                    {
                        await CloseIfAtCapAsync(threads, threadId.Value, replyChannel);
                    }
                }
                else
                {
                    await replyChannel.SendMessageAsync(_options.Messages.ErrorMessage, messageReference: reference);

                    _logger.LogWarning(
                        "Assistant request failed for user {UserId} in guild {GuildId}: {Error}",
                        userId, guildId, result.ErrorMessage ?? "Unknown error");
                }

                BotActivitySource.SetSuccess(activity);
            }
            finally
            {
                gate?.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process assistant request for user {UserId} in guild {GuildId}",
                userId, guildId);

            BotActivitySource.RecordException(activity, ex);

            try
            {
                await message.Channel.SendMessageAsync(
                    _options.Messages.ErrorMessage,
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
    /// Opens a public thread off the member's message. Null when the channel cannot host one or the
    /// bot lacks the permission, in which case the caller answers in the channel instead: the
    /// member still gets an answer, and the operator gets one warning an hour.
    /// </summary>
    private async Task<IThreadChannel?> TryCreateThreadAsync(SocketMessage message, SocketGuildChannel guildChannel, string question)
    {
        if (message.Channel is not ITextChannel textChannel || guildChannel is SocketThreadChannel)
        {
            return null;
        }

        try
        {
            return await textChannel.CreateThreadAsync(
                ThreadName(question, message.Author),
                ThreadType.PublicThread,
                ArchiveDuration(_options.Threads.AutoArchiveMinutes),
                message);
        }
        catch (HttpException ex) when (ex.HttpCode == System.Net.HttpStatusCode.Forbidden)
        {
            WarnOnce(ThreadCreateWarnedKeyPrefix + textChannel.Id,
                "The bot may not open a thread in channel {ChannelId} of guild {GuildId} ({Reason}); answering in the channel instead. Give it Create Public Threads and Send Messages in Threads there.",
                textChannel.Id, guildChannel.Guild.Id, ex.Reason ?? ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not open an assistant thread in channel {ChannelId}; answering in the channel instead", textChannel.Id);
            return null;
        }
    }

    /// <summary>
    /// After a thread's turn count reaches the cap, say so and close the row; later messages in the
    /// thread are ignored. Bounds what one thread can cost.
    /// </summary>
    private async Task CloseIfAtCapAsync(IAssistantThreadRepository threads, ulong threadId, IMessageChannel channel)
    {
        var cap = _options.Threads.MaxTurnsPerThread;
        if (cap <= 0)
        {
            return;
        }

        // Re-read: the context bumped the count during the run
        var row = await threads.GetByThreadIdAsync(threadId);
        if (row is null || row.Status == AssistantThreadStatus.Closed || row.TurnCount < cap)
        {
            return;
        }

        row.Status = AssistantThreadStatus.Closed;
        await threads.UpdateAsync(row);
        _threadLocks.TryRemove(threadId, out _);

        try
        {
            await channel.SendMessageAsync("This conversation has reached its limit. Mention me in the channel to start a new one.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not post the turn-cap notice in thread {ThreadId}", threadId);
        }

        _logger.LogInformation("Assistant thread {ThreadId} closed at {Turns} turns", threadId, row.TurnCount);
    }

    /// <summary>The thread's name: the question's first line, or the member's name when that is blank.</summary>
    public static string ThreadName(string question, IUser author)
    {
        var firstLine = (question ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? string.Empty;

        if (firstLine.Length == 0)
        {
            return $"Assistant · {(author as IGuildUser)?.DisplayName ?? author.Username}";
        }

        return TextDisplay.Truncate(firstLine, MaxThreadNameLength, "…");
    }

    /// <summary>Discord offers four archive durations; take the smallest that covers the setting.</summary>
    public static ThreadArchiveDuration ArchiveDuration(int minutes) => minutes switch
    {
        <= 60 => ThreadArchiveDuration.OneHour,
        <= 1440 => ThreadArchiveDuration.OneDay,
        <= 4320 => ThreadArchiveDuration.ThreeDays,
        _ => ThreadArchiveDuration.OneWeek
    };

    /// <summary>True the first time a user is prompted in a thread within the hour; false after.</summary>
    private bool MarkConsentPrompted(ulong threadId, ulong userId)
    {
        var key = $"{ConsentPromptedKeyPrefix}{threadId}:{userId}";
        if (_cache.TryGetValue(key, out _))
        {
            return false;
        }

        _cache.Set(key, true, new MemoryCacheEntryOptions().SetAbsoluteExpiration(OncePerHour).SetSize(1));
        return true;
    }

    private void WarnOnce(string key, string messageTemplate, params object[] args)
    {
        if (_cache.TryGetValue(key, out _))
        {
            return;
        }

        _cache.Set(key, true, new MemoryCacheEntryOptions().SetAbsoluteExpiration(OncePerHour).SetSize(1));
#pragma warning disable CA2254 // One constant template, chosen by the caller
        _logger.LogWarning(messageTemplate, args);
#pragma warning restore CA2254
    }

    /// <summary>
    /// Extracts the question from the message content by removing bot mentions.
    /// </summary>
    private string ExtractQuestion(string content)
    {
        return content
            .Replace($"<@{_client.CurrentUser.Id}>", "")
            .Replace($"<@!{_client.CurrentUser.Id}>", "")
            .Trim();
    }

    /// <summary>
    /// Sends a message to the user explaining that consent is required.
    /// </summary>
    private static async Task SendConsentRequiredMessageAsync(SocketMessage message)
    {
        var embed = new EmbedBuilder()
            .WithTitle("Consent Required")
            .WithDescription(
                "To use the AI assistant feature, you need to grant consent first.\n\n" +
                "Run `/consent grant AssistantUsage` to enable this feature.\n\n" +
                "By consenting, you agree that when you @-mention the bot the text of your message is sent to OpenRouter and on to the model provider serving the configured model, " +
                "and that your question and the bot's response are stored in this bot's database for quality, troubleshooting, and usage metrics.")
            .WithColor(Color.Orange)
            .WithCurrentTimestamp()
            .Build();

        await message.Channel.SendMessageAsync(
            embed: embed,
            messageReference: new MessageReference(message.Id));
    }
}
