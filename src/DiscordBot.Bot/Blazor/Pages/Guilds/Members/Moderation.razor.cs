using Discord.WebSocket;
using DiscordBot.Bot.Blazor.Common;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.Extensions;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Blazor.Pages.Guilds.Members;

/// <summary>
/// Code-behind for the routable replacement of <c>Pages/Guilds/Members/Moderation.cshtml</c> +
/// <c>ModerationModel</c> (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4d). Loads Cases/
/// Notes/Tags/Flags sequentially on the same <c>BotDbContext</c>, same as the legacy page did, and
/// refreshes only the one list a mutation touched (an in-place refresh, not the legacy page's full
/// reload) using a fresh scope per <c>docs/architecture/patterns.md</c> "Blazor Components" §
/// Per-operation scopes.
/// </summary>
public partial class Moderation : GuildPageBase
{
    [Parameter]
    public long UserId { get; set; }

    [Inject]
    private IGuildMemberService MemberService { get; set; } = default!;

    [Inject]
    private IModerationService ModerationService { get; set; } = default!;

    [Inject]
    private IModNoteService ModNoteService { get; set; } = default!;

    [Inject]
    private IModTagService ModTagService { get; set; } = default!;

    [Inject]
    private IFlaggedEventService FlaggedEventService { get; set; } = default!;

    [Inject]
    private DiscordSocketClient DiscordClient { get; set; } = default!;

    [Inject]
    private IServiceScopeFactory ScopeFactory { get; set; } = default!;

    [Inject]
    private IToastService Toast { get; set; } = default!;

    [Inject]
    private ILogger<Moderation> Logger { get; set; } = default!;

    protected GuildMemberDto? Member { get; private set; }
    protected bool NotFoundState { get; private set; }
    protected DateTime AccountCreatedAt { get; private set; }

    protected IReadOnlyList<ModerationCaseDto> Cases { get; private set; } = [];
    protected IReadOnlyList<ModNoteDto> Notes { get; private set; } = [];
    protected IReadOnlyList<UserModTagDto> Tags { get; private set; } = [];
    protected IReadOnlyList<FlaggedEventDto> FlaggedEvents { get; private set; } = [];
    protected IReadOnlyList<ModTagDto> AvailableTags { get; private set; } = [];

    protected ulong CurrentUserId { get; private set; }
    protected string ActiveTabId { get; set; } = "cases";
    protected string NewNoteText { get; set; } = string.Empty;
    protected bool IsAddingNote { get; private set; }

    protected string? PendingRemoveTagName { get; private set; }

    private long _resolvedUserId = -1;

    protected override async Task OnGuildContextReadyAsync()
    {
        if (Guild is null)
        {
            return;
        }

        CurrentUserId = await GetCurrentDiscordUserIdAsync();
        await LoadAllAsync();
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        if (Guild is not null && _resolvedUserId != UserId)
        {
            _ = InvokeAsync(async () =>
            {
                await LoadAllAsync();
                StateHasChanged();
            });
        }
    }

    private async Task<ulong> GetCurrentDiscordUserIdAsync()
    {
        if (AuthenticationStateTask is null)
        {
            return 0;
        }

        var state = await AuthenticationStateTask;
        return state.User.GetDiscordUserId();
    }

    private async Task LoadAllAsync()
    {
        _resolvedUserId = UserId;
        var guildId = (ulong)GuildId;
        var userId = (ulong)UserId;

        try
        {
            Member = await MemberService.GetMemberAsync(guildId, userId);
            NotFoundState = Member is null;
            if (NotFoundState)
            {
                return;
            }

            var discordUser = DiscordClient.GetGuild(guildId)?.GetUser(userId);
            AccountCreatedAt = discordUser?.CreatedAt.UtcDateTime ?? DateTime.UtcNow.AddYears(-1);

            // Sequential - same BotDbContext, not thread-safe (matches the legacy page's own
            // comment on ModerationModel.OnGetAsync).
            var casesResult = await ModerationService.GetUserCasesAsync(guildId, userId);
            Cases = casesResult.Items.ToList();
            Notes = (await ModNoteService.GetNotesAsync(guildId, userId)).OrderByDescending(n => n.CreatedAt).ToList();
            Tags = (await ModTagService.GetUserTagsAsync(guildId, userId)).ToList();
            FlaggedEvents = (await FlaggedEventService.GetUserEventsAsync(guildId, userId)).OrderByDescending(f => f.CreatedAt).ToList();
            AvailableTags = (await ModTagService.GetGuildTagsAsync(guildId)).ToList();

            RequestLocalTimeScan();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load moderation profile for user {UserId} in guild {GuildId}", UserId, GuildId);
            Toast.Error("Failed to load this member's moderation profile.");
        }
    }

    protected IEnumerable<ModTagDto> AddableTags =>
        AvailableTags.Where(t => Tags.All(ut => ut.TagName != t.Name));

    protected async Task AddNoteAsync()
    {
        if (string.IsNullOrWhiteSpace(NewNoteText) || IsAddingNote)
        {
            return;
        }

        IsAddingNote = true;
        try
        {
            await ScopeFactory.RunAsync<IModNoteService>(s =>
                s.AddNoteAsync((ulong)GuildId, (ulong)UserId, NewNoteText, CurrentUserId));

            NewNoteText = string.Empty;
            Notes = (await ScopeFactory.RunAsync<IModNoteService, IEnumerable<ModNoteDto>>(
                s => s.GetNotesAsync((ulong)GuildId, (ulong)UserId))).OrderByDescending(n => n.CreatedAt).ToList();
            Toast.Success("Note added.");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to add note for user {UserId} in guild {GuildId}", UserId, GuildId);
            Toast.Error("Failed to add the note.");
        }
        finally
        {
            IsAddingNote = false;
        }
    }

    protected async Task DeleteNoteAsync(Guid noteId)
    {
        try
        {
            var deleted = await ScopeFactory.RunAsync<IModNoteService, bool>(
                s => s.DeleteNoteAsync(noteId, CurrentUserId));

            if (!deleted)
            {
                Toast.Error("Couldn't delete that note.");
                return;
            }

            Notes = (await ScopeFactory.RunAsync<IModNoteService, IEnumerable<ModNoteDto>>(
                s => s.GetNotesAsync((ulong)GuildId, (ulong)UserId))).OrderByDescending(n => n.CreatedAt).ToList();
            Toast.Success("Note deleted.");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to delete note {NoteId} for user {UserId} in guild {GuildId}", noteId, UserId, GuildId);
            Toast.Error("Failed to delete the note.");
        }
    }

    protected async Task AddTagAsync(string tagName)
    {
        try
        {
            var result = await ScopeFactory.RunAsync<IModTagService, UserModTagDto?>(
                s => s.ApplyTagAsync((ulong)GuildId, (ulong)UserId, tagName, CurrentUserId));

            if (result is null)
            {
                Toast.Error($"Tag '{tagName}' no longer exists.");
                return;
            }

            Tags = (await ScopeFactory.RunAsync<IModTagService, IEnumerable<UserModTagDto>>(
                s => s.GetUserTagsAsync((ulong)GuildId, (ulong)UserId))).ToList();
            Toast.Success($"Tag '{tagName}' added.");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to apply tag {TagName} to user {UserId} in guild {GuildId}", tagName, UserId, GuildId);
            Toast.Error("Failed to add the tag.");
        }
    }

    private ConfirmModal? _confirmRemoveTagModal;

    protected async Task RequestRemoveTagAsync(string tagName)
    {
        PendingRemoveTagName = tagName;
        var confirmed = await _confirmRemoveTagModal!.ShowAsync();
        if (!confirmed)
        {
            PendingRemoveTagName = null;
            return;
        }

        await RemoveTagAsync(tagName);
        PendingRemoveTagName = null;
    }

    private async Task RemoveTagAsync(string tagName)
    {
        try
        {
            var removed = await ScopeFactory.RunAsync<IModTagService, bool>(
                s => s.RemoveTagAsync((ulong)GuildId, (ulong)UserId, tagName));

            if (!removed)
            {
                Toast.Error($"Tag '{tagName}' was already removed.");
                return;
            }

            Tags = (await ScopeFactory.RunAsync<IModTagService, IEnumerable<UserModTagDto>>(
                s => s.GetUserTagsAsync((ulong)GuildId, (ulong)UserId))).ToList();
            Toast.Success($"Tag '{tagName}' removed.");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to remove tag {TagName} from user {UserId} in guild {GuildId}", tagName, UserId, GuildId);
            Toast.Error("Failed to remove the tag.");
        }
    }

    protected static string CaseBadgeClass(DiscordBot.Core.Enums.CaseType type) => type switch
    {
        DiscordBot.Core.Enums.CaseType.Warn => "bg-warning-bg text-warning border border-warning-border",
        DiscordBot.Core.Enums.CaseType.Mute => "bg-info-bg text-info border border-info-border",
        DiscordBot.Core.Enums.CaseType.Kick => "bg-error-bg text-error border border-error-border",
        DiscordBot.Core.Enums.CaseType.Ban => "bg-error-bg text-error border border-error-border",
        _ => "bg-bg-tertiary text-text-secondary border border-border-primary"
    };

    protected static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 1)
        {
            var days = (int)duration.TotalDays;
            return days == 1 ? "1 day" : $"{days} days";
        }
        if (duration.TotalHours >= 1)
        {
            var hours = (int)duration.TotalHours;
            return hours == 1 ? "1 hour" : $"{hours} hours";
        }
        if (duration.TotalMinutes >= 1)
        {
            var minutes = (int)duration.TotalMinutes;
            return minutes == 1 ? "1 minute" : $"{minutes} minutes";
        }
        var seconds = (int)duration.TotalSeconds;
        return seconds == 1 ? "1 second" : $"{seconds} seconds";
    }

    protected static string Initials(string displayName) =>
        displayName.Length >= 2 ? displayName[..2].ToUpperInvariant() : displayName.ToUpperInvariant();

    protected static string? BuildAvatarUrl(ulong userId, string? avatarHash)
    {
        if (string.IsNullOrEmpty(avatarHash))
        {
            return null;
        }

        var extension = avatarHash.StartsWith("a_", StringComparison.Ordinal) ? "gif" : "png";
        return $"https://cdn.discordapp.com/avatars/{userId}/{avatarHash}.{extension}";
    }
}
