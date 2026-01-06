using DiscordBot.Core.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Pages;

/// <summary>
/// PageModel for the public landing page.
/// This page is accessible without authentication.
/// </summary>
[AllowAnonymous]
public class LandingModel : PageModel
{
    private readonly ApplicationOptions _applicationOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="LandingModel"/> class.
    /// </summary>
    /// <param name="applicationOptions">Application configuration options.</param>
    public LandingModel(IOptions<ApplicationOptions> applicationOptions)
    {
        _applicationOptions = applicationOptions.Value;
    }

    /// <summary>
    /// Gets the application version to display in the footer.
    /// </summary>
    public string Version => _applicationOptions.Version;

    /// <summary>
    /// Handles GET requests for the landing page.
    /// </summary>
    public void OnGet()
    {
        // No additional processing needed for the landing page
    }
}
