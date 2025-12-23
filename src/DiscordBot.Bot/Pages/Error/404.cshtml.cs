using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Error;

/// <summary>
/// Page model for 404 Not Found error page.
/// </summary>
[AllowAnonymous]
public class NotFoundModel : PageModel
{
    /// <summary>
    /// The path that was requested but not found.
    /// </summary>
    public string? RequestedPath { get; set; }

    /// <summary>
    /// Handles GET request to display the 404 error page.
    /// </summary>
    public void OnGet()
    {
        RequestedPath = HttpContext.Request.Path.Value;
    }
}
