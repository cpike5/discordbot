using Discord.WebSocket;
using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Guilds.Members;

/// <summary>
/// Page model for displaying a user's moderation profile with cases, notes, tags, and flagged events.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
[Authorize(Policy = "GuildAccess")]
public class ModerationModel : PageModel
{
    private readonly IGuildService _guildService;
    private readonly IGuildMemberService _memberService;
    private readonly IModerationService _moderationService;
    private readonly IModNoteService _modNoteService;
    private readonly IModTagService _modTagService;
    private readonly IFlaggedEventService _flaggedEventService;
    private readonly DiscordSocketClient _discordClient;
    private readonly ILogger<ModerationModel> _logger;

    public ModerationModel(
        IGuildService guildService,
        IGuildMemberService memberService,
        IModerationService moderationService,
        IModNoteService modNoteService,
        IModTagService modTagService,
        IFlaggedEventService flaggedEventService,
        DiscordSocketClient discordClient,
        ILogger<ModerationModel> logger)
    {
        _guildService = guildService;
        _memberService = memberService;
        _moderationService = moderationService;
        _modNoteService = modNoteService;
        _modTagService = modTagService;
        _flaggedEventService = flaggedEventService;
        _discordClient = discordClient;
        _logger = logger;
    }

    /// <summary>
    /// The Discord guild snowflake ID from route.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public ulong GuildId { get; set; }

    /// <summary>
    /// The Discord user snowflake ID from route.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public ulong UserId { get; set; }

    /// <summary>
    /// The view model containing all moderation profile data.
    /// </summary>
    public UserModerationProfileViewModel ViewModel { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        _logger.LogDebug("Loading moderation profile for user {UserId} in guild {GuildId}", UserId, GuildId);

        // Get guild information
        var guild = await _guildService.GetGuildByIdAsync(GuildId);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", GuildId);
            return NotFound();
        }

        // Get member information
        var member = await _memberService.GetMemberAsync(GuildId, UserId);
        if (member == null)
        {
            _logger.LogWarning("Member {UserId} not found in guild {GuildId}", UserId, GuildId);
            return NotFound();
        }

        // Get Discord user for account creation date
        var discordGuild = _discordClient.GetGuild(GuildId);
        var discordUser = discordGuild?.GetUser(UserId);

        // Load all moderation data in parallel for performance
        var casesTask = _moderationService.GetUserCasesAsync(GuildId, UserId);
        var notesTask = _modNoteService.GetNotesAsync(GuildId, UserId);
        var tagsTask = _modTagService.GetUserTagsAsync(GuildId, UserId);
        var flagsTask = _flaggedEventService.GetUserEventsAsync(GuildId, UserId);
        var availableTagsTask = _modTagService.GetGuildTagsAsync(GuildId);

        await Task.WhenAll(casesTask, notesTask, tagsTask, flagsTask, availableTagsTask);

        // Get current user ID for identifying the logged-in moderator
        var currentUserId = User.GetDiscordUserId();

        // Build avatar URL from hash if available
        string? avatarUrl = null;
        if (!string.IsNullOrEmpty(member.AvatarHash))
        {
            avatarUrl = $"https://cdn.discordapp.com/avatars/{UserId}/{member.AvatarHash}.png";
        }

        // Build the view model
        ViewModel = new UserModerationProfileViewModel
        {
            GuildId = GuildId,
            GuildName = guild.Name,
            UserId = UserId,
            DisplayName = member.DisplayName,
            Username = member.Username,
            AvatarUrl = avatarUrl,
            AccountCreatedAt = discordUser?.CreatedAt.UtcDateTime ?? DateTime.UtcNow.AddYears(-1), // Fallback if Discord user not available
            JoinedGuildAt = member.JoinedAt,
            Roles = member.Roles.Select(r => r.Name).ToList(),
            Cases = casesTask.Result.Items.ToList(),
            Notes = notesTask.Result.ToList(),
            Tags = tagsTask.Result.ToList(),
            FlaggedEvents = flagsTask.Result.ToList(),
            AvailableTags = availableTagsTask.Result.ToList(),
            CurrentUserId = currentUserId
        };

        _logger.LogInformation("Loaded moderation profile for user {UserId} in guild {GuildId}: {CaseCount} cases, {NoteCount} notes, {TagCount} tags, {FlagCount} flags",
            UserId, GuildId, ViewModel.Cases.Count, ViewModel.Notes.Count, ViewModel.Tags.Count, ViewModel.FlaggedEvents.Count);

        return Page();
    }
}
