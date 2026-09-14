using Discord.WebSocket;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Bot.Services.Preview;

/// <summary>
/// Default <see cref="IPreviewService"/>. Extracted from <c>PreviewController</c>
/// (docs/plans/blazor-port-plan.md Phase 4 cluster 4d) so both the controller (still serving
/// <c>wwwroot/js/preview-popup.js</c> for the not-yet-ported Razor Pages) and
/// <c>Blazor/Shared/Overlays/UserPreview.razor</c>/<c>GuildPreview.razor</c> (calling this
/// directly, in-circuit, via a fresh <see cref="Microsoft.Extensions.DependencyInjection.IServiceScopeFactory"/>
/// scope) share one implementation. Behaviourally identical to the controller's former inline
/// logic - same Discord-cache-only lookups, same "guild not found or user not a member falls back
/// to a plain user preview" behaviour.
/// </summary>
public class PreviewService : IPreviewService
{
    private readonly DiscordSocketClient _client;
    private readonly BotDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<PreviewService> _logger;

    public PreviewService(
        DiscordSocketClient client,
        BotDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        ILogger<PreviewService> logger)
    {
        _client = client;
        _dbContext = dbContext;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<UserPreviewDto?> GetUserPreviewAsync(ulong userId, ulong? guildId, CancellationToken cancellationToken = default)
    {
        if (guildId.HasValue)
        {
            var discordGuild = _client.GetGuild(guildId.Value);
            var guildUser = discordGuild?.GetUser(userId);
            if (guildUser != null)
            {
                _logger.LogDebug("User preview with guild context retrieved for {Username} in {GuildName}",
                    guildUser.Username, discordGuild!.Name);
                return await BuildGuildUserPreviewAsync(guildUser, guildId.Value, cancellationToken);
            }
        }

        var discordUser = _client.GetUser(userId);
        if (discordUser == null)
        {
            _logger.LogDebug("User {UserId} not found in Discord cache", userId);
            return null;
        }

        var preview = await BuildUserPreviewAsync(discordUser, guildId, cancellationToken);
        _logger.LogDebug("User preview retrieved for {Username}", discordUser.Username);
        return preview;
    }

    public async Task<GuildPreviewDto?> GetGuildPreviewAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        var discordGuild = _client.GetGuild(guildId);
        if (discordGuild == null)
        {
            _logger.LogDebug("Guild {GuildId} not found in Discord cache", guildId);
            return null;
        }

        var preview = await BuildGuildPreviewAsync(discordGuild, cancellationToken);
        _logger.LogDebug("Guild preview retrieved for {GuildName}", discordGuild.Name);
        return preview;
    }

    private async Task<UserPreviewDto> BuildUserPreviewAsync(
        SocketUser discordUser,
        ulong? guildId,
        CancellationToken cancellationToken)
    {
        var lastActive = await _dbContext.CommandLogs
            .Where(c => c.UserId == discordUser.Id)
            .OrderByDescending(c => c.ExecutedAt)
            .Select(c => (DateTime?)c.ExecutedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var isVerified = await _userManager.Users
            .AnyAsync(u => u.DiscordUserId == discordUser.Id, cancellationToken);

        var hasActiveModeration = guildId.HasValue && await HasActiveModerationAsync(guildId.Value, discordUser.Id, cancellationToken);

        return new UserPreviewDto
        {
            UserId = discordUser.Id,
            Username = discordUser.Username,
            DisplayName = discordUser.GlobalName,
            AvatarUrl = discordUser.GetAvatarUrl() ?? discordUser.GetDefaultAvatarUrl(),
            LastActive = lastActive,
            IsVerified = isVerified,
            HasActiveModeration = hasActiveModeration,
            Roles = [],
            MemberSince = null
        };
    }

    private async Task<UserPreviewDto> BuildGuildUserPreviewAsync(
        SocketGuildUser guildUser,
        ulong guildId,
        CancellationToken cancellationToken)
    {
        var lastActive = await _dbContext.CommandLogs
            .Where(c => c.UserId == guildUser.Id)
            .OrderByDescending(c => c.ExecutedAt)
            .Select(c => (DateTime?)c.ExecutedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var isVerified = await _userManager.Users
            .AnyAsync(u => u.DiscordUserId == guildUser.Id, cancellationToken);

        var hasActiveModeration = await HasActiveModerationAsync(guildId, guildUser.Id, cancellationToken);

        var roles = guildUser.Roles
            .Where(r => !r.IsEveryone)
            .OrderByDescending(r => r.Position)
            .Take(5)
            .Select(r => r.Name)
            .ToList();

        return new UserPreviewDto
        {
            UserId = guildUser.Id,
            Username = guildUser.Username,
            DisplayName = guildUser.DisplayName,
            AvatarUrl = guildUser.GetGuildAvatarUrl() ?? guildUser.GetAvatarUrl() ?? guildUser.GetDefaultAvatarUrl(),
            MemberSince = guildUser.JoinedAt?.UtcDateTime,
            Roles = roles,
            LastActive = lastActive,
            IsVerified = isVerified,
            HasActiveModeration = hasActiveModeration
        };
    }

    private async Task<GuildPreviewDto> BuildGuildPreviewAsync(
        SocketGuild discordGuild,
        CancellationToken cancellationToken)
    {
        var dbGuild = await _dbContext.Guilds
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == discordGuild.Id, cancellationToken);

        var owner = discordGuild.Owner;
        var ownerUsername = owner?.Username ?? "Unknown";

        var activeFeatures = new List<string>();
        if (dbGuild?.Settings != null)
        {
            var settings = GuildSettingsViewModel.Parse(dbGuild.Settings);
            if (settings.WelcomeMessagesEnabled) activeFeatures.Add("Welcome");
            if (settings.AutoModEnabled) activeFeatures.Add("AutoMod");
            if (settings.ModerationAlertsEnabled) activeFeatures.Add("Moderation");
            if (settings.CommandLoggingEnabled) activeFeatures.Add("Logging");
        }

        var hasRatWatch = await _dbContext.GuildRatWatchSettings
            .AnyAsync(r => r.GuildId == discordGuild.Id && r.IsEnabled, cancellationToken);
        if (hasRatWatch) activeFeatures.Add("RatWatch");

        var hasScheduledMessages = await _dbContext.ScheduledMessages
            .AnyAsync(s => s.GuildId == discordGuild.Id && s.IsEnabled, cancellationToken);
        if (hasScheduledMessages) activeFeatures.Add("Scheduled Messages");

        return new GuildPreviewDto
        {
            GuildId = discordGuild.Id,
            Name = discordGuild.Name,
            IconUrl = discordGuild.IconUrl,
            MemberCount = discordGuild.MemberCount,
            OnlineMemberCount = discordGuild.Users.Count(u => u.Status != Discord.UserStatus.Offline),
            OwnerUsername = ownerUsername,
            BotJoinedAt = dbGuild?.JoinedAt ?? discordGuild.CurrentUser?.JoinedAt?.UtcDateTime ?? DateTime.UtcNow,
            ActiveFeatures = activeFeatures,
            IsActive = dbGuild?.IsActive ?? true
        };
    }

    private async Task<bool> HasActiveModerationAsync(
        ulong guildId,
        ulong userId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.ModerationCases
            .AnyAsync(c =>
                c.GuildId == guildId &&
                c.TargetUserId == userId &&
                (c.ExpiresAt == null || c.ExpiresAt > DateTime.UtcNow),
                cancellationToken);
    }
}
