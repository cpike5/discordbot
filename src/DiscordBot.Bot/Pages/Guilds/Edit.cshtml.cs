using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace DiscordBot.Bot.Pages.Guilds;

/// <summary>
/// Page model for editing guild settings.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class EditModel : PageModel
{
    private readonly IGuildService _guildService;
    private readonly ILogger<EditModel> _logger;

    public EditModel(
        IGuildService guildService,
        ILogger<EditModel> logger)
    {
        _guildService = guildService;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>
    /// View model for display-only properties (name, icon).
    /// </summary>
    public GuildEditViewModel ViewModel { get; set; } = new();

    /// <summary>
    /// Error message to display on the page.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Success message from TempData.
    /// </summary>
    [TempData]
    public string? SuccessMessage { get; set; }

    /// <summary>
    /// Input model for form binding with validation attributes.
    /// </summary>
    public class InputModel
    {
        public ulong GuildId { get; set; }

        [Display(Name = "Bot Active")]
        public bool IsActive { get; set; } = true;
    }

    public async Task<IActionResult> OnGetAsync(ulong id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("User accessing guild edit page for guild {GuildId}", id);

        var guild = await _guildService.GetGuildByIdAsync(id, cancellationToken);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", id);
            return NotFound();
        }

        // Populate display view model
        ViewModel = GuildEditViewModel.FromDto(guild);

        // Populate form input model
        Input = new InputModel
        {
            GuildId = guild.Id,
            IsActive = ViewModel.IsActive
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("User submitting guild edit for guild {GuildId}, IsActive={IsActive}",
            Input.GuildId, Input.IsActive);

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("ModelState is invalid for guild {GuildId}. Errors: {Errors}",
                Input.GuildId,
                string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

            await LoadViewModelAsync(Input.GuildId, cancellationToken);
            return Page();
        }

        // Create the update request
        var updateRequest = new GuildUpdateRequestDto
        {
            IsActive = Input.IsActive
        };

        _logger.LogInformation("Updating guild {GuildId} with IsActive={IsActive}",
            Input.GuildId, updateRequest.IsActive);

        var result = await _guildService.UpdateGuildAsync(Input.GuildId, updateRequest, cancellationToken);

        if (result == null)
        {
            _logger.LogWarning("Failed to update guild {GuildId} - guild not found", Input.GuildId);
            ErrorMessage = "Guild not found. It may have been removed.";
            await LoadViewModelAsync(Input.GuildId, cancellationToken);
            return Page();
        }

        _logger.LogInformation("Successfully updated guild {GuildId}", Input.GuildId);
        SuccessMessage = "Guild settings saved successfully.";

        return RedirectToPage("Details", new { id = Input.GuildId });
    }

    private async Task LoadViewModelAsync(ulong guildId, CancellationToken cancellationToken)
    {
        var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
        if (guild != null)
        {
            ViewModel = GuildEditViewModel.FromDto(guild);
        }
    }
}
