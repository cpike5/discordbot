using DiscordBot.Bot.Blazor.Shared;
using Microsoft.AspNetCore.Components;

namespace DiscordBot.Bot.Blazor.Pages.Admin.Logs;

/// <summary>
/// Code-behind for the unified Logs page. Only decides which tab is active (default "messages")
/// and renders that tab's component - each tab owns its own filters/paging/data (see
/// <c>Tabs/MessagesTab.razor.cs</c> and <c>Tabs/AuditTab.razor.cs</c>), reproducing
/// <c>IndexModel.OnGetAsync</c>'s "only load data for the active tab" behaviour.
/// </summary>
public partial class Index : ComponentBase
{
    [SupplyParameterFromQuery(Name = "tab")]
    [Parameter]
    public string? Tab { get; set; }

    protected string ActiveTab => string.IsNullOrWhiteSpace(Tab) ? "messages" : Tab.ToLowerInvariant();

    protected List<TabItem> Tabs =>
    [
        new() { Id = "messages", Label = "Messages", Href = "/Admin/Logs?tab=messages" },
        new() { Id = "audit", Label = "Audit", Href = "/Admin/Logs?tab=audit" },
        new() { Id = "application", Label = "Application", Href = "/Admin/Logs?tab=application", Disabled = true }
    ];
}
