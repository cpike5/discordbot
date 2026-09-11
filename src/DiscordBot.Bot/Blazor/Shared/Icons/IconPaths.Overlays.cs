namespace DiscordBot.Bot.Blazor.Shared;

/// <summary>
/// Tier 3 (Navigation &amp; Overlays) additions to <see cref="IconPaths"/> - see that class's XML
/// doc for the naming/sourcing convention. Added by <c>GuildContextSelector</c>,
/// <c>RestartBanner</c> and <c>ConfirmModal</c>.
/// </summary>
public static partial class IconPaths
{
    // ---- GuildContextSelector (_GuildContextSelector.cshtml) ----------------------------------

    /// <summary>Home/building outline. The single-guild "Open in {guild}" link and the
    /// multi-guild dropdown trigger.</summary>
    public const string Home = "M2.25 12l8.954-8.955c.44-.439 1.152-.439 1.591 0L21.75 12M4.5 9.75v10.125c0 .621.504 1.125 1.125 1.125H9.75v-4.875c0-.621.504-1.125 1.125-1.125h2.25c.621 0 1.125.504 1.125 1.125V21h4.125c.621 0 1.125-.504 1.125-1.125V9.75M8.25 21h8.25";

    /// <summary>Rightward arrow. The single-guild "Open in {guild}" link's trailing icon.</summary>
    public const string ArrowRight = "M13 7l5 5m0 0l-5 5m5-5H6";

    // ---- RestartBanner (_RestartBanner.cshtml) -------------------------------------------------

    /// <summary>Circular refresh arrows. The "Go to Bot Control" action link.</summary>
    public const string ArrowPath = "M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15";
}
