// src/DiscordBot.Bot/Pages/Guilds/GuildPageModelBase.cs
using DiscordBot.Bot.Configuration;
using DiscordBot.Bot.ViewModels.Components;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Guilds;

/// <summary>
/// Abstract base class for guild-related pages that use the standardized guild layout.
/// Provides helper properties and methods for building breadcrumbs and navigation.
/// </summary>
public abstract class GuildPageModelBase : PageModel
{
    /// <summary>
    /// Breadcrumb navigation for the page.
    /// </summary>
    public GuildBreadcrumbViewModel Breadcrumb { get; set; } = new();

    /// <summary>
    /// Guild header information including guild icon, title, and actions.
    /// </summary>
    public GuildHeaderViewModel Header { get; set; } = new();

    /// <summary>
    /// Guild navigation bar with tabs.
    /// </summary>
    public GuildNavBarViewModel Navigation { get; set; } = new();

    /// <summary>
    /// Builds a basic breadcrumb with Home > Servers > Guild Name.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="guildName">Guild name.</param>
    /// <returns>Breadcrumb view model.</returns>
    protected GuildBreadcrumbViewModel BuildBasicBreadcrumb(ulong guildId, string guildName)
    {
        return new GuildBreadcrumbViewModel
        {
            Items = new List<BreadcrumbItem>
            {
                new() { Label = "Home", Url = "/" },
                new() { Label = "Servers", Url = "/Guilds" },
                new() { Label = guildName, Url = $"/Guilds/Details/{guildId}", IsCurrent = true }
            }
        };
    }

    /// <summary>
    /// Builds a breadcrumb with Home > Servers > Guild Name > Page Name.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="guildName">Guild name.</param>
    /// <param name="pageName">Current page name.</param>
    /// <returns>Breadcrumb view model.</returns>
    protected GuildBreadcrumbViewModel BuildPageBreadcrumb(ulong guildId, string guildName, string pageName)
    {
        return new GuildBreadcrumbViewModel
        {
            Items = new List<BreadcrumbItem>
            {
                new() { Label = "Home", Url = "/" },
                new() { Label = "Servers", Url = "/Guilds" },
                new() { Label = guildName, Url = $"/Guilds/Details/{guildId}" },
                new() { Label = pageName, IsCurrent = true }
            }
        };
    }

    /// <summary>
    /// Builds navigation bar with all tabs.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="activeTabId">ID of the currently active tab.</param>
    /// <returns>Navigation bar view model.</returns>
    protected GuildNavBarViewModel BuildNavigation(ulong guildId, string activeTabId)
    {
        return new GuildNavBarViewModel
        {
            GuildId = guildId,
            ActiveTab = activeTabId,
            Tabs = GuildNavigationConfig.GetTabs().ToList()
        };
    }

    /// <summary>
    /// Builds a basic guild header with title and description.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="guildName">Guild name.</param>
    /// <param name="guildIconUrl">URL to guild icon (optional).</param>
    /// <param name="pageTitle">Page title to display.</param>
    /// <param name="pageDescription">Optional page description.</param>
    /// <returns>Header view model.</returns>
    protected GuildHeaderViewModel BuildHeader(
        ulong guildId,
        string guildName,
        string? guildIconUrl,
        string pageTitle,
        string? pageDescription = null)
    {
        return new GuildHeaderViewModel
        {
            GuildId = guildId,
            GuildName = guildName,
            GuildIconUrl = guildIconUrl,
            PageTitle = pageTitle,
            PageDescription = pageDescription
        };
    }
}
