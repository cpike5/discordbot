using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using AngleSharp;
using AngleSharp.Dom;
using cpike.Agent;
using DiscordBot.Infrastructure.Services.LLM.Tooling;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Services.LLM.Tools;

/// <summary>
/// <c>fetch_url</c> — fetches and extracts readable content from a URL.
/// Includes SSRF protections to prevent access to internal network resources.
/// </summary>
[cpike.Agent.Tools.ExternalContent("web")]
public sealed class FetchUrlTool : IAgentTool
{
    private static readonly AgentToolDefinition ToolDefinition = AgentTools.Define(
        "fetch_url",
        "Fetches and extracts readable text content from a URL. For HTML pages, strips scripts, styles, navigation, headers, and footers to return the main content. For non-HTML content, returns the raw text. Use this to look up reference material, documentation, or articles that the user shares.",
        JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "url": {
                        "type": "string",
                        "description": "The URL to fetch content from. Must be an HTTP or HTTPS URL."
                    },
                    "max_length": {
                        "type": "integer",
                        "description": "Maximum length of the returned content in characters. Default is 8000, minimum 2000, maximum 16000.",
                        "default": 8000,
                        "minimum": 2000,
                        "maximum": 16000
                    }
                },
                "required": ["url"]
            }
            """).RootElement.Clone());

    private readonly ILogger<FetchUrlTool> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    private const int DefaultMaxLength = 8000;
    private const int MinMaxLength = 2000;
    private const int MaxMaxLength = 16000;
    private const int MaxResponseBytes = 512 * 1024;

    /// <summary>Creates the tool.</summary>
    public FetchUrlTool(
        ILogger<FetchUrlTool> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    /// <inheritdoc />
    public AgentToolDefinition Definition => ToolDefinition;

    /// <inheritdoc />
    public async Task<string> InvokeAsync(
        IReadOnlyDictionary<string, JsonElement> arguments,
        CancellationToken cancellationToken)
    {
        var input = ToolInput.ToElement(arguments);

        try
        {
            return await ExecuteFetchUrlAsync(input, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error executing tool {ToolName}", "fetch_url");
            return ToolResults.Error($"Error executing tool: {ex.Message}");
        }
    }

    private async Task<string> ExecuteFetchUrlAsync(
        JsonElement input,
        CancellationToken cancellationToken)
    {
        if (!input.TryGetProperty("url", out var urlElement))
        {
            return ToolResults.Error("Missing required parameter: url");
        }

        var urlString = urlElement.GetString();
        if (string.IsNullOrWhiteSpace(urlString))
        {
            return ToolResults.Error("Parameter url cannot be empty.");
        }

        if (!Uri.TryCreate(urlString, UriKind.Absolute, out var uri))
        {
            return ToolResults.Error("Invalid URL format.");
        }

        if (uri.Scheme != "http" && uri.Scheme != "https")
        {
            return ToolResults.Error("Only HTTP and HTTPS URLs are supported.");
        }

        var maxLength = DefaultMaxLength;
        if (input.TryGetProperty("max_length", out var maxLengthElement))
        {
            maxLength = Math.Clamp(maxLengthElement.GetInt32(), MinMaxLength, MaxMaxLength);
        }

        // SSRF protection: resolve DNS and check for private/reserved addresses
        var ssrfCheck = await CheckSsrfAsync(uri);
        if (ssrfCheck != null)
        {
            return ToolResults.Error(ssrfCheck);
        }

        _logger.LogDebug("Fetching URL {Url} with max length {MaxLength}", uri.Host, maxLength);

        var client = _httpClientFactory.CreateClient("DmAssistantWebFetch");

        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            return ToolResults.Error("Request timed out.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP request failed for {Host}", uri.Host);
            return ToolResults.Error($"Failed to fetch URL: {ex.Message}");
        }

        if (!response.IsSuccessStatusCode)
        {
            return ToolResults.Error($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        // Limit response size
        var contentLength = response.Content.Headers.ContentLength;
        if (contentLength > MaxResponseBytes)
        {
            return ToolResults.Error($"Response too large ({contentLength} bytes). Maximum is {MaxResponseBytes} bytes.");
        }

        var contentType = response.Content.Headers.ContentType?.MediaType ?? "text/plain";
        var rawContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (rawContent.Length > MaxResponseBytes)
        {
            rawContent = rawContent[..MaxResponseBytes];
        }

        string extractedContent;
        string? title = null;

        if (contentType.Contains("html", StringComparison.OrdinalIgnoreCase))
        {
            (title, extractedContent) = await ExtractHtmlContentAsync(rawContent, cancellationToken);
        }
        else
        {
            extractedContent = rawContent;
        }

        // Truncate to max_length
        var wasTruncated = extractedContent.Length > maxLength;
        if (wasTruncated)
        {
            extractedContent = extractedContent[..maxLength] + "\n\n[Content truncated]";
        }

        _logger.LogDebug("Fetched {Length} chars from {Host}", extractedContent.Length, uri.Host);

        return CreateJsonResult(new
        {
            url = uri.ToString(),
            title,
            content = extractedContent,
            content_type = contentType,
            content_length = extractedContent.Length,
            truncated = wasTruncated
        });
    }

    /// <summary>
    /// Checks if the URL target resolves to a private/reserved IP address (SSRF protection).
    /// </summary>
    /// <returns>An error message if blocked, or null if safe.</returns>
    private static async Task<string?> CheckSsrfAsync(Uri uri)
    {
        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(uri.Host);
        }
        catch (SocketException)
        {
            return "Could not resolve hostname.";
        }

        if (addresses.Length == 0)
        {
            return "Could not resolve hostname.";
        }

        foreach (var address in addresses)
        {
            if (IsPrivateOrReserved(address))
            {
                return "Access to internal/private network addresses is not allowed.";
            }
        }

        return null;
    }

    private static bool IsPrivateOrReserved(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
            return true;

        // IPv4-mapped IPv6 (::ffff:x.x.x.x) — check the embedded IPv4 address
        if (address.IsIPv4MappedToIPv6)
            return IsPrivateOrReserved(address.MapToIPv4());

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal) // fe80::/10
                return true;
            if (address.Equals(IPAddress.IPv6Loopback)) // ::1
                return true;

            // Unique local addresses (fc00::/7 — fd and fc prefixes)
            var ipBytes = address.GetAddressBytes();
            if ((ipBytes[0] & 0xFE) == 0xFC)
                return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();

            // 10.0.0.0/8
            if (bytes[0] == 10)
                return true;

            // 172.16.0.0/12
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                return true;

            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168)
                return true;

            // 127.0.0.0/8
            if (bytes[0] == 127)
                return true;

            // 169.254.0.0/16 (link-local)
            if (bytes[0] == 169 && bytes[1] == 254)
                return true;

            // 0.0.0.0/8
            if (bytes[0] == 0)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Extracts title and body text from HTML using AngleSharp, stripping unwanted elements.
    /// </summary>
    private static async Task<(string? Title, string Content)> ExtractHtmlContentAsync(
        string html,
        CancellationToken cancellationToken)
    {
        var config = AngleSharp.Configuration.Default;
        var browsingContext = BrowsingContext.New(config);
        var document = await browsingContext.OpenAsync(req => req.Content(html), cancellationToken);

        var title = document.Title;

        // Remove unwanted elements
        var selectorsToRemove = new[] { "script", "style", "nav", "header", "footer", "noscript", "iframe", "svg" };
        foreach (var selector in selectorsToRemove)
        {
            foreach (var element in document.QuerySelectorAll(selector).ToList())
            {
                element.Remove();
            }
        }

        var body = document.Body;
        var textContent = body?.TextContent ?? string.Empty;

        // Normalize whitespace: collapse multiple whitespace/newlines into single spaces
        textContent = System.Text.RegularExpressions.Regex.Replace(textContent, @"\s+", " ").Trim();

        return (title, textContent);
    }

    private static string CreateJsonResult(object data) =>
        JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        });
}
