namespace DiscordBot.Bot.Interfaces;

/// <summary>
/// An uploaded profile image.
/// </summary>
/// <param name="Content">The file's bytes.</param>
/// <param name="Length">The file's size in bytes, as the upload reports it.</param>
public sealed record BotProfileImage(Stream Content, long Length);

/// <summary>
/// What the Bot profile form posted. A null member was not on the form and is left alone.
/// </summary>
public sealed class BotProfileUpdateRequest
{
    public string? Username { get; init; }
    public string? AboutMe { get; init; }
    public BotProfileImage? Avatar { get; init; }
    public bool RemoveAvatar { get; init; }
    public BotProfileImage? Banner { get; init; }
    public bool RemoveBanner { get; init; }
}

/// <summary>
/// The answer to "what is the bot's profile right now".
/// </summary>
/// <param name="Available">False when the profile could not be read; <paramref name="Message"/> says why.</param>
/// <param name="Profile">The profile, when available.</param>
/// <param name="Message">Why the profile is unavailable, for display.</param>
public sealed record BotProfileLoadResult(bool Available, BotProfile? Profile, string? Message);

/// <summary>
/// The Bot profile card on the Bot Control tab of Application Settings: reads the bot's Discord
/// profile and saves changes to it (username, avatar, banner, "About me"), with validation and
/// audit logging.
/// </summary>
public interface IBotProfileService
{
    /// <summary>Reads the profile from Discord. Never throws; an unreadable profile is reported as unavailable.</summary>
    Task<BotProfileLoadResult> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Validates and applies the changed parts of <paramref name="request"/>, and audit-logs what changed.</summary>
    Task<SettingsSectionResult> SaveAsync(BotProfileUpdateRequest request, string userId, CancellationToken cancellationToken = default);
}
