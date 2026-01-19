using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Commands;

/// <summary>
/// Page model for displaying all registered command modules and their commands.
/// </summary>
[Authorize(Policy = "RequireViewer")]
public class IndexModel : PageModel
{
    private readonly ICommandMetadataService _commandMetadataService;
    private readonly ICommandRegistrationService _commandRegistrationService;
    private readonly IGuildService _guildService;
    private readonly ILogger<IndexModel> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexModel"/> class.
    /// </summary>
    /// <param name="commandMetadataService">The command metadata service.</param>
    /// <param name="commandRegistrationService">The command registration service.</param>
    /// <param name="guildService">The guild service.</param>
    /// <param name="logger">The logger.</param>
    public IndexModel(
        ICommandMetadataService commandMetadataService,
        ICommandRegistrationService commandRegistrationService,
        IGuildService guildService,
        ILogger<IndexModel> logger)
    {
        _commandMetadataService = commandMetadataService;
        _commandRegistrationService = commandRegistrationService;
        _guildService = guildService;
        _logger = logger;
    }

    /// <summary>
    /// Gets or sets the active tab identifier.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string ActiveTab { get; set; } = "command-list";

    /// <summary>
    /// Gets the view model containing command list data for the Command List tab.
    /// </summary>
    public CommandsListViewModel ViewModel { get; private set; } = new();

    /// <summary>
    /// Gets the view model containing command logs data for the Execution Logs tab.
    /// This will be populated by AJAX calls in a future update.
    /// </summary>
    public CommandLogListViewModel CommandLogs { get; private set; } = new();

    /// <summary>
    /// Gets the view model containing analytics data for the Analytics tab.
    /// This will be populated by AJAX calls in a future update.
    /// </summary>
    public CommandAnalyticsViewModel CommandAnalytics { get; private set; } = new();

    /// <summary>
    /// Gets or sets the start date for filtering command logs and analytics.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Gets or sets the end date for filtering command logs and analytics.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Gets or sets the guild ID for filtering command logs and analytics.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public ulong? GuildId { get; set; }

    /// <summary>
    /// Gets or sets the search term for filtering command logs.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Gets or sets the command name for filtering command logs.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? CommandName { get; set; }

    /// <summary>
    /// Gets or sets the status filter for command logs (true=success, false=failure, null=all).
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public bool? StatusFilter { get; set; }

    /// <summary>
    /// Gets the list of available guilds for filtering.
    /// </summary>
    public IReadOnlyList<GuildDto> AvailableGuilds { get; private set; } = Array.Empty<GuildDto>();

    /// <summary>
    /// Gets the clear commands confirmation modal configuration.
    /// </summary>
    public ConfirmationModalViewModel ClearCommandsModal { get; private set; } = null!;

    /// <summary>
    /// Handles the GET request for the Commands page.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("User accessing commands page");

        // Load available guilds for filter dropdown
        AvailableGuilds = await _guildService.GetAllGuildsAsync(cancellationToken);

        // Apply default date range (last 7 days) when no shared filters are active.
        // Note: This differs from CommandLogs page which defaults to "Today".
        // The requirements for issue #1220 explicitly specify "Last 7 days when page first loads".
        // We only check shared filters (StartDate, EndDate, GuildId) here to avoid conflicts
        // between tabs. The Execution Logs and Analytics tabs have additional filters
        // (SearchTerm, CommandName, StatusFilter) that should not affect date range defaults.
        if (!StartDate.HasValue && !EndDate.HasValue && !GuildId.HasValue)
        {
            EndDate = DateTime.UtcNow.Date;
            StartDate = EndDate.Value.AddDays(-7);
        }

        // Load Command List data (always loaded for first tab)
        var modules = await _commandMetadataService.GetAllModulesAsync(cancellationToken);
        ViewModel = CommandsListViewModel.FromDtos(modules);

        _logger.LogDebug(
            "Loaded {ModuleCount} modules with {CommandCount} total commands",
            ViewModel.ModuleCount,
            ViewModel.TotalCommandCount);

        // Initialize placeholder ViewModels for other tabs
        // These will be populated by AJAX calls in issue #1221
        CommandLogs = new CommandLogListViewModel();
        CommandAnalytics = new CommandAnalyticsViewModel();

        // Initialize modal
        ClearCommandsModal = new ConfirmationModalViewModel
        {
            Id = "clearCommandsModal",
            Title = "Clear & Re-register Commands Globally",
            Message = "This will clear all registered commands (global and guild-specific) and re-register them globally. Global commands may take up to 1 hour to propagate to all servers. Are you sure you want to continue?",
            ConfirmText = "Clear & Re-register",
            CancelText = "Cancel",
            Variant = ConfirmationVariant.Warning,
            FormHandler = "ClearAndRegisterGlobally"
        };
    }

    /// <summary>
    /// Handles POST requests to clear and re-register commands globally.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>JSON result indicating success or failure.</returns>
    public async Task<IActionResult> OnPostClearAndRegisterGloballyAsync(CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("SuperAdmin"))
        {
            _logger.LogWarning("Non-admin user {UserId} attempted to clear and re-register commands", User.Identity?.Name);
            return Forbid();
        }

        _logger.LogWarning("Clear and re-register commands requested by user {UserId}", User.Identity?.Name);

        try
        {
            var result = await _commandRegistrationService.ClearAndRegisterGloballyAsync(cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation(
                    "Commands cleared and re-registered successfully by {UserId}: {CommandCount} commands, {GuildCount} guilds cleared",
                    User.Identity?.Name,
                    result.GlobalCommandsRegistered,
                    result.GuildsCleared);
            }

            return new JsonResult(new
            {
                success = result.Success,
                message = result.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear and re-register commands, requested by {UserId}", User.Identity?.Name);

            return new JsonResult(new
            {
                success = false,
                message = "Failed to clear and re-register commands. Please check logs for details."
            })
            {
                StatusCode = 500
            };
        }
    }
}
