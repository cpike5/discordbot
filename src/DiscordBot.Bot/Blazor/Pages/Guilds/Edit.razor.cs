using System.ComponentModel.DataAnnotations;
using DiscordBot.Bot.Blazor.Common;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Blazor.Pages.Guilds;

/// <summary>
/// Code-behind for the routable replacement of <c>Pages/Guilds/Edit.cshtml</c> + <c>EditModel</c>
/// (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4b) - the first real <see cref="GuildPageBase"/>
/// consumer. Reproduces <c>EditModel.OnGetAsync</c>/<c>OnPostAsync</c>: the bot-active toggle plus the
/// (best-effort, hide-on-failure) audio settings section, saved via <see cref="IGuildService"/> and
/// <see cref="IGuildAudioSettingsService"/> respectively.
/// </summary>
/// <remarks>
/// A successful save toasts and navigates to <c>/Guilds/Details/{id}</c>, matching the legacy
/// <c>RedirectToPage("Details", new { id })</c>. A failure at either step (guild not found, audio
/// settings threw) sets <see cref="ErrorMessage"/> and stays on the page instead.
/// </remarks>
public partial class Edit : GuildPageBase
{
    [Inject]
    private IGuildService GuildService { get; set; } = default!;

    [Inject]
    private IGuildAudioSettingsService AudioSettingsService { get; set; } = default!;

    [Inject]
    private IServiceScopeFactory ScopeFactory { get; set; } = default!;

    [Inject]
    private IToastService Toast { get; set; } = default!;

    [Inject]
    private NavigationManager Nav { get; set; } = default!;

    [Inject]
    private ILogger<Edit> Logger { get; set; } = default!;

    protected InputModel Input { get; set; } = new();
    protected bool AudioSettingsLoaded { get; private set; }
    protected string? ErrorMessage { get; set; }

    protected override async Task OnGuildContextReadyAsync()
    {
        if (Guild is null)
        {
            return;
        }

        Input = new InputModel { IsActive = Guild.Guild.IsActive };
        await LoadAudioSettingsAsync();
    }

    private async Task LoadAudioSettingsAsync()
    {
        try
        {
            var settings = await AudioSettingsService.GetSettingsAsync((ulong)GuildId);
            Input.AudioEnabled = settings.AudioEnabled;
            Input.AutoLeaveTimeoutMinutes = settings.AutoLeaveTimeoutMinutes;
            Input.QueueEnabled = settings.QueueEnabled;
            AudioSettingsLoaded = true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load audio settings for guild {GuildId}", GuildId);
            AudioSettingsLoaded = false;
        }
    }

    protected async Task HandleValidSubmit()
    {
        if (Guild is null)
        {
            return;
        }

        var guildId = (ulong)GuildId;
        Logger.LogInformation("User submitting guild edit for guild {GuildId}, IsActive={IsActive}", guildId, Input.IsActive);

        var updateRequest = new GuildUpdateRequestDto { IsActive = Input.IsActive };
        var result = await ScopeFactory.RunAsync<IGuildService, GuildDto?>(s => s.UpdateGuildAsync(guildId, updateRequest));

        if (result is null)
        {
            Logger.LogWarning("Failed to update guild {GuildId} - guild not found", guildId);
            ErrorMessage = "Guild not found. It may have been removed.";
            return;
        }

        try
        {
            await ScopeFactory.RunAsync<IGuildAudioSettingsService, GuildAudioSettings>(s => s.UpdateSettingsAsync(guildId, settings =>
            {
                settings.AudioEnabled = Input.AudioEnabled;
                settings.AutoLeaveTimeoutMinutes = Input.AutoLeaveTimeoutMinutes;
                settings.QueueEnabled = Input.QueueEnabled;
            }));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to update audio settings for guild {GuildId}", guildId);
            ErrorMessage = "Guild settings saved, but audio settings failed to update. Please try again.";
            return;
        }

        Logger.LogInformation("Successfully updated guild {GuildId}", guildId);
        ErrorMessage = null;
        Toast.Success("Guild settings saved successfully.");
        Nav.NavigateTo($"/Guilds/Details/{guildId}");
    }

    /// <summary>Mirrors <c>EditModel.InputModel</c> (minus <c>GuildId</c>, which <see cref="GuildPageBase.GuildId"/> already carries).</summary>
    public sealed class InputModel
    {
        [Display(Name = "Bot Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Audio Enabled")]
        public bool AudioEnabled { get; set; } = true;

        [Display(Name = "Auto-leave Timeout")]
        [Range(0, 1440, ErrorMessage = "Auto-leave timeout must be between 0 and 1440 minutes")]
        public int AutoLeaveTimeoutMinutes { get; set; } = 5;

        [Display(Name = "Queue Enabled")]
        public bool QueueEnabled { get; set; } = true;
    }
}
