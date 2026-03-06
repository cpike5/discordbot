using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using AngleSharp;
using AngleSharp.Dom;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Infrastructure.Services.LLM.Implementations;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Services.LLM.Providers;

/// <summary>
/// Tool provider for fetching and extracting content from URLs in DM assistant context.
/// Includes SSRF protections to prevent access to internal network resources.
/// </summary>
public class WebFetchToolProvider : IDmToolProvider
{
    private readonly ILogger<WebFetchToolProvider> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    private const int DefaultMaxLength = 8000;
    private const int MinMaxLength = 2000;
    private const int MaxMaxLength = 16000;
    private const int MaxResponseBytes = 512 * 1024;

    /// <inheritdoc />
    public string Name => "WebFetch";

    /// <inheritdoc />
    public string Description => "Fetch and extract readable content from URLs";

    public WebFetchToolProvider(
        ILogger<WebFetchToolProvider> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    /// <inheritdoc />
    public IEnumerable<LlmToolDefinition> GetTools()
    {
        return WebFetchTools.GetAllTools();
    }

    /// <inheritdoc />
    public async Task<ToolExecutionResult> ExecuteToolAsync(
        string toolName,
        JsonElement input,
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Executing web fetch tool {ToolName}", toolName);

        try
        {
            return toolName.ToLowerInvariant() switch
            {
                WebFetchTools.FetchUrl => await ExecuteFetchUrlAsync(input, cancellationToken),
                _ => throw new NotSupportedException($"Tool '{toolName}' is not supported by this provider")
            };
        }
        catch (NotSupportedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing web fetch tool {ToolName}", toolName);
            return ToolExecutionResult.CreateError($"Error executing tool: {ex.Message}");
        }
    }

    private async Task<ToolExecutionResult> ExecuteFetchUrlAsync(
        JsonElement input,
        CancellationToken cancellationToken)
    {
        if (!input.TryGetProperty("url", out var urlElement))
        {
            return ToolExecutionResult.CreateError("Missing required parameter: url");
        }

        var urlString = urlElement.GetString();
        if (string.IsNullOrWhiteSpace(urlString))
        {
            return ToolExecutionResult.CreateError("Parameter url cannot be empty.");
        }

        if (!Uri.TryCreate(urlString, UriKind.Absolute, out var uri))
        {
            return ToolExecutionResult.CreateError("Invalid URL format.");
        }

        if (uri.Scheme != "http" && uri.Scheme != "https")
        {
            return ToolExecutionResult.CreateError("Only HTTP and HTTPS URLs are supported.");
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
            return ToolExecutionResult.CreateError(ssrfCheck);
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
            return ToolExecutionResult.CreateError("Request timed out.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP request failed for {Host}", uri.Host);
            return ToolExecutionResult.CreateError($"Failed to fetch URL: {ex.Message}");
        }

        if (!response.IsSuccessStatusCode)
        {
            return ToolExecutionResult.CreateError($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        // Limit response size
        var contentLength = response.Content.Headers.ContentLength;
        if (contentLength > MaxResponseBytes)
        {
            return ToolExecutionResult.CreateError($"Response too large ({contentLength} bytes). Maximum is {MaxResponseBytes} bytes.");
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

    private static ToolExecutionResult CreateJsonResult(object data)
    {
        var jsonString = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        });
        var jsonElement = JsonDocument.Parse(jsonString).RootElement.Clone();
        return ToolExecutionResult.CreateSuccess(jsonElement);
    }
}
