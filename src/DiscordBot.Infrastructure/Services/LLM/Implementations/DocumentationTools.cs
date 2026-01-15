using System.Text.Json;
using DiscordBot.Core.DTOs.LLM;

namespace DiscordBot.Infrastructure.Services.LLM.Implementations;

/// <summary>
/// Static tool definitions for documentation access.
/// Defines the schema and metadata for documentation-related tools.
/// </summary>
public static class DocumentationTools
{
    /// <summary>
    /// Tool name for getting feature documentation.
    /// </summary>
    public const string GetFeatureDocumentation = "get_feature_documentation";

    /// <summary>
    /// Tool name for searching commands.
    /// </summary>
    public const string SearchCommands = "search_commands";

    /// <summary>
    /// Tool name for getting command details.
    /// </summary>
    public const string GetCommandDetails = "get_command_details";

    /// <summary>
    /// Tool name for listing all features.
    /// </summary>
    public const string ListFeatures = "list_features";

    /// <summary>
    /// Gets all documentation tool definitions.
    /// </summary>
    /// <returns>Collection of tool definitions.</returns>
    public static IEnumerable<LlmToolDefinition> GetAllTools()
    {
        yield return CreateGetFeatureDocumentationTool();
        yield return CreateSearchCommandsTool();
        yield return CreateGetCommandDetailsTool();
        yield return CreateListFeaturesTool();
    }

    /// <summary>
    /// Creates the get_feature_documentation tool definition.
    /// </summary>
    private static LlmToolDefinition CreateGetFeatureDocumentationTool()
    {
        var schema = JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "feature_name": {
                        "type": "string",
                        "description": "The name of the feature to get documentation for. Use lowercase with hyphens (e.g., 'soundboard', 'rat-watch', 'tts-support', 'reminder-system', 'member-directory')."
                    }
                },
                "required": ["feature_name"]
            }
            """);

        return new LlmToolDefinition
        {
            Name = GetFeatureDocumentation,
            Description = "Retrieves comprehensive documentation for a specific bot feature. Returns markdown content including overview, configuration, usage instructions, and examples. Use this when users ask about how to use specific features.",
            InputSchema = schema.RootElement.Clone()
        };
    }

    /// <summary>
    /// Creates the search_commands tool definition.
    /// </summary>
    private static LlmToolDefinition CreateSearchCommandsTool()
    {
        var schema = JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "query": {
                        "type": "string",
                        "description": "Search keyword to find commands (e.g., 'moderation', 'sound', 'remind', 'ban')."
                    },
                    "limit": {
                        "type": "integer",
                        "description": "Maximum number of results to return. Default is 10, maximum is 50.",
                        "default": 10,
                        "minimum": 1,
                        "maximum": 50
                    }
                },
                "required": ["query"]
            }
            """);

        return new LlmToolDefinition
        {
            Name = SearchCommands,
            Description = "Searches available slash commands by keyword. Returns matching commands with their descriptions, parameters, and permission requirements. Use this when users ask what commands are available or search for specific functionality.",
            InputSchema = schema.RootElement.Clone()
        };
    }

    /// <summary>
    /// Creates the get_command_details tool definition.
    /// </summary>
    private static LlmToolDefinition CreateGetCommandDetailsTool()
    {
        var schema = JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "command_name": {
                        "type": "string",
                        "description": "The command name without the leading slash (e.g., 'remind', 'play', 'ban', 'warn')."
                    }
                },
                "required": ["command_name"]
            }
            """);

        return new LlmToolDefinition
        {
            Name = GetCommandDetails,
            Description = "Gets detailed information about a specific slash command including all parameters, their types, descriptions, default values, permission requirements, and usage examples. Use this when users ask how to use a specific command.",
            InputSchema = schema.RootElement.Clone()
        };
    }

    /// <summary>
    /// Creates the list_features tool definition.
    /// </summary>
    private static LlmToolDefinition CreateListFeaturesTool()
    {
        var schema = JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {},
                "required": []
            }
            """);

        return new LlmToolDefinition
        {
            Name = ListFeatures,
            Description = "Lists all available bot features with brief descriptions and availability status. Use this when users ask what the bot can do or want an overview of capabilities.",
            InputSchema = schema.RootElement.Clone()
        };
    }
}
