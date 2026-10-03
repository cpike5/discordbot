using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Error;

/// <summary>
/// The one error page, for every status code. <c>UseStatusCodePagesWithReExecute("/Error/{0}")</c>
/// and <c>UseExceptionHandler("/Error/500")</c> both land here, re-executing the failed request
/// with its original method, so every verb has a handler and antiforgery is not checked (a
/// 400 from a stale antiforgery token would otherwise fail again here and render nothing).
/// A request made by a script gets a JSON problem body instead of HTML.
/// </summary>
[AllowAnonymous]
[IgnoreAntiforgeryToken]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class ErrorPageModel : PageModel
{
    private readonly ILogger<ErrorPageModel> _logger;
    private readonly IWebHostEnvironment _environment;

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorPageModel"/> class.
    /// </summary>
    public ErrorPageModel(ILogger<ErrorPageModel> logger, IWebHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    /// <summary>The status code the page describes.</summary>
    public int Code { get; private set; }

    /// <summary>What went wrong, as a heading.</summary>
    public string Heading { get; private set; } = string.Empty;

    /// <summary>What happened and what the user can do about it.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>The path and query that failed; null when the error page was opened directly.</summary>
    public string? OriginalPath { get; private set; }

    /// <summary>A reference to quote to an administrator (5xx only).</summary>
    public string? RequestId { get; private set; }

    /// <summary>Seconds to wait before retrying, from a 429's Retry-After header.</summary>
    public int? RetryAfterSeconds { get; private set; }

    /// <summary>True when the failed request was a GET, so reloading it is safe.</summary>
    public bool CanReload { get; private set; }

    /// <summary>The page that failed, when it is a local URL; otherwise "/". Used for Sign in and Reload.</summary>
    public string LocalOriginalUrl => ReturnUrlHelper.Sanitize(OriginalPath, "/");

    /// <summary>Exception message, in Development only.</summary>
    public string? ExceptionMessage { get; private set; }

    /// <summary>Stack trace, in Development only.</summary>
    public string? ExceptionStackTrace { get; private set; }

    /// <summary>Renders the error page for a GET (or HEAD).</summary>
    public IActionResult OnGet(int statusCode) => Render(statusCode);

    /// <summary>Renders the error page for a failed POST.</summary>
    public IActionResult OnPost(int statusCode) => Render(statusCode);

    /// <summary>Renders the error page for a failed PUT.</summary>
    public IActionResult OnPut(int statusCode) => Render(statusCode);

    /// <summary>Renders the error page for a failed PATCH.</summary>
    public IActionResult OnPatch(int statusCode) => Render(statusCode);

    /// <summary>Renders the error page for a failed DELETE.</summary>
    public IActionResult OnDelete(int statusCode) => Render(statusCode);

    private IActionResult Render(int statusCode)
    {
        Code = statusCode is >= 400 and <= 599 ? statusCode : 500;

        var reExecute = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();
        var exception = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        OriginalPath = reExecute != null
            ? reExecute.OriginalPath + reExecute.OriginalQueryString
            : exception?.Path;
        CanReload = HttpMethods.IsGet(Request.Method) || HttpMethods.IsHead(Request.Method);

        if (Code == StatusCodes.Status429TooManyRequests
            && int.TryParse(Response.Headers.RetryAfter.ToString(), out var retryAfter)
            && retryAfter > 0)
        {
            RetryAfterSeconds = retryAfter;
        }

        if (Code >= 500)
        {
            RequestId = HttpContext.GetCorrelationId();
            if (exception?.Error != null)
            {
                _logger.LogError(exception.Error, "Unhandled exception. Request ID: {RequestId}, Path: {Path}", RequestId, exception.Path);
                if (_environment.IsDevelopment())
                {
                    ExceptionMessage = exception.Error.Message;
                    ExceptionStackTrace = exception.Error.StackTrace;
                }
            }
        }

        (Heading, Description) = Describe(Code, User.Identity?.IsAuthenticated == true, RetryAfterSeconds);

        if (IsFromScript(OriginalPath))
        {
            var problem = new ProblemDetails { Status = Code, Title = Heading, Detail = Description };
            if (RequestId != null)
            {
                problem.Extensions["requestId"] = RequestId;
            }

            return new ObjectResult(problem) { StatusCode = Code, ContentTypes = { "application/problem+json" } };
        }

        return Page();
    }

    /// <summary>
    /// True when the failed request came from a script. After a re-execute the request path is
    /// this page, so the path check uses the original path; headers are the original ones.
    /// </summary>
    private bool IsFromScript(string? originalPath)
    {
        if (originalPath == null)
        {
            return false;
        }

        var path = new PathString(originalPath.Split('?')[0]);
        return path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase)
            || Request.IsScriptRequest();
    }

    /// <summary>The heading and plain-language description for a status code.</summary>
    /// <param name="code">The HTTP status code.</param>
    /// <param name="signedIn">Whether the user is signed in (changes the 403 copy).</param>
    /// <param name="retryAfterSeconds">A 429's Retry-After, when known.</param>
    public static (string Heading, string Description) Describe(int code, bool signedIn, int? retryAfterSeconds = null) => code switch
    {
        400 => ("This page has expired",
            "The page was open for a long time, or you signed in again somewhere else, so it could not be sent. Reload the page and try again. Anything you typed may need entering again."),
        401 => ("Sign in to continue",
            "Your session has ended. Sign in again to carry on where you left off."),
        403 when signedIn => ("You don't have access to this page",
            "Your account doesn't have permission to open it. If you need access, ask an administrator."),
        403 => ("Sign in to continue",
            "This page is only for signed-in users. Sign in, and you'll come back here if your account has access."),
        404 => ("Page not found",
            "There is nothing at this address. It may have been moved or deleted, or the link may be wrong."),
        405 => ("That action isn't available here",
            "This address doesn't accept that kind of request. Go back and try again from the page itself."),
        408 => ("The request took too long",
            "The server stopped waiting for the request. Check your connection and try again."),
        413 => ("That upload is too large",
            "The file or form was bigger than the server accepts. Try a smaller file."),
        429 => ("Too many requests",
            retryAfterSeconds is > 0
                ? $"You're doing that too often. Wait {retryAfterSeconds} {(retryAfterSeconds == 1 ? "second" : "seconds")}, then try again."
                : "You're doing that too often. Wait a moment, then try again."),
        503 => ("Temporarily unavailable",
            "The portal is restarting or busy. Try again in a minute."),
        >= 500 => ("Something went wrong",
            "An unexpected error stopped this page from loading. Try again in a moment. If it keeps happening, give an administrator the reference below."),
        _ => ("Something went wrong",
            "The request could not be completed. Go back and try again.")
    };
}
