using Discord.WebSocket;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service implementation for managing the watchlist of flagged users.
/// Handles adding/removing users from watchlist and querying watchlist entries.
/// </summary>
public class WatchlistService : IWatchlistService
{
    private readonly IWatchlistRepository _watchlistRepository;
    private readonly DiscordSocketClient _client;
    private readonly ILogger<WatchlistService> _logger;

    public WatchlistService(
        IWatchlistRepository watchlistRepository,
        DiscordSocketClient client,
        ILogger<WatchlistService> logger)
    {
        _watchlistRepository = watchlistRepository;
        _client = client;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<WatchlistEntryDto> AddToWatchlistAsync(ulong guildId, ulong userId, string? reason, ulong addedById, CancellationToken ct = default)
    {
        _logger.LogInformation("Adding user {UserId} to watchlist in guild {GuildId} by moderator {AddedById}",
            userId, guildId, addedById);

        // Check if user is already on watchlist
        var existing = await _watchlistRepository.GetByUserAsync(guildId, userId, ct);
        if (existing != null)
        {
            _logger.LogWarning("User {UserId} is already on the watchlist in guild {GuildId}", userId, guildId);
            throw new InvalidOperationException("User is already on the watchlist.");
        }

        var entry = new Watchlist
        {
            Id = Guid.NewGuid(),
            GuildId = guildId,
            UserId = userId,
            AddedByUserId = addedById,
            Reason = reason,
            AddedAt = DateTime.UtcNow
        };

        await _watchlistRepository.AddAsync(entry, ct);

        _logger.LogInformation("User {UserId} added to watchlist successfully in guild {GuildId}", userId, guildId);

        return await MapToDtoAsync(entry, ct);
    }

    /// <inheritdoc/>
    public async Task<bool> RemoveFromWatchlistAsync(ulong guildId, ulong userId, CancellationToken ct = default)
    {
        _logger.LogInformation("Removing user {UserId} from watchlist in guild {GuildId}", userId, guildId);

        var entry = await _watchlistRepository.GetByUserAsync(guildId, userId, ct);
        if (entry == null)
        {
            _logger.LogWarning("User {UserId} is not on the watchlist in guild {GuildId}", userId, guildId);
            return false;
        }

        await _watchlistRepository.DeleteAsync(entry, ct);

        _logger.LogInformation("User {UserId} removed from watchlist successfully in guild {GuildId}", userId, guildId);

        return true;
    }

    /// <inheritdoc/>
    public async Task<(IEnumerable<WatchlistEntryDto> Items, int TotalCount)> GetWatchlistAsync(ulong guildId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        _logger.LogDebug("Retrieving watchlist for guild {GuildId}, page {Page}, size {PageSize}",
            guildId, page, pageSize);

        var (entries, totalCount) = await _watchlistRepository.GetByGuildAsync(guildId, page, pageSize, ct);
        var entriesList = entries.ToList();

        var dtos = new List<WatchlistEntryDto>();
        foreach (var entry in entriesList)
        {
            dtos.Add(await MapToDtoAsync(entry, ct));
        }

        _logger.LogDebug("Retrieved {Count} watchlist entries out of {TotalCount} for guild {GuildId}",
            dtos.Count, totalCount, guildId);

        return (dtos, totalCount);
    }

    /// <inheritdoc/>
    public async Task<bool> IsOnWatchlistAsync(ulong guildId, ulong userId, CancellationToken ct = default)
    {
        _logger.LogDebug("Checking if user {UserId} is on watchlist in guild {GuildId}", userId, guildId);

        var isOnWatchlist = await _watchlistRepository.IsOnWatchlistAsync(guildId, userId, ct);

        _logger.LogDebug("User {UserId} watchlist status in guild {GuildId}: {IsOnWatchlist}",
            userId, guildId, isOnWatchlist);

        return isOnWatchlist;
    }

    /// <inheritdoc/>
    public async Task<WatchlistEntryDto?> GetEntryAsync(ulong guildId, ulong userId, CancellationToken ct = default)
    {
        _logger.LogDebug("Retrieving watchlist entry for user {UserId} in guild {GuildId}", userId, guildId);

        var entry = await _watchlistRepository.GetByUserAsync(guildId, userId, ct);
        if (entry == null)
        {
            _logger.LogWarning("Watchlist entry for user {UserId} not found in guild {GuildId}", userId, guildId);
            return null;
        }

        return await MapToDtoAsync(entry, ct);
    }

    /// <summary>
    /// Maps a Watchlist entity to a DTO with resolved usernames.
    /// </summary>
    private async Task<WatchlistEntryDto> MapToDtoAsync(Watchlist entry, CancellationToken ct = default)
    {
        var username = await GetUsernameAsync(entry.UserId);
        var addedByUsername = await GetUsernameAsync(entry.AddedByUserId);

        return new WatchlistEntryDto
        {
            Id = entry.Id,
            GuildId = entry.GuildId,
            UserId = entry.UserId,
            Username = username,
            AddedByUserId = entry.AddedByUserId,
            AddedByUsername = addedByUsername,
            Reason = entry.Reason,
            AddedAt = entry.AddedAt
        };
    }

    /// <summary>
    /// Resolves a Discord user ID to username.
    /// </summary>
    private async Task<string> GetUsernameAsync(ulong userId)
    {
        try
        {
            var user = await _client.Rest.GetUserAsync(userId);
            return user?.Username ?? $"Unknown#{userId}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve username for user {UserId}", userId);
            return $"Unknown#{userId}";
        }
    }
}
