using System.Text.Json;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Infrastructure.Services.LLM.Implementations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordBot.Infrastructure.Services.LLM.Providers;

/// <summary>
/// Tool provider for documentation access.
/// Provides tools for reading feature documentation, searching commands, and listing features.
/// </summary>
public class DocumentationToolProvider : IToolProvider
{
    private readonly ILogger<DocumentationToolProvider> _logger;
    private readonly ICommandMetadataService _commandMetadataService;
    private readonly IOptions<AssistantOptions> _assistantOptions;
    private readonly IOptions<ApplicationOptions> _applicationOptions;

    /// <inheritdoc />
    public string Name => "Documentation";

    /// <inheritdoc />
    public string Description => "Access bot documentation, command information, and feature guides";

    // Feature name to documentation file mappings
    private static readonly Dictionary<string, string> FeatureDocumentationMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "soundboard", "soundboard.md" },
        { "rat-watch", "rat-watch.md" },
        { "ratwatch", "rat-watch.md" },
        { "tts", "tts-support.md" },
        { "tts-support", "tts-support.md" },
        { "text-to-speech", "tts-support.md" },
        { "reminder", "reminder-system.md" },
        { "reminder-system", "reminder-system.md" },
        { "reminders", "reminder-system.md" },
        { "member-directory", "member-directory.md" },
        { "members", "member-directory.md" },
        { "moderation", "authorization-policies.md" },
        { "welcome", "welcome-system.md" },
        { "welcome-system", "welcome-system.md" },
        { "scheduled-messages", "scheduled-messages.md" },
        { "schedule", "scheduled-messages.md" },
        { "consent", "consent-privacy.md" },
        { "privacy", "consent-privacy.md" },
        { "commands", "commands-page.md" },
        { "settings", "settings-page.md" },
        { "audio", "audio-dependencies.md" },
        { "performance", "bot-performance-dashboard.md" },
        { "audit", "audit-log-system.md" },
        { "audit-log", "audit-log-system.md" },
        { "vox", "vox-system-spec.md" },
        { "vox-system", "vox-system-spec.md" },
        { "fvox", "vox-system-spec.md" },
        { "hgrunt", "vox-system-spec.md" }
    };

    // Feature metadata for listing
    private static readonly List<FeatureInfo> AllFeatures = new()
    {
        new("Soundboard", "Audio", "Upload and play audio clips in voice channels.", "soundboard"),
        new("Text-to-Speech", "Audio", "Send text-to-speech messages to voice channels using Azure Cognitive Services.", "tts-support"),
        new("VOX System", "Audio", "Half-Life style concatenated clip announcements using /vox, /fvox, and /hgrunt commands.", "vox-system-spec"),
        new("Rat Watch", "Accountability", "Community accountability system with voting, tracking, and leaderboards.", "rat-watch"),
        new("Reminders", "Productivity", "Personal reminders with natural language time parsing.", "reminder-system"),
        new("Scheduled Messages", "Automation", "Schedule one-time or recurring messages to channels.", "scheduled-messages"),
        new("Member Directory", "Management", "Browse and search guild members with filtering.", "member-directory"),
        new("Moderation", "Administration", "User moderation tools including warn, mute, kick, and ban.", "authorization-policies"),
        new("Welcome System", "Engagement", "Automated welcome messages for new members.", "welcome-system"),
        new("Commands", "Core", "Slash command system with module organization.", "commands-page"),
        new("Performance Dashboard", "Monitoring", "Real-time bot performance and health metrics.", "bot-performance-dashboard"),
        new("Audit Logs", "Security", "Track administrative actions and changes.", "audit-log-system"),
        new("Consent & Privacy", "Privacy", "User consent management for data collection features.", "consent-privacy")
    };

    /// <summary>
    /// Initializes a new instance of the DocumentationToolProvider.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="commandMetadataService">Service for command metadata.</param>
    /// <param name="assistantOptions">Assistant configuration options.</param>
    /// <param name="applicationOptions">Application configuration options.</param>
    public DocumentationToolProvider(
        ILogger<DocumentationToolProvider> logger,
        ICommandMetadataService commandMetadataService,
        IOptions<AssistantOptions> assistantOptions,
        IOptions<ApplicationOptions> applicationOptions)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _commandMetadataService = commandMetadataService ?? throw new ArgumentNullException(nameof(commandMetadataService));
        _assistantOptions = assistantOptions ?? throw new ArgumentNullException(nameof(assistantOptions));
        _applicationOptions = applicationOptions ?? throw new ArgumentNullException(nameof(applicationOptions));
    }

    /// <inheritdoc />
    public IEnumerable<LlmToolDefinition> GetTools()
    {
        return DocumentationTools.GetAllTools();
    }

    /// <inheritdoc />
    public async Task<ToolExecutionResult> ExecuteToolAsync(
        string toolName,
        JsonElement input,
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Executing documentation tool {ToolName}", toolName);

        try
        {
            return toolName.ToLowerInvariant() switch
            {
                "get_feature_documentation" => await ExecuteGetFeatureDocumentationAsync(input, context, cancellationToken),
                "search_commands" => await ExecuteSearchCommandsAsync(input, cancellationToken),
                "get_command_details" => await ExecuteGetCommandDetailsAsync(input, cancellationToken),
                "list_features" => ExecuteListFeatures(context),
                _ => throw new NotSupportedException($"Tool '{toolName}' is not supported by this provider")
            };
        }
        catch (NotSupportedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing documentation tool {ToolName}", toolName);
            return ToolExecutionResult.CreateError($"Error executing tool: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes the get_feature_documentation tool.
    /// </summary>
    private async Task<ToolExecutionResult> ExecuteGetFeatureDocumentationAsync(
        JsonElement input,
        ToolContext context,
        CancellationToken cancellationToken)
    {
        if (!input.TryGetProperty("feature_name", out var featureNameElement))
        {
            return ToolExecutionResult.CreateError("Missing required parameter: feature_name");
        }

        var featureName = featureNameElement.GetString();
        if (string.IsNullOrWhiteSpace(featureName))
        {
            return ToolExecutionResult.CreateError("Parameter feature_name cannot be empty");
        }

        _logger.LogDebug("Getting documentation for feature: {FeatureName}", featureName);

        // Map feature name to documentation file
        if (!FeatureDocumentationMap.TryGetValue(featureName, out var fileName))
        {
            // Try with .md extension
            fileName = $"{featureName}.md";
        }

        var docPath = Path.Combine(_assistantOptions.Value.DocumentationBasePath, fileName);

        // Resolve path
        var fullPath = Path.IsPathRooted(docPath)
            ? docPath
            : Path.Combine(Directory.GetCurrentDirectory(), docPath);

        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("Documentation file not found: {Path}", fullPath);
            return CreateJsonResult(new
            {
                error = true,
                message = $"Documentation for feature '{featureName}' not found. Available features can be listed using list_features tool."
            });
        }

        try
        {
            var content = await File.ReadAllTextAsync(fullPath, cancellationToken);

            // Generate guild-specific URLs in the content
            var baseUrl = GetBaseUrl();
            if (!string.IsNullOrEmpty(baseUrl) && context.GuildId > 0)
            {
                content = content
                    .Replace("{BASE_URL}", baseUrl)
                    .Replace("{GUILD_ID}", context.GuildId.ToString());
            }

            _logger.LogDebug("Successfully loaded documentation for {FeatureName} ({Length} chars)", featureName, content.Length);

            return CreateJsonResult(new
            {
                feature = featureName,
                content,
                available = true,
                last_updated = File.GetLastWriteTimeUtc(fullPath).ToString("o")
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading documentation file: {Path}", fullPath);
            return ToolExecutionResult.CreateError($"Error reading documentation: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes the search_commands tool.
    /// </summary>
    private async Task<ToolExecutionResult> ExecuteSearchCommandsAsync(
        JsonElement input,
        CancellationToken cancellationToken)
    {
        if (!input.TryGetProperty("query", out var queryElement))
        {
            return ToolExecutionResult.CreateError("Missing required parameter: query");
        }

        var query = queryElement.GetString();
        if (string.IsNullOrWhiteSpace(query))
        {
            return ToolExecutionResult.CreateError("Parameter query cannot be empty");
        }

        var limit = 10;
        if (input.TryGetProperty("limit", out var limitElement))
        {
            limit = Math.Clamp(limitElement.GetInt32(), 1, 50);
        }

        _logger.LogDebug("Searching commands with query: {Query}, limit: {Limit}", query, limit);

        var modules = await _commandMetadataService.GetAllModulesAsync(cancellationToken);

        var matchingCommands = modules
            .SelectMany(m => m.Commands)
            .Where(c =>
                c.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                c.FullName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                c.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                c.ModuleName.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .Select(c => new
            {
                name = c.FullName,
                description = c.Description,
                module = c.ModuleName,
                parameters = c.Parameters.Count,
                preconditions = c.Preconditions.Select(p => p.Name).ToList()
            })
            .ToList();

        var totalMatches = modules
            .SelectMany(m => m.Commands)
            .Count(c =>
                c.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                c.FullName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                c.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                c.ModuleName.Contains(query, StringComparison.OrdinalIgnoreCase));

        _logger.LogDebug("Found {Count} commands matching '{Query}'", matchingCommands.Count, query);

        return CreateJsonResult(new
        {
            results = matchingCommands,
            total_matches = totalMatches,
            limited_to = limit
        });
    }

    /// <summary>
    /// Executes the get_command_details tool.
    /// </summary>
    private async Task<ToolExecutionResult> ExecuteGetCommandDetailsAsync(
        JsonElement input,
        CancellationToken cancellationToken)
    {
        if (!input.TryGetProperty("command_name", out var commandNameElement))
        {
            return ToolExecutionResult.CreateError("Missing required parameter: command_name");
        }

        var commandName = commandNameElement.GetString();
        if (string.IsNullOrWhiteSpace(commandName))
        {
            return ToolExecutionResult.CreateError("Parameter command_name cannot be empty");
        }

        // Remove leading slash if present
        commandName = commandName.TrimStart('/');

        _logger.LogDebug("Getting details for command: {CommandName}", commandName);

        var modules = await _commandMetadataService.GetAllModulesAsync(cancellationToken);

        var command = modules
            .SelectMany(m => m.Commands)
            .FirstOrDefault(c =>
                c.Name.Equals(commandName, StringComparison.OrdinalIgnoreCase) ||
                c.FullName.Equals(commandName, StringComparison.OrdinalIgnoreCase));

        if (command == null)
        {
            _logger.LogDebug("Command not found: {CommandName}", commandName);
            return CreateJsonResult(new
            {
                error = true,
                message = $"Command '{commandName}' not found. Use search_commands to find available commands."
            });
        }

        _logger.LogDebug("Found command {CommandName} in module {Module}", command.FullName, command.ModuleName);

        return CreateJsonResult(new
        {
            name = command.FullName,
            description = command.Description,
            module = command.ModuleName,
            parameters = command.Parameters.Select(p => new
            {
                name = p.Name,
                type = p.Type,
                required = p.IsRequired,
                description = p.Description,
                @default = p.DefaultValue
            }).ToList(),
            preconditions = command.Preconditions.Select(p => new
            {
                name = p.Name,
                configuration = p.Configuration
            }).ToList(),
            examples = GenerateCommandExamples(command.FullName, command.Parameters.Select(p => (p.Name, p.IsRequired)).ToList())
        });
    }

    /// <summary>
    /// Executes the list_features tool.
    /// </summary>
    private ToolExecutionResult ExecuteListFeatures(ToolContext context)
    {
        _logger.LogDebug("Listing all features");

        var baseUrl = GetBaseUrl();
        var features = AllFeatures.Select(f => new
        {
            name = f.Name,
            category = f.Category,
            description = f.Description,
            enabled = true,
            availability = "All guilds",
            documentation_url = !string.IsNullOrEmpty(baseUrl)
                ? $"{baseUrl}/docs/articles/{f.DocumentationFile}"
                : null
        }).ToList();

        return CreateJsonResult(new
        {
            features,
            total_count = features.Count
        });
    }

    /// <summary>
    /// Gets the base URL for generating links.
    /// </summary>
    private string? GetBaseUrl()
    {
        return _assistantOptions.Value.BaseUrl ?? _applicationOptions.Value.BaseUrl;
    }

    /// <summary>
    /// Generates example usage for a command.
    /// </summary>
    private static List<string> GenerateCommandExamples(string commandName, List<(string Name, bool IsRequired)> parameters)
    {
        var examples = new List<string>();

        // Basic example with required parameters only
        var requiredParams = parameters.Where(p => p.IsRequired).ToList();
        if (requiredParams.Any())
        {
            var basicExample = $"/{commandName} " + string.Join(" ", requiredParams.Select(p => $"{p.Name}:<value>"));
            examples.Add(basicExample);
        }
        else
        {
            examples.Add($"/{commandName}");
        }

        // Full example with all parameters
        if (parameters.Any() && parameters.Count > requiredParams.Count)
        {
            var fullExample = $"/{commandName} " + string.Join(" ", parameters.Select(p => $"{p.Name}:<value>"));
            examples.Add(fullExample);
        }

        return examples;
    }

    /// <summary>
    /// Creates a JSON result from an object.
    /// </summary>
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

    /// <summary>
    /// Internal record for feature information.
    /// </summary>
    private record FeatureInfo(string Name, string Category, string Description, string DocumentationFile);
}
