using System.Diagnostics;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Tracing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for UserConsent entities with consent-specific operations.
/// </summary>
public class UserConsentRepository : Repository<UserConsent>, IUserConsentRepository
{
    private readonly ILogger<UserConsentRepository> _logger;
    private const int SlowOperationThresholdMs = 100;

    public UserConsentRepository(
        BotDbContext context,
        ILogger<UserConsentRepository> logger,
        ILogger<Repository<UserConsent>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    public async Task<UserConsent?> GetActiveConsentAsync(
        ulong discordUserId,
        ConsentType type,
        CancellationToken cancellationToken = default)
    {
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName: "GetActiveConsentAsync",
            entityType: nameof(UserConsent),
            dbOperation: "SELECT",
            entityId: $"{discordUserId}:{type}");

        var stopwatch = Stopwatch.StartNew();
        _logger.LogDebug(
            "Retrieving active consent for user {DiscordUserId} and type {ConsentType}",
            discordUserId, type);

        try
        {
            var consent = await DbSet
                .AsNoTracking()
                .Where(c => c.DiscordUserId == discordUserId
                         && c.ConsentType == type
                         && c.RevokedAt == null)
                .OrderByDescending(c => c.GrantedAt)
                .FirstOrDefaultAsync(cancellationToken);

            stopwatch.Stop();

            _logger.LogDebug(
                "Retrieved active consent for user {DiscordUserId} and type {ConsentType} in {ElapsedMs}ms. Found={Found}",
                discordUserId, type, stopwatch.ElapsedMilliseconds, consent != null);

            if (stopwatch.ElapsedMilliseconds > SlowOperationThresholdMs)
            {
                _logger.LogWarning(
                    "UserConsentRepository.GetActiveConsentAsync slow operation. ElapsedMs={ElapsedMs}, Threshold={ThresholdMs}ms, DiscordUserId={DiscordUserId}, ConsentType={ConsentType}",
                    stopwatch.ElapsedMilliseconds, SlowOperationThresholdMs, discordUserId, type);
            }

            InfrastructureActivitySource.CompleteActivity(activity, stopwatch.ElapsedMilliseconds);

            return consent;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            InfrastructureActivitySource.RecordException(activity, ex, stopwatch.ElapsedMilliseconds);

            _logger.LogError(ex,
                "Failed to retrieve active consent for user {DiscordUserId} and type {ConsentType}. ElapsedMs={ElapsedMs}, Error={Error}",
                discordUserId, type, stopwatch.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }

    public async Task<IEnumerable<UserConsent>> GetUserConsentsAsync(
        ulong discordUserId,
        CancellationToken cancellationToken = default)
    {
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName: "GetUserConsentsAsync",
            entityType: nameof(UserConsent),
            dbOperation: "SELECT",
            entityId: discordUserId.ToString());

        var stopwatch = Stopwatch.StartNew();
        _logger.LogDebug("Retrieving all consents for user {DiscordUserId}", discordUserId);

        try
        {
            var consents = await DbSet
                .AsNoTracking()
                .Where(c => c.DiscordUserId == discordUserId)
                .OrderByDescending(c => c.GrantedAt)
                .ToListAsync(cancellationToken);

            stopwatch.Stop();

            _logger.LogDebug(
                "Retrieved {Count} consent records for user {DiscordUserId} in {ElapsedMs}ms",
                consents.Count, discordUserId, stopwatch.ElapsedMilliseconds);

            if (stopwatch.ElapsedMilliseconds > SlowOperationThresholdMs)
            {
                _logger.LogWarning(
                    "UserConsentRepository.GetUserConsentsAsync slow operation. ElapsedMs={ElapsedMs}, Threshold={ThresholdMs}ms, DiscordUserId={DiscordUserId}, Count={Count}",
                    stopwatch.ElapsedMilliseconds, SlowOperationThresholdMs, discordUserId, consents.Count);
            }

            InfrastructureActivitySource.CompleteActivity(activity, stopwatch.ElapsedMilliseconds);

            return consents;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            InfrastructureActivitySource.RecordException(activity, ex, stopwatch.ElapsedMilliseconds);

            _logger.LogError(ex,
                "Failed to retrieve consents for user {DiscordUserId}. ElapsedMs={ElapsedMs}, Error={Error}",
                discordUserId, stopwatch.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }

    public async Task<bool> HasActiveConsentAsync(
        ulong discordUserId,
        ConsentType type,
        CancellationToken cancellationToken = default)
    {
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName: "HasActiveConsentAsync",
            entityType: nameof(UserConsent),
            dbOperation: "EXISTS",
            entityId: $"{discordUserId}:{type}");

        var stopwatch = Stopwatch.StartNew();
        _logger.LogDebug(
            "Checking active consent for user {DiscordUserId} and type {ConsentType}",
            discordUserId, type);

        try
        {
            var hasConsent = await DbSet
                .AsNoTracking()
                .AnyAsync(c => c.DiscordUserId == discordUserId
                            && c.ConsentType == type
                            && c.RevokedAt == null,
                    cancellationToken);

            stopwatch.Stop();

            _logger.LogDebug(
                "Checked active consent for user {DiscordUserId} and type {ConsentType} in {ElapsedMs}ms. HasConsent={HasConsent}",
                discordUserId, type, stopwatch.ElapsedMilliseconds, hasConsent);

            if (stopwatch.ElapsedMilliseconds > SlowOperationThresholdMs)
            {
                _logger.LogWarning(
                    "UserConsentRepository.HasActiveConsentAsync slow operation. ElapsedMs={ElapsedMs}, Threshold={ThresholdMs}ms, DiscordUserId={DiscordUserId}, ConsentType={ConsentType}",
                    stopwatch.ElapsedMilliseconds, SlowOperationThresholdMs, discordUserId, type);
            }

            InfrastructureActivitySource.CompleteActivity(activity, stopwatch.ElapsedMilliseconds);

            return hasConsent;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            InfrastructureActivitySource.RecordException(activity, ex, stopwatch.ElapsedMilliseconds);

            _logger.LogError(ex,
                "Failed to check active consent for user {DiscordUserId} and type {ConsentType}. ElapsedMs={ElapsedMs}, Error={Error}",
                discordUserId, type, stopwatch.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }

    public async Task<IEnumerable<ulong>> GetUsersWithActiveConsentAsync(
        IEnumerable<ulong> discordUserIds,
        ConsentType type,
        CancellationToken cancellationToken = default)
    {
        var userIdsList = discordUserIds.ToList();

        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName: "GetUsersWithActiveConsentAsync",
            entityType: nameof(UserConsent),
            dbOperation: "SELECT",
            entityId: $"Batch:{type}:Count={userIdsList.Count}");

        var stopwatch = Stopwatch.StartNew();
        _logger.LogDebug(
            "Batch checking active consent for {Count} users and type {ConsentType}",
            userIdsList.Count, type);

        try
        {
            var usersWithConsent = await DbSet
                .AsNoTracking()
                .Where(c => userIdsList.Contains(c.DiscordUserId)
                         && c.ConsentType == type
                         && c.RevokedAt == null)
                .Select(c => c.DiscordUserId)
                .Distinct()
                .ToListAsync(cancellationToken);

            stopwatch.Stop();

            _logger.LogDebug(
                "Batch checked active consent for {TotalCount} users and type {ConsentType} in {ElapsedMs}ms. Found={FoundCount}",
                userIdsList.Count, type, stopwatch.ElapsedMilliseconds, usersWithConsent.Count);

            if (stopwatch.ElapsedMilliseconds > SlowOperationThresholdMs)
            {
                _logger.LogWarning(
                    "UserConsentRepository.GetUsersWithActiveConsentAsync slow operation. ElapsedMs={ElapsedMs}, Threshold={ThresholdMs}ms, ConsentType={ConsentType}, TotalCount={TotalCount}, FoundCount={FoundCount}",
                    stopwatch.ElapsedMilliseconds, SlowOperationThresholdMs, type, userIdsList.Count, usersWithConsent.Count);
            }

            InfrastructureActivitySource.CompleteActivity(activity, stopwatch.ElapsedMilliseconds);

            return usersWithConsent;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            InfrastructureActivitySource.RecordException(activity, ex, stopwatch.ElapsedMilliseconds);

            _logger.LogError(ex,
                "Failed to batch check active consent for {Count} users and type {ConsentType}. ElapsedMs={ElapsedMs}, Error={Error}",
                userIdsList.Count, type, stopwatch.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }
}
