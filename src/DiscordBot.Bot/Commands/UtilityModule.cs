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
    /// Dangerous permissions that warrant warning indicators.
    /// </summary>
    private static readonly GuildPermission[] DangerousPermissions =
    [
        GuildPermission.Administrator,
        GuildPermission.ManageGuild,
        GuildPermission.ManageRoles,
        GuildPermission.ManageChannels,
        GuildPermission.BanMembers,
        GuildPermission.MentionEveryone
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

    /// <summary>
    /// Displays detailed information about the current server.
    /// </summary>
    [SlashCommand("serverinfo", "Display detailed information about this server")]
    [RequireContext(ContextType.Guild)]
    public async Task ServerInfoAsync()
    {
        _logger.LogInformation(
            "Serverinfo command executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            Context.Guild.Name,
            Context.Guild.Id);

        var guild = Context.Guild;
        var embed = BuildServerInfoEmbed(guild);
        await RespondAsync(embed: embed, ephemeral: true);

        _logger.LogDebug("Serverinfo command response sent successfully for guild {GuildId}", guild.Id);
    }

    /// <summary>
    /// Displays detailed information about a role.
    /// </summary>
    /// <param name="role">The role to get information about.</param>
    [SlashCommand("roleinfo", "Display detailed information about a role")]
    [RequireContext(ContextType.Guild)]
    public async Task RoleInfoAsync(
        [Summary("role", "The role to get information about")]
        IRole role)
    {
        _logger.LogInformation(
            "Roleinfo command executed by {Username} (ID: {UserId}) for role {RoleName} (ID: {RoleId}) in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            role.Name,
            role.Id,
            Context.Guild.Name,
            Context.Guild.Id);

        var embed = BuildRoleInfoEmbed(role, Context.Guild);
        await RespondAsync(embed: embed, ephemeral: true);

        _logger.LogDebug("Roleinfo command response sent successfully for role {RoleId}", role.Id);
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

    private Embed BuildServerInfoEmbed(SocketGuild guild)
    {
        // Color based on boost level
        var color = guild.PremiumTier switch
        {
            PremiumTier.Tier3 => new Color(0xFF73FA),
            PremiumTier.Tier2 => new Color(0xFAA61A),
            PremiumTier.Tier1 => new Color(0x7289DA),
            _ => Color.Blue
        };

        var createdAt = guild.CreatedAt.ToUnixTimeSeconds();

        var builder = new EmbedBuilder()
            .WithAuthor("Server Information", guild.IconUrl)
            .WithTitle(guild.Name)
            .WithThumbnailUrl(guild.IconUrl)
            .WithColor(color)
            .WithCurrentTimestamp()
            .WithFooter($"ID: {guild.Id}");

        // Description (if set)
        if (!string.IsNullOrEmpty(guild.Description))
        {
            builder.WithDescription(guild.Description);
        }

        // Owner
        var owner = guild.Owner;
        builder.AddField("Owner", owner != null ? $"<@{owner.Id}>" : "Unknown", inline: true);

        // Created
        builder.AddField("Created", $"<t:{createdAt}:F>\n<t:{createdAt}:R>", inline: true);

        // Members
        var totalMembers = guild.MemberCount;
        var onlineMembers = guild.Users.Count(u => u.Status != UserStatus.Offline && !u.IsBot);
        var botCount = guild.Users.Count(u => u.IsBot);
        builder.AddField("Members",
            $"{totalMembers:N0} total\n{onlineMembers:N0} online\n{botCount:N0} bots",
            inline: true);

        // Channels
        var textChannels = guild.TextChannels.Count;
        var voiceChannels = guild.VoiceChannels.Count;
        var categories = guild.CategoryChannels.Count;
        builder.AddField("Channels",
            $"{textChannels} text\n{voiceChannels} voice\n{categories} categories",
            inline: true);

        // Roles
        builder.AddField("Roles", $"{guild.Roles.Count} roles", inline: true);

        // Boost status
        var boostText = guild.PremiumTier switch
        {
            PremiumTier.None => "No boosts",
            _ => $"Level {(int)guild.PremiumTier}\n{guild.PremiumSubscriptionCount} boosts"
        };
        builder.AddField("Boost Status", boostText, inline: true);

        // Vanity URL
        if (!string.IsNullOrEmpty(guild.VanityURLCode))
        {
            builder.AddField("Vanity URL", $"discord.gg/{guild.VanityURLCode}", inline: true);
        }

        // Features
        var features = GetServerFeatures(guild);
        if (features.Count > 0)
        {
            builder.AddField("Features", string.Join(", ", features), inline: false);
        }

        return builder.Build();
    }

    private Embed BuildRoleInfoEmbed(IRole role, SocketGuild guild)
    {
        // Use role's color or default to gray
        var color = role.Color.RawValue != 0 ? role.Color : new Color(0x99AAB5);

        // Special handling for @everyone
        var isEveryone = role.Id == guild.Id;
        var createdAt = role.CreatedAt.ToUnixTimeSeconds();

        var memberCount = isEveryone
            ? guild.MemberCount
            : guild.Users.Count(u => u.Roles.Any(r => r.Id == role.Id));

        var builder = new EmbedBuilder()
            .WithAuthor("Role Information")
            .WithTitle(role.Name)
            .WithColor(color)
            .WithCurrentTimestamp()
            .WithFooter($"Role ID: {role.Id}");

        // Managed role notice
        if (role.IsManaged)
        {
            var managedBy = GetManagedByDescription(role, guild);
            builder.WithDescription($"*Managed by: {managedBy}*");
        }

        // Color
        var colorText = role.Color.RawValue != 0
            ? $"#{role.Color.RawValue:X6}"
            : "No color (default)";
        builder.AddField("Color", colorText, inline: true);

        // Members
        var memberText = isEveryone
            ? $"{memberCount:N0} members (everyone)"
            : $"{memberCount:N0} member{(memberCount != 1 ? "s" : "")}";
        builder.AddField("Members", memberText, inline: true);

        // Position
        var positionText = isEveryone
            ? "Base role"
            : $"#{guild.Roles.Count - role.Position} in hierarchy";
        builder.AddField("Position", positionText, inline: true);

        // Created
        builder.AddField("Created", $"<t:{createdAt}:F>", inline: true);

        // Mentionable
        builder.AddField("Mentionable", role.IsMentionable ? "Yes" : "No", inline: true);

        // Hoisted
        var hoistedText = role.IsHoisted ? "Yes (displays separately)" : "No";
        builder.AddField("Hoisted", hoistedText, inline: true);

        // Permissions
        if (role.Permissions.Administrator)
        {
            builder.AddField("Permissions", "**Administrator** (all permissions)", inline: false);
        }
        else
        {
            var dangerousPerms = GetDangerousPermissions(role.Permissions);
            var otherKeyPerms = GetKeyPermissions(role.Permissions)
                .Except(dangerousPerms)
                .ToList();

            if (dangerousPerms.Count > 0)
            {
                builder.AddField("Dangerous Permissions",
                    string.Join(", ", dangerousPerms.Select(p => $"⚠️ {p}")),
                    inline: false);
            }

            if (otherKeyPerms.Count > 0)
            {
                builder.AddField("Key Permissions", string.Join(", ", otherKeyPerms), inline: false);
            }

            if (dangerousPerms.Count == 0 && otherKeyPerms.Count == 0)
            {
                var basicPerms = GetBasicPermissions(role.Permissions);
                if (basicPerms.Count > 0)
                {
                    builder.AddField("Permissions", string.Join(", ", basicPerms), inline: false);
                }
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

    private List<string> GetDangerousPermissions(GuildPermissions permissions)
    {
        var result = new List<string>();

        foreach (var perm in DangerousPermissions)
        {
            if (permissions.Has(perm))
            {
                result.Add(FormatPermissionName(perm));
            }
        }

        return result;
    }

    private static List<string> GetBasicPermissions(GuildPermissions permissions)
    {
        var result = new List<string>();

        if (permissions.SendMessages) result.Add("Send Messages");
        if (permissions.ViewChannel) result.Add("View Channels");
        if (permissions.EmbedLinks) result.Add("Embed Links");
        if (permissions.AttachFiles) result.Add("Attach Files");
        if (permissions.ReadMessageHistory) result.Add("Read Message History");

        return result;
    }

    private static List<string> GetServerFeatures(SocketGuild guild)
    {
        var features = new List<string>();

        if (guild.Features.HasFeature(GuildFeature.Verified))
            features.Add("Verified");
        if (guild.Features.HasFeature(GuildFeature.Partnered))
            features.Add("Discord Partner");
        if (guild.Features.HasFeature(GuildFeature.Discoverable))
            features.Add("Server Discovery");
        if (guild.Features.HasFeature(GuildFeature.Community))
            features.Add("Community Server");
        if (guild.Features.HasFeature(GuildFeature.VanityUrl))
            features.Add("Custom Invite");
        if (guild.Features.HasFeature(GuildFeature.AnimatedIcon))
            features.Add("Animated Icon");
        if (guild.Features.HasFeature(GuildFeature.Banner))
            features.Add("Server Banner");
        if (guild.Features.HasFeature(GuildFeature.WelcomeScreenEnabled))
            features.Add("Welcome Screen");

        return features;
    }

    private static string GetManagedByDescription(IRole role, SocketGuild guild)
    {
        if (role.Tags?.IsPremiumSubscriberRole == true)
        {
            return "Server Boost";
        }

        if (role.Tags?.BotId != null)
        {
            var bot = guild.GetUser(role.Tags.BotId.Value);
            return bot != null ? $"Bot: {bot.Username}" : "Bot";
        }

        if (role.Tags?.IntegrationId != null)
        {
            return "Integration";
        }

        return "Unknown";
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
