using Discord;
using Discord.WebSocket;
using DiscordBot.Bot.Components;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that periodically checks for due Rat Watches and expired voting sessions.
/// Runs at configured intervals and processes watches concurrently with timeout protection.
/// </summary>
public class RatWatchExecutionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<RatWatchOptions> _options;
    private readonly ILogger<RatWatchExecutionService> _logger;
    private readonly DiscordSocketClient _client;
    private readonly IRatWatchStatusService _ratWatchStatusService;

    /// <summary>
    /// Initializes a new instance of the <see cref="RatWatchExecutionService"/> class.
    /// </summary>
    /// <param name="scopeFactory">The service scope factory for creating scoped services.</param>
    /// <param name="options">The Rat Watch configuration options.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="client">The Discord socket client for posting messages.</param>
    /// <param name="ratWatchStatusService">The Rat Watch status service for bot status updates.</param>
    public RatWatchExecutionService(
        IServiceScopeFactory scopeFactory,
        IOptions<RatWatchOptions> options,
        ILogger<RatWatchExecutionService> logger,
        DiscordSocketClient client,
        IRatWatchStatusService ratWatchStatusService)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
        _client = client;
        _ratWatchStatusService = ratWatchStatusService;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Rat Watch execution service starting");

        _logger.LogInformation(
            "Rat Watch execution service enabled. Check interval: {IntervalSeconds}s, Max concurrent: {MaxConcurrent}, Timeout: {TimeoutSeconds}s",
            _options.Value.CheckIntervalSeconds,
            _options.Value.MaxConcurrentExecutions,
            _options.Value.ExecutionTimeoutSeconds);

        // Initial delay to let the app start up and Discord client connect
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Process both due watches and expired voting in parallel
                var dueWatchesTask = ProcessDueWatchesAsync(stoppingToken);
                var expiredVotingTask = ProcessExpiredVotingAsync(stoppingToken);

                await Task.WhenAll(dueWatchesTask, expiredVotingTask);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Rat Watch processing");
            }

            // Wait for next check interval
            var interval = TimeSpan.FromSeconds(_options.Value.CheckIntervalSeconds);
            await Task.Delay(interval, stoppingToken);
        }

        _logger.LogInformation("Rat Watch execution service stopping");
    }

    /// <summary>
    /// Processes all due Rat Watches by posting voting messages and starting the voting process.
    /// Skips watches that are more than 5 minutes past their scheduled time (expired).
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to respect during processing.</param>
    private async Task ProcessDueWatchesAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("Checking for due Rat Watches");

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IRatWatchService>();

        // Get all due watches
        var dueWatches = await service.GetDueWatchesAsync(stoppingToken);
        var watchList = dueWatches.ToList();

        if (watchList.Count == 0)
        {
            _logger.LogTrace("No Rat Watches due for execution");
            return;
        }

        _logger.LogInformation("Found {Count} Rat Watches due for execution", watchList.Count);

        // Create a semaphore to limit concurrent executions
        using var semaphore = new SemaphoreSlim(_options.Value.MaxConcurrentExecutions);
        var executionTimeout = TimeSpan.FromSeconds(_options.Value.ExecutionTimeoutSeconds);
        var expirationThreshold = TimeSpan.FromMinutes(5);

        // Execute watches concurrently with semaphore and timeout protection
        var executionTasks = watchList.Select(async watch =>
        {
            await semaphore.WaitAsync(stoppingToken);
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                cts.CancelAfter(executionTimeout);

                var now = DateTime.UtcNow;
                var timeSinceScheduled = now - watch.ScheduledAt;

                // Skip if expired (more than 5 minutes past scheduled time)
                if (timeSinceScheduled > expirationThreshold)
                {
                    _logger.LogWarning("Rat Watch {WatchId} expired ({Minutes:F1} minutes late), skipping",
                        watch.Id, timeSinceScheduled.TotalMinutes);
                    return;
                }

                _logger.LogDebug("Executing Rat Watch {WatchId} for user {UserId}",
                    watch.Id, watch.AccusedUserId);

                // Post the voting message to Discord
                var success = await PostVotingMessageAsync(
                    watch.Id,
                    watch.GuildId,
                    watch.ChannelId,
                    watch.AccusedUserId,
                    watch.OriginalMessageId,
                    watch.CustomMessage,
                    cts.Token);

                if (success)
                {
                    _logger.LogInformation("Successfully executed Rat Watch {WatchId} for user {UserId}",
                        watch.Id, watch.AccusedUserId);
                }
                else
                {
                    _logger.LogWarning("Failed to execute Rat Watch {WatchId} for user {UserId}",
                        watch.Id, watch.AccusedUserId);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Rat Watch execution cancelled due to shutdown: {WatchId}",
                    watch.Id);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Rat Watch execution timed out after {Timeout}s: {WatchId}",
                    executionTimeout.TotalSeconds, watch.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing Rat Watch {WatchId} for user {UserId}",
                    watch.Id, watch.AccusedUserId);
            }
            finally
            {
                semaphore.Release();
            }
        });

        // Wait for all executions to complete
        await Task.WhenAll(executionTasks);

        _logger.LogInformation("Completed processing {Count} due Rat Watches", watchList.Count);

        // Notify that voting has started for one or more watches - update bot status
        _ratWatchStatusService.RequestStatusUpdate();
    }

    /// <summary>
    /// Processes all Rat Watches with expired voting by finalizing the vote and updating Discord messages.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to respect during processing.</param>
    private async Task ProcessExpiredVotingAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("Checking for expired Rat Watch voting");

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IRatWatchService>();

        // Get all watches with expired voting
        var expiredVoting = await service.GetExpiredVotingAsync(stoppingToken);
        var votingList = expiredVoting.ToList();

        if (votingList.Count == 0)
        {
            _logger.LogTrace("No Rat Watches with expired voting");
            return;
        }

        _logger.LogInformation("Found {Count} Rat Watches with expired voting", votingList.Count);

        // Create a semaphore to limit concurrent executions
        using var semaphore = new SemaphoreSlim(_options.Value.MaxConcurrentExecutions);
        var executionTimeout = TimeSpan.FromSeconds(_options.Value.ExecutionTimeoutSeconds);

        // Execute voting finalization concurrently with semaphore and timeout protection
        var executionTasks = votingList.Select(async watch =>
        {
            await semaphore.WaitAsync(stoppingToken);
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                cts.CancelAfter(executionTimeout);

                _logger.LogDebug("Finalizing voting for Rat Watch {WatchId}", watch.Id);

                // Finalize the voting in the database
                var success = await service.FinalizeVotingAsync(watch.Id, cts.Token);

                if (success)
                {
                    // Update the Discord message with final results
                    await UpdateVotingMessageAsync(watch.Id, cts.Token);

                    _logger.LogInformation("Successfully finalized voting for Rat Watch {WatchId}", watch.Id);
                }
                else
                {
                    _logger.LogWarning("Failed to finalize voting for Rat Watch {WatchId}", watch.Id);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Voting finalization cancelled due to shutdown: {WatchId}",
                    watch.Id);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Voting finalization timed out after {Timeout}s: {WatchId}",
                    executionTimeout.TotalSeconds, watch.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finalizing voting for Rat Watch {WatchId}", watch.Id);
            }
            finally
            {
                semaphore.Release();
            }
        });

        // Wait for all executions to complete
        await Task.WhenAll(executionTasks);

        _logger.LogInformation("Completed processing {Count} expired voting sessions", votingList.Count);

        // Notify that voting has ended for one or more watches - update bot status
        _ratWatchStatusService.RequestStatusUpdate();
    }

    /// <summary>
    /// Posts the voting message to Discord with voting buttons.
    /// </summary>
    /// <param name="watchId">The Rat Watch ID.</param>
    /// <param name="guildId">The Discord guild ID.</param>
    /// <param name="channelId">The Discord channel ID.</param>
    /// <param name="accusedUserId">The accused user's Discord ID.</param>
    /// <param name="originalMessageId">The original message ID.</param>
    /// <param name="customMessage">Optional custom message.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if successful, false otherwise.</returns>
    private async Task<bool> PostVotingMessageAsync(
        Guid watchId,
        ulong guildId,
        ulong channelId,
        ulong accusedUserId,
        ulong originalMessageId,
        string? customMessage,
        CancellationToken ct)
    {
        try
        {
            var channel = _client.GetChannel(channelId) as IMessageChannel;
            if (channel == null)
            {
                _logger.LogWarning("Channel {ChannelId} not found for Rat Watch {WatchId}",
                    channelId, watchId);
                return false;
            }

            // Build the message content
            var messageLink = $"https://discord.com/channels/{guildId}/{channelId}/{originalMessageId}";
            var mention = $"<@{accusedUserId}>";

            var messageContent = $"🐀 **Rat check!** {mention}";
            if (!string.IsNullOrWhiteSpace(customMessage))
            {
                messageContent += $"\n> {customMessage}";
            }
            messageContent += $"\n[Jump to original message]({messageLink})";

            // Build the voting buttons
            var components = new ComponentBuilder()
                .WithButton("Rat 🐀", ComponentIdBuilder.Build("ratwatch", "vote", accusedUserId, watchId.ToString(), "guilty"), ButtonStyle.Danger)
                .WithButton("Not Rat ✓", ComponentIdBuilder.Build("ratwatch", "vote", accusedUserId, watchId.ToString(), "notguilty"), ButtonStyle.Success)
                .Build();

            // Post the message
            var message = await channel.SendMessageAsync(messageContent, components: components);

            // Start the voting process and set the voting message ID in a single operation
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IRatWatchService>();

            await service.StartVotingAsync(watchId, message.Id, ct);

            _logger.LogDebug("Posted voting message {MessageId} for Rat Watch {WatchId}",
                message.Id, watchId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post voting message for Rat Watch {WatchId}", watchId);
            return false;
        }
    }

    /// <summary>
    /// Updates the voting message with final results and disables the voting buttons.
    /// </summary>
    /// <param name="watchId">The Rat Watch ID.</param>
    /// <param name="ct">Cancellation token.</param>
    private async Task UpdateVotingMessageAsync(Guid watchId, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IRatWatchService>();

            var watchDto = await service.GetByIdAsync(watchId, ct);
            if (watchDto == null)
            {
                _logger.LogWarning("Rat Watch {WatchId} not found for message update", watchId);
                return;
            }

            var channel = _client.GetChannel(watchDto.ChannelId) as IMessageChannel;
            if (channel == null)
            {
                _logger.LogWarning("Channel {ChannelId} not found for Rat Watch {WatchId}",
                    watchDto.ChannelId, watchId);
                return;
            }

            // Try to get the voting message
            if (!watchDto.VotingMessageId.HasValue)
            {
                _logger.LogWarning("No voting message ID for Rat Watch {WatchId}", watchId);
                return;
            }

            var message = await channel.GetMessageAsync(watchDto.VotingMessageId.Value);
            if (message is not IUserMessage userMessage)
            {
                _logger.LogWarning("Voting message {MessageId} not found or not a user message",
                    watchDto.VotingMessageId.Value);
                return;
            }

            // Build the verdict message
            var isGuilty = watchDto.Status == Core.Enums.RatWatchStatus.Guilty;
            var verdictEmoji = isGuilty ? "🐀" : "✅";
            var verdictText = isGuilty ? "**GUILTY**" : "**CLEARED**";

            var updatedContent = $"{verdictEmoji} {verdictText} — {watchDto.GuiltyVotes} Rat, {watchDto.NotGuiltyVotes} Not Rat";

            // Disable the buttons
            var disabledComponents = new ComponentBuilder()
                .WithButton("Rat 🐀", "disabled_guilty", ButtonStyle.Danger, disabled: true)
                .WithButton("Not Rat ✓", "disabled_notguilty", ButtonStyle.Success, disabled: true)
                .Build();

            // Update the message
            await userMessage.ModifyAsync(props =>
            {
                props.Content = updatedContent;
                props.Components = disabledComponents;
            });

            _logger.LogDebug("Updated voting message {MessageId} for Rat Watch {WatchId} with verdict: {Verdict}",
                watchDto.VotingMessageId.Value, watchId, isGuilty ? "Guilty" : "Not Guilty");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update voting message for Rat Watch {WatchId}", watchId);
        }
    }
}
