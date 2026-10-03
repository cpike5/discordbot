using DiscordBot.Bot.Interfaces;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services.Portal;

/// <summary>
/// The development-only way into the member portal (plan decision D15).
/// <para>
/// Offline mode never logs in to Discord, so the portal can find no guild, no members and no voice
/// channels, and every portal page answers 404. In that one setup, and only there, this lets a
/// seeded Discord identity count as a member of every guild in the database so the portal can be
/// run, tested and screenshotted.
/// </para>
/// <para>
/// The gate is <see cref="IsEnabled"/>: the host must be in the Development environment
/// <b>and</b> <c>Discord:OfflineMode</c> must be on. Both the service registration and the seeder
/// ask it, so a production host (or any host connected to Discord) registers the Discord-backed
/// directory and seeds nothing.
/// </para>
/// </summary>
public static class DevelopmentPortal
{
    /// <summary>Email of the seeded member: an Identity user with no role, linked to Discord.</summary>
    public const string MemberEmail = "portal-member@example.com";

    /// <summary>Display name of the seeded member.</summary>
    public const string MemberDisplayName = "Portal Member";

    /// <summary>The fake Discord user ID the seeded default admin is linked to.</summary>
    public const ulong AdminDiscordUserId = 900000000000000101UL;

    /// <summary>The fake Discord user ID the seeded member is linked to.</summary>
    public const ulong MemberDiscordUserId = 900000000000000102UL;

    /// <summary>The Discord user IDs that count as guild members while the development portal is on.</summary>
    public static readonly IReadOnlyCollection<ulong> MemberDiscordUserIds =
        [AdminDiscordUserId, MemberDiscordUserId];

    /// <summary>
    /// The voice channels the development portal offers. They exist nowhere but here: joining one
    /// fails in the audio layer, which is the point, because the UI's error states are then testable.
    /// </summary>
    public static readonly IReadOnlyList<PortalVoiceChannel> VoiceChannels =
    [
        new(900000000000000201UL, "General", 0),
        new(900000000000000202UL, "Gaming", 0),
        new(900000000000000203UL, "Music Lounge", 0)
    ];

    /// <summary>
    /// Whether the development portal may run: Development environment and offline mode, both.
    /// </summary>
    /// <param name="environment">The host environment.</param>
    /// <param name="offlineMode">The value of <c>Discord:OfflineMode</c>.</param>
    public static bool IsEnabled(IHostEnvironment environment, bool offlineMode)
        => offlineMode && environment.IsDevelopment();

    /// <summary>
    /// Links the seeded default admin and a role-less seeded member to the fake Discord IDs the
    /// development directory accepts. Idempotent. Seeds nothing, and says so in the log, when the
    /// default admin already has a real Discord link. When not <see cref="IsEnabled"/> it seeds
    /// nothing and instead undoes an earlier seed (see <see cref="RemoveSeedAsync"/>), so a database
    /// carried out of development does not keep the fake links or the member account.
    /// </summary>
    /// <param name="services">A scoped provider.</param>
    /// <param name="environment">The host environment.</param>
    /// <param name="offlineMode">The value of <c>Discord:OfflineMode</c>.</param>
    /// <param name="logger">Logger.</param>
    /// <returns>True when anything was seeded.</returns>
    public static async Task<bool> SeedAsync(
        IServiceProvider services,
        IHostEnvironment environment,
        bool offlineMode,
        ILogger logger)
    {
        if (!IsEnabled(environment, offlineMode))
        {
            await RemoveSeedAsync(services, logger);
            return false;
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var userRepository = services.GetRequiredService<IUserRepository>();
        var admin = services.GetRequiredService<IOptions<IdentityConfigOptions>>().Value.DefaultAdmin;

        if (string.IsNullOrWhiteSpace(admin?.Email) || string.IsNullOrWhiteSpace(admin.Password))
        {
            logger.LogInformation("Development portal: no default admin configured, nothing to link");
            return false;
        }

        // The admin's link is the real one once they have made it: never put a fake ID on an
        // account that has a Discord link of its own. A link that is already the fake one is ours.
        var adminUser = await userManager.FindByEmailAsync(admin.Email);
        if (adminUser?.DiscordUserId is { } existingLink && existingLink != AdminDiscordUserId)
        {
            logger.LogInformation(
                "Development portal: {AdminEmail} already has a Discord link, so nothing was seeded",
                admin.Email);
            return false;
        }

        await LinkAsync(userManager, userRepository, admin.Email, AdminDiscordUserId, "System Administrator", logger);

        var member = await userManager.FindByEmailAsync(MemberEmail);
        if (member == null)
        {
            member = new ApplicationUser
            {
                UserName = MemberEmail,
                Email = MemberEmail,
                EmailConfirmed = true,
                DisplayName = MemberDisplayName,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            // Same password as the default admin, so there is one thing to remember. No role: a portal
            // member is not a dashboard user, which is exactly what the portal endpoints must serve.
            var created = await userManager.CreateAsync(member, admin.Password);
            if (!created.Succeeded)
            {
                logger.LogError("Development portal: could not create {Email}: {Errors}",
                    MemberEmail, string.Join(", ", created.Errors.Select(e => e.Description)));
                return false;
            }
        }

        await LinkAsync(userManager, userRepository, MemberEmail, MemberDiscordUserId, MemberDisplayName, logger);

        logger.LogWarning(
            "Development portal is ON (Development + Discord:OfflineMode): {AdminEmail} and {MemberEmail} are treated as members of every guild",
            admin.Email, MemberEmail);
        return true;
    }

    /// <summary>
    /// Undoes <see cref="SeedAsync"/> on a host where the development portal is off: clears the
    /// fake Discord IDs from every user carrying exactly <see cref="AdminDiscordUserId"/> or
    /// <see cref="MemberDiscordUserId"/>, and deactivates and locks the seeded member account. No
    /// user is deleted. Real links are never touched.
    /// </summary>
    /// <param name="services">A scoped provider.</param>
    /// <param name="logger">Logger.</param>
    public static async Task RemoveSeedAsync(IServiceProvider services, ILogger logger)
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        var linked = await userManager.Users
            .Where(u => u.DiscordUserId == AdminDiscordUserId || u.DiscordUserId == MemberDiscordUserId)
            .ToListAsync();

        foreach (var user in linked)
        {
            user.DiscordUserId = null;
            var updated = await userManager.UpdateAsync(user);
            if (updated.Succeeded)
            {
                logger.LogWarning(
                    "Development portal is off: removed the development Discord link from {Email}", user.Email);
            }
            else
            {
                logger.LogError("Development portal: could not unlink {Email}: {Errors}",
                    user.Email, string.Join(", ", updated.Errors.Select(e => e.Description)));
            }
        }

        var member = await userManager.FindByEmailAsync(MemberEmail);
        if (member != null && (member.IsActive || member.LockoutEnd != DateTimeOffset.MaxValue))
        {
            member.IsActive = false;
            member.LockoutEnabled = true;
            member.LockoutEnd = DateTimeOffset.MaxValue;
            var updated = await userManager.UpdateAsync(member);
            if (updated.Succeeded)
            {
                logger.LogWarning("Development portal is off: deactivated and locked {Email}", MemberEmail);
            }
            else
            {
                logger.LogError("Development portal: could not deactivate {Email}: {Errors}",
                    MemberEmail, string.Join(", ", updated.Errors.Select(e => e.Description)));
            }
        }
    }

    private static async Task LinkAsync(
        UserManager<ApplicationUser> userManager,
        IUserRepository userRepository,
        string email,
        ulong discordUserId,
        string username,
        ILogger logger)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            return;
        }

        // Never replace a real link
        if (!user.DiscordUserId.HasValue)
        {
            user.DiscordUserId = discordUserId;
            var updated = await userManager.UpdateAsync(user);
            if (!updated.Succeeded)
            {
                logger.LogError("Development portal: could not link {Email}: {Errors}",
                    email, string.Join(", ", updated.Errors.Select(e => e.Description)));
                return;
            }
        }

        // Favourites, uploads and play logs point at the Users table
        if (user.DiscordUserId == discordUserId)
        {
            var now = DateTime.UtcNow;
            await userRepository.UpsertAsync(new User
            {
                Id = discordUserId,
                Username = username,
                FirstSeenAt = now,
                LastSeenAt = now
            });
        }
    }
}

/// <summary>
/// The <see cref="IPortalGuildDirectory"/> for <see cref="DevelopmentPortal"/>: every guild in the
/// database is "available", the seeded Discord identities are its members, and it has a few
/// made-up voice channels. Registered only when <see cref="DevelopmentPortal.IsEnabled"/>.
/// </summary>
public sealed class DevelopmentPortalGuildDirectory : IPortalGuildDirectory
{
    private readonly IGuildRepository _guilds;

    public DevelopmentPortalGuildDirectory(IGuildRepository guilds)
    {
        _guilds = guilds;
    }

    /// <inheritdoc />
    /// <remarks>Offline mode has no gateway, so the bot is never online.</remarks>
    public bool IsBotOnline => false;

    /// <inheritdoc />
    public async Task<bool> IsGuildAvailableAsync(ulong guildId, CancellationToken cancellationToken = default)
        => await _guilds.GetByDiscordIdAsync(guildId, cancellationToken) != null;

    /// <inheritdoc />
    public IReadOnlyList<PortalVoiceChannel> GetVoiceChannels(ulong guildId) => DevelopmentPortal.VoiceChannels;

    /// <inheritdoc />
    public PortalVoiceChannel? FindVoiceChannel(ulong guildId, ulong channelId)
        => DevelopmentPortal.VoiceChannels.FirstOrDefault(c => c.Id == channelId);

    /// <inheritdoc />
    public async Task<bool> IsMemberAsync(ulong guildId, ulong discordUserId, CancellationToken cancellationToken = default)
        => DevelopmentPortal.MemberDiscordUserIds.Contains(discordUserId)
           && await IsGuildAvailableAsync(guildId, cancellationToken);
}
