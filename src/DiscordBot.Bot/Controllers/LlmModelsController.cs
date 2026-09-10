using System.Security.Claims;
using DiscordBot.Bot.Extensions;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Admin endpoints for the local OpenRouter model catalog and allowlist: list/filter, refresh from
/// OpenRouter, enable/disable a model, and the effective per-mode default slugs. All catalog work is
/// delegated to <see cref="ILlmModelCatalogService"/>; this controller only maps to/from DTOs.
/// </summary>
[Route("api/admin/llm-models")]
[Authorize(Policy = "RequireAdmin")]
public class LlmModelsController : ApiControllerBase
{
    private readonly ILlmModelCatalogService _catalogService;
    private readonly ISettingsService _settingsService;
    private readonly IOptions<AssistantOptions> _assistantOptions;
    private readonly IOptions<DmAssistantOptions> _dmAssistantOptions;
    private readonly IOptions<FeatureRequestsOptions> _featureRequestsOptions;
    private readonly ILogger<LlmModelsController> _logger;

    private const string GuildAssistantKey = "Assistant:Sampling:Model";
    private const string DmAssistantKey = "DmAssistant:Model";
    private const string FeatureRequestsKey = "FeatureRequests:RequirementsGatheringModel";

    public LlmModelsController(
        ILlmModelCatalogService catalogService,
        ISettingsService settingsService,
        IOptions<AssistantOptions> assistantOptions,
        IOptions<DmAssistantOptions> dmAssistantOptions,
        IOptions<FeatureRequestsOptions> featureRequestsOptions,
        ILogger<LlmModelsController> logger)
    {
        _catalogService = catalogService;
        _settingsService = settingsService;
        _assistantOptions = assistantOptions;
        _dmAssistantOptions = dmAssistantOptions;
        _featureRequestsOptions = featureRequestsOptions;
        _logger = logger;
    }

    /// <summary>
    /// Lists the local catalog, filtered and sorted per the query parameters.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(LlmModelListResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LlmModelListResponseDto>> GetCatalog(
        [FromQuery] string? search,
        [FromQuery] string? vendor,
        [FromQuery] bool enabledOnly = false,
        [FromQuery] bool availableOnly = false,
        [FromQuery] bool toolsOnly = false,
        [FromQuery] LlmModelSortBy sortBy = LlmModelSortBy.Name,
        [FromQuery] bool descending = false,
        CancellationToken cancellationToken = default)
    {
        var filter = new LlmModelCatalogFilter
        {
            SearchText = search,
            Vendor = vendor,
            EnabledOnly = enabledOnly,
            AvailableOnly = availableOnly,
            ToolsOnly = toolsOnly,
            SortBy = sortBy,
            Descending = descending
        };

        var models = await _catalogService.GetCatalogAsync(filter, cancellationToken);

        // Vendor list is drawn from the full catalog, not the filtered page, so the vendor <select>
        // doesn't shrink as other filters are applied. GetVendorsAsync runs a dedicated distinct-vendor
        // query instead of a second unfiltered GetCatalogAsync pass over every column.
        var vendors = await _catalogService.GetVendorsAsync(cancellationToken);

        var lastRefreshAt = await _catalogService.GetLastRefreshAsync(cancellationToken);

        return Ok(new LlmModelListResponseDto
        {
            Models = models.Select(ToDto).ToList(),
            Vendors = vendors,
            LastRefreshAt = lastRefreshAt
        });
    }

    /// <summary>Refreshes the catalog from OpenRouter.</summary>
    [HttpPost("refresh")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(typeof(LlmCatalogRefreshResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<LlmCatalogRefreshResult>> Refresh(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        _logger.LogInformation("LLM catalog refresh requested by user {UserId}", userId);

        var result = await _catalogService.RefreshAsync(userId, cancellationToken);

        _logger.LogInformation(
            "LLM catalog refresh completed: {Added} added, {Updated} updated, {Removed} removed",
            result.Added, result.Updated, result.Removed);

        return Ok(result);
    }

    /// <summary>
    /// Enables or disables one model. The slug travels in the request body (not the route) because
    /// OpenRouter slugs contain "/" (e.g. "anthropic/claude-sonnet-4.6"), which does not round-trip
    /// cleanly through a route segment or an ASP.NET Core catch-all (a catch-all must be the final
    /// route segment, which "/{**slug}/enabled" is not).
    /// </summary>
    [HttpPut("enabled")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(typeof(LlmModelEnableResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LlmModelEnableResult>> SetEnabled(
        [FromBody] LlmModelSetSlugEnabledDto request,
        CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Slug))
        {
            return BadRequestError("Invalid request", "A model slug is required.");
        }

        if (request.Slug.Length > 200)
        {
            return BadRequestError("Invalid request", "A model slug cannot exceed 200 characters.");
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var result = await _catalogService.SetEnabledAsync(request.Slug, request.Enabled, userId, cancellationToken);

        if (!result.Success)
        {
            _logger.LogWarning(
                "LLM model {Slug} enabled={Enabled} rejected: {Error}", request.Slug, request.Enabled, result.Error);

            return BadRequestError(result.Error ?? "Unable to change this model's enabled state.");
        }

        _logger.LogInformation(
            "LLM model {Slug} enabled={Enabled} by user {UserId}", request.Slug, request.Enabled, userId);

        return Ok(result);
    }

    /// <summary>
    /// Returns the effective slug for each mode (guild assistant, DM assistant, feature requests) and
    /// where it comes from (a DB override vs. the bound configuration value), plus whether that slug
    /// is currently known/enabled/available in the catalog.
    /// </summary>
    [HttpGet("defaults")]
    [ProducesResponseType(typeof(LlmModelDefaultsResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LlmModelDefaultsResponseDto>> GetDefaults(CancellationToken cancellationToken)
    {
        var catalog = await _catalogService.GetCatalogAsync(new LlmModelCatalogFilter(), cancellationToken);
        var bySlug = catalog.ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);

        var modes = new List<LlmModeDefaultDto>
        {
            await ResolveModeAsync(
                "GuildAssistant", "Guild Assistant", GuildAssistantKey,
                _assistantOptions.Value.Sampling.Model, bySlug, cancellationToken),
            await ResolveModeAsync(
                "DmAssistant", "DM Assistant", DmAssistantKey,
                _dmAssistantOptions.Value.Model, bySlug, cancellationToken),
            await ResolveModeAsync(
                "FeatureRequests", "Feature Requests", FeatureRequestsKey,
                _featureRequestsOptions.Value.RequirementsGatheringModel, bySlug, cancellationToken)
        };

        return Ok(new LlmModelDefaultsResponseDto { Modes = modes });
    }

    private async Task<LlmModeDefaultDto> ResolveModeAsync(
        string mode,
        string label,
        string settingKey,
        string configuredSlug,
        IReadOnlyDictionary<string, LlmModel> bySlug,
        CancellationToken cancellationToken)
    {
        var storedValue = await _settingsService.GetStoredValueAsync(settingKey, cancellationToken);
        var hasDbOverride = !string.IsNullOrWhiteSpace(storedValue);

        var slug = hasDbOverride ? storedValue! : configuredSlug;
        var source = hasDbOverride ? LlmModelDefaultSource.Db : LlmModelDefaultSource.Config;

        var known = bySlug.TryGetValue(slug, out var model);

        return new LlmModeDefaultDto
        {
            Mode = mode,
            Label = label,
            SettingKey = settingKey,
            Slug = slug,
            Source = source,
            IsKnown = known,
            IsEnabled = known && model!.IsEnabled,
            IsAvailable = known && model!.IsAvailable
        };
    }

    private static LlmModelDto ToDto(LlmModel model) => new()
    {
        Slug = model.Id,
        Name = model.Name,
        Description = model.Description,
        Vendor = model.Vendor,
        ContextLength = model.ContextLength,
        PromptPricePerMillion = model.PromptPricePerMillion,
        CompletionPricePerMillion = model.CompletionPricePerMillion,
        CacheReadPricePerMillion = model.CacheReadPricePerMillion,
        CacheWritePricePerMillion = model.CacheWritePricePerMillion,
        SupportsTools = model.SupportsTools,
        SupportsImages = model.SupportsImages,
        ReleasedAt = model.ReleasedAt,
        IsAvailable = model.IsAvailable,
        IsEnabled = model.IsEnabled,
        EnabledAt = model.EnabledAt
    };
}
