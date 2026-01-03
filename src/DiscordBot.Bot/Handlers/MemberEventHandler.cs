using System.Text.Json;
using Discord;
using Discord.WebSocket;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Handlers;

/// <summary>
/// Handles Discord member events (UserJoined, UserLeft, GuildMemberUpdated) to maintain
/// the local guild member cache in the database.
/// </summary>
public class MemberEventHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MemberEventHandler> _logger;

    public MemberEventHandler(
        IServiceScopeFactory scopeFactory,
        ILogger<MemberEventHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Handles the UserJoined event. Creates new GuildMember record.
    /// </summary>
    /// <param name="user">The user who joined the guild.</param>
    public async Task HandleUserJoinedAsync(SocketGuildUser user)
    {
        using var activity = BotActivitySource.StartEventActivity(
            TracingConstants.Spans.DiscordEventMemberJoined,
            guildId: user.Guild.Id,
            userId: user.Id);

        try
        {
            var accountAgeDays = (DateTime.UtcNow - user.CreatedAt.UtcDateTime).Days;

            activity?.SetTag(TracingConstants.Attributes.MemberIsBot, user.IsBot);
            activity?.SetTag(TracingConstants.Attributes.MemberAccountAgeDays, accountAgeDays);

            _logger.LogDebug(
                "Processing UserJoined event for user {UserId} ({Username}) in guild {GuildId}",
                user.Id, user.Username, user.Guild.Id);

            using var scope = _scopeFactory.CreateScope();

            // Upsert User entity
            var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            await userRepo.UpsertAsync(MapToUser(user), CancellationToken.None);

            // Create GuildMember entity
            var memberRepo = scope.ServiceProvider.GetRequiredService<IGuildMemberRepository>();
            var member = new GuildMember
            {
                GuildId = user.Guild.Id,
                UserId = user.Id,
                JoinedAt = user.JoinedAt?.UtcDateTime ?? DateTime.UtcNow,
                Nickname = user.Nickname,
                CachedRolesJson = SerializeRoles(user.Roles),
                LastCachedAt = DateTime.UtcNow,
                IsActive = true
            };

            await memberRepo.UpsertAsync(member, CancellationToken.None);

            _logger.LogInformation(
                "Created GuildMember record for user {UserId} ({Username}) in guild {GuildId} ({GuildName})",
                user.Id, user.Username, user.Guild.Id, user.Guild.Name);

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to handle UserJoined for user {UserId} in guild {GuildId}",
                user.Id, user.Guild.Id);
            BotActivitySource.RecordException(activity, ex);
        }
    }

    /// <summary>
    /// Handles the UserLeft event. Soft deletes GuildMember (sets IsActive=false).
    /// </summary>
    /// <param name="guild">The guild the user left.</param>
    /// <param name="user">The user who left.</param>
    public async Task HandleUserLeftAsync(SocketGuild guild, SocketUser user)
    {
        using var activity = BotActivitySource.StartEventActivity(
            TracingConstants.Spans.DiscordEventMemberLeft,
            guildId: guild.Id,
            userId: user.Id);

        try
        {
            var accountAgeDays = (DateTime.UtcNow - user.CreatedAt.UtcDateTime).Days;

            activity?.SetTag(TracingConstants.Attributes.MemberIsBot, user.IsBot);
            activity?.SetTag(TracingConstants.Attributes.MemberAccountAgeDays, accountAgeDays);

            _logger.LogDebug(
                "Processing UserLeft event for user {UserId} ({Username}) in guild {GuildId}",
                user.Id, user.Username, guild.Id);

            using var scope = _scopeFactory.CreateScope();
            var memberRepo = scope.ServiceProvider.GetRequiredService<IGuildMemberRepository>();

            var result = await memberRepo.MarkInactiveAsync(guild.Id, user.Id, CancellationToken.None);

            activity?.SetTag("member.found", result);

            if (result)
            {
                _logger.LogInformation(
                    "Marked GuildMember inactive for user {UserId} ({Username}) in guild {GuildId} ({GuildName})",
                    user.Id, user.Username, guild.Id, guild.Name);
            }
            else
            {
                _logger.LogDebug(
                    "No GuildMember record found for user {UserId} in guild {GuildId} to mark inactive",
                    user.Id, guild.Id);
            }

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to handle UserLeft for user {UserId} in guild {GuildId}",
                user.Id, guild.Id);
            BotActivitySource.RecordException(activity, ex);
        }
    }

    /// <summary>
    /// Handles the GuildMemberUpdated event. Updates nickname and roles.
    /// </summary>
    /// <param name="before">The cached state before the update (may not be available).</param>
    /// <param name="after">The current state after the update.</param>
    public async Task HandleGuildMemberUpdatedAsync(
        Cacheable<SocketGuildUser, ulong> before,
        SocketGuildUser after)
    {
        using var activity = BotActivitySource.StartEventActivity(
            TracingConstants.Spans.DiscordEventMemberUpdated,
            guildId: after.Guild.Id,
            userId: after.Id);

        try
        {
            // Only update if nickname or roles changed
            var beforeUser = before.HasValue ? before.Value : null;
            var nicknameChanged = beforeUser?.Nickname != after.Nickname;
            var rolesChanged = beforeUser == null ||
                !beforeUser.Roles.Select(r => r.Id).SequenceEqual(after.Roles.Select(r => r.Id));

            if (!nicknameChanged && !rolesChanged)
            {
                _logger.LogTrace(
                    "GuildMemberUpdated for user {UserId} - no relevant changes (nickname or roles)",
                    after.Id);
                BotActivitySource.SetSuccess(activity);
                return;
            }

            // Determine update type
            var updateType = (nicknameChanged, rolesChanged) switch
            {
                (true, true) => "nickname_and_roles_changed",
                (true, false) => "nickname_changed",
                (false, true) => "roles_changed",
                _ => "unknown"
            };

            activity?.SetTag(TracingConstants.Attributes.MemberUpdateType, updateType);

            _logger.LogDebug(
                "Processing GuildMemberUpdated for user {UserId} ({Username}) in guild {GuildId}. " +
                "NicknameChanged: {NicknameChanged}, RolesChanged: {RolesChanged}",
                after.Id, after.Username, after.Guild.Id, nicknameChanged, rolesChanged);

            using var scope = _scopeFactory.CreateScope();
            var memberRepo = scope.ServiceProvider.GetRequiredService<IGuildMemberRepository>();

            var result = await memberRepo.UpdateMemberInfoAsync(
                after.Guild.Id,
                after.Id,
                after.Nickname,
                SerializeRoles(after.Roles),
                CancellationToken.None);

            activity?.SetTag("member.found", result);

            if (result)
            {
                _logger.LogInformation(
                    "Updated GuildMember info for user {UserId} ({Username}) in guild {GuildId} ({GuildName})",
                    after.Id, after.Username, after.Guild.Id, after.Guild.Name);
            }
            else
            {
                // Member not in database - this shouldn't happen with proper sync
                _logger.LogWarning(
                    "GuildMember record not found for updated user {UserId} in guild {GuildId}. " +
                    "Member may need to be synced.",
                    after.Id, after.Guild.Id);
            }

            // Also update User entity if relevant info changed
            var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            await userRepo.UpsertAsync(MapToUser(after), CancellationToken.None);

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to handle GuildMemberUpdated for user {UserId} in guild {GuildId}",
                after.Id, after.Guild.Id);
            BotActivitySource.RecordException(activity, ex);
        }
    }

    /// <summary>
    /// Serializes Discord roles to JSON array format.
    /// Excludes the @everyone role.
    /// </summary>
    private static string SerializeRoles(IReadOnlyCollection<SocketRole> roles)
    {
        var roleIds = roles
            .Where(r => !r.IsEveryone)
            .Select(r => r.Id)
            .ToList();

        return JsonSerializer.Serialize(roleIds);
    }

    /// <summary>
    /// Maps a Discord guild user to a User entity.
    /// </summary>
    private static User MapToUser(SocketGuildUser discordUser)
    {
        return new User
        {
            Id = discordUser.Id,
            Username = discordUser.Username,
            Discriminator = discordUser.Discriminator,
            FirstSeenAt = DateTime.UtcNow, // Only used if new
            LastSeenAt = DateTime.UtcNow,
            AccountCreatedAt = discordUser.CreatedAt.UtcDateTime,
            AvatarHash = discordUser.AvatarId,
            GlobalDisplayName = discordUser.GlobalName
        };
    }
}
