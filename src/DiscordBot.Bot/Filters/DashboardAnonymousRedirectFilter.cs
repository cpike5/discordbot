using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DiscordBot.Bot.Filters;

/// <summary>
/// Page filter that redirects anonymous users accessing the dashboard to the landing page.
/// This filter runs before authorization and handles the special case where the dashboard
/// should redirect unauthenticated users to /Landing instead of /Account/Login.
/// </summary>
public class DashboardAnonymousRedirectFilter : IAsyncPageFilter
{
    private readonly ILogger<DashboardAnonymousRedirectFilter> _logger;

    public DashboardAnonymousRedirectFilter(ILogger<DashboardAnonymousRedirectFilter> logger)
    {
        _logger = logger;
    }

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context)
    {
        // Check if this is the dashboard page and user is anonymous
        var path = context.HttpContext.Request.Path.Value?.TrimEnd('/');
        if ((string.IsNullOrEmpty(path) || path == "/Index") &&
            context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            _logger.LogDebug("Anonymous user redirected from dashboard to landing page");
            // Short-circuit by setting the handler descriptor to null, which causes MVC to not process
            // We can't set Result here, so we handle redirect in OnPageHandlerExecutionAsync
        }

        return Task.CompletedTask;
    }

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        // Check if this is the dashboard page and user is anonymous
        var path = context.HttpContext.Request.Path.Value?.TrimEnd('/');
        if ((string.IsNullOrEmpty(path) || path == "/Index") &&
            context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            _logger.LogDebug("Anonymous user redirected from dashboard to landing page");
            context.Result = new RedirectToPageResult("/Landing");
            return;
        }

        await next();
    }
}
