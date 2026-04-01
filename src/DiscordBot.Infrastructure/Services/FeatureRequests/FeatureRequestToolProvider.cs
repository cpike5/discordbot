using System.Text.Json;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Core.Models.FeatureRequests;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Services.FeatureRequests;

/// <summary>
/// Tool provider for the feature request requirements-gathering conversation.
/// Exposes a single <c>submit_feature_request</c> tool that the agent calls
/// when it has gathered enough information from the user.
/// </summary>
public class FeatureRequestToolProvider : IToolProvider
{
    private const string ToolName = "submit_feature_request";

    private static readonly JsonElement InputSchema = JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "title": {
                    "type": "string",
                    "description": "A short, descriptive title for the feature request (max 100 characters)."
                },
                "problem_statement": {
                    "type": "string",
                    "description": "What problem does this feature solve, or what is the user trying to accomplish?"
                },
                "success_criteria": {
                    "type": "string",
                    "description": "How would the user know this feature is working well? What does success look like?"
                },
                "priority": {
                    "type": "string",
                    "description": "How important is this? Nice-to-have, important, or blocking?"
                },
                "summary": {
                    "type": "string",
                    "description": "A consolidated summary of the entire feature request including all gathered context."
                }
            },
            "required": ["title", "problem_statement", "success_criteria", "priority", "summary"]
        }
        """).RootElement.Clone();

    private static readonly LlmToolDefinition ToolDefinition = new()
    {
        Name = ToolName,
        Description = "Submit the feature request with all gathered requirements. " +
                      "Call this once you have enough information from the user to write a clear, actionable request.",
        InputSchema = InputSchema
    };

    private readonly IFeatureRequestService _featureRequestService;
    private readonly ILogger<FeatureRequestToolProvider> _logger;

    public FeatureRequestToolProvider(
        IFeatureRequestService featureRequestService,
        ILogger<FeatureRequestToolProvider> logger)
    {
        _featureRequestService = featureRequestService;
        _logger = logger;
    }

    public string Name => "FeatureRequest";
    public string Description => "Submits a feature request after requirements gathering.";

    public IEnumerable<LlmToolDefinition> GetTools() => [ToolDefinition];

    public async Task<ToolExecutionResult> ExecuteToolAsync(
        string toolName,
        JsonElement input,
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        if (!toolName.Equals(ToolName, StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Tool '{toolName}' is not supported by {Name} provider.");

        try
        {
            var title = input.GetProperty("title").GetString() ?? "Untitled";
            var problemStatement = input.GetProperty("problem_statement").GetString() ?? string.Empty;
            var successCriteria = input.GetProperty("success_criteria").GetString() ?? string.Empty;
            var priority = input.GetProperty("priority").GetString() ?? string.Empty;
            var summary = input.GetProperty("summary").GetString() ?? string.Empty;

            if (title.Length > 100)
                title = title[..100];

            var gathered = new GatheredRequirements
            {
                ProblemStatement = problemStatement,
                SuccessCriteria = successCriteria,
                Priority = priority
            };

            var submission = new FeatureRequestSubmission
            {
                GuildId = context.GuildId,
                SubmittedByUserId = context.UserId,
                Description = summary,
                GatheredRequirementsJson = JsonSerializer.Serialize(gathered),
                ConsolidatedSummary = summary
            };

            var request = await _featureRequestService.SubmitAsync(submission);
            var shortId = request.Id.ToString("N")[..8].ToUpperInvariant();

            _logger.LogInformation(
                "Feature request #{ShortId} submitted via AI gathering for user {UserId} in guild {GuildId}",
                shortId, context.UserId, context.GuildId);

            var resultJson = JsonSerializer.SerializeToElement(new
            {
                success = true,
                request_id = request.Id.ToString(),
                short_id = shortId,
                title
            });

            return ToolExecutionResult.CreateSuccess(resultJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit feature request via tool for user {UserId}", context.UserId);
            return ToolExecutionResult.CreateError($"Failed to submit feature request: {ex.Message}");
        }
    }
}
