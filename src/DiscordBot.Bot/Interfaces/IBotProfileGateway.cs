namespace DiscordBot.Bot.Interfaces;

/// <summary>
/// The bot's own Discord profile, as Discord reports it.
/// </summary>
/// <param name="Username">The bot's global username.</param>
/// <param name="AvatarUrl">The avatar image (the default avatar when none is set).</param>
/// <param name="HasCustomAvatar">False while the bot shows Discord's default avatar.</param>
/// <param name="BannerUrl">The profile banner, or null when none is set.</param>
/// <param name="AboutMe">The "About me" text (Discord stores it as the application description).</param>
public sealed record BotProfile(string Username, string AvatarUrl, bool HasCustomAvatar, string? BannerUrl, string AboutMe);

/// <summary>
/// Changes to the bot's user record. A null or false member leaves that part alone.
/// </summary>
public sealed class BotUserChanges
{
    /// <summary>The new username, or null to keep the current one.</summary>
    public string? Username { get; init; }

    /// <summary>The new avatar image, or null to keep the current one.</summary>
    public Stream? Avatar { get; init; }

    /// <summary>True to go back to Discord's default avatar. Ignored when <see cref="Avatar"/> is set.</summary>
    public bool RemoveAvatar { get; init; }

    /// <summary>The new banner image, or null to keep the current one.</summary>
    public Stream? Banner { get; init; }

    /// <summary>True to remove the banner. Ignored when <see cref="Banner"/> is set.</summary>
    public bool RemoveBanner { get; init; }

    /// <summary>True when there is something to send.</summary>
    public bool HasChanges => Username != null || Avatar != null || RemoveAvatar || Banner != null || RemoveBanner;
}

/// <summary>
/// Discord refused or failed a profile request. <see cref="Exception.Message"/> is written for the
/// admin who made the change.
/// </summary>
public sealed class BotProfileGatewayException : Exception
{
    public BotProfileGatewayException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// The Discord calls behind the Bot profile card, kept behind an interface so
/// <see cref="IBotProfileService"/> can be tested without a Discord client. Failures surface as
/// <see cref="BotProfileGatewayException"/>.
/// </summary>
public interface IBotProfileGateway
{
    /// <summary>True when the bot is logged in to Discord (false in offline mode and before login).</summary>
    bool IsAvailable { get; }

    /// <summary>Reads the current profile from Discord.</summary>
    Task<BotProfile> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Changes the username, avatar and banner.</summary>
    Task ModifyUserAsync(BotUserChanges changes, CancellationToken cancellationToken = default);

    /// <summary>Replaces the "About me" text. An empty string clears it.</summary>
    Task SetAboutMeAsync(string aboutMe, CancellationToken cancellationToken = default);
}
