using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Account;

/// <summary>
/// Page model for access denied (403) page.
/// </summary>
[AllowAnonymous]
public class AccessDeniedModel : PageModel
{
    /// <summary>
    /// Return URL that was denied access.
    /// </summary>
    public string? ReturnUrl { get; set; }

    /// <summary>
    /// Handles GET request to display the access denied page.
    /// </summary>
    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
    }
}
