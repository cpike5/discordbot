using Discord.WebSocket;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for guild operations and settings management.
/// </summary>
public class GuildService : IGuildService
{
    private readonly IGuildRepository _guildRepository;
    private readonly DiscordSocketClient _client;
    private readonly ILogger<GuildService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GuildService"/> class.
    /// </summary>
    /// <param name="guildRepository">The guild repository.</param>
    /// <param name="client">The Discord socket client.</param>
    /// <param name="logger">The logger.</param>
    public GuildService(
        IGuildRepository guildRepository,
        DiscordSocketClient client,
        ILogger<GuildService> logger)
    {
        _guildRepository = guildRepository;
        _client = client;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<GuildDto>> GetAllGuildsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving all guilds with merged Discord and database data");

        var dbGuilds = await _guildRepository.GetAllAsync(cancellationToken);
        var dbGuildIds = dbGuilds.Select(g => g.Id).ToHashSet();
        var guilds = new List<GuildDto>();

        // Add guilds from database, enriched with live Discord data
        foreach (var dbGuild in dbGuilds)
        {
            var discordGuild = _client.GetGuild(dbGuild.Id);
            guilds.Add(MapToDto(dbGuild, discordGuild));
        }

        // Add guilds from Discord client that aren't in the database yet
        foreach (var discordGuild in _client.Guilds)
        {
            if (!dbGuildIds.Contains(discordGuild.Id))
            {
                guilds.Add(new GuildDto
                {
                    Id = discordGuild.Id,
                    Name = discordGuild.Name,
                    JoinedAt = discordGuild.CurrentUser?.JoinedAt?.UtcDateTime ?? DateTime.UtcNow,
                    IsActive = true,
                    MemberCount = discordGuild.MemberCount,
                    IconUrl = discordGuild.IconUrl
                });
            }
        }

        _logger.LogInformation("Retrieved {Count} guilds", guilds.Count);

        return guilds.AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task<PaginatedResponseDto<GuildDto>> GetGuildsAsync(
        GuildSearchQueryDto query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving guilds with query: Search={Search}, IsActive={IsActive}, " +
            "Page={Page}, PageSize={PageSize}, SortBy={SortBy}, SortDesc={SortDesc}",
            query.SearchTerm, query.IsActive, query.Page, query.PageSize,
            query.SortBy, query.SortDescending);

        // Get all guilds first (using existing method)
        var allGuilds = await GetAllGuildsAsync(cancellationToken);

        // Apply filters
        var filtered = allGuilds.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var searchLower = query.SearchTerm.ToLowerInvariant();
            filtered = filtered.Where(g =>
                g.Name.ToLowerInvariant().Contains(searchLower) ||
                g.Id.ToString().Contains(searchLower));
        }

        if (query.IsActive.HasValue)
        {
            filtered = filtered.Where(g => g.IsActive == query.IsActive.Value);
        }

        // Apply sorting
        filtered = query.SortBy?.ToLowerInvariant() switch
        {
            "membercount" => query.SortDescending
                ? filtered.OrderByDescending(g => g.MemberCount ?? 0)
                : filtered.OrderBy(g => g.MemberCount ?? 0),
            "joinedat" => query.SortDescending
                ? filtered.OrderByDescending(g => g.JoinedAt)
                : filtered.OrderBy(g => g.JoinedAt),
            _ => query.SortDescending
                ? filtered.OrderByDescending(g => g.Name)
                : filtered.OrderBy(g => g.Name)
        };

        var filteredList = filtered.ToList();
        var totalCount = filteredList.Count;

        // Apply pagination
        var items = filteredList
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        _logger.LogInformation("Retrieved {Count} of {Total} guilds for page {Page}",
            items.Count, totalCount, query.Page);

        return new PaginatedResponseDto<GuildDto>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }

    /// <inheritdoc/>
    public async Task<GuildDto?> GetGuildByIdAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving guild {GuildId}", guildId);

        var dbGuild = await _guildRepository.GetByDiscordIdAsync(guildId, cancellationToken);
        if (dbGuild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found in database", guildId);
            return null;
        }

        var discordGuild = _client.GetGuild(guildId);
        var dto = MapToDto(dbGuild, discordGuild);

        _logger.LogDebug("Retrieved guild {GuildId}: {GuildName}", guildId, dto.Name);

        return dto;
    }

    /// <inheritdoc/>
    public async Task<GuildDto?> UpdateGuildAsync(ulong guildId, GuildUpdateRequestDto request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating guild {GuildId} with request: Prefix={Prefix}, IsActive={IsActive}",
            guildId, request.Prefix, request.IsActive);

        var dbGuild = await _guildRepository.GetByDiscordIdAsync(guildId, cancellationToken);
        if (dbGuild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found in database, cannot update", guildId);
            return null;
        }

        // Apply updates only for non-null fields
        if (request.Prefix != null)
        {
            dbGuild.Prefix = request.Prefix;
        }

        if (request.Settings != null)
        {
            dbGuild.Settings = request.Settings;
        }

        if (request.IsActive.HasValue)
        {
            dbGuild.IsActive = request.IsActive.Value;
        }

        await _guildRepository.UpdateAsync(dbGuild, cancellationToken);

        _logger.LogInformation("Guild {GuildId} updated successfully", guildId);

        var discordGuild = _client.GetGuild(guildId);
        return MapToDto(dbGuild, discordGuild);
    }

    /// <inheritdoc/>
    public async Task<bool> SyncGuildAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Syncing guild {GuildId} from Discord to database", guildId);

        var discordGuild = _client.GetGuild(guildId);
        if (discordGuild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found in Discord client, cannot sync", guildId);
            return false;
        }

        var guild = new Guild
        {
            Id = discordGuild.Id,
            Name = discordGuild.Name,
            JoinedAt = discordGuild.CurrentUser?.JoinedAt?.UtcDateTime ?? DateTime.UtcNow,
            IsActive = true
        };

        await _guildRepository.UpsertAsync(guild, cancellationToken);

        _logger.LogInformation("Guild {GuildId} synced successfully: {GuildName}", guildId, guild.Name);

        return true;
    }

    /// <inheritdoc/>
    public async Task<int> SyncAllGuildsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Syncing all connected guilds from Discord to database");

        var connectedGuilds = _client.Guilds;
        if (connectedGuilds.Count == 0)
        {
            _logger.LogWarning("No guilds connected to sync");
            return 0;
        }

        var syncedCount = 0;

        foreach (var discordGuild in connectedGuilds)
        {
            try
            {
                var guild = new Guild
                {
                    Id = discordGuild.Id,
                    Name = discordGuild.Name,
                    JoinedAt = discordGuild.CurrentUser?.JoinedAt?.UtcDateTime ?? DateTime.UtcNow,
                    IsActive = true
                };

                await _guildRepository.UpsertAsync(guild, cancellationToken);
                syncedCount++;

                _logger.LogDebug("Synced guild {GuildId}: {GuildName}", guild.Id, guild.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync guild {GuildId}: {GuildName}", discordGuild.Id, discordGuild.Name);
            }
        }

        _logger.LogInformation("Synced {SyncedCount} of {TotalCount} guilds successfully", syncedCount, connectedGuilds.Count);

        return syncedCount;
    }

    /// <summary>
    /// Maps a Guild entity and optional Discord guild to a GuildDto.
    /// </summary>
    /// <param name="dbGuild">The database guild entity.</param>
    /// <param name="discordGuild">The optional Discord guild from the client.</param>
    /// <returns>The mapped GuildDto.</returns>
    private static GuildDto MapToDto(Guild dbGuild, SocketGuild? discordGuild)
    {
        return new GuildDto
        {
            Id = dbGuild.Id,
            Name = discordGuild?.Name ?? dbGuild.Name,
            JoinedAt = dbGuild.JoinedAt,
            IsActive = dbGuild.IsActive,
            Prefix = dbGuild.Prefix,
            Settings = dbGuild.Settings,
            MemberCount = discordGuild?.MemberCount,
            IconUrl = discordGuild?.IconUrl
        };
    }
}
