using System.Text.Json;
using cpike.Agent;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Models.FeatureRequests;
using DiscordBot.Infrastructure.Services.LLM.Tooling;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Services.FeatureRequests;

/// <summary>
/// <c>submit_feature_request</c> — the agent calls it once the requirements-gathering conversation
/// has produced enough information to write a clear, actionable request.
/// </summary>
public sealed class SubmitFeatureRequestTool : IAgentTool
{
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

    private static readonly AgentToolDefinition ToolDefinition = AgentTools.Define(
        "submit_feature_request",
        "Submit the feature request with all gathered requirements. " +
        "Call this once you have enough information from the user to write a clear, actionable request.",
        InputSchema,
        requiresEdit: true);

    private readonly IFeatureRequestService _featureRequestService;
    private readonly DiscordRunContext _run;
    private readonly ILogger<SubmitFeatureRequestTool> _logger;

    /// <summary>Creates the tool.</summary>
    public SubmitFeatureRequestTool(
        IFeatureRequestService featureRequestService,
        DiscordRunContext run,
        ILogger<SubmitFeatureRequestTool> logger)
    {
        _featureRequestService = featureRequestService;
        _run = run;
        _logger = logger;
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
                GuildId = _run.GuildId,
                SubmittedByUserId = _run.UserId,
                Description = summary,
                GatheredRequirementsJson = JsonSerializer.Serialize(gathered),
                ConsolidatedSummary = summary
            };

            var request = await _featureRequestService.SubmitAsync(submission);
            var shortId = request.Id.ToString("N")[..8].ToUpperInvariant();

            _logger.LogInformation(
                "Feature request #{ShortId} submitted via AI gathering for user {UserId} in guild {GuildId}",
                shortId, _run.UserId, _run.GuildId);

            return ToolResults.Json(new
            {
                success = true,
                request_id = request.Id.ToString(),
                short_id = shortId,
                title
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit feature request via tool for user {UserId}", _run.UserId);
            return ToolResults.Error($"Failed to submit feature request: {ex.Message}");
        }
    }
}
