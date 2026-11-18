using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Bot.Preconditions;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// Utility commands module providing user, server, and role information.
/// </summary>
[RequireGuildActive]
public class UtilityModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger<UtilityModule> _logger;

    /// <summary>
    /// Key permissions to highlight when displaying user/role permissions.
    /// </summary>
    private static readonly GuildPermission[] KeyPermissions =
    [
        GuildPermission.Administrator,
        GuildPermission.ManageGuild,
        GuildPermission.ManageChannels,
        GuildPermission.ManageRoles,
        GuildPermission.ManageMessages,
        GuildPermission.KickMembers,
        GuildPermission.BanMembers,
        GuildPermission.ModerateMembers
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="UtilityModule"/> class.
    /// </summary>
    public UtilityModule(ILogger<UtilityModule> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Displays detailed information about a user.
    /// </summary>
    /// <param name="user">The user to get info for (defaults to yourself).</param>
    [SlashCommand("userinfo", "Display detailed information about a user")]
    [RequireContext(ContextType.Guild)]
    public async Task UserInfoAsync(
        [Summary("user", "The user to get info for (defaults to yourself)")]
        IUser? user = null)
    {
        var targetUser = user ?? Context.User;
        var guildUser = targetUser as SocketGuildUser ?? Context.Guild.GetUser(targetUser.Id);

        _logger.LogInformation(
            "Userinfo command executed by {Username} (ID: {UserId}) for target {TargetUsername} (ID: {TargetId}) in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            targetUser.Username,
            targetUser.Id,
            Context.Guild.Name,
            Context.Guild.Id);

        var embed = BuildUserInfoEmbed(targetUser, guildUser);
        await RespondAsync(embed: embed, ephemeral: true);

        _logger.LogDebug("Userinfo command response sent successfully for user {UserId}", targetUser.Id);
    }

    private Embed BuildUserInfoEmbed(IUser user, SocketGuildUser? guildUser)
    {
        // Determine embed color: pink for boosters, blue for default
        var color = guildUser?.PremiumSince.HasValue == true
            ? new Color(0xF47FFF)
            : Color.Blue;

        var createdAt = user.CreatedAt.ToUnixTimeSeconds();
        var titleText = user.Username;
        if (user.IsBot)
        {
            titleText += " [BOT]";
        }

        var builder = new EmbedBuilder()
            .WithAuthor("User Information", user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
            .WithTitle(titleText)
            .WithThumbnailUrl(user.GetAvatarUrl(ImageFormat.Auto, 256) ?? user.GetDefaultAvatarUrl())
            .WithColor(color)
            .WithCurrentTimestamp()
            .WithFooter($"ID: {user.Id}");

        // Display name (nickname) if different from username
        if (guildUser != null && !string.IsNullOrEmpty(guildUser.DisplayName) &&
            guildUser.DisplayName != user.Username)
        {
            builder.WithDescription($"**Display Name:** {guildUser.DisplayName}");
        }

        // Account created
        builder.AddField("Account Created",
            $"<t:{createdAt}:F>\n<t:{createdAt}:R>",
            inline: true);

        // Joined server (guild members only)
        if (guildUser?.JoinedAt != null)
        {
            var joinedAt = guildUser.JoinedAt.Value.ToUnixTimeSeconds();
            builder.AddField("Joined Server",
                $"<t:{joinedAt}:F>\n<t:{joinedAt}:R>",
                inline: true);
        }
        else if (guildUser == null)
        {
            builder.AddField("Joined Server", "Not in this server", inline: true);
        }

        // Boost status
        if (guildUser?.PremiumSince != null)
        {
            var boostSince = guildUser.PremiumSince.Value.ToUnixTimeSeconds();
            builder.AddField("Boost Status",
                $"Boosting since <t:{boostSince}:R>",
                inline: true);
        }

        // Roles (guild members only)
        if (guildUser != null)
        {
            var roles = guildUser.Roles
                .Where(r => !r.IsEveryone)
                .OrderByDescending(r => r.Position)
                .ToList();

            string rolesText;
            if (roles.Count == 0)
            {
                rolesText = "No roles";
            }
            else
            {
                const int maxRoles = 5;
                var displayedRoles = roles.Take(maxRoles).Select(r => r.Mention);
                rolesText = string.Join(", ", displayedRoles);
                if (roles.Count > maxRoles)
                {
                    rolesText += $" (+{roles.Count - maxRoles} more)";
                }
            }

            builder.AddField($"Roles [{roles.Count}]", rolesText, inline: false);

            // Key permissions
            var permissions = GetKeyPermissions(guildUser.GuildPermissions);
            if (permissions.Count > 0)
            {
                builder.AddField("Key Permissions", string.Join(", ", permissions), inline: false);
            }
        }

        return builder.Build();
    }

    private List<string> GetKeyPermissions(GuildPermissions permissions)
    {
        var result = new List<string>();

        foreach (var perm in KeyPermissions)
        {
            if (permissions.Has(perm))
            {
                result.Add(FormatPermissionName(perm));
            }
        }

        return result;
    }

    private static string FormatPermissionName(GuildPermission permission)
    {
        return permission switch
        {
            GuildPermission.Administrator => "Administrator",
            GuildPermission.ManageGuild => "Manage Server",
            GuildPermission.ManageChannels => "Manage Channels",
            GuildPermission.ManageRoles => "Manage Roles",
            GuildPermission.ManageMessages => "Manage Messages",
            GuildPermission.KickMembers => "Kick Members",
            GuildPermission.BanMembers => "Ban Members",
            GuildPermission.ModerateMembers => "Timeout Members",
            GuildPermission.MentionEveryone => "Mention @everyone",
            GuildPermission.ManageWebhooks => "Manage Webhooks",
            GuildPermission.ViewAuditLog => "View Audit Log",
            _ => permission.ToString()
        };
    }
}
