using System.Security.Claims;
using DiscordBot.Bot.Helpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services.Search;

/// <summary>
/// Search provider for the <see cref="SearchCategory.Commands"/> category.
/// Searches registered slash-command metadata.
/// </summary>
public class CommandsSearchProvider : ISearchProvider
{
    private readonly ICommandMetadataService _commandMetadataService;
    private readonly ILogger<CommandsSearchProvider> _logger;

    /// <inheritdoc/>
    public SearchCategory Category => SearchCategory.Commands;

    /// <inheritdoc/>
    public bool RequiresAdmin => false;

    public CommandsSearchProvider(ICommandMetadataService commandMetadataService, ILogger<CommandsSearchProvider> logger)
    {
        _commandMetadataService = commandMetadataService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<SearchCategoryResult> SearchAsync(
        string searchTerm,
        int maxResults,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Searching commands for term: {SearchTerm}", searchTerm);

        var modules = await _commandMetadataService.GetAllModulesAsync(cancellationToken);

        // Flatten all commands from all modules
        var allCommands = modules.SelectMany(m => m.Commands).ToList();

        var items = allCommands
            .Select(cmd => new
            {
                Command = cmd,
                Score = SearchScoringHelper.CalculateRelevanceScore(cmd.FullName, searchTerm) +
                        SearchScoringHelper.CalculateRelevanceScore(cmd.Name, searchTerm) +
                        SearchScoringHelper.CalculateRelevanceScore(cmd.Description, searchTerm) / 2 +
                        SearchScoringHelper.CalculateRelevanceScore(cmd.ModuleName, searchTerm) / 2
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(maxResults)
            .Select(x => new SearchResultItemDto
            {
                Id = x.Command.FullName,
                Title = $"/{x.Command.FullName}",
                Subtitle = x.Command.ModuleName,
                Description = x.Command.Description,
                BadgeText = x.Command.ModuleName,
                BadgeVariant = "primary",
                Url = $"/Commands#cmd-{x.Command.FullName.Replace(" ", "-")}",
                RelevanceScore = SearchScoringHelper.Clamp(x.Score),
                Metadata = new Dictionary<string, string>
                {
                    ["ParameterCount"] = x.Command.Parameters.Count.ToString(),
                    ["PreconditionCount"] = x.Command.Preconditions.Count.ToString(),
                    ["ModuleName"] = x.Command.ModuleName
                }
            })
            .ToList();

        var matchingCount = allCommands.Count(cmd =>
            SearchScoringHelper.CalculateRelevanceScore(cmd.FullName, searchTerm) +
            SearchScoringHelper.CalculateRelevanceScore(cmd.Name, searchTerm) > 0);

        return new SearchCategoryResult
        {
            Category = SearchCategory.Commands,
            DisplayName = "Commands",
            Items = items,
            TotalCount = items.Count,
            HasMore = matchingCount > maxResults,
            ViewAllUrl = $"/Commands?search={Uri.EscapeDataString(searchTerm)}"
        };
    }
}
