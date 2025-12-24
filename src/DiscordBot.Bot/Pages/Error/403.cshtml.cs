using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Error;

/// <summary>
/// Page model for 403 Forbidden error page.
/// </summary>
[AllowAnonymous]
public class ForbiddenModel : PageModel
{
    /// <summary>
    /// Handles GET request to display the 403 error page.
    /// </summary>
    public void OnGet()
    {
        // Page displays different messages based on User.Identity.IsAuthenticated
        // No additional logic needed here
    }
}
