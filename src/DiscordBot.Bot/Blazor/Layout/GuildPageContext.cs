using DiscordBot.Bot.ViewModels.Components;

namespace DiscordBot.Bot.Blazor.Layout;

/// <summary>
/// Per-page guild chrome data for <c>GuildLayout</c> — the Blazor replacement for
/// the ViewModel properties (<c>Model.Breadcrumb</c> / <c>Model.Header</c> /
/// <c>Model.Navigation</c>) that guild page models fed to
/// <c>_GuildLayout.cshtml</c>.
///
/// Data flows the only direction Blazor layouts allow: the layout owns one
/// instance and cascades it down to the routed page; the page populates it
/// (typically from <c>OnInitializedAsync</c>) via <see cref="Set"/>, which
/// raises <see cref="Changed"/> so the layout re-renders its breadcrumb,
/// header, and nav-tab region.
/// </summary>
public sealed class GuildPageContext
{
    /// <summary>Breadcrumb trail (twin of the page model's <c>Breadcrumb</c>).</summary>
    public GuildBreadcrumbViewModel? Breadcrumb { get; private set; }

    /// <summary>Guild header — icon, title, description, actions, status badge.</summary>
    public GuildHeaderViewModel? Header { get; private set; }

    /// <summary>Guild nav tabs + active tab (twin of the page model's <c>Navigation</c>).</summary>
    public GuildNavBarViewModel? Navigation { get; private set; }

    /// <summary>Raised after <see cref="Set"/>; GuildLayout re-renders on it.</summary>
    public event Action? Changed;

    /// <summary>Populates the guild chrome; call once the page has loaded its guild data.</summary>
    public void Set(
        GuildBreadcrumbViewModel? breadcrumb,
        GuildHeaderViewModel? header,
        GuildNavBarViewModel? navigation)
    {
        Breadcrumb = breadcrumb;
        Header = header;
        Navigation = navigation;
        Changed?.Invoke();
    }
}
