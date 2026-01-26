using System.Text;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace DiscordBot.Bot.TagHelpers;

/// <summary>
/// Tag helper for rendering a collapsible filter panel with consistent styling.
/// </summary>
/// <example>
/// <filter-panel title="Filters"
///               is-collapsible="true"
///               default-expanded="@Model.HasActiveFilters"
///               active-filter-count="@Model.ActiveFilterCount">
///     <form method="get">
///         <!-- Filter content here -->
///     </form>
/// </filter-panel>
/// </example>
[HtmlTargetElement("filter-panel")]
public class FilterPanelTagHelper : TagHelper
{
    /// <summary>
    /// Title displayed in the filter panel header (default: "Filters").
    /// </summary>
    [HtmlAttributeName("title")]
    public string Title { get; set; } = "Filters";

    /// <summary>
    /// Whether the panel can be collapsed/expanded (default: true).
    /// </summary>
    [HtmlAttributeName("is-collapsible")]
    public bool IsCollapsible { get; set; } = true;

    /// <summary>
    /// Whether the panel should be expanded by default (default: false).
    /// </summary>
    [HtmlAttributeName("default-expanded")]
    public bool DefaultExpanded { get; set; }

    /// <summary>
    /// Number of active filters to display in the badge.
    /// If null or 0, no badge is shown.
    /// </summary>
    [HtmlAttributeName("active-filter-count")]
    public int? ActiveFilterCount { get; set; }

    /// <summary>
    /// Use hidden class toggle instead of max-height animation (default: false).
    /// When true, uses hidden class for toggle (requires page-specific JS).
    /// When false, uses max-height animation with toggleFilterPanel() from filter-panel.js.
    /// </summary>
    [HtmlAttributeName("use-hidden-toggle")]
    public bool UseHiddenToggle { get; set; }

    /// <summary>
    /// ID for the filter toggle button (default: "filterToggle").
    /// </summary>
    [HtmlAttributeName("toggle-id")]
    public string ToggleId { get; set; } = "filterToggle";

    /// <summary>
    /// ID for the filter content area (default: "filterContent").
    /// </summary>
    [HtmlAttributeName("content-id")]
    public string ContentId { get; set; } = "filterContent";

    /// <summary>
    /// ID for the chevron icon (default: "filterChevron").
    /// </summary>
    [HtmlAttributeName("chevron-id")]
    public string ChevronId { get; set; } = "filterChevron";

    /// <summary>
    /// Additional CSS class to apply to the container (optional).
    /// </summary>
    [HtmlAttributeName("container-class")]
    public string? ContainerClass { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var childContent = await output.GetChildContentAsync();

        output.TagName = "div";

        // Container class
        var containerClass = "bg-bg-secondary border border-border-primary rounded-lg mb-6";
        if (!string.IsNullOrEmpty(ContainerClass))
        {
            containerClass += " " + ContainerClass;
        }
        output.Attributes.SetAttribute("class", containerClass);

        var sb = new StringBuilder();

        // Compute CSS classes based on state
        var chevronClass = "w-5 h-5 text-text-secondary transition-transform duration-200";
        string contentClass;

        if (UseHiddenToggle)
        {
            contentClass = "border-t border-border-primary p-5";
            if (!DefaultExpanded)
            {
                contentClass += " hidden";
                chevronClass += " -rotate-90";
            }
        }
        else
        {
            contentClass = "overflow-hidden transition-all duration-200";
            if (DefaultExpanded)
            {
                contentClass += " max-h-screen";
            }
            else
            {
                contentClass += " max-h-0";
                chevronClass += " -rotate-90";
            }
        }

        if (IsCollapsible)
        {
            // Collapsible Header Button
            sb.Append($@"<button type=""button""
                id=""{ToggleId}""
                class=""w-full flex items-center justify-between px-5 py-4 text-left hover:bg-bg-hover transition-colors focus-visible:ring-2 focus-visible:ring-offset-2 focus-visible:ring-border-focus""
                aria-expanded=""{(DefaultExpanded ? "true" : "false")}""
                aria-controls=""{ContentId}""");

            if (!UseHiddenToggle)
            {
                sb.Append(@" onclick=""toggleFilterPanel()""");
            }
            sb.AppendLine(">");

            // Header content with icon and title
            sb.Append($@"<div class=""flex items-center gap-3"">
                <svg class=""w-5 h-5 text-text-secondary"" fill=""none"" viewBox=""0 0 24 24"" stroke=""currentColor"">
                    <path stroke-linecap=""round"" stroke-linejoin=""round"" stroke-width=""2"" d=""M3 4a1 1 0 011-1h16a1 1 0 011 1v2.586a1 1 0 01-.293.707l-6.414 6.414a1 1 0 00-.293.707V17l-4 4v-6.586a1 1 0 00-.293-.707L3.293 7.293A1 1 0 013 6.586V4z"" />
                </svg>
                <span class=""text-lg font-semibold text-text-primary"">{System.Net.WebUtility.HtmlEncode(Title)}</span>");

            // Badge for active filters
            if (ActiveFilterCount.HasValue && ActiveFilterCount.Value > 0)
            {
                var badgeText = ActiveFilterCount.Value == 1 ? "Active" : $"{ActiveFilterCount.Value} active";
                sb.Append($@"<span class=""inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-accent-orange text-white"">{badgeText}</span>");
            }

            sb.AppendLine("</div>");

            // Chevron icon
            sb.Append($@"<svg id=""{ChevronId}"" class=""{chevronClass}"" fill=""none"" viewBox=""0 0 24 24"" stroke=""currentColor"">
                <path stroke-linecap=""round"" stroke-linejoin=""round"" stroke-width=""2"" d=""M19 9l-7 7-7-7"" />
            </svg>");

            sb.AppendLine("</button>");

            // Content wrapper
            if (UseHiddenToggle)
            {
                sb.Append($@"<div id=""{ContentId}"" class=""{contentClass}"">");
                sb.Append(childContent.GetContent());
                sb.AppendLine("</div>");
            }
            else
            {
                sb.Append($@"<div id=""{ContentId}"" class=""{contentClass}"">
                    <div class=""border-t border-border-primary p-5"">");
                sb.Append(childContent.GetContent());
                sb.AppendLine("</div></div>");
            }
        }
        else
        {
            // Non-collapsible Header
            sb.Append($@"<div class=""flex items-center gap-3 px-5 py-4"">
                <svg class=""w-5 h-5 text-text-secondary"" fill=""none"" viewBox=""0 0 24 24"" stroke=""currentColor"">
                    <path stroke-linecap=""round"" stroke-linejoin=""round"" stroke-width=""2"" d=""M3 4a1 1 0 011-1h16a1 1 0 011 1v2.586a1 1 0 01-.293.707l-6.414 6.414a1 1 0 00-.293.707V17l-4 4v-6.586a1 1 0 00-.293-.707L3.293 7.293A1 1 0 013 6.586V4z"" />
                </svg>
                <span class=""text-lg font-semibold text-text-primary"">{System.Net.WebUtility.HtmlEncode(Title)}</span>");

            // Badge for active filters
            if (ActiveFilterCount.HasValue && ActiveFilterCount.Value > 0)
            {
                var badgeText = ActiveFilterCount.Value == 1 ? "Active" : $"{ActiveFilterCount.Value} active";
                sb.Append($@"<span class=""inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-accent-orange text-white"">{badgeText}</span>");
            }

            sb.AppendLine("</div>");

            // Content area
            sb.Append(@"<div class=""border-t border-border-primary p-5"">");
            sb.Append(childContent.GetContent());
            sb.AppendLine("</div>");
        }

        output.Content.SetHtmlContent(sb.ToString());
    }
}
