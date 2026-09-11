using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Models.Llm;

/// <summary>
/// The house catalogue of agent tools: name, category, label and default state, for the settings
/// checklist, the per-tool metrics table and the prompt-surface report.
/// </summary>
/// <remarks>
/// This is deliberately a static table and not a scan of the registry. The settings page has to
/// render a guild's choices without constructing tool providers (which need an API key and a
/// request scope), and an admin needs a description written for a human rather than the one written
/// for the model. A tool missing from the table is not dropped: <see cref="Describe"/> synthesises
/// an entry in the <see cref="OtherCategory"/> bucket so a new tool shows up as unclassified rather
/// than invisible.
/// </remarks>
public static class ToolCatalog
{
    /// <summary>Bucket for a tool with no catalogue entry, so it is visible rather than missing.</summary>
    public const string OtherCategory = "Other";

    private static readonly ToolCatalogEntry[] Entries =
    {
        // Documentation - advertised to both the guild assistant and the DM assistant.
        new("get_feature_documentation", "Documentation", "Read feature documentation",
            "Reads a feature's documentation page so the assistant can explain how it works.",
            ToolScopes.Guild | ToolScopes.Dm),
        new("search_commands", "Documentation", "Search commands",
            "Searches the bot's slash commands by keyword.",
            ToolScopes.Guild | ToolScopes.Dm),
        new("get_command_details", "Documentation", "Command details",
            "Returns the parameters, permissions and usage of one slash command.",
            ToolScopes.Guild | ToolScopes.Dm),
        new("list_features", "Documentation", "List features",
            "Lists the bot's documented features.",
            ToolScopes.Guild | ToolScopes.Dm),

        // Server and user lookups - guild assistant only.
        new("get_user_profile", "Server & Users", "User profile",
            "Looks up a member's profile: join date, roles and display name.",
            ToolScopes.Guild),
        new("get_guild_info", "Server & Users", "Server info",
            "Returns this server's name, member count and channel counts.",
            ToolScopes.Guild),
        new("get_user_roles", "Server & Users", "User roles",
            "Lists the roles a member holds in this server.",
            ToolScopes.Guild),

        // Rat Watch - guild assistant only.
        new("get_rat_watch_leaderboard", "Rat Watch", "Rat Watch leaderboard",
            "Returns this server's Rat Watch leaderboard.",
            ToolScopes.Guild),
        new("get_rat_watch_user_stats", "Rat Watch", "Rat Watch user stats",
            "Returns one member's Rat Watch record.",
            ToolScopes.Guild),
        new("get_rat_watch_summary", "Rat Watch", "Rat Watch summary",
            "Summarises recent Rat Watch activity in this server.",
            ToolScopes.Guild),

        // DM assistant - owner only, not governed by the per-guild allow-list.
        new("save_note", "Memory", "Save note", "Saves a note to the assistant's memory.", ToolScopes.Dm),
        new("search_notes", "Memory", "Search notes", "Searches saved notes.", ToolScopes.Dm),
        new("get_note", "Memory", "Get note", "Reads one saved note.", ToolScopes.Dm),
        new("list_notes", "Memory", "List notes", "Lists saved notes.", ToolScopes.Dm),
        new("delete_note", "Memory", "Delete note", "Deletes a saved note.", ToolScopes.Dm),

        new("clear_conversation", "Conversation", "Clear conversation",
            "Clears the DM conversation history.", ToolScopes.Dm),
        new("summarize_conversation", "Conversation", "Summarise conversation",
            "Summarises the DM conversation so far.", ToolScopes.Dm),

        new("list_guilds", "Bot Management", "List servers",
            "Lists the servers the bot is in.", ToolScopes.Dm),
        new("set_active_guild", "Bot Management", "Set active server",
            "Chooses which server later DM tools operate against.", ToolScopes.Dm),
        new("get_bot_health", "Bot Management", "Bot health",
            "Reports uptime, latency and background-service health.", ToolScopes.Dm),
        new("search_audit_logs", "Bot Management", "Search audit logs",
            "Searches the portal's audit log.", ToolScopes.Dm),

        new("get_moderation_cases", "Moderation", "Moderation cases",
            "Lists recent moderation cases.", ToolScopes.Dm),
        new("get_user_mod_history", "Moderation", "User moderation history",
            "Returns one member's moderation history.", ToolScopes.Dm),

        new("get_server_activity_summary", "Analytics", "Server activity",
            "Summarises message and voice activity for a server.", ToolScopes.Dm),
        new("get_command_analytics", "Analytics", "Command analytics",
            "Reports slash-command usage and failure rates.", ToolScopes.Dm),

        new("execute_python", "Code Execution", "Run Python",
            "Runs a short Python snippet in a sandbox.", ToolScopes.Dm),
        new("fetch_url", "Web", "Fetch URL",
            "Fetches a web page and returns its readable text.", ToolScopes.Dm),

        // Feature requests - its own single-provider registry, neither guild nor DM.
        new("submit_feature_request", "Feature Requests", "Submit feature request",
            "Files a feature request from a guided conversation.", ToolScopes.FeatureRequests)
    };

    private static readonly Dictionary<string, ToolCatalogEntry> ByName =
        Entries.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);

    /// <summary>Every catalogued tool, in declaration order.</summary>
    public static IReadOnlyList<ToolCatalogEntry> All => Entries;

    /// <summary>The catalogued tools advertised on <paramref name="scope"/>.</summary>
    public static IReadOnlyList<ToolCatalogEntry> ForScope(ToolScopes scope) =>
        Entries.Where(e => (e.Scopes & scope) != 0).ToList();

    /// <summary>
    /// The house default set for <paramref name="scope"/> - the tools a guild gets when it has
    /// selected none.
    /// </summary>
    public static IReadOnlySet<string> DefaultsForScope(ToolScopes scope) =>
        Entries
            .Where(e => (e.Scopes & scope) != 0 && e.EnabledByDefault)
            .Select(e => e.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The catalogue entry for <paramref name="toolName"/>, or a synthesised
    /// <see cref="OtherCategory"/> entry when the tool is not catalogued, so an uncatalogued tool
    /// is visibly unclassified rather than silently absent.
    /// </summary>
    public static ToolCatalogEntry Describe(string toolName)
    {
        if (!string.IsNullOrWhiteSpace(toolName) && ByName.TryGetValue(toolName, out var entry))
        {
            return entry;
        }

        var name = toolName ?? string.Empty;
        return new ToolCatalogEntry(name, OtherCategory, name, "Not in the tool catalogue.", ToolScopes.None);
    }

    /// <summary>Whether <paramref name="toolName"/> has a catalogue entry.</summary>
    public static bool IsCatalogued(string toolName) =>
        !string.IsNullOrWhiteSpace(toolName) && ByName.ContainsKey(toolName);

    /// <summary>
    /// Turns a checklist submission into what should be stored for <paramref name="scope"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Names the catalogue does not know, or that belong to a different scope, are dropped — a
    /// stale or hand-crafted form post cannot widen the stored list past what the checklist offered.
    /// </para>
    /// <para>
    /// A selection that is exactly the default set is stored as <em>empty</em>, which is what keeps
    /// "empty means default" honest. The settings page shows the default set ticked when a guild has
    /// chosen nothing, so saving that page untouched posts every default tool back; storing those
    /// names literally would silently pin the guild to today's defaults and leave it behind when a
    /// new tool joins the default set.
    /// </para>
    /// </remarks>
    /// <param name="selected">Tool names ticked in the checklist.</param>
    /// <param name="scope">The scope the checklist covers.</param>
    /// <returns>The list to persist; empty means "use the house default set".</returns>
    public static List<string> NormalizeSelection(IEnumerable<string>? selected, ToolScopes scope)
    {
        var inScope = Entries
            .Where(e => (e.Scopes & scope) != 0)
            .Select(e => e.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var chosen = (selected ?? Enumerable.Empty<string>())
            .Where(name => !string.IsNullOrWhiteSpace(name) && inScope.Contains(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var defaults = DefaultsForScope(scope);

        return chosen.Count == defaults.Count && chosen.All(defaults.Contains)
            ? new List<string>()
            : chosen;
    }
}
