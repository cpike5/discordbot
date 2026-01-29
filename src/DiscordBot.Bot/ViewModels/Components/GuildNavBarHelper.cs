namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// Helper methods for converting GuildNavBar components to unified TabPanel format.
/// </summary>
public static class GuildNavBarHelper
{
    /// <summary>
    /// Creates a TabPanelViewModel configured for guild navigation bar.
    /// Converts GuildNavItem list to TabPanel format with Pills style variant.
    /// </summary>
    /// <param name="guildId">Discord guild ID (Snowflake)</param>
    /// <param name="activeTab">ID of the currently active tab</param>
    /// <param name="navItems">Collection of guild navigation items</param>
    /// <returns>Configured TabPanelViewModel ready for rendering</returns>
    public static TabPanelViewModel CreateGuildNavBar(
        ulong guildId,
        string activeTab,
        IEnumerable<GuildNavItem> navItems)
    {
        var tabs = navItems
            .OrderBy(item => item.Order)
            .Select(item => new TabItemViewModel
            {
                Id = item.Id,
                Label = item.Label,
                Href = item.GetUrl(guildId),
                IconPathOutline = item.IconOutline
            })
            .ToList();

        return new TabPanelViewModel
        {
            Id = "guildNav",
            Tabs = tabs,
            ActiveTabId = activeTab,
            StyleVariant = TabStyleVariant.Pills,
            NavigationMode = TabNavigationMode.PageNavigation,
            PersistenceMode = TabPersistenceMode.None,
            AriaLabel = "Guild sections"
        };
    }
}
