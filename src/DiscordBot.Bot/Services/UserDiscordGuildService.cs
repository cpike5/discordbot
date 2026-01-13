using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for managing Discord guild memberships captured during OAuth authentication.
/// Stores guild membership data locally to enable guild-based access control without
/// requiring real-time Discord API calls.
/// </summary>
public class UserDiscordGuildService : IUserDiscordGuildService
{
    private readonly BotDbContext _context;
    private readonly ILogger<UserDiscordGuildService> _logger;
    private readonly IInstrumentedCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDiscordTokenService _tokenService;
    private readonly GuildMembershipCacheOptions _cacheOptions;

    private const string CacheKeyPrefix = "userguilds:";

    /// <summary>
    /// Initializes a new instance of the <see cref="UserDiscordGuildService"/> class.
    /// </summary>
    /// <param name="context">The database context for storing guild memberships.</param>
    /// <param name="logger">Logger for diagnostic information.</param>
    /// <param name="cache">Instrumented cache for caching guild memberships.</param>
    /// <param name="httpClientFactory">Factory for creating HTTP clients.</param>
    /// <param name="tokenService">Service for retrieving OAuth access tokens.</param>
    /// <param name="cacheOptions">Configuration options for caching durations.</param>
    public UserDiscordGuildService(
        BotDbContext context,
        ILogger<UserDiscordGuildService> logger,
        IInstrumentedCache cache,
        IHttpClientFactory httpClientFactory,
        IDiscordTokenService tokenService,
        IOptions<GuildMembershipCacheOptions> cacheOptions)
    {
        _context = context;
        _logger = logger;
        _cache = cache;
        _httpClientFactory = httpClientFactory;
        _tokenService = tokenService;
        _cacheOptions = cacheOptions.Value;
    }

    /// <inheritdoc />
    public async Task<int> StoreGuildMembershipsAsync(
        string applicationUserId,
        IEnumerable<DiscordGuildDto> guilds,
        CancellationToken cancellationToken = default)
    {
        var guildList = guilds.ToList();
        _logger.LogDebug("Storing {Count} guild memberships for user {UserId}",
            guildList.Count, applicationUserId);

        try
        {
            // Get existing guild memberships for this user
            var existingMemberships = await _context.UserDiscordGuilds
                .Where(g => g.ApplicationUserId == applicationUserId)
                .ToListAsync(cancellationToken);

            var existingGuildIds = existingMemberships.Select(m => m.GuildId).ToHashSet();
            var newGuildIds = guildList.Select(g => g.Id).ToHashSet();

            // Track counts for logging
            var addedCount = 0;
            var updatedCount = 0;
            var removedCount = 0;

            // Remove memberships for guilds the user has left
            var guildsToRemove = existingMemberships.Where(m => !newGuildIds.Contains(m.GuildId)).ToList();
            if (guildsToRemove.Any())
            {
                _context.UserDiscordGuilds.RemoveRange(guildsToRemove);
                removedCount = guildsToRemove.Count;
                _logger.LogDebug("Removing {Count} stale guild memberships for user {UserId}",
                    removedCount, applicationUserId);
            }

            // Process each guild from OAuth
            foreach (var guild in guildList)
            {
                var existing = existingMemberships.FirstOrDefault(m => m.GuildId == guild.Id);

                if (existing != null)
                {
                    // Update existing membership
                    existing.GuildName = guild.Name;
                    existing.GuildIconHash = guild.Icon;
                    existing.IsOwner = guild.Owner;
                    existing.Permissions = guild.Permissions;
                    existing.LastUpdatedAt = DateTime.UtcNow;
                    updatedCount++;
                }
                else
                {
                    // Add new membership
                    var newMembership = new UserDiscordGuild
                    {
                        Id = Guid.NewGuid(),
                        ApplicationUserId = applicationUserId,
                        GuildId = guild.Id,
                        GuildName = guild.Name,
                        GuildIconHash = guild.Icon,
                        IsOwner = guild.Owner,
                        Permissions = guild.Permissions,
                        CapturedAt = DateTime.UtcNow,
                        LastUpdatedAt = DateTime.UtcNow
                    };
                    _context.UserDiscordGuilds.Add(newMembership);
                    addedCount++;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            // Invalidate cache after updating database
            InvalidateCache(applicationUserId);

            _logger.LogInformation(
                "Stored guild memberships for user {UserId}: {Added} added, {Updated} updated, {Removed} removed",
                applicationUserId, addedCount, updatedCount, removedCount);

            return guildList.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store guild memberships for user {UserId}", applicationUserId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserDiscordGuild>> GetUserGuildsAsync(
        string applicationUserId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{CacheKeyPrefix}{applicationUserId}";

        // Try to get from cache
        if (_cache.TryGetValue<List<UserDiscordGuild>>(cacheKey, out var cached))
        {
            _logger.LogDebug("Cache hit for user guilds: {UserId}", applicationUserId);
            return cached!.AsReadOnly();
        }

        _logger.LogDebug("Cache miss for user guilds: {UserId}", applicationUserId);
        _logger.LogTrace("Retrieving guild memberships for user {UserId} from database", applicationUserId);

        try
        {
            var guilds = await _context.UserDiscordGuilds
                .Where(g => g.ApplicationUserId == applicationUserId)
                .OrderBy(g => g.GuildName)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            _logger.LogDebug("Retrieved {Count} guild memberships for user {UserId} from database",
                guilds.Count, applicationUserId);

            // Cache the result
            var expiration = TimeSpan.FromMinutes(_cacheOptions.StoredGuildMembershipDurationMinutes);
            _cache.Set(cacheKey, guilds, expiration);

            return guilds.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve guild memberships for user {UserId}", applicationUserId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> HasGuildMembershipAsync(
        string applicationUserId,
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Checking guild {GuildId} membership for user {UserId}",
            guildId, applicationUserId);

        try
        {
            // Use cached GetUserGuildsAsync to avoid duplicate database queries
            var guilds = await GetUserGuildsAsync(applicationUserId, cancellationToken);
            var hasMembership = guilds.Any(g => g.GuildId == guildId);

            _logger.LogDebug("User {UserId} {HasMembership} guild {GuildId} membership",
                applicationUserId, hasMembership ? "has" : "does not have", guildId);

            return hasMembership;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check guild {GuildId} membership for user {UserId}",
                guildId, applicationUserId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteUserGuildsAsync(
        string applicationUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting all guild memberships for user {UserId}", applicationUserId);

        try
        {
            var guildsToDelete = await _context.UserDiscordGuilds
                .Where(g => g.ApplicationUserId == applicationUserId)
                .ToListAsync(cancellationToken);

            if (guildsToDelete.Any())
            {
                _context.UserDiscordGuilds.RemoveRange(guildsToDelete);
                await _context.SaveChangesAsync(cancellationToken);

                // Invalidate cache after deletion
                InvalidateCache(applicationUserId);

                _logger.LogInformation("Deleted {Count} guild memberships for user {UserId}",
                    guildsToDelete.Count, applicationUserId);
            }
            else
            {
                _logger.LogDebug("No guild memberships found to delete for user {UserId}", applicationUserId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete guild memberships for user {UserId}", applicationUserId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task RefreshUserGuildsAsync(
        string applicationUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Refreshing guild memberships from Discord API for user {UserId}", applicationUserId);

        try
        {
            // Get access token
            var accessToken = await _tokenService.GetAccessTokenAsync(applicationUserId, cancellationToken);
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("No valid access token found for user {UserId}", applicationUserId);
                return;
            }

            // Create HTTP client and make API request
            var httpClient = _httpClientFactory.CreateClient("Discord");
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.GetAsync("users/@me/guilds", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to fetch guilds from Discord API for user {UserId}, status {StatusCode}: {ReasonPhrase}",
                    applicationUserId, response.StatusCode, response.ReasonPhrase);
                return;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var discordGuilds = JsonSerializer.Deserialize<List<DiscordApiGuildResponse>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (discordGuilds == null)
            {
                _logger.LogWarning("Failed to deserialize Discord guilds response for user {UserId}", applicationUserId);
                return;
            }

            // Map to DTOs
            var guilds = discordGuilds.Select(g => new DiscordGuildDto
            {
                Id = ulong.Parse(g.Id),
                Name = g.Name,
                Icon = g.Icon,
                Owner = g.Owner,
                Permissions = long.Parse(g.Permissions)
            }).ToList();

            // Store guild memberships (this will also invalidate cache)
            var count = await StoreGuildMembershipsAsync(applicationUserId, guilds, cancellationToken);

            _logger.LogInformation(
                "Successfully refreshed {Count} guild memberships for user {UserId}",
                count, applicationUserId);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed while refreshing guilds for user {UserId}", applicationUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh guild memberships for user {UserId}", applicationUserId);
            throw;
        }
    }

    /// <inheritdoc />
    public void InvalidateCache(string applicationUserId)
    {
        var cacheKey = $"{CacheKeyPrefix}{applicationUserId}";
        _cache.Remove(cacheKey);
        _logger.LogDebug("Invalidated guild membership cache for user: {UserId}", applicationUserId);
    }

    // Private classes for Discord API response deserialization
    private class DiscordApiGuildResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public bool Owner { get; set; }
        public string Permissions { get; set; } = "0";
    }
}
