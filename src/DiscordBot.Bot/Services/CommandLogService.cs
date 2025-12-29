using System.Diagnostics;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for command log retrieval and statistics.
/// </summary>
public class CommandLogService : ICommandLogService
{
    private readonly ICommandLogRepository _commandLogRepository;
    private readonly ILogger<CommandLogService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandLogService"/> class.
    /// </summary>
    /// <param name="commandLogRepository">The command log repository.</param>
    /// <param name="logger">The logger.</param>
    public CommandLogService(
        ICommandLogRepository commandLogRepository,
        ILogger<CommandLogService> logger)
    {
        _commandLogRepository = commandLogRepository;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<PaginatedResponseDto<CommandLogDto>> GetLogsAsync(CommandLogQueryDto query, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Querying command logs with filters: SearchTerm={SearchTerm}, GuildId={GuildId}, UserId={UserId}, CommandName={CommandName}, Page={Page}, PageSize={PageSize}",
            query.SearchTerm, query.GuildId, query.UserId, query.CommandName, query.Page, query.PageSize);

        // Validate pagination parameters
        if (query.Page < 1)
        {
            query.Page = 1;
        }

        if (query.PageSize < 1 || query.PageSize > 100)
        {
            query.PageSize = 50;
        }

        // Execute database query with performance timing
        var stopwatch = Stopwatch.StartNew();

        var (items, totalCount) = await _commandLogRepository.GetFilteredLogsAsync(
            searchTerm: query.SearchTerm,
            guildId: query.GuildId,
            userId: query.UserId,
            commandName: query.CommandName,
            startDate: query.StartDate,
            endDate: query.EndDate,
            successOnly: query.SuccessOnly,
            page: query.Page,
            pageSize: query.PageSize,
            cancellationToken: cancellationToken);

        stopwatch.Stop();
        var queryTimeMs = stopwatch.ElapsedMilliseconds;

        // Log performance metrics
        _logger.LogInformation(
            "Retrieved {Count} of {TotalCount} command logs (Page {Page}/{TotalPages}) in {QueryTimeMs}ms",
            items.Count, totalCount, query.Page, (int)Math.Ceiling((double)totalCount / query.PageSize), queryTimeMs);

        // Warn if query took too long
        if (queryTimeMs > 500)
        {
            _logger.LogWarning(
                "Command log query exceeded 500ms threshold: {QueryTimeMs}ms. Filters: SearchTerm={SearchTerm}, GuildId={GuildId}, UserId={UserId}, CommandName={CommandName}",
                queryTimeMs, query.SearchTerm, query.GuildId, query.UserId, query.CommandName);
        }

        var pagedLogs = items.Select(MapToDto).ToList();

        return new PaginatedResponseDto<CommandLogDto>
        {
            Items = pagedLogs.AsReadOnly(),
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }

    /// <inheritdoc/>
    public async Task<IDictionary<string, int>> GetCommandStatsAsync(DateTime? since = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving command usage statistics since {Since}", since);

        var stats = await _commandLogRepository.GetCommandUsageStatsAsync(since, cancellationToken);

        _logger.LogInformation("Retrieved usage statistics for {Count} commands", stats.Count);

        return stats;
    }

    /// <inheritdoc/>
    public async Task<CommandLogDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving command log with ID {Id}", id);

        var log = await _commandLogRepository.GetByIdAsync(id, cancellationToken);

        if (log is null)
        {
            _logger.LogWarning("Command log with ID {Id} not found", id);
            return null;
        }

        _logger.LogInformation("Retrieved command log {Id} for command {CommandName}", id, log.CommandName);

        return MapToDto(log);
    }

    /// <inheritdoc/>
    public async Task<IDictionary<ulong, int>> GetCommandCountsByGuildAsync(DateTime since, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving command counts by guild since {Since}", since);

        var counts = await _commandLogRepository.GetCommandCountsByGuildAsync(since, cancellationToken);

        _logger.LogInformation("Retrieved command counts for {GuildCount} guilds", counts.Count);

        return counts;
    }

    /// <summary>
    /// Maps a CommandLog entity to a CommandLogDto.
    /// </summary>
    /// <param name="log">The command log entity.</param>
    /// <returns>The mapped CommandLogDto.</returns>
    private static CommandLogDto MapToDto(CommandLog log)
    {
        return new CommandLogDto
        {
            Id = log.Id,
            GuildId = log.GuildId,
            GuildName = log.Guild?.Name,
            UserId = log.UserId,
            Username = log.User?.Username,
            CommandName = log.CommandName,
            Parameters = log.Parameters,
            ExecutedAt = log.ExecutedAt,
            ResponseTimeMs = log.ResponseTimeMs,
            Success = log.Success,
            ErrorMessage = log.ErrorMessage
        };
    }
}
