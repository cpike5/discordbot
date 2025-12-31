using Discord.WebSocket;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for managing guild members with business logic, caching, and export capabilities.
/// </summary>
public class GuildMemberService : IGuildMemberService
{
    private readonly IGuildMemberRepository _memberRepository;
    private readonly DiscordSocketClient _client;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GuildMemberService> _logger;
    private readonly CachingOptions _cachingOptions;

    private const string MemberListCacheKeyPrefix = "guildmembers:";
    private const string MemberDetailCacheKeyPrefix = "guildmember:";

    /// <summary>
    /// Initializes a new instance of the <see cref="GuildMemberService"/> class.
    /// </summary>
    /// <param name="memberRepository">The guild member repository.</param>
    /// <param name="client">The Discord socket client.</param>
    /// <param name="cache">The memory cache.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="cachingOptions">The caching configuration options.</param>
    public GuildMemberService(
        IGuildMemberRepository memberRepository,
        DiscordSocketClient client,
        IMemoryCache cache,
        ILogger<GuildMemberService> logger,
        IOptions<CachingOptions> cachingOptions)
    {
        _memberRepository = memberRepository;
        _client = client;
        _cache = cache;
        _logger = logger;
        _cachingOptions = cachingOptions.Value;
    }

    /// <inheritdoc />
    public async Task<PaginatedResponseDto<GuildMemberDto>> GetMembersAsync(
        ulong guildId,
        GuildMemberQueryDto query,
        CancellationToken cancellationToken = default)
    {
        // Validate input
        if (query.Page < 1)
        {
            throw new ArgumentException("Page must be greater than 0", nameof(query));
        }

        if (query.PageSize < 1 || query.PageSize > 100)
        {
            throw new ArgumentException("PageSize must be between 1 and 100", nameof(query));
        }

        _logger.LogDebug(
            "Getting members for guild {GuildId} with query: SearchTerm={SearchTerm}, Page={Page}, PageSize={PageSize}",
            guildId, query.SearchTerm, query.Page, query.PageSize);

        // Generate cache key based on query parameters
        var cacheKey = GenerateCacheKey(guildId, query);

        // Try to get from cache
        if (_cache.TryGetValue(cacheKey, out PaginatedResponseDto<GuildMemberDto>? cachedResult) && cachedResult != null)
        {
            _logger.LogTrace("Retrieved member list for guild {GuildId} from cache", guildId);
            return cachedResult;
        }

        // Fetch from repository
        var (members, totalCount) = await _memberRepository.GetMembersAsync(
            guildId,
            query.SearchTerm,
            query.RoleIds,
            query.JoinedAtStart,
            query.JoinedAtEnd,
            query.LastActiveAtStart,
            query.LastActiveAtEnd,
            query.IsActive,
            query.SortBy,
            query.SortDescending,
            query.Page,
            query.PageSize,
            cancellationToken);

        // Get Discord guild for role information
        var guild = _client.GetGuild(guildId);

        // Map to DTOs
        var memberDtos = members.Select(m => MapToDto(m, guild)).ToList();

        var result = new PaginatedResponseDto<GuildMemberDto>
        {
            Items = memberDtos,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };

        // Cache the result
        _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_cachingOptions.GuildMemberListDurationMinutes)
        });

        _logger.LogInformation(
            "Retrieved {Count} of {TotalCount} members for guild {GuildId}",
            memberDtos.Count, totalCount, guildId);

        return result;
    }

    /// <inheritdoc />
    public async Task<GuildMemberDto?> GetMemberAsync(
        ulong guildId,
        ulong userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting member {UserId} for guild {GuildId}", userId, guildId);

        var cacheKey = $"{MemberDetailCacheKeyPrefix}{guildId}:{userId}";

        // Try to get from cache
        if (_cache.TryGetValue(cacheKey, out GuildMemberDto? cachedMember) && cachedMember != null)
        {
            _logger.LogTrace("Retrieved member {UserId} for guild {GuildId} from cache", userId, guildId);
            return cachedMember;
        }

        // Fetch from repository
        var member = await _memberRepository.GetMemberAsync(guildId, userId, cancellationToken);

        if (member == null)
        {
            _logger.LogDebug("Member {UserId} not found in guild {GuildId}", userId, guildId);
            return null;
        }

        // Get Discord guild for role information
        var guild = _client.GetGuild(guildId);

        var memberDto = MapToDto(member, guild);

        // Cache the result
        _cache.Set(cacheKey, memberDto, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_cachingOptions.GuildMemberDetailDurationMinutes)
        });

        _logger.LogDebug("Retrieved member {UserId} for guild {GuildId}", userId, guildId);

        return memberDto;
    }

    /// <inheritdoc />
    public async Task<int> GetMemberCountAsync(
        ulong guildId,
        GuildMemberQueryDto query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting member count for guild {GuildId}", guildId);

        // If no filters are applied, use the simple repository method
        if (string.IsNullOrWhiteSpace(query.SearchTerm) &&
            (query.RoleIds == null || !query.RoleIds.Any()) &&
            !query.JoinedAtStart.HasValue &&
            !query.JoinedAtEnd.HasValue &&
            !query.LastActiveAtStart.HasValue &&
            !query.LastActiveAtEnd.HasValue)
        {
            var activeOnly = query.IsActive ?? true;
            return await _memberRepository.GetMemberCountAsync(guildId, activeOnly, cancellationToken);
        }

        // For filtered queries, we need to run the full query to get an accurate count
        // This is less efficient but necessary when filters are applied
        var (_, totalCount) = await _memberRepository.GetMembersAsync(
            guildId,
            query.SearchTerm,
            query.RoleIds,
            query.JoinedAtStart,
            query.JoinedAtEnd,
            query.LastActiveAtStart,
            query.LastActiveAtEnd,
            query.IsActive,
            query.SortBy,
            query.SortDescending,
            page: 1,
            pageSize: 1, // Just get one item to get the total count
            cancellationToken);

        _logger.LogDebug("Member count for guild {GuildId}: {Count}", guildId, totalCount);

        return totalCount;
    }

    /// <inheritdoc />
    public async Task<byte[]> ExportMembersToCsvAsync(
        ulong guildId,
        GuildMemberQueryDto query,
        int maxRows = 10000,
        CancellationToken cancellationToken = default)
    {
        if (maxRows < 1 || maxRows > 100000)
        {
            throw new ArgumentException("maxRows must be between 1 and 100,000", nameof(maxRows));
        }

        _logger.LogInformation("Exporting members to CSV for guild {GuildId}, maxRows: {MaxRows}", guildId, maxRows);

        // Override query pagination to get all results up to maxRows
        query.Page = 1;
        query.PageSize = maxRows;

        // Fetch members
        var (members, totalCount) = await _memberRepository.GetMembersAsync(
            guildId,
            query.SearchTerm,
            query.RoleIds,
            query.JoinedAtStart,
            query.JoinedAtEnd,
            query.LastActiveAtStart,
            query.LastActiveAtEnd,
            query.IsActive,
            query.SortBy,
            query.SortDescending,
            query.Page,
            query.PageSize,
            cancellationToken);

        if (members.Count == 0)
        {
            _logger.LogWarning("No members found for export in guild {GuildId}", guildId);
            throw new InvalidOperationException("No members found matching the specified criteria");
        }

        // Get Discord guild for role information
        var guild = _client.GetGuild(guildId);

        // Build CSV
        var csv = new StringBuilder();

        // CSV Header
        csv.AppendLine("UserId,Username,Discriminator,GlobalDisplayName,Nickname,DisplayName,JoinedAt,LastActiveAt,AccountCreatedAt,RoleIds,RoleNames,IsActive");

        // CSV Rows
        foreach (var member in members)
        {
            var dto = MapToDto(member, guild);

            csv.Append(CsvEscape(dto.UserId.ToString())).Append(',');
            csv.Append(CsvEscape(dto.Username)).Append(',');
            csv.Append(CsvEscape(dto.Discriminator)).Append(',');
            csv.Append(CsvEscape(dto.GlobalDisplayName ?? string.Empty)).Append(',');
            csv.Append(CsvEscape(dto.Nickname ?? string.Empty)).Append(',');
            csv.Append(CsvEscape(dto.DisplayName)).Append(',');
            csv.Append(CsvEscape(dto.JoinedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))).Append(',');
            csv.Append(CsvEscape(dto.LastActiveAt?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty)).Append(',');
            csv.Append(CsvEscape(dto.AccountCreatedAt?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty)).Append(',');
            csv.Append(CsvEscape(string.Join(";", dto.RoleIds))).Append(',');
            csv.Append(CsvEscape(string.Join(";", dto.Roles.Select(r => r.Name)))).Append(',');
            csv.Append(CsvEscape(dto.IsActive.ToString()));
            csv.AppendLine();
        }

        _logger.LogInformation(
            "Exported {Count} of {TotalCount} members to CSV for guild {GuildId}",
            members.Count, totalCount, guildId);

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    /// <summary>
    /// Maps a GuildMember entity to a GuildMemberDto with role information from Discord.
    /// </summary>
    /// <param name="member">The guild member entity.</param>
    /// <param name="guild">The Discord guild (may be null if bot is not connected).</param>
    /// <returns>The guild member DTO.</returns>
    private GuildMemberDto MapToDto(Core.Entities.GuildMember member, SocketGuild? guild)
    {
        var dto = new GuildMemberDto
        {
            UserId = member.UserId,
            Username = member.User.Username,
            Discriminator = member.User.Discriminator,
            GlobalDisplayName = member.User.GlobalDisplayName,
            Nickname = member.Nickname,
            AvatarHash = member.User.AvatarHash,
            JoinedAt = member.JoinedAt,
            LastActiveAt = member.LastActiveAt,
            AccountCreatedAt = member.User.AccountCreatedAt,
            IsActive = member.IsActive,
            LastCachedAt = member.LastCachedAt
        };

        // Parse role IDs from cached JSON
        if (!string.IsNullOrWhiteSpace(member.CachedRolesJson))
        {
            try
            {
                var roleIds = JsonSerializer.Deserialize<List<ulong>>(member.CachedRolesJson);
                if (roleIds != null)
                {
                    dto.RoleIds = roleIds;

                    // Fetch role details from Discord if available
                    if (guild != null)
                    {
                        dto.Roles = roleIds
                            .Select(roleId => guild.GetRole(roleId))
                            .Where(role => role != null)
                            .Select(role => new GuildRoleDto
                            {
                                Id = role.Id,
                                Name = role.Name,
                                Color = role.Color.RawValue,
                                Position = role.Position
                            })
                            .ToList();
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex,
                    "Failed to deserialize CachedRolesJson for member {UserId} in guild {GuildId}",
                    member.UserId, member.GuildId);
            }
        }

        return dto;
    }

    /// <summary>
    /// Generates a cache key for member list queries based on all query parameters.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="query">The query parameters.</param>
    /// <returns>A unique cache key string.</returns>
    private string GenerateCacheKey(ulong guildId, GuildMemberQueryDto query)
    {
        // Create a deterministic string representation of the query
        var queryString = $"{guildId}|{query.SearchTerm}|{string.Join(",", query.RoleIds ?? new List<ulong>())}|" +
            $"{query.JoinedAtStart:O}|{query.JoinedAtEnd:O}|{query.LastActiveAtStart:O}|{query.LastActiveAtEnd:O}|" +
            $"{query.IsActive}|{query.SortBy}|{query.SortDescending}|{query.Page}|{query.PageSize}";

        // Hash the query string to create a shorter cache key
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(queryString));
        var hash = Convert.ToHexString(hashBytes)[..16]; // Take first 16 characters

        return $"{MemberListCacheKeyPrefix}{guildId}:{hash}";
    }

    /// <summary>
    /// Escapes a string for CSV format by wrapping in quotes and escaping internal quotes.
    /// </summary>
    /// <param name="value">The value to escape.</param>
    /// <returns>The escaped CSV value.</returns>
    private static string CsvEscape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        // If the value contains comma, quote, or newline, wrap in quotes and escape internal quotes
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
