using System.Text.Json;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that synchronizes Discord guild members with the local database.
/// Performs initial full sync for all guilds on startup and runs daily reconciliation.
/// </summary>
public class MemberSyncService : MonitoredBackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemberSyncQueue _syncQueue;
    private readonly DiscordSocketClient _client;
    private readonly IOptions<BackgroundServicesOptions> _options;
    private readonly SemaphoreSlim _apiSemaphore = new(1, 1);
    private DateTime _lastApiCall = DateTime.MinValue;
    private DateTime _lastReconciliation = DateTime.MinValue;

    public override string ServiceName => "MemberSyncService";

    /// <summary>
    /// Gets the service name in snake_case format for tracing.
    /// </summary>
    protected virtual string TracingServiceName =>
        ServiceName.ToLowerInvariant().Replace(" ", "_");

    public MemberSyncService(
        IServiceScopeFactory scopeFactory,
        IMemberSyncQueue syncQueue,
        DiscordSocketClient client,
        IOptions<BackgroundServicesOptions> options,
        ILogger<MemberSyncService> logger,
        IServiceProvider serviceProvider)
        : base(serviceProvider, logger)
    {
        _scopeFactory = scopeFactory;
        _syncQueue = syncQueue;
        _client = client;
        _options = options;
    }

    protected override async Task ExecuteMonitoredAsync(CancellationToken stoppingToken)
    {
        // Check if member sync is enabled
        if (!_options.Value.MemberSyncEnabled)
        {
            SetStatus("Disabled");
            return;
        }

        // Initial delay to let the bot fully connect
        var initialDelay = TimeSpan.FromMinutes(_options.Value.MemberSyncInitialDelayMinutes);
        await Task.Delay(initialDelay, stoppingToken);

        // Queue initial sync for all guilds in database
        SetStatus("Queuing");
        await QueueInitialSyncAsync(stoppingToken);

        // Start processing queue
        _ = ProcessQueueAsync(stoppingToken);

        // Schedule daily reconciliation
        SetStatus("Running");
        await RunReconciliationLoopAsync(stoppingToken);
    }

    /// <summary>
    /// Queues initial sync for all guilds in the database.
    /// </summary>
    private async Task QueueInitialSyncAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var guildRepository = scope.ServiceProvider.GetRequiredService<IGuildRepository>();

            var guilds = await guildRepository.GetAllAsync(stoppingToken);

            _logger.LogInformation("Queuing initial sync for {Count} guilds", guilds.Count);

            foreach (var guild in guilds)
            {
                _syncQueue.EnqueueGuild(guild.Id, MemberSyncReason.InitialSync);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue initial sync for guilds");
        }
    }

    /// <summary>
    /// Processes sync requests from the queue.
    /// </summary>
    private async Task ProcessQueueAsync(CancellationToken stoppingToken)
    {
        var queueExecutionCycle = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            queueExecutionCycle++;
            var correlationId = Guid.NewGuid().ToString("N")[..16];

            using var activity = BotActivitySource.StartBackgroundServiceActivity(
                $"{TracingServiceName}_queue",
                queueExecutionCycle,
                correlationId);

            UpdateHeartbeat();
            try
            {
                var (guildId, reason) = await _syncQueue.DequeueAsync(stoppingToken);
                await ProcessSyncRequestAsync(guildId, reason, stoppingToken);

                BotActivitySource.SetRecordsProcessed(activity, 1);
                BotActivitySource.SetSuccess(activity);
                ClearError();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                BotActivitySource.RecordException(activity, ex);
                RecordError(ex);
                // Continue processing next item
            }
        }
    }

    /// <summary>
    /// Runs the daily reconciliation loop.
    /// </summary>
    private async Task RunReconciliationLoopAsync(CancellationToken stoppingToken)
    {
        var reconciliationInterval = TimeSpan.FromHours(_options.Value.MemberSyncReconciliationIntervalHours);
        _lastReconciliation = DateTime.UtcNow;
        var reconciliationCycle = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            reconciliationCycle++;
            var correlationId = Guid.NewGuid().ToString("N")[..16];

            using var activity = BotActivitySource.StartBackgroundServiceActivity(
                $"{TracingServiceName}_reconciliation",
                reconciliationCycle,
                correlationId);

            UpdateHeartbeat();
            try
            {
                await Task.Delay(reconciliationInterval, stoppingToken);

                using var scope = _scopeFactory.CreateScope();
                var guildRepository = scope.ServiceProvider.GetRequiredService<IGuildRepository>();
                var guilds = await guildRepository.GetAllAsync(stoppingToken);

                foreach (var guild in guilds)
                {
                    _syncQueue.EnqueueGuild(guild.Id, MemberSyncReason.DailyReconciliation);
                }

                BotActivitySource.SetRecordsProcessed(activity, guilds.Count);
                BotActivitySource.SetSuccess(activity);

                _lastReconciliation = DateTime.UtcNow;
                ClearError();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                BotActivitySource.RecordException(activity, ex);
                RecordError(ex);
            }
        }
    }

    /// <summary>
    /// Processes a single guild member sync request.
    /// </summary>
    private async Task ProcessSyncRequestAsync(ulong guildId, MemberSyncReason reason, CancellationToken ct)
    {
        _logger.LogInformation(
            "Starting member sync for guild {GuildId}, reason: {Reason}",
            guildId, reason);

        try
        {
            // Get guild from Discord client
            var guild = _client.GetGuild(guildId);
            if (guild == null)
            {
                _logger.LogWarning("Guild {GuildId} not found in Discord client, skipping sync", guildId);
                return;
            }

            // Fetch all members from Discord
            var members = await FetchAllMembersAsync(guild, ct);
            _logger.LogInformation(
                "Fetched {Count} members from Discord for guild {GuildId} ({GuildName})",
                members.Count, guildId, guild.Name);

            // Map to entities
            var guildMembers = new List<GuildMember>();
            var users = new List<User>();

            foreach (var member in members)
            {
                ct.ThrowIfCancellationRequested();

                users.Add(MapToUser(member));
                guildMembers.Add(MapToGuildMember(member, guildId));
            }

            // Batch upsert to database
            using var scope = _scopeFactory.CreateScope();
            var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var memberRepository = scope.ServiceProvider.GetRequiredService<IGuildMemberRepository>();

            using var batchActivity = BotActivitySource.StartBackgroundBatchActivity(
                TracingServiceName,
                members.Count,
                "member_upsert");

            try
            {
                _logger.LogDebug("Upserting {Count} users to database", users.Count);
                var usersAffected = await userRepository.BatchUpsertAsync(users, ct);

                _logger.LogDebug("Upserting {Count} guild members to database", guildMembers.Count);
                var membersAffected = await memberRepository.BatchUpsertAsync(guildMembers, ct);

                // For reconciliation, mark absent members as inactive
                if (reason == MemberSyncReason.DailyReconciliation || reason == MemberSyncReason.InitialSync)
                {
                    var activeUserIds = members.Select(m => m.Id).ToList();
                    var inactiveCount = await memberRepository.MarkInactiveExceptAsync(guildId, activeUserIds, ct);

                    _logger.LogInformation(
                        "Reconciliation completed for guild {GuildId}. Marked {InactiveCount} members as inactive",
                        guildId, inactiveCount);
                }

                BotActivitySource.SetRecordsProcessed(batchActivity, membersAffected);
                BotActivitySource.SetSuccess(batchActivity);

                _logger.LogInformation(
                    "Member sync completed for guild {GuildId} ({GuildName}). " +
                    "Reason: {Reason}, Members: {MemberCount}, Users affected: {UsersAffected}, Members affected: {MembersAffected}",
                    guildId, guild.Name, reason, members.Count, usersAffected, membersAffected);
            }
            catch (Exception ex)
            {
                BotActivitySource.RecordException(batchActivity, ex);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to sync members for guild {GuildId}, reason: {Reason}",
                guildId, reason);
        }
    }

    /// <summary>
    /// Fetches all members for a guild with rate limiting and pagination.
    /// </summary>
    private async Task<List<SocketGuildUser>> FetchAllMembersAsync(SocketGuild guild, CancellationToken ct)
    {
        var members = new List<SocketGuildUser>();

        try
        {
            // Use exponential backoff for retries
            await ExecuteWithBackoffAsync(async () =>
            {
                await WaitForRateLimitAsync(ct);

                _logger.LogDebug(
                    "Downloading users for guild {GuildId} ({GuildName}), current cache size: {CacheSize}",
                    guild.Id, guild.Name, guild.Users.Count);

                // Discord.NET's DownloadUsersAsync handles pagination internally
                await guild.DownloadUsersAsync();

                return true;
            }, ct);

            // Collect all cached users
            foreach (var user in guild.Users)
            {
                ct.ThrowIfCancellationRequested();
                members.Add(user);
            }

            _logger.LogDebug(
                "Successfully fetched {Count} members for guild {GuildId} ({GuildName})",
                members.Count, guild.Id, guild.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to fetch members for guild {GuildId} ({GuildName})",
                guild.Id, guild.Name);
            throw;
        }

        return members;
    }

    /// <summary>
    /// Waits for rate limit delay between API calls.
    /// </summary>
    private async Task WaitForRateLimitAsync(CancellationToken ct)
    {
        await _apiSemaphore.WaitAsync(ct);
        try
        {
            var timeSinceLastCall = DateTime.UtcNow - _lastApiCall;
            var minDelay = TimeSpan.FromMilliseconds(_options.Value.MemberSyncApiDelayMs);

            if (timeSinceLastCall < minDelay)
            {
                var waitTime = minDelay - timeSinceLastCall;
                _logger.LogTrace("Rate limit delay: waiting {WaitMs}ms", waitTime.TotalMilliseconds);
                await Task.Delay(waitTime, ct);
            }

            _lastApiCall = DateTime.UtcNow;
        }
        finally
        {
            _apiSemaphore.Release();
        }
    }

    /// <summary>
    /// Executes an operation with exponential backoff on rate limit errors.
    /// </summary>
    private async Task<T> ExecuteWithBackoffAsync<T>(Func<Task<T>> operation, CancellationToken ct)
    {
        var retryCount = 0;
        var maxRetries = _options.Value.MemberSyncMaxRetries;

        while (true)
        {
            try
            {
                return await operation();
            }
            catch (HttpException ex) when (ex.HttpCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                retryCount++;
                if (retryCount > maxRetries)
                {
                    _logger.LogError(
                        "Rate limit exceeded after {Retries} retries. Giving up.",
                        maxRetries);
                    throw;
                }

                var backoffSeconds = Math.Pow(2, retryCount); // 2, 4, 8...
                var maxBackoff = 16; // Cap at 16 seconds
                backoffSeconds = Math.Min(backoffSeconds, maxBackoff);

                _logger.LogWarning(
                    "Rate limited. Backing off for {Seconds} seconds (attempt {Attempt}/{Max})",
                    backoffSeconds, retryCount, maxRetries);

                await Task.Delay(TimeSpan.FromSeconds(backoffSeconds), ct);
            }
        }
    }

    /// <summary>
    /// Maps a Discord guild user to a User entity.
    /// </summary>
    private static User MapToUser(SocketGuildUser discordUser)
    {
        return new User
        {
            Id = discordUser.Id,
            Username = discordUser.Username,
            Discriminator = discordUser.Discriminator,
            FirstSeenAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow,
            AccountCreatedAt = discordUser.CreatedAt.UtcDateTime,
            AvatarHash = discordUser.AvatarId,
            GlobalDisplayName = discordUser.GlobalName
        };
    }

    /// <summary>
    /// Maps a Discord guild user to a GuildMember entity.
    /// </summary>
    private static GuildMember MapToGuildMember(SocketGuildUser discordUser, ulong guildId)
    {
        return new GuildMember
        {
            GuildId = guildId,
            UserId = discordUser.Id,
            JoinedAt = discordUser.JoinedAt?.UtcDateTime ?? DateTime.UtcNow,
            Nickname = discordUser.Nickname,
            CachedRolesJson = SerializeRoles(discordUser.Roles),
            LastCachedAt = DateTime.UtcNow,
            IsActive = true
        };
    }

    /// <summary>
    /// Serializes Discord roles to JSON array format.
    /// Excludes the @everyone role.
    /// </summary>
    private static string SerializeRoles(IReadOnlyCollection<SocketRole> roles)
    {
        var roleIds = roles
            .Where(r => !r.IsEveryone)
            .Select(r => r.Id)
            .ToList();

        return JsonSerializer.Serialize(roleIds);
    }
}
