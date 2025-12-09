using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace DiscordBot.Bot.TagHelpers;

/// <summary>
/// Tag helper that conditionally renders content based on authorization policy.
/// </summary>
/// <example>
/// <authorize-view policy="RequireAdmin">
///     <p>Only visible to admins</p>
/// </authorize-view>
/// </example>
[HtmlTargetElement("authorize-view")]
public class AuthorizeViewTagHelper : TagHelper
{
    private readonly IAuthorizationService _authorizationService;

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    /// <summary>
    /// The authorization policy to check.
    /// </summary>
    [HtmlAttributeName("policy")]
    public string? Policy { get; set; }

    /// <summary>
    /// Comma-separated list of roles (any match grants access).
    /// </summary>
    [HtmlAttributeName("roles")]
    public string? Roles { get; set; }

    public AuthorizeViewTagHelper(IAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null; // Don't render the tag itself

        var user = ViewContext.HttpContext.User;

        // Check if user is authenticated
        if (user?.Identity?.IsAuthenticated != true)
        {
            output.SuppressOutput();
            return;
        }

        bool isAuthorized = false;

        // Check policy if specified
        if (!string.IsNullOrEmpty(Policy))
        {
            var result = await _authorizationService.AuthorizeAsync(user, Policy);
            isAuthorized = result.Succeeded;
        }
        // Check roles if specified
        else if (!string.IsNullOrEmpty(Roles))
        {
            var roleList = Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            isAuthorized = roleList.Any(role => user.IsInRole(role));
        }
        else
        {
            // No policy or roles specified, just require authentication
            isAuthorized = true;
        }

        if (!isAuthorized)
        {
            output.SuppressOutput();
        }
    }
}

/// <summary>
/// Tag helper that shows content only to specific roles.
/// </summary>
/// <example>
/// <require-role roles="SuperAdmin,Admin">
///     <button>Admin Action</button>
/// </require-role>
/// </example>
[HtmlTargetElement("require-role")]
public class RequireRoleTagHelper : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    /// <summary>
    /// Comma-separated list of roles (any match grants access).
    /// </summary>
    [HtmlAttributeName("roles")]
    public string Roles { get; set; } = string.Empty;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null; // Don't render the tag itself

        var user = ViewContext.HttpContext.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            output.SuppressOutput();
            return;
        }

        var roleList = Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (!roleList.Any(role => user.IsInRole(role)))
        {
            output.SuppressOutput();
        }
    }
}
