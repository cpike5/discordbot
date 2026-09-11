using System.Security.Claims;
using DiscordBot.Bot.Extensions;
using DiscordBot.Core.DTOs;
using DiscordBot.Agents.Contracts;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Agents.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DiscordBot.Core.DTOs.Llm.Reporting;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Admin endpoints for the local OpenRouter model catalog and allowlist: list/filter, refresh from
/// OpenRouter, enable/disable a model, and the effective per-mode default slugs. All catalog work is
/// delegated to <see cref="ILlmModelCatalogService"/>; per-mode default resolution is delegated to
/// <see cref="ILlmModelResolver"/> - the same single resolution path the guild/DM assistant context
/// factories and the feature-request conversation service use. This controller only maps to/from DTOs.
/// </summary>
[Route("api/admin/llm-models")]
[Authorize(Policy = "RequireAdmin")]
public class LlmModelsController : ApiControllerBase
{
    private readonly ILlmModelCatalogService _catalogService;
    private readonly ILlmModelResolver _modelResolver;
    private readonly ILogger<LlmModelsController> _logger;

    public LlmModelsController(
        ILlmModelCatalogService catalogService,
        ILlmModelResolver modelResolver,
        ILogger<LlmModelsController> logger)
    {
        _catalogService = catalogService;
        _modelResolver = modelResolver;
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
    /// where it comes from (a DB override, the bound configuration value, or the last-resort
    /// <c>OpenRouter:DefaultModel</c> fallback), plus whether that slug is currently known/enabled/
    /// available in the catalog. Delegates entirely to <see cref="ILlmModelResolver"/> - the same
    /// resolution path used at message-send time.
    /// </summary>
    [HttpGet("defaults")]
    [ProducesResponseType(typeof(LlmModelDefaultsResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LlmModelDefaultsResponseDto>> GetDefaults(CancellationToken cancellationToken)
    {
        var modes = new List<LlmModeDefaultDto>();

        foreach (var mode in LlmModeSettings.All)
        {
            var resolved = await _modelResolver.ResolveAsync(mode, cancellationToken);

            modes.Add(new LlmModeDefaultDto
            {
                Mode = mode.ToString(),
                Label = LlmModeSettings.LabelFor(mode),
                SettingKey = LlmModeSettings.KeyFor(mode),
                Slug = resolved.Slug,
                Source = ToDtoSource(resolved.Source),
                ConfiguredSlug = resolved.ConfiguredSlug,
                IsKnown = resolved.IsEnabled.HasValue,
                IsEnabled = resolved.IsEnabled ?? false,
                IsAvailable = resolved.IsAvailable ?? false
            });
        }

        return Ok(new LlmModelDefaultsResponseDto { Modes = modes });
    }

    private static LlmModelDefaultSource ToDtoSource(LlmModelResolutionSource source) => source switch
    {
        LlmModelResolutionSource.Database => LlmModelDefaultSource.Db,
        LlmModelResolutionSource.Configuration => LlmModelDefaultSource.Config,
        LlmModelResolutionSource.Fallback => LlmModelDefaultSource.Fallback,
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown resolution source.")
    };

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
