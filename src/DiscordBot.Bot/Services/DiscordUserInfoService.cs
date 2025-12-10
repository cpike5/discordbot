using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http.Headers;
using System.Text.Json;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for retrieving Discord user information via the Discord API.
/// Provides caching to minimize API calls.
/// </summary>
public class DiscordUserInfoService : IDiscordUserInfoService
{
    private readonly IDiscordTokenService _tokenService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DiscordUserInfoService> _logger;

    private const string UserInfoCacheKeyPrefix = "discord:user:";
    private const string UserGuildsCacheKeyPrefix = "discord:guilds:";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscordUserInfoService"/> class.
    /// </summary>
    /// <param name="tokenService">Service for retrieving OAuth access tokens.</param>
    /// <param name="httpClientFactory">Factory for creating HTTP clients.</param>
    /// <param name="cache">Memory cache for caching Discord API responses.</param>
    /// <param name="logger">Logger for diagnostic information.</param>
    public DiscordUserInfoService(
        IDiscordTokenService tokenService,
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<DiscordUserInfoService> logger)
    {
        _tokenService = tokenService;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DiscordUserInfoDto?> GetUserInfoAsync(
        string applicationUserId,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{UserInfoCacheKeyPrefix}{applicationUserId}";

        // Try to get from cache if not forcing refresh
        if (!forceRefresh && _cache.TryGetValue(cacheKey, out DiscordUserInfoDto? cachedUserInfo))
        {
            _logger.LogTrace("Retrieved user info for {UserId} from cache", applicationUserId);
            return cachedUserInfo;
        }

        _logger.LogDebug("Fetching user info from Discord API for user {UserId}", applicationUserId);

        try
        {
            // Get access token
            var accessToken = await _tokenService.GetAccessTokenAsync(applicationUserId, cancellationToken);
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("No valid access token found for user {UserId}", applicationUserId);
                return null;
            }

            // Create HTTP client and make API request
            var httpClient = _httpClientFactory.CreateClient("Discord");
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.GetAsync("users/@me", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Discord API request failed for user {UserId} with status {StatusCode}: {ReasonPhrase}",
                    applicationUserId, response.StatusCode, response.ReasonPhrase);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var discordUser = JsonSerializer.Deserialize<DiscordApiUserResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (discordUser == null)
            {
                _logger.LogError("Failed to deserialize Discord user info response for user {UserId}", applicationUserId);
                return null;
            }

            // Map to DTO
            var userInfo = new DiscordUserInfoDto
            {
                Id = ulong.Parse(discordUser.Id),
                Username = discordUser.Username,
                GlobalName = discordUser.GlobalName,
                AvatarHash = discordUser.Avatar,
                Email = discordUser.Email,
                Verified = discordUser.Verified
            };

            // Cache the result
            _cache.Set(cacheKey, userInfo, new MemoryCacheEntryOptions
            {
                SlidingExpiration = CacheDuration
            });

            _logger.LogInformation("Successfully retrieved and cached user info for {UserId}, Discord user {DiscordUserId}",
                applicationUserId, userInfo.Id);

            return userInfo;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed while fetching user info for user {UserId}", applicationUserId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve user info for user {UserId}", applicationUserId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DiscordGuildDto>> GetUserGuildsAsync(
        string applicationUserId,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{UserGuildsCacheKeyPrefix}{applicationUserId}";

        // Try to get from cache if not forcing refresh
        if (!forceRefresh && _cache.TryGetValue(cacheKey, out IReadOnlyList<DiscordGuildDto>? cachedGuilds))
        {
            _logger.LogTrace("Retrieved guilds for {UserId} from cache ({Count} guilds)",
                applicationUserId, cachedGuilds?.Count ?? 0);
            return cachedGuilds ?? Array.Empty<DiscordGuildDto>();
        }

        _logger.LogDebug("Fetching user guilds from Discord API for user {UserId}", applicationUserId);

        try
        {
            // Get access token
            var accessToken = await _tokenService.GetAccessTokenAsync(applicationUserId, cancellationToken);
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("No valid access token found for user {UserId}", applicationUserId);
                return Array.Empty<DiscordGuildDto>();
            }

            // Create HTTP client and make API request
            var httpClient = _httpClientFactory.CreateClient("Discord");
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.GetAsync("users/@me/guilds", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Discord API request for guilds failed for user {UserId} with status {StatusCode}: {ReasonPhrase}",
                    applicationUserId, response.StatusCode, response.ReasonPhrase);
                return Array.Empty<DiscordGuildDto>();
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var discordGuilds = JsonSerializer.Deserialize<List<DiscordApiGuildResponse>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (discordGuilds == null)
            {
                _logger.LogError("Failed to deserialize Discord guilds response for user {UserId}", applicationUserId);
                return Array.Empty<DiscordGuildDto>();
            }

            // Map to DTOs
            var guilds = discordGuilds.Select(g => new DiscordGuildDto
            {
                Id = ulong.Parse(g.Id),
                Name = g.Name,
                Icon = g.Icon,
                Owner = g.Owner,
                Permissions = long.Parse(g.Permissions)
            }).ToList().AsReadOnly();

            // Cache the result
            _cache.Set(cacheKey, guilds, new MemoryCacheEntryOptions
            {
                SlidingExpiration = CacheDuration
            });

            _logger.LogInformation("Successfully retrieved and cached {Count} guilds for user {UserId}",
                guilds.Count, applicationUserId);

            return guilds;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed while fetching guilds for user {UserId}", applicationUserId);
            return Array.Empty<DiscordGuildDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve guilds for user {UserId}", applicationUserId);
            throw;
        }
    }

    /// <inheritdoc />
    public void InvalidateCache(string applicationUserId)
    {
        var userInfoCacheKey = $"{UserInfoCacheKeyPrefix}{applicationUserId}";
        var guildsCacheKey = $"{UserGuildsCacheKeyPrefix}{applicationUserId}";

        _cache.Remove(userInfoCacheKey);
        _cache.Remove(guildsCacheKey);

        _logger.LogDebug("Invalidated cache for user {UserId}", applicationUserId);
    }

    // Private classes for Discord API response deserialization
    private class DiscordApiUserResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string? GlobalName { get; set; }
        public string? Avatar { get; set; }
        public string? Email { get; set; }
        public bool Verified { get; set; }
    }

    private class DiscordApiGuildResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public bool Owner { get; set; }
        public string Permissions { get; set; } = "0";
    }
}
