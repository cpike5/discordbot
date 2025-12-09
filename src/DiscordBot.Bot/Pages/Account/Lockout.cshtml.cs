using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Account;

/// <summary>
/// Page model for account lockout notification.
/// </summary>
[AllowAnonymous]
public class LockoutModel : PageModel
{
    /// <summary>
    /// Handles GET request to display the lockout page.
    /// </summary>
    public void OnGet()
    {
    }
}
