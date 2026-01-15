using System.Text.RegularExpressions;
using DiscordBot.Core.Interfaces.LLM;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Services.LLM;

/// <summary>
/// Loads and renders prompt templates with variable substitution.
/// Supports file caching for improved performance.
/// </summary>
public partial class PromptTemplate : IPromptTemplate
{
    private readonly ILogger<PromptTemplate> _logger;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    // Regex to match {{variable}} placeholders
    [GeneratedRegex(@"\{\{(\w+)\}\}", RegexOptions.Compiled)]
    private static partial Regex PlaceholderRegex();

    /// <summary>
    /// Initializes a new instance of the PromptTemplate class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="cache">Memory cache for template caching.</param>
    public PromptTemplate(ILogger<PromptTemplate> logger, IMemoryCache cache)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <inheritdoc />
    public async Task<string> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var cacheKey = $"prompt_template:{filePath}";

        if (_cache.TryGetValue(cacheKey, out string? cachedContent) && cachedContent != null)
        {
            _logger.LogDebug("Loaded template from cache: {FilePath}", filePath);
            return cachedContent;
        }

        _logger.LogDebug("Loading template from disk: {FilePath}", filePath);

        // Resolve relative paths from application root
        var fullPath = Path.IsPathRooted(filePath)
            ? filePath
            : Path.Combine(AppContext.BaseDirectory, filePath);

        // Also check relative to working directory if not found
        if (!File.Exists(fullPath))
        {
            var workingDirPath = Path.Combine(Directory.GetCurrentDirectory(), filePath);
            if (File.Exists(workingDirPath))
            {
                fullPath = workingDirPath;
            }
        }

        if (!File.Exists(fullPath))
        {
            _logger.LogError("Template file not found: {FilePath} (resolved to {FullPath})", filePath, fullPath);
            throw new FileNotFoundException($"Template file not found: {filePath}", filePath);
        }

        try
        {
            var content = await File.ReadAllTextAsync(fullPath, cancellationToken);

            // Cache the content
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(_cacheDuration)
                .SetSize(content.Length); // Track memory size

            _cache.Set(cacheKey, content, cacheOptions);

            _logger.LogDebug(
                "Loaded and cached template: {FilePath} ({Length} chars, cached for {Duration})",
                filePath,
                content.Length,
                _cacheDuration);

            return content;
        }
        catch (Exception ex) when (ex is not FileNotFoundException)
        {
            _logger.LogError(ex, "Failed to read template file: {FilePath}", filePath);
            throw;
        }
    }

    /// <inheritdoc />
    public string Render(string template, Dictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(template);
        variables ??= new Dictionary<string, string>();

        if (variables.Count == 0)
        {
            _logger.LogDebug("No variables provided for template substitution");
            return template;
        }

        var result = PlaceholderRegex().Replace(template, match =>
        {
            var variableName = match.Groups[1].Value;

            if (variables.TryGetValue(variableName, out var value))
            {
                _logger.LogDebug("Substituting variable {{{VariableName}}}", variableName);
                return value ?? string.Empty;
            }

            // Leave unmatched placeholders as-is (don't fail silently)
            _logger.LogDebug("Variable not found, leaving placeholder: {{{VariableName}}}", variableName);
            return match.Value;
        });

        var substitutionCount = variables.Keys.Count(v =>
            template.Contains($"{{{{{v}}}}}", StringComparison.OrdinalIgnoreCase));

        _logger.LogDebug(
            "Rendered template with {SubstitutionCount}/{TotalVariables} variable substitutions",
            substitutionCount,
            variables.Count);

        return result;
    }

    /// <summary>
    /// Invalidates the cached template for a specific file path.
    /// </summary>
    /// <param name="filePath">The file path to invalidate.</param>
    public void InvalidateCache(string filePath)
    {
        var cacheKey = $"prompt_template:{filePath}";
        _cache.Remove(cacheKey);
        _logger.LogDebug("Invalidated template cache: {FilePath}", filePath);
    }
}
