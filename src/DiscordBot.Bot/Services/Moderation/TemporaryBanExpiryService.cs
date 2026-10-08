using System.Net;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services.Moderation;

/// <summary>
/// Background service that lifts temporary bans once they expire.
/// Each cycle it asks <see cref="IModerationService.GetExpiredTemporaryActionsAsync"/> for expired bans,
/// removes each ban in Discord, and records an Unban case with the bot as moderator, the same record a
/// manual <c>/unban</c> leaves. That Unban case closes the ban: the expiry query no longer returns it.
/// </summary>
public class TemporaryBanExpiryService : MonitoredBackgroundService
{
    private const string TracingServiceName = "temporary_ban_expiry_service";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<ModerationOptions> _options;
    private readonly DiscordSocketClient? _client;
    private readonly Func<ulong, IGuild?> _getGuild;
    private readonly Func<ulong> _getBotUserId;

    public override string ServiceName => "Temporary Ban Expiry Service";

    /// <summary>
    /// Initializes a new instance of the <see cref="TemporaryBanExpiryService"/> class.
    /// </summary>
    public TemporaryBanExpiryService(
        IServiceProvider serviceProvider,
        IServiceScopeFactory scopeFactory,
        IOptions<ModerationOptions> options,
        DiscordSocketClient client,
        ILogger<TemporaryBanExpiryService> logger)
        : this(serviceProvider, scopeFactory, options, client.GetGuild, () => client.CurrentUser?.Id ?? 0, logger, client)
    {
    }

    /// <summary>
    /// Test seam: <see cref="DiscordSocketClient.GetGuild"/> is not virtual, so tests supply the lookups.
    /// </summary>
    internal TemporaryBanExpiryService(
        IServiceProvider serviceProvider,
        IServiceScopeFactory scopeFactory,
        IOptions<ModerationOptions> options,
        Func<ulong, IGuild?> getGuild,
        Func<ulong> getBotUserId,
        ILogger<TemporaryBanExpiryService> logger,
        DiscordSocketClient? client = null)
        : base(serviceProvider, logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _getGuild = getGuild;
        _getBotUserId = getBotUserId;
        _client = client;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteMonitoredAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Temporary ban expiry service starting. Check interval: {IntervalSeconds}s",
            _options.Value.TempBanExpiryCheckIntervalSeconds);

        while (_client != null && _client.ConnectionState != ConnectionState.Connected && !stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug("Waiting for Discord client to connect (current state: {State})", _client.ConnectionState);
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        var executionCycle = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            executionCycle++;
            var correlationId = Guid.NewGuid().ToString("N")[..16];

            using var activity = BotActivitySource.StartBackgroundServiceActivity(
                TracingServiceName,
                executionCycle,
                correlationId);

            UpdateHeartbeat();

            try
            {
                var lifted = await ProcessExpiredBansAsync(stoppingToken);

                BotActivitySource.SetRecordsProcessed(activity, lifted);
                BotActivitySource.SetSuccess(activity);
                ClearError();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while lifting expired temporary bans");
                BotActivitySource.RecordException(activity, ex);
                RecordError(ex);
            }

            var interval = TimeSpan.FromSeconds(Math.Max(1, _options.Value.TempBanExpiryCheckIntervalSeconds));
            await Task.Delay(interval, stoppingToken);
        }

        _logger.LogInformation("Temporary ban expiry service stopping");
    }

    /// <summary>
    /// Lifts every expired temporary ban once. Cases are handled one at a time on one scope, so
    /// they never share a DbContext concurrently.
    /// </summary>
    /// <returns>The number of bans closed this cycle.</returns>
    internal async Task<int> ProcessExpiredBansAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var moderationService = scope.ServiceProvider.GetRequiredService<IModerationService>();

        var expired = (await moderationService.GetExpiredTemporaryActionsAsync(ct))
            .Where(c => c.Type == CaseType.Ban)
            .ToList();

        if (expired.Count == 0)
        {
            _logger.LogTrace("No expired temporary bans");
            return 0;
        }

        _logger.LogInformation("Found {Count} expired temporary bans to lift", expired.Count);

        var closed = 0;
        foreach (var banCase in expired)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                if (await LiftBanAsync(moderationService, banCase, ct))
                {
                    closed++;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Leave the case open so the next cycle retries it.
                _logger.LogWarning(ex,
                    "Failed to lift expired ban case #{CaseNumber} for user {UserId} in guild {GuildId}; will retry",
                    banCase.CaseNumber, banCase.TargetUserId, banCase.GuildId);
            }
        }

        return closed;
    }

    private async Task<bool> LiftBanAsync(IModerationService moderationService, ModerationCase banCase, CancellationToken ct)
    {
        var guild = _getGuild(banCase.GuildId);
        if (guild == null)
        {
            _logger.LogDebug(
                "Guild {GuildId} is unavailable; expired ban case #{CaseNumber} will be retried",
                banCase.GuildId, banCase.CaseNumber);
            return false;
        }

        var reason = $"Temporary ban expired (case #{banCase.CaseNumber})";

        // Unban first, then record the case: if the process stops in between, the ban is gone and
        // the next cycle's unban 404s and closes the case, rather than a banned user with a closed case.
        try
        {
            await guild.RemoveBanAsync(banCase.TargetUserId, new RequestOptions { AuditLogReason = reason, CancelToken = ct });
            _logger.LogInformation(
                "User {UserId} unbanned from guild {GuildId}: temporary ban case #{CaseNumber} expired",
                banCase.TargetUserId, banCase.GuildId, banCase.CaseNumber);
        }
        catch (HttpException ex) when (ex.HttpCode == HttpStatusCode.NotFound)
        {
            _logger.LogInformation(
                "User {UserId} is no longer banned in guild {GuildId}; closing expired ban case #{CaseNumber}",
                banCase.TargetUserId, banCase.GuildId, banCase.CaseNumber);
        }

        var unbanCase = await moderationService.CreateCaseAsync(new ModerationCaseCreateDto
        {
            GuildId = banCase.GuildId,
            TargetUserId = banCase.TargetUserId,
            ModeratorUserId = _getBotUserId(),
            Type = CaseType.Unban,
            Reason = reason
        }, ct);

        _logger.LogInformation(
            "Unban case created: Case #{CaseNumber} for user {TargetId} closing expired ban case #{BanCaseNumber}",
            unbanCase.CaseNumber, banCase.TargetUserId, banCase.CaseNumber);

        return true;
    }
}
