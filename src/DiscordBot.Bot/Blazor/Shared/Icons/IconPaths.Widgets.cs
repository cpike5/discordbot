namespace DiscordBot.Bot.Blazor.Shared;

/// <summary>
/// Tier 4 (live dashboard widgets) additions to <see cref="IconPaths"/> — 24x24 stroke-outline
/// Heroicons harvested from the widget partials in <c>Pages/Shared/Components/*.cshtml</c> as
/// they're ported (<c>docs/articles/blazor-components.md</c> "Component contract", point 8).
/// Filled (non stroke-outline) icons used by these widgets — the "Now Playing" note, the queue
/// skip icon, the playback stop square — stay as literal inline <c>&lt;svg&gt;</c> markup in the
/// component that uses them, the same exception <c>Badge.razor</c> documents for its own icon.
/// </summary>
public static partial class IconPaths
{
    // ---- Bot status (_BotStatusBanner.cshtml, _BotStatusCard.cshtml, notification bell "BotStatus" type icon) ----

    /// <summary>Shield with a check mark. Online/connected state; also the "BotStatus"
    /// notification type icon in the navbar bell (same path, both places).</summary>
    public const string ShieldCheck = "M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z";

    /// <summary>Shield with an exclamation mark. Offline/disconnected state.</summary>
    public const string ShieldExclamation = "M20.618 5.984A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016zM12 9v2m0 4h.01";

    // ---- Activity feed (_ActivityFeed.cshtml / _ActivityFeedTimeline.cshtml) ----

    /// <summary>Lightning bolt. Live Activity card header.</summary>
    public const string Lightning = "M13 10V3L4 14h7v7l9-11h-7z";

    /// <summary>Pause (two bars). Pause/resume toggle button, paused state.</summary>
    public const string Pause = "M10 9v6m4-6v6";

    /// <summary>Play (triangle). Pause/resume toggle button, playing state.</summary>
    public const string Play = "M14.752 11.168l-3.197-2.132A1 1 0 0010 9.87v4.263a1 1 0 001.555.832l3.197-2.132a1 1 0 000-1.664z";

    /// <summary>Open book/document. Activity feed empty state.</summary>
    public const string BookOpen = "M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2";

    /// <summary>Right chevron. "View all" links.</summary>
    public const string ChevronRight = "M9 5l7 7-7 7";

    /// <summary>Circular refresh arrows. Refresh buttons (Recent Activity, Bot status metrics).</summary>
    public const string Refresh = "M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15";

    // ---- Audit log / command stats (_AuditLogCard.cshtml, _CommandStatsCard.cshtml, _RecentActivityCard.cshtml) ----

    /// <summary>Document with lines. Audit log card header/empty state, also reused for the
    /// generic "document" empty state on command stats.</summary>
    public const string DocumentText = "M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z";

    /// <summary>Bar chart glyph. Command usage card header.</summary>
    public const string ChartBar = "M8 9l3 3-3 3m5 0h3M5 20h14a2 2 0 002-2V6a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z";

    /// <summary>Stacked bars (analytics). Total commands executed metric.</summary>
    public const string ChartBarSquare = "M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z";

    /// <summary>Clock. Recent Activity card header.</summary>
    public const string Clock = "M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z";

    // ---- Connected servers widget (_ConnectedServersWidget.cshtml) ----

    /// <summary>Vertical ellipsis (kebab menu). Per-row action menu trigger.</summary>
    public const string EllipsisVertical = "M12 5v.01M12 12v.01M12 19v.01M12 6a1 1 0 110-2 1 1 0 010 2zm0 7a1 1 0 110-2 1 1 0 010 2zm0 7a1 1 0 110-2 1 1 0 010 2z";

    /// <summary>Eye (view details). Single &lt;path&gt; combining the Heroicon's two subpaths —
    /// safe because both share the same stroke/no-fill styling as one &lt;path&gt; element.</summary>
    public const string Eye = "M15 12a3 3 0 11-6 0 3 3 0 016 0z M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z";

    /// <summary>Two overlapping rectangles (copy/clipboard). "Copy Server ID" action.</summary>
    public const string ClipboardCopy = "M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z";

    /// <summary>Server rack (two stacked bars). Connected Servers empty state.</summary>
    public const string ServerStack = "M5 12h14M5 12a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v4a2 2 0 01-2 2M5 12a2 2 0 00-2 2v4a2 2 0 002 2h14a2 2 0 002-2v-4a2 2 0 00-2-2m-2-4h.01M17 16h.01";

    // ---- Voice channel panel (_VoiceChannelPanel.cshtml) ----

    /// <summary>Microphone in a rounded frame. Voice channel connection status.</summary>
    public const string Microphone = "M19 11a7 7 0 01-7 7m0 0a7 7 0 01-7-7m7 7v4m0 0H8m4 0h4m-4-8a3 3 0 01-3-3V5a3 3 0 116 0v6a3 3 0 01-3 3z";

    /// <summary>Arrow exiting a door frame. "Leave" voice channel button.</summary>
    public const string ArrowRightOnRectangle = "M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1";

    /// <summary>Empty tray/queue. No sounds queued.</summary>
    public const string QueueListEmpty = "M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10";

    // ---- Notification bell (Pages/Shared/_Navbar.cshtml bell markup + notification-bell.js) ----

    /// <summary>Bell. Navbar notification bell button and the notification empty state.</summary>
    public const string Bell = "M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9";

    /// <summary>Checkmark. "Mark all read" button.</summary>
    public const string Check = "M5 13l4 4L19 7";

    /// <summary>Envelope-open check. Per-item "mark as read" action.</summary>
    public const string EnvelopeOpen = "M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z";

    /// <summary>Two overlapping people (users). "GuildEvent" notification type icon.</summary>
    public const string Users = "M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z";

    /// <summary>Angle brackets (code). "CommandError" notification type icon.</summary>
    public const string CodeBracket = "M10 20l4-16m4 4l4 4-4 4M6 16l-4-4 4-4";
}
