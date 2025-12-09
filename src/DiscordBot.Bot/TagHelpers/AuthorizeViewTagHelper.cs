using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace DiscordBot.Bot.TagHelpers;

/// <summary>
/// Tag helper for conditional rendering based on authorization policies.
/// Usage: &lt;authorize policy="RequireAdmin"&gt;Admin-only content&lt;/authorize&gt;
/// </summary>
[HtmlTargetElement("authorize")]
public class AuthorizeViewTagHelper : TagHelper
{
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizeViewTagHelper"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service for policy evaluation.</param>
    public AuthorizeViewTagHelper(IAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Gets or sets the view context.
    /// </summary>
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    /// <summary>
    /// The authorization policy name to evaluate.
    /// </summary>
    [HtmlAttributeName("policy")]
    public string? Policy { get; set; }

    /// <summary>
    /// Comma-separated list of roles to check (any role grants access).
    /// </summary>
    [HtmlAttributeName("roles")]
    public string? Roles { get; set; }

    /// <summary>
    /// If true, content is shown when user is NOT authorized (inverse logic).
    /// </summary>
    [HtmlAttributeName("negate")]
    public bool Negate { get; set; }

    /// <summary>
    /// Processes the tag helper to conditionally render content based on authorization.
    /// </summary>
    /// <param name="context">The tag helper context.</param>
    /// <param name="output">The tag helper output.</param>
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        // Remove the <authorize> tag itself, just render children if authorized
        output.TagName = null;

        var user = ViewContext.HttpContext.User;
        var isAuthorized = false;

        // Check policy-based authorization
        if (!string.IsNullOrEmpty(Policy))
        {
            var result = await _authorizationService.AuthorizeAsync(user, Policy);
            isAuthorized = result.Succeeded;
        }
        // Check role-based authorization
        else if (!string.IsNullOrEmpty(Roles))
        {
            var roleList = Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            isAuthorized = roleList.Any(role => user.IsInRole(role));
        }
        // Default to checking if authenticated
        else
        {
            isAuthorized = user.Identity?.IsAuthenticated ?? false;
        }

        // Apply negation if requested
        if (Negate)
        {
            isAuthorized = !isAuthorized;
        }

        // Suppress output if not authorized
        if (!isAuthorized)
        {
            output.SuppressOutput();
        }
    }
}
