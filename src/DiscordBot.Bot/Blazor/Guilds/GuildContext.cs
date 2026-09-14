using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.Blazor.Guilds;

/// <summary>
/// Everything a guild page or <c>GuildLayout</c> needs to render its chrome, resolved once per
/// guild route by <see cref="IGuildContextProvider"/> instead of the ~27 independent
/// <c>IGuildService.GetGuildByIdAsync</c> + breadcrumb/header/nav builds that
/// <c>Pages/Guilds/GuildPageModelBase.cs</c> repeats today (see
/// <c>docs/plans/blazor-port-plan.md</c> §4.7, "Deferred from Phase 1"). Deliberately a plain,
/// JSON-serializable record - no <see cref="System.Security.Claims.ClaimsPrincipal"/> and no
/// engine/service references - so <see cref="GuildPageBase"/> can round-trip it through
/// <see cref="Microsoft.AspNetCore.Components.PersistentComponentState"/> across the
/// prerender-to-circuit boundary (see "Auth in components" and the new "GuildContext" section in
/// <c>docs/architecture/patterns.md</c> for why that boundary means a fresh DI scope, not just a
/// fresh render).
/// </summary>
/// <param name="Guild">The guild DTO, as returned by <c>IGuildService.GetGuildByIdAsync</c>.</param>
/// <param name="GuildId">The guild's Discord snowflake ID as a <see cref="ulong"/>, for C# logic.</param>
/// <param name="GuildIdString">
/// The same ID as a <see cref="string"/> - the only form that may cross into markup, an
/// <c>href</c>, or a JS interop call (snowflakes silently round-trip incorrectly through
/// JavaScript's <c>Number</c> otherwise; see the Gotchas section of <c>CLAUDE.md</c>).
/// </param>
/// <param name="IsAppAdmin">True when the signed-in user is in the Admin or SuperAdmin application role.</param>
/// <param name="IsGuildAdmin">
/// True when <c>IGuildMembershipService.IsGuildAdminAsync</c> says the user has admin permissions
/// (owner, Administrator, or Manage Guild) in this specific Discord guild.
/// </param>
/// <param name="CanEdit">
/// <c>IsGuildAdmin || IsAppAdmin</c> - the same rule <c>Pages/Guilds/Details.cshtml.cs</c> uses to
/// gate its "Sync"/"Edit Settings" header actions.
/// </param>
/// <param name="AudioEnabled">Whether the soundboard/TTS/VOX features are enabled for this guild (<c>GuildAudioSettings.AudioEnabled</c>).</param>
/// <param name="RatWatchEnabled">Whether the Rat Watch feature is enabled for this guild (<c>GuildRatWatchSettings.IsEnabled</c>).</param>
/// <param name="Tabs">The guild navigation tabs from <c>GuildNavigationConfig.GetTabs()</c>, unfiltered (today's behavior - see the Facts note in the Phase 3 brief).</param>
public sealed record GuildContext(
    GuildDto Guild,
    ulong GuildId,
    string GuildIdString,
    bool IsAppAdmin,
    bool IsGuildAdmin,
    bool CanEdit,
    bool AudioEnabled,
    bool RatWatchEnabled,
    IReadOnlyList<GuildNavItem> Tabs)
{
    /// <summary>
    /// Resolves the URL for one of <see cref="Tabs"/> by its <see cref="GuildNavItem.Id"/> (e.g.
    /// <c>"members"</c>), substituting this context's <see cref="GuildId"/>. Returns an empty
    /// string for an unknown tab id rather than throwing, since it is typically used directly in
    /// markup (<c>href="@Guild.TabUrl("members")"</c>).
    /// </summary>
    public string TabUrl(string tabId)
    {
        var tab = Tabs.FirstOrDefault(t => string.Equals(t.Id, tabId, StringComparison.OrdinalIgnoreCase));
        return tab is null ? string.Empty : tab.GetUrl(GuildId);
    }

    /// <summary>
    /// Builds the guild breadcrumb, reproducing
    /// <c>GuildPageModelBase.BuildBasicBreadcrumb</c>/<c>BuildPageBreadcrumb</c> exactly: Home &gt;
    /// Servers &gt; Guild Name, with an optional trailing page name. Passing <c>null</c> (the
    /// default) reproduces <c>BuildBasicBreadcrumb</c> - the guild name itself is the current
    /// page (used by the Overview/Details page); passing a page name reproduces
    /// <c>BuildPageBreadcrumb</c> - the guild name becomes a link and the page name is current
    /// (used by every other guild page).
    /// </summary>
    public IReadOnlyList<BreadcrumbItem> Breadcrumb(string? pageName = null)
    {
        var guildUrl = $"/Guilds/Details/{GuildId}";

        if (pageName is null)
        {
            return new List<BreadcrumbItem>
            {
                new() { Label = "Home", Url = "/" },
                new() { Label = "Servers", Url = "/Guilds" },
                new() { Label = Guild.Name, Url = guildUrl, IsCurrent = true }
            };
        }

        return new List<BreadcrumbItem>
        {
            new() { Label = "Home", Url = "/" },
            new() { Label = "Servers", Url = "/Guilds" },
            new() { Label = Guild.Name, Url = guildUrl },
            new() { Label = pageName, IsCurrent = true }
        };
    }
}
