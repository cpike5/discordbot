using System.Security.Claims;
using DiscordBot.Bot.Configuration;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace DiscordBot.Bot.Blazor.Guilds;

/// <summary>
/// Default <see cref="IGuildContextProvider"/>. Reproduces the checks
/// <c>Pages/Guilds/Details.cshtml.cs</c> makes today: load the guild (404 on null), authorize
/// against the consolidated <c>GuildAccess</c> policy the same way a future non-HTTP caller is
/// documented to (<c>Authorization/GuildAccessHandler.cs</c> resolves the guild id from
/// <c>AuthorizationHandlerContext.Resource</c> as a raw <see cref="ulong"/> first, specifically
/// for this caller), then compute <c>CanEdit</c>/feature flags/tabs.
/// </summary>
public sealed class GuildContextProvider : IGuildContextProvider
{
    private readonly IGuildService _guildService;
    private readonly IGuildMembershipService _guildMembershipService;
    private readonly IGuildAudioSettingsService _guildAudioSettingsService;
    private readonly IRatWatchService _ratWatchService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger<GuildContextProvider> _logger;

    // Memoisation per (guildId) for the lifetime of this scope (docs/architecture/patterns.md,
    // "GuildContext"): a guild page and GuildLayout wrapping it both resolve the same guildId
    // within the same request/circuit scope, so the second call is served from here instead of
    // repeating the guild lookup, the authorization check and the two settings lookups. Guarded
    // by a lock because a static SSR layout and its page can both start resolving during the same
    // prerender pass before either awaits.
    private readonly Dictionary<ulong, Task<GuildContextResult>> _cache = new();
    private readonly object _cacheLock = new();

    public GuildContextProvider(
        IGuildService guildService,
        IGuildMembershipService guildMembershipService,
        IGuildAudioSettingsService guildAudioSettingsService,
        IRatWatchService ratWatchService,
        IAuthorizationService authorizationService,
        ILogger<GuildContextProvider> logger)
    {
        _guildService = guildService;
        _guildMembershipService = guildMembershipService;
        _guildAudioSettingsService = guildAudioSettingsService;
        _ratWatchService = ratWatchService;
        _authorizationService = authorizationService;
        _logger = logger;
    }

    public Task<GuildContextResult> GetAsync(ulong guildId, ClaimsPrincipal user, CancellationToken ct = default)
    {
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(guildId, out var cached))
            {
                return cached;
            }

            var task = ResolveAsync(guildId, user, ct);
            _cache[guildId] = task;
            EvictOnFailure(guildId, task);
            return task;
        }
    }

    // A faulted or cancelled resolution must not stick around as the cached entry for the rest of
    // this scope's lifetime - a transient DB/Discord failure would otherwise be served to every
    // later caller for this guild id (regardless of that caller's own CancellationToken), instead
    // of retrying. Evicting only when the cached entry is still this exact task guards against a
    // race where GetAsync has already replaced it (not currently possible - only this method
    // removes entries - but keeps the check correct if that ever changes).
    private void EvictOnFailure(ulong guildId, Task<GuildContextResult> task)
    {
        task.ContinueWith(
            t =>
            {
                _ = t.Exception; // observe the exception so it isn't reported as unobserved
                lock (_cacheLock)
                {
                    if (_cache.TryGetValue(guildId, out var cached) && ReferenceEquals(cached, t))
                    {
                        _cache.Remove(guildId);
                    }
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.NotOnRanToCompletion,
            TaskScheduler.Default);
    }

    private async Task<GuildContextResult> ResolveAsync(ulong guildId, ClaimsPrincipal user, CancellationToken ct)
    {
        var guild = await _guildService.GetGuildByIdAsync(guildId, ct);
        if (guild is null)
        {
            _logger.LogDebug("GuildContext: guild {GuildId} not found", guildId);
            return GuildContextResult.NotFound();
        }

        // Non-HTTP caller: pass the guild id as the authorization Resource, which
        // GuildAccessHandler.ResolveGuildId checks before any route/query value.
        var authResult = await _authorizationService.AuthorizeAsync(user, guildId, "GuildAccess");
        if (!authResult.Succeeded)
        {
            _logger.LogDebug("GuildContext: user denied GuildAccess for guild {GuildId}", guildId);
            return GuildContextResult.Forbidden();
        }

        var isAppAdmin = user.IsInRole("Admin") || user.IsInRole("SuperAdmin");

        var currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var isGuildAdmin = !string.IsNullOrEmpty(currentUserId)
            && await _guildMembershipService.IsGuildAdminAsync(currentUserId, guildId, ct);

        var audioSettings = await _guildAudioSettingsService.GetSettingsAsync(guildId, ct);
        var ratWatchSettings = await _ratWatchService.GetGuildSettingsAsync(guildId, ct);

        var context = new GuildContext(
            Guild: guild,
            GuildId: guildId,
            GuildIdString: guildId.ToString(),
            IsAppAdmin: isAppAdmin,
            IsGuildAdmin: isGuildAdmin,
            CanEdit: isGuildAdmin || isAppAdmin,
            AudioEnabled: audioSettings?.AudioEnabled ?? false,
            RatWatchEnabled: ratWatchSettings?.IsEnabled ?? false,
            Tabs: GuildNavigationConfig.GetTabs());

        _logger.LogDebug(
            "GuildContext: resolved guild {GuildId} (CanEdit={CanEdit}, AudioEnabled={AudioEnabled}, RatWatchEnabled={RatWatchEnabled})",
            guildId, context.CanEdit, context.AudioEnabled, context.RatWatchEnabled);

        return GuildContextResult.Ok(context);
    }
}
