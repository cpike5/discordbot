using Discord;
using Discord.WebSocket;
using DiscordBot.Bot.Extensions;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Controller for autocomplete search endpoints used by UI components.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "RequireViewer")]
public class AutocompleteController : ControllerBase
{
    private readonly ILogger<AutocompleteController> _logger;
    private readonly IMessageLogRepository _messageLogRepository;
    private readonly IGuildService _guildService;
    private readonly ICommandMetadataService _commandMetadataService;
    private readonly DiscordSocketClient _discordClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutocompleteController"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="messageLogRepository">The message log repository.</param>
    /// <param name="guildService">The guild service.</param>
    /// <param name="commandMetadataService">The command metadata service.</param>
    /// <param name="discordClient">The Discord socket client.</param>
    public AutocompleteController(
        ILogger<AutocompleteController> logger,
        IMessageLogRepository messageLogRepository,
        IGuildService guildService,
        ICommandMetadataService commandMetadataService,
        DiscordSocketClient discordClient)
    {
        _logger = logger;
        _messageLogRepository = messageLogRepository;
        _guildService = guildService;
        _commandMetadataService = commandMetadataService;
        _discordClient = discordClient;
    }

    /// <summary>
    /// Searches for users by username in message logs.
    /// </summary>
    /// <param name="search">The search term to match against usernames.</param>
    /// <param name="guildId">Optional guild ID to filter results to a specific guild.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of user autocomplete suggestions.</returns>
    [HttpGet("users")]
    [ProducesResponseType(typeof(List<AutocompleteSuggestionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<AutocompleteSuggestionDto>>> SearchUsers(
        [FromQuery] string search,
        [FromQuery] ulong? guildId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("User search requested: {Search}, guildId: {GuildId}", search, guildId);

        if (string.IsNullOrWhiteSpace(search))
        {
            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid request",
                Detail = "Search term cannot be empty.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        var authors = await _messageLogRepository.SearchAuthorsAsync(
            search,
            guildId,
            limit: 25,
            cancellationToken);

        var suggestions = authors
            .Select(a => new AutocompleteSuggestionDto
            {
                Id = a.UserId.ToString(),
                DisplayText = a.Username
            })
            .ToList();

        _logger.LogTrace("Found {Count} users matching: {Search}", suggestions.Count, search);

        return Ok(suggestions);
    }

    /// <summary>
    /// Searches for guilds by name.
    /// </summary>
    /// <param name="search">The search term to match against guild names.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of guild autocomplete suggestions.</returns>
    [HttpGet("guilds")]
    [ProducesResponseType(typeof(List<AutocompleteSuggestionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<AutocompleteSuggestionDto>>> SearchGuilds(
        [FromQuery] string search,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Guild search requested: {Search}", search);

        if (string.IsNullOrWhiteSpace(search))
        {
            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid request",
                Detail = "Search term cannot be empty.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        var allGuilds = await _guildService.GetAllGuildsAsync(cancellationToken);

        var searchLower = search.ToLowerInvariant();
        var matchingGuilds = allGuilds
            .Where(g => g.Name.ToLowerInvariant().Contains(searchLower))
            .Take(25)
            .Select(g => new AutocompleteSuggestionDto
            {
                Id = g.Id.ToString(),
                DisplayText = g.Name
            })
            .ToList();

        _logger.LogTrace("Found {Count} guilds matching: {Search}", matchingGuilds.Count, search);

        return Ok(matchingGuilds);
    }

    /// <summary>
    /// Searches for channels by name within a guild.
    /// </summary>
    /// <param name="search">The search term to match against channel names.</param>
    /// <param name="guildId">The guild ID to search channels in (required).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of channel autocomplete suggestions.</returns>
    [HttpGet("channels")]
    [ProducesResponseType(typeof(List<ChannelSuggestionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ChannelSuggestionDto>>> SearchChannels(
        [FromQuery] string search,
        [FromQuery] ulong? guildId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Channel search requested: {Search}, guildId: {GuildId}", search, guildId);

        if (string.IsNullOrWhiteSpace(search))
        {
            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid request",
                Detail = "Search term cannot be empty.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        if (!guildId.HasValue)
        {
            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid request",
                Detail = "Guild ID is required for channel search.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        var guild = _discordClient.GetGuild(guildId.Value);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found for channel search", guildId.Value);

            return NotFound(new ApiErrorDto
            {
                Message = "Guild not found",
                Detail = $"No guild with ID {guildId.Value} is connected to the bot.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        var searchLower = search.ToLowerInvariant();
        var matchingChannels = guild.Channels
            .Where(c => c is ITextChannel || c is IVoiceChannel || c is IThreadChannel)
            .Where(c => c.Name.ToLowerInvariant().Contains(searchLower))
            .OrderBy(c => c.Name)
            .Take(25)
            .Select(c => new ChannelSuggestionDto
            {
                Id = c.Id.ToString(),
                DisplayText = c.Name,
                ChannelType = GetChannelTypeName(c)
            })
            .ToList();

        _logger.LogTrace("Found {Count} channels matching: {Search} in guild {GuildId}",
            matchingChannels.Count, search, guildId.Value);

        return Ok(matchingChannels);
    }

    /// <summary>
    /// Searches for commands by name.
    /// </summary>
    /// <param name="search">The search term to match against command names.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of command autocomplete suggestions.</returns>
    [HttpGet("commands")]
    [ProducesResponseType(typeof(List<AutocompleteSuggestionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<AutocompleteSuggestionDto>>> SearchCommands(
        [FromQuery] string search,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Command search requested: {Search}", search);

        if (string.IsNullOrWhiteSpace(search))
        {
            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid request",
                Detail = "Search term cannot be empty.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        var modules = await _commandMetadataService.GetAllModulesAsync(cancellationToken);

        var searchLower = search.ToLowerInvariant();
        var allCommands = modules
            .SelectMany(m => m.Commands)
            .Where(c => c.FullName.ToLowerInvariant().Contains(searchLower))
            .OrderBy(c => c.FullName)
            .Take(25)
            .Select(c => new AutocompleteSuggestionDto
            {
                Id = c.FullName,
                DisplayText = $"/{c.FullName} - {c.Description}"
            })
            .ToList();

        _logger.LogTrace("Found {Count} commands matching: {Search}", allCommands.Count, search);

        return Ok(allCommands);
    }

    /// <summary>
    /// Gets a human-readable channel type name.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <returns>The channel type name.</returns>
    private static string GetChannelTypeName(IChannel channel)
    {
        return channel switch
        {
            IThreadChannel thread => thread.Type == ThreadType.PublicThread
                ? "Public Thread"
                : "Private Thread",
            ICategoryChannel => "Category",
            IStageChannel => "Stage",
            INewsChannel => "News",
            IVoiceChannel => "Voice",
            ITextChannel => "Text",
            _ => "Unknown"
        };
    }
}
