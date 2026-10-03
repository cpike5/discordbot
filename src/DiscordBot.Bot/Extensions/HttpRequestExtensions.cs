using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Tells requests made by page scripts apart from browser navigations, so a script gets a
/// status code and JSON it can act on rather than a redirect to an HTML page.
/// </summary>
public static class HttpRequestExtensions
{
    /// <summary>
    /// True for a request made from JavaScript rather than by the browser navigating: anything
    /// under <c>/api</c> or <c>/hubs</c>, anything sent with <c>X-Requested-With: XMLHttpRequest</c>
    /// (ApiClient and the partial loaders send it), and anything that accepts JSON but not HTML.
    /// </summary>
    /// <param name="request">The request.</param>
    public static bool IsScriptRequest(this HttpRequest request)
    {
        if (request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
            || request.Path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(request.Headers.XRequestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var accept = request.Headers.Accept.ToString();
        return accept.Contains("application/json", StringComparison.OrdinalIgnoreCase)
            && !accept.Contains("text/html", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Writes an RFC 7807 problem response with the given status and plain-language detail.
    /// </summary>
    /// <param name="response">The response to write.</param>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="title">A short summary of the problem.</param>
    /// <param name="detail">What happened and what the user can do about it.</param>
    public static Task WriteProblemAsync(this HttpResponse response, int statusCode, string title, string detail)
    {
        response.StatusCode = statusCode;
        return response.WriteAsJsonAsync(
            new ProblemDetails { Status = statusCode, Title = title, Detail = detail },
            options: null,
            contentType: "application/problem+json");
    }
}
