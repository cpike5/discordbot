using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Error;

/// <summary>
/// Page model for 500 Internal Server Error page.
/// </summary>
[AllowAnonymous]
public class ServerErrorModel : PageModel
{
    private readonly ILogger<ServerErrorModel> _logger;
    private readonly IWebHostEnvironment _environment;

    /// <summary>
    /// Request ID for tracking this error.
    /// </summary>
    public string? RequestId { get; set; }

    /// <summary>
    /// Whether the application is running in development mode.
    /// </summary>
    public bool IsDevelopment { get; set; }

    /// <summary>
    /// Exception message (only shown in development).
    /// </summary>
    public string? ExceptionMessage { get; set; }

    /// <summary>
    /// Stack trace (only shown in development).
    /// </summary>
    public string? StackTrace { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerErrorModel"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="environment">Web host environment.</param>
    public ServerErrorModel(ILogger<ServerErrorModel> logger, IWebHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    /// <summary>
    /// Handles GET request to display the 500 error page.
    /// </summary>
    public void OnGet(string? requestId = null)
    {
        RequestId = requestId ?? HttpContext.TraceIdentifier;
        IsDevelopment = _environment.IsDevelopment();

        // In development, attempt to get exception details
        if (IsDevelopment)
        {
            var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            if (exceptionFeature?.Error != null)
            {
                var exception = exceptionFeature.Error;
                ExceptionMessage = exception.Message;
                StackTrace = exception.StackTrace;

                _logger.LogError(exception, "Unhandled exception occurred. Request ID: {RequestId}, Path: {Path}",
                    RequestId, exceptionFeature.Path);
            }
        }
        else
        {
            // In production, just log the request ID without exposing exception details
            _logger.LogError("Server error page displayed. Request ID: {RequestId}", RequestId);
        }
    }
}
