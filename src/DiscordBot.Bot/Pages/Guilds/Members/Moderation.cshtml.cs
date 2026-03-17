using Discord.WebSocket;
using DiscordBot.Bot.Configuration;
using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Pages.Guilds.Members;

/// <summary>
/// Page model for displaying a user's moderation profile with cases, notes, tags, and flagged events.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
[Authorize(Policy = "GuildAccess")]
public class ModerationModel : GuildPageModelBase
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

        // Load moderation data sequentially — DbContext is not thread-safe
        var casesResult = await _moderationService.GetUserCasesAsync(GuildId, UserId);
        var notes = await _modNoteService.GetNotesAsync(GuildId, UserId);
        var tags = await _modTagService.GetUserTagsAsync(GuildId, UserId);
        var flags = await _flaggedEventService.GetUserEventsAsync(GuildId, UserId);
        var availableTags = await _modTagService.GetGuildTagsAsync(GuildId);

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
            Cases = casesResult.Items.ToList(),
            Notes = notes.ToList(),
            Tags = tags.ToList(),
            FlaggedEvents = flags.ToList(),
            AvailableTags = availableTags.ToList(),
            CurrentUserId = currentUserId
        };

        _logger.LogInformation("Loaded moderation profile for user {UserId} in guild {GuildId}: {CaseCount} cases, {NoteCount} notes, {TagCount} tags, {FlagCount} flags",
            UserId, GuildId, ViewModel.Cases.Count, ViewModel.Notes.Count, ViewModel.Tags.Count, ViewModel.FlaggedEvents.Count);

        // Populate guild layout ViewModels
        Breadcrumb = new GuildBreadcrumbViewModel
        {
            Items = new List<BreadcrumbItem>
            {
                new() { Label = "Home", Url = "/" },
                new() { Label = "Servers", Url = "/Guilds" },
                new() { Label = guild.Name, Url = $"/Guilds/Details/{GuildId}" },
                new() { Label = "Members", Url = $"/Guilds/{GuildId}/Members" },
                new() { Label = member.DisplayName, Url = $"/Guilds/{GuildId}/Members/{UserId}/Moderation" },
                new() { Label = "Moderation", IsCurrent = true }
            }
        };

        Header = BuildHeader(guild.Id, guild.Name, guild.IconUrl,
            $"Moderation Profile: {member.DisplayName}", $"Moderation history and profile for {member.DisplayName}");

        Navigation = BuildNavigation(guild.Id, "members");

        return Page();
    }
}
