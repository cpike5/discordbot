using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for CommandLog entities with audit-specific operations.
/// </summary>
public class CommandLogRepository : Repository<CommandLog>, ICommandLogRepository
{
    private readonly ILogger<CommandLogRepository> _logger;

    public CommandLogRepository(BotDbContext context, ILogger<CommandLogRepository> logger, ILogger<Repository<CommandLog>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<CommandLog>> GetByGuildAsync(
        ulong guildId,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving {Limit} command logs for guild {GuildId}", limit, guildId);

        return await DbSet
            .Where(c => c.GuildId == guildId)
            .OrderByDescending(c => c.ExecutedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CommandLog>> GetByUserAsync(
        ulong userId,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving {Limit} command logs for user {UserId}", limit, userId);

        return await DbSet
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.ExecutedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CommandLog>> GetByCommandNameAsync(
        string commandName,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving {Limit} command logs for command {CommandName}", limit, commandName);

        return await DbSet
            .Where(c => c.CommandName == commandName)
            .OrderByDescending(c => c.ExecutedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CommandLog>> GetByDateRangeAsync(
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving command logs between {StartDate} and {EndDate}", start, end);

        return await DbSet
            .Where(c => c.ExecutedAt >= start && c.ExecutedAt <= end)
            .OrderByDescending(c => c.ExecutedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CommandLog>> GetFailedCommandsAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving {Limit} failed command logs", limit);

        return await DbSet
            .Where(c => !c.Success)
            .OrderByDescending(c => c.ExecutedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IDictionary<string, int>> GetCommandUsageStatsAsync(
        DateTime? since = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving command usage statistics since {Since}", since);

        var query = DbSet.AsQueryable();

        if (since.HasValue)
        {
            query = query.Where(c => c.ExecutedAt >= since.Value);
        }

        var stats = await query
            .GroupBy(c => c.CommandName)
            .Select(g => new { CommandName = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CommandName, x => x.Count, cancellationToken);

        _logger.LogInformation("Retrieved usage statistics for {CommandCount} commands", stats.Count);
        return stats;
    }

    public async Task<CommandLog> LogCommandAsync(
        ulong? guildId,
        ulong userId,
        string commandName,
        string? parameters,
        int responseTimeMs,
        bool success,
        string? errorMessage = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var commandLog = new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = guildId,
            UserId = userId,
            CommandName = commandName,
            Parameters = parameters,
            ExecutedAt = DateTime.UtcNow,
            ResponseTimeMs = responseTimeMs,
            Success = success,
            ErrorMessage = errorMessage,
            CorrelationId = correlationId
        };

        _logger.LogInformation(
            "Logging command execution: {CommandName} by user {UserId} in guild {GuildId}, success: {Success}, response time: {ResponseTimeMs}ms, correlationId: {CorrelationId}",
            commandName, userId, guildId, success, responseTimeMs, correlationId);

        await DbSet.AddAsync(commandLog, cancellationToken);
        await Context.SaveChangesAsync(cancellationToken);

        return commandLog;
    }

    public async Task<IReadOnlyList<UsageOverTimeDto>> GetUsageOverTimeAsync(
        DateTime start,
        DateTime end,
        ulong? guildId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving command usage over time from {StartDate} to {EndDate} for guild {GuildId}",
            start, end, guildId);

        var query = DbSet.AsQueryable();

        query = query.Where(l => l.ExecutedAt >= start && l.ExecutedAt < end);

        if (guildId.HasValue)
        {
            query = query.Where(l => l.GuildId == guildId.Value);
        }

        var result = await query
            .GroupBy(l => l.ExecutedAt.Date)
            .Select(g => new UsageOverTimeDto
            {
                Date = g.Key,
                Count = g.Count()
            })
            .OrderBy(x => x.Date)
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Retrieved usage over time data with {DataPointCount} data points", result.Count);
        return result;
    }

    public async Task<CommandSuccessRateDto> GetSuccessRateAsync(
        DateTime? since = null,
        ulong? guildId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving command success rate since {Since} for guild {GuildId}", since, guildId);

        var query = DbSet.AsQueryable();

        if (since.HasValue)
        {
            query = query.Where(l => l.ExecutedAt >= since.Value);
        }

        if (guildId.HasValue)
        {
            query = query.Where(l => l.GuildId == guildId.Value);
        }

        var successCount = await query.CountAsync(l => l.Success, cancellationToken);
        var failureCount = await query.CountAsync(l => !l.Success, cancellationToken);

        var result = new CommandSuccessRateDto
        {
            SuccessCount = successCount,
            FailureCount = failureCount
        };

        _logger.LogInformation("Retrieved success rate: {SuccessCount} successful, {FailureCount} failed, {SuccessRate:F2}%",
            result.SuccessCount, result.FailureCount, result.SuccessRate);

        return result;
    }

    public async Task<IReadOnlyList<CommandPerformanceDto>> GetCommandPerformanceAsync(
        DateTime? since = null,
        ulong? guildId = null,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving command performance metrics since {Since} for guild {GuildId}, limit {Limit}",
            since, guildId, limit);

        var query = DbSet.AsQueryable();

        if (since.HasValue)
        {
            query = query.Where(l => l.ExecutedAt >= since.Value);
        }

        if (guildId.HasValue)
        {
            query = query.Where(l => l.GuildId == guildId.Value);
        }

        var result = await query
            .GroupBy(l => l.CommandName)
            .Select(g => new CommandPerformanceDto
            {
                CommandName = g.Key,
                AvgResponseTimeMs = g.Average(l => l.ResponseTimeMs),
                MinResponseTimeMs = g.Min(l => l.ResponseTimeMs),
                MaxResponseTimeMs = g.Max(l => l.ResponseTimeMs),
                ExecutionCount = g.Count()
            })
            .OrderByDescending(x => x.ExecutionCount)
            .Take(limit)
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Retrieved performance metrics for {CommandCount} commands", result.Count);
        return result;
    }

    public async Task<(IReadOnlyList<CommandLog> Items, int TotalCount)> GetFilteredLogsAsync(
        string? searchTerm = null,
        ulong? guildId = null,
        ulong? userId = null,
        string? commandName = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        bool? successOnly = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving filtered command logs: SearchTerm={SearchTerm}, GuildId={GuildId}, UserId={UserId}, CommandName={CommandName}, StartDate={StartDate}, EndDate={EndDate}, SuccessOnly={SuccessOnly}, Page={Page}, PageSize={PageSize}",
            searchTerm, guildId, userId, commandName, startDate, endDate, successOnly, page, pageSize);

        var query = DbSet
            .Include(l => l.User)
            .Include(l => l.Guild)
            .AsQueryable();

        // Apply search term filter (case-insensitive search across multiple fields)
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var searchLower = searchTerm.ToLowerInvariant();
            query = query.Where(l =>
                l.CommandName.ToLower().Contains(searchLower) ||
                (l.User != null && l.User.Username != null && l.User.Username.ToLower().Contains(searchLower)) ||
                (l.Guild != null && l.Guild.Name != null && l.Guild.Name.ToLower().Contains(searchLower)));
        }

        // Apply guild ID filter
        if (guildId.HasValue)
        {
            query = query.Where(l => l.GuildId == guildId.Value);
        }

        // Apply user ID filter
        if (userId.HasValue)
        {
            query = query.Where(l => l.UserId == userId.Value);
        }

        // Apply command name filter (case-insensitive exact match)
        if (!string.IsNullOrWhiteSpace(commandName))
        {
            var commandNameLower = commandName.ToLowerInvariant();
            query = query.Where(l => l.CommandName.ToLower() == commandNameLower);
        }

        // Apply date range filters
        if (startDate.HasValue)
        {
            query = query.Where(l => l.ExecutedAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(l => l.ExecutedAt <= endDate.Value);
        }

        // Apply success filter
        if (successOnly.HasValue)
        {
            query = query.Where(l => l.Success == successOnly.Value);
        }

        // Get total count for pagination
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply ordering and pagination
        var items = await query
            .OrderByDescending(l => l.ExecutedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} of {TotalCount} filtered command logs", items.Count, totalCount);

        return (items, totalCount);
    }
}
