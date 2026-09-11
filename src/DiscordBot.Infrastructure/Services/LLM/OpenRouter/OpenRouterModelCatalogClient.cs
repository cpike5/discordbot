using System.Globalization;
using System.Text.Json;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Interfaces.LLM;
using Microsoft.Extensions.Logging;
using DiscordBot.Core.DTOs.Llm.Reporting;

namespace DiscordBot.Infrastructure.Services.LLM.OpenRouter;

/// <summary>
/// OpenRouter implementation of <see cref="IOpenRouterModelCatalogClient"/>: a second typed
/// <see cref="HttpClient"/> alongside <see cref="OpenRouterLlmClient"/>, hitting <c>GET /models</c>
/// with the same auth and attribution headers. No retry loop - a refresh is user-initiated or
/// scheduled and can simply fail with a message.
/// </summary>
public class OpenRouterModelCatalogClient : IOpenRouterModelCatalogClient
{
    private readonly HttpClient _http;
    private readonly ILogger<OpenRouterModelCatalogClient> _logger;

    public OpenRouterModelCatalogClient(HttpClient http, ILogger<OpenRouterModelCatalogClient> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LlmCatalogModel>> GetModelsAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync("models", cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "OpenRouter returned {Status} for GET /models: {Body}", (int)response.StatusCode, Truncate(body));
            throw new OpenRouterException(
                $"OpenRouter error {(int)response.StatusCode} fetching model catalog: {Truncate(body)}",
                (int)response.StatusCode);
        }

        ModelListResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<ModelListResponse>(body, OpenRouterJson.Options);
        }
        catch (JsonException ex)
        {
            throw new OpenRouterException(
                $"OpenRouter returned a model catalog this client could not parse: {Truncate(body)}",
                inner: ex);
        }

        var results = new List<LlmCatalogModel>();
        foreach (var model in parsed?.Data ?? Array.Empty<ModelInfo>())
        {
            if (string.IsNullOrWhiteSpace(model.Id) || model.Id.StartsWith('~'))
            {
                // Alias entries point at a concrete slug (alias_target); admins pick concrete slugs.
                continue;
            }

            results.Add(ToCatalogModel(model));
        }

        _logger.LogInformation("Fetched {Count} model(s) from the OpenRouter catalog", results.Count);
        return results;
    }

    private static LlmCatalogModel ToCatalogModel(ModelInfo model)
    {
        var id = model.Id!;
        var slashIndex = id.IndexOf('/');
        var vendor = slashIndex > 0 ? id[..slashIndex] : id;

        return new LlmCatalogModel
        {
            Id = id,
            Name = string.IsNullOrWhiteSpace(model.Name) ? id : model.Name,
            Description = string.IsNullOrEmpty(model.Description)
                ? null
                : Truncate(model.Description, 1000),
            Vendor = vendor,
            ContextLength = model.ContextLength ?? 0,
            PromptPricePerMillion = ParsePricePerMillion(model.Pricing?.Prompt),
            CompletionPricePerMillion = ParsePricePerMillion(model.Pricing?.Completion),
            CacheReadPricePerMillion = ParsePricePerMillion(model.Pricing?.InputCacheRead),
            CacheWritePricePerMillion = ParsePricePerMillion(model.Pricing?.InputCacheWrite),
            SupportsTools = model.SupportedParameters?.Contains("tools") == true,
            SupportsImages = model.Architecture?.InputModalities?.Contains("image") == true,
            ReleasedAt = model.Created is { } created
                ? DateTimeOffset.FromUnixTimeSeconds(created).UtcDateTime
                : null,
        };
    }

    /// <summary>
    /// Converts a catalog per-token USD price string to a per-million-token decimal. "-1" (OpenRouter's
    /// "unknown") and anything else that doesn't parse becomes null - unknown is not free.
    /// </summary>
    private static decimal? ParsePricePerMillion(string? perTokenPrice)
    {
        if (string.IsNullOrWhiteSpace(perTokenPrice))
        {
            return null;
        }

        if (!decimal.TryParse(perTokenPrice, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return null;
        }

        if (value < 0)
        {
            // "-1" is OpenRouter's sentinel for "unknown".
            return null;
        }

        return value * 1_000_000m;
    }

    private static string Truncate(string? value, int max = 500)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value ?? string.Empty;
        }

        if (value.Length <= max)
        {
            return value;
        }

        // Must never exceed max: the Description column is HasMaxLength(1000) and Postgres enforces
        // it at write time, so "..." has to come out of the budget, not be appended on top of it.
        var truncateLength = Math.Max(0, max - 3);
        return value[..truncateLength] + "...";
    }
}
