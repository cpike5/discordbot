using DiscordBot.Bot.Helpers;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace DiscordBot.Bot.TagHelpers;

/// <summary>
/// Tag helper for highlighting search terms in text.
/// Usage: <highlight text="@Model.Description" search-term="@Model.SearchTerm" />
/// </summary>
[HtmlTargetElement("highlight")]
public class HighlightTagHelper : TagHelper
{
    [HtmlAttributeName("text")]
    public string Text { get; set; } = string.Empty;

    [HtmlAttributeName("search-term")]
    public string SearchTerm { get; set; } = string.Empty;

    [HtmlAttributeName("max-length")]
    public int? MaxLength { get; set; }

    [HtmlAttributeName("show-context")]
    public bool ShowContext { get; set; } = false;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "span";

        string highlighted;

        if (MaxLength.HasValue)
        {
            highlighted = ShowContext
                ? TextHighlightHelper.HighlightWithContext(Text, SearchTerm, MaxLength.Value)
                : TextHighlightHelper.TruncateAndHighlight(Text, SearchTerm, MaxLength.Value);
        }
        else
        {
            highlighted = TextHighlightHelper.HighlightMatches(Text, SearchTerm);
        }

        output.Content.SetHtmlContent(highlighted);
    }
}
