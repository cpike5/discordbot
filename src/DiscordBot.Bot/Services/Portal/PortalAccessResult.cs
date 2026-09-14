using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.Services.Portal;

/// <summary>
/// The four outcomes <see cref="IPortalAccessService.ResolveAsync"/> can produce - the same
/// three-state gate (plus "the guild doesn't exist at all") that
/// <c>Pages/Portal/PortalPageModelBase.PortalAuthResult</c> has always expressed, extracted here
/// so it isn't duplicated per page model. See "Portal three-state gate" in
/// <c>docs/architecture/patterns.md</c>.
/// </summary>
public enum PortalAccessOutcome
{
    /// <summary>The guild doesn't exist in the database, or isn't visible to the bot's Discord client.</summary>
    GuildNotFound,

    /// <summary>
    /// Anonymous visitor, or a signed-in user with no linked Discord account - show the landing
    /// page (sign-in prompt) rather than the portal.
    /// </summary>
    ShowLanding,

    /// <summary>Signed in with Discord linked, but not a member of this guild (and not an app Admin/SuperAdmin).</summary>
    NotGuildMember,

    /// <summary>Authorized - <see cref="PortalAccessResult.Context"/> is populated and the full portal should render.</summary>
    Authorized
}

/// <summary>
/// Guild data needed by the portal chrome (<c>_PortalHeader</c>/<c>_PortalLanding</c> and their
/// view models) - a deliberately small projection of <see cref="GuildDto"/>, not the Discord.Net
/// <c>SocketGuild</c> the old page-model code also carried (see
/// <c>PortalPageModelBase.PortalAuthContext</c> - it still holds the <c>SocketGuild</c> for the
/// three Portal Index pages that read it directly for voice-channel listing; this record is
/// deliberately narrower).
/// </summary>
/// <param name="Guild">The guild DTO.</param>
/// <param name="GuildIdString">The guild id as a string, for markup/interop (see the Gotchas section of <c>CLAUDE.md</c>).</param>
/// <param name="GuildName">Shorthand for <c>Guild.Name</c>.</param>
/// <param name="IconUrl">Shorthand for <c>Guild.IconUrl</c>.</param>
/// <param name="IsBotOnline">
/// Whether the bot's Discord gateway connection is currently <c>Connected</c> - a global
/// connection-state flag, not specific to this guild (matches
/// <c>PortalPageModelBase.IsOnline</c>'s existing meaning).
/// </param>
public sealed record PortalContext(
    GuildDto Guild,
    string GuildIdString,
    string GuildName,
    string? IconUrl,
    bool IsBotOnline);

/// <summary>
/// Result of <see cref="IPortalAccessService.ResolveAsync"/>. <see cref="Context"/> is non-null
/// for every outcome except <see cref="PortalAccessOutcome.GuildNotFound"/>;
/// <see cref="LoginUrl"/> is non-empty for every outcome except
/// <see cref="PortalAccessOutcome.GuildNotFound"/> too (matching
/// <c>PortalPageModelBase.LoginUrl</c>, which today is only ever left at its empty-string default
/// when the guild isn't found).
/// </summary>
public sealed record PortalAccessResult(PortalAccessOutcome Outcome, PortalContext? Context, string LoginUrl)
{
    public static PortalAccessResult GuildNotFound() => new(PortalAccessOutcome.GuildNotFound, null, string.Empty);

    public static PortalAccessResult ShowLanding(PortalContext context, string loginUrl) =>
        new(PortalAccessOutcome.ShowLanding, context, loginUrl);

    public static PortalAccessResult NotGuildMember(PortalContext context, string loginUrl) =>
        new(PortalAccessOutcome.NotGuildMember, context, loginUrl);

    public static PortalAccessResult Authorized(PortalContext context, string loginUrl) =>
        new(PortalAccessOutcome.Authorized, context, loginUrl);
}
