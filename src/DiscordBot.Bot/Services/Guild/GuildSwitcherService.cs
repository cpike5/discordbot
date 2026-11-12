using System.Security.Claims;
using DiscordBot.Bot.Authorization;
using DiscordBot.Core.Authorization;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Lists the guilds a user can open: the active guilds for which the <c>GuildAccess</c> policy's
/// <see cref="GuildAccessRequirement"/> succeeds.
/// </summary>
/// <remarks>
/// <para>
/// Rather than restate the access rules, every candidate guild is put to
/// <see cref="IAuthorizationService"/> with the guild ID as the resource, exactly as a page outside a
/// guild route asks about one guild. Whatever handler enforces the requirement
/// (<see cref="GuildAccessHandler"/> today) therefore decides the list too. SuperAdmins pass that
/// handler for every guild, so they skip the per-guild calls.
/// </para>
/// <para>
/// Results are cached per user for <see cref="CacheDuration"/>, so the guild header and the sidebar
/// cost nothing extra on most page loads. The list only decides what is offered; the guild pages
/// still authorize every request.
/// </para>
/// </remarks>
public class GuildSwitcherService : IGuildSwitcherService
{
    /// <summary>How long one user's list is cached.</summary>
    public static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);

    private readonly IGuildService _guildService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GuildSwitcherService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GuildSwitcherService"/> class.
    /// </summary>
    public GuildSwitcherService(
        IGuildService guildService,
        IAuthorizationService authorizationService,
        IMemoryCache cache,
        ILogger<GuildSwitcherService> logger)
    {
        _guildService = guildService;
        _authorizationService = authorizationService;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<GuildSwitcherEntryDto>> GetAccessibleGuildsAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        if (user.Identity?.IsAuthenticated != true)
            return [];

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return [];

        var isSuperAdmin = user.IsInRole(Roles.SuperAdmin);

        // The role is part of the key, so a role change shows up as soon as the cookie carries it
        var cacheKey = $"guild-switcher:{userId}:{(isSuperAdmin ? "super" : "checked")}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<GuildSwitcherEntryDto>? cached) && cached != null)
            return cached;

        try
        {
            var candidates = (await _guildService.GetAllGuildsAsync(cancellationToken))
                .Where(g => g.IsActive)
                .OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(g => g.Id)
                .ToList();

            var allowed = new List<GuildSwitcherEntryDto>(candidates.Count);
            foreach (var guild in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!isSuperAdmin)
                {
                    var access = await _authorizationService.AuthorizeAsync(user, guild.Id, new GuildAccessRequirement());
                    if (!access.Succeeded)
                        continue;
                }

                allowed.Add(new GuildSwitcherEntryDto(guild.Id, guild.Name, guild.IconUrl));
            }

            IReadOnlyList<GuildSwitcherEntryDto> result = allowed.AsReadOnly();
            _cache.Set(cacheKey, result, CacheDuration);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The switcher is a convenience: a page still renders, with a plain heading, without it
            _logger.LogWarning(ex, "Could not load the guild switcher list for user {UserId}", userId);
            return [];
        }
    }
}
