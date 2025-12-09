using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace DiscordBot.Bot.TagHelpers;

/// <summary>
/// Simple tag helper for role-based conditional rendering.
/// Usage: &lt;div if-role="SuperAdmin,Admin"&gt;Admin content&lt;/div&gt;
/// </summary>
[HtmlTargetElement(Attributes = "if-role")]
public class IfRoleTagHelper : TagHelper
{
    /// <summary>
    /// Gets or sets the view context.
    /// </summary>
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    /// <summary>
    /// Comma-separated list of roles. Content shown if user has ANY of these roles.
    /// </summary>
    [HtmlAttributeName("if-role")]
    public string Roles { get; set; } = string.Empty;

    /// <summary>
    /// Processes the tag helper to conditionally render content based on user roles.
    /// </summary>
    /// <param name="context">The tag helper context.</param>
    /// <param name="output">The tag helper output.</param>
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        // Remove the attribute from output
        output.Attributes.RemoveAll("if-role");

        var user = ViewContext.HttpContext.User;
        var roleList = Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var hasRole = roleList.Any(role => user.IsInRole(role));

        if (!hasRole)
        {
            output.SuppressOutput();
        }
    }
}
