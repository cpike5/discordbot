namespace DiscordBot.Bot.Blazor.Guilds;

/// <summary>
/// The three outcomes <see cref="IGuildContextProvider.GetAsync"/> can produce, mirroring the
/// 404 (guild not found)/403 (no <c>GuildAccess</c>)/200 split every guild <c>PageModel</c>
/// handles today. A small discriminated-union-style enum + payload record rather than three
/// derived types, matching this codebase's existing tri-state result shape
/// (<c>Pages/Portal/PortalPageModelBase.PortalAuthResult</c>).
/// </summary>
public enum GuildContextStatus
{
    /// <summary>The guild does not exist (<c>IGuildService.GetGuildByIdAsync</c> returned null).</summary>
    NotFound,

    /// <summary>
    /// The guild exists but the signed-in user failed the <c>GuildAccess</c> authorization policy.
    /// </summary>
    Forbidden,

    /// <summary>The guild exists and the user is authorized; <see cref="GuildContextResult.Context"/> is populated.</summary>
    Ok
}

/// <summary>
/// Result of resolving a guild route to a <see cref="Guilds.GuildContext"/>. Exactly one of the
/// static factories below should be used to construct one; <see cref="Context"/> is non-null iff
/// <see cref="Status"/> is <see cref="GuildContextStatus.Ok"/>.
/// </summary>
public sealed record GuildContextResult(GuildContextStatus Status, GuildContext? Context)
{
    public static GuildContextResult NotFound() => new(GuildContextStatus.NotFound, null);

    public static GuildContextResult Forbidden() => new(GuildContextStatus.Forbidden, null);

    public static GuildContextResult Ok(GuildContext context) => new(GuildContextStatus.Ok, context);
}
