using DiscordBot.Bot.Configuration;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Core.DTOs;
using Microsoft.AspNetCore.Routing.Patterns;

namespace DiscordBot.Bot.Helpers;

/// <summary>
/// Where the guild switcher sends the user: the same place in another guild when that place exists
/// for every guild, else the closest thing that does.
/// </summary>
/// <remarks>
/// In order:
/// <list type="number">
///   <item>The current page, when its only route parameter is <c>guildId</c>
///     (<c>/Guilds/TextToSpeech/{A}</c> goes to <c>/Guilds/TextToSpeech/{B}</c>).</item>
///   <item>The active tab's page from <see cref="GuildNavigationConfig"/>, for pages about one entity
///     (<c>/Guilds/ScheduledMessages/Edit/{A}/{id}</c> goes to <c>/Guilds/ScheduledMessages/{B}</c>),
///     since that entity belongs to guild A.</item>
///   <item>The guild's Overview, for anything else.</item>
/// </list>
/// The query string is never carried over: filters can name channels or members of guild A.
/// </remarks>
public static class GuildSwitchTarget
{
    private const string GuildIdParameter = "guildId";
    private const string OverviewTabId = "overview";

    /// <summary>
    /// The URL to open in <paramref name="targetGuildId"/>.
    /// </summary>
    /// <param name="activeTabId">The current page's navigation tab ID (for example <c>audio</c>).</param>
    /// <param name="targetGuildId">The guild to switch to.</param>
    /// <param name="currentRouteTemplate">The current page's route template, as the endpoint's
    /// <c>RoutePattern.RawText</c> gives it (for example <c>Guilds/Soundboard/{guildId:long}</c>).
    /// Null when unknown.</param>
    public static string For(string? activeTabId, ulong targetGuildId, string? currentRouteTemplate = null)
    {
        var samePage = SamePageUrl(currentRouteTemplate, targetGuildId);
        if (samePage != null)
            return samePage;

        var tabs = GuildNavigationConfig.GetTabs();
        var tab = tabs.FirstOrDefault(t => string.Equals(t.Id, activeTabId, StringComparison.OrdinalIgnoreCase))
            ?? tabs.First(t => t.Id == OverviewTabId);
        return tab.GetUrl(targetGuildId);
    }

    /// <summary>
    /// The switcher's options: every guild the user can open, each with the URL
    /// <see cref="For"/> picks, and the current guild marked.
    /// </summary>
    /// <param name="currentGuildId">The guild the page is about.</param>
    /// <param name="activeTabId">The current page's navigation tab ID.</param>
    /// <param name="currentRouteTemplate">The current page's route template, if known.</param>
    /// <param name="guilds">The guilds the user can open, in display order.</param>
    public static IReadOnlyList<GuildSwitcherOption> BuildOptions(
        ulong currentGuildId,
        string? activeTabId,
        string? currentRouteTemplate,
        IReadOnlyList<GuildSwitcherEntryDto> guilds)
    {
        return guilds
            .Select(g => new GuildSwitcherOption
            {
                GuildId = g.Id.ToString(),
                Name = g.Name,
                IconUrl = g.IconUrl,
                Url = For(activeTabId, g.Id, currentRouteTemplate),
                IsCurrent = g.Id == currentGuildId
            })
            .ToList()
            .AsReadOnly();
    }

    // The current page for another guild, or null when the page needs more than a guild ID
    private static string? SamePageUrl(string? routeTemplate, ulong targetGuildId)
    {
        if (string.IsNullOrWhiteSpace(routeTemplate))
            return null;

        RoutePattern pattern;
        try
        {
            pattern = RoutePatternFactory.Parse(routeTemplate);
        }
        catch (RoutePatternException)
        {
            return null;
        }

        if (pattern.Parameters.Count != 1
            || !string.Equals(pattern.Parameters[0].Name, GuildIdParameter, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var segments = new List<string>(pattern.PathSegments.Count);
        foreach (var segment in pattern.PathSegments)
        {
            var text = string.Concat(segment.Parts.Select(part => part switch
            {
                RoutePatternLiteralPart literal => Uri.EscapeDataString(literal.Content),
                RoutePatternSeparatorPart separator => separator.Content,
                RoutePatternParameterPart => targetGuildId.ToString(),
                _ => string.Empty
            }));
            segments.Add(text);
        }

        return "/" + string.Join("/", segments);
    }
}
