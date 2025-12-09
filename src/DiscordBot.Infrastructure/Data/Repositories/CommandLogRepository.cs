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

    public CommandLogRepository(BotDbContext context, ILogger<CommandLogRepository> logger) : base(context)
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
            ErrorMessage = errorMessage
        };

        _logger.LogInformation(
            "Logging command execution: {CommandName} by user {UserId} in guild {GuildId}, success: {Success}, response time: {ResponseTimeMs}ms",
            commandName, userId, guildId, success, responseTimeMs);

        await DbSet.AddAsync(commandLog, cancellationToken);
        await Context.SaveChangesAsync(cancellationToken);

        return commandLog;
    }
}
