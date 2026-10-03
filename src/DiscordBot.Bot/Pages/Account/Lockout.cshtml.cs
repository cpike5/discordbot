using DiscordBot.Bot.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Pages.Account;

/// <summary>
/// Page model for account lockout notification.
/// </summary>
[AllowAnonymous]
public class LockoutModel : PageModel
{
    private readonly IOptions<IdentityOptions> _identityOptions;

    public LockoutModel(IOptions<IdentityOptions> identityOptions)
    {
        _identityOptions = identityOptions;
    }

    /// <summary>
    /// How long a lockout lasts, in words ("15 minutes", "2 hours"), taken from the Identity
    /// lockout configuration (<c>Identity:LockoutTimeSpanMinutes</c>) so the page never states a
    /// duration the server does not enforce.
    /// </summary>
    public string LockoutDuration => Describe(_identityOptions.Value.Lockout.DefaultLockoutTimeSpan);

    /// <summary>
    /// Handles GET request to display the lockout page.
    /// </summary>
    public void OnGet()
    {
    }

    /// <summary>Writes a lockout length in whole minutes, or in hours or days when it divides evenly.</summary>
    public static string Describe(TimeSpan duration)
    {
        var minutes = (long)Math.Ceiling(duration.TotalMinutes);
        if (minutes < 1) return "a moment";
        if (minutes % 1440 == 0) return DisplayFormat.Plural(minutes / 1440, "day");
        if (minutes % 60 == 0) return DisplayFormat.Plural(minutes / 60, "hour");
        return DisplayFormat.Plural(minutes, "minute");
    }
}
