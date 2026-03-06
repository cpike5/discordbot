using System.Text.Json;
using DiscordBot.Core.DTOs.LLM;

namespace DiscordBot.Infrastructure.Services.LLM.Implementations;

/// <summary>
/// Static tool definitions for web content fetching.
/// </summary>
public static class WebFetchTools
{
    public const string FetchUrl = "fetch_url";

    /// <summary>
    /// Gets all web fetch tool definitions.
    /// </summary>
    public static IEnumerable<LlmToolDefinition> GetAllTools()
    {
        yield return CreateFetchUrlTool();
    }

    private static LlmToolDefinition CreateFetchUrlTool()
    {
        var schema = JsonDocument.Parse("""
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
            """);

        return new LlmToolDefinition
        {
            Name = FetchUrl,
            Description = "Fetches and extracts readable text content from a URL. For HTML pages, strips scripts, styles, navigation, headers, and footers to return the main content. For non-HTML content, returns the raw text. Use this to look up reference material, documentation, or articles that the user shares.",
            InputSchema = schema.RootElement.Clone()
        };
    }
}
