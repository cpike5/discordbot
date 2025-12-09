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
        _logger.LogDebug("Querying command logs with filters: GuildId={GuildId}, UserId={UserId}, CommandName={CommandName}, Page={Page}, PageSize={PageSize}",
            query.GuildId, query.UserId, query.CommandName, query.Page, query.PageSize);

        // Validate pagination parameters
        if (query.Page < 1)
        {
            query.Page = 1;
        }

        if (query.PageSize < 1 || query.PageSize > 100)
        {
            query.PageSize = 50;
        }

        // Get all logs and apply filters in memory
        // Note: For production, this should be done at the database level with proper filtering
        var allLogs = await _commandLogRepository.GetAllAsync(cancellationToken);

        var filteredLogs = allLogs.AsEnumerable();

        if (query.GuildId.HasValue)
        {
            filteredLogs = filteredLogs.Where(l => l.GuildId == query.GuildId.Value);
        }

        if (query.UserId.HasValue)
        {
            filteredLogs = filteredLogs.Where(l => l.UserId == query.UserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.CommandName))
        {
            filteredLogs = filteredLogs.Where(l => l.CommandName.Equals(query.CommandName, StringComparison.OrdinalIgnoreCase));
        }

        if (query.StartDate.HasValue)
        {
            filteredLogs = filteredLogs.Where(l => l.ExecutedAt >= query.StartDate.Value);
        }

        if (query.EndDate.HasValue)
        {
            filteredLogs = filteredLogs.Where(l => l.ExecutedAt <= query.EndDate.Value);
        }

        if (query.SuccessOnly.HasValue)
        {
            filteredLogs = filteredLogs.Where(l => l.Success == query.SuccessOnly.Value);
        }

        var totalCount = filteredLogs.Count();

        var pagedLogs = filteredLogs
            .OrderByDescending(l => l.ExecutedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(MapToDto)
            .ToList();

        _logger.LogInformation("Retrieved {Count} of {TotalCount} command logs (Page {Page}/{TotalPages})",
            pagedLogs.Count, totalCount, query.Page, (int)Math.Ceiling((double)totalCount / query.PageSize));

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
