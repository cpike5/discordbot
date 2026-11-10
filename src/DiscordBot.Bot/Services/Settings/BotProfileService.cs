using DiscordBot.Bot.Interfaces;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using System.Text.Json;

namespace DiscordBot.Bot.Services.Settings;

/// <summary>
/// Default implementation of <see cref="IBotProfileService"/>. Validates the form, sends only what
/// changed through <see cref="IBotProfileGateway"/>, and audit-logs what was applied.
/// </summary>
public class BotProfileService : IBotProfileService
{
    /// <summary>Discord's username length limits.</summary>
    public const int MinUsernameLength = 2;
    public const int MaxUsernameLength = 32;

    /// <summary>Discord's limit on the application description, which is the bot's "About me".</summary>
    public const int MaxAboutMeLength = 400;

    /// <summary>Largest image accepted for the avatar or the banner.</summary>
    public const long MaxImageBytes = 8 * 1024 * 1024;

    private const string NotConnectedMessage =
        "The bot is not connected to Discord, so its profile cannot be read or changed right now.";

    private readonly IBotProfileGateway _gateway;
    private readonly IAuditLogQueue _auditLogQueue;
    private readonly ILogger<BotProfileService> _logger;

    public BotProfileService(
        IBotProfileGateway gateway,
        IAuditLogQueue auditLogQueue,
        ILogger<BotProfileService> logger)
    {
        _gateway = gateway;
        _auditLogQueue = auditLogQueue;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<BotProfileLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!_gateway.IsAvailable)
        {
            return new BotProfileLoadResult(false, null, NotConnectedMessage);
        }

        try
        {
            return new BotProfileLoadResult(true, await _gateway.GetAsync(cancellationToken), null);
        }
        catch (BotProfileGatewayException ex)
        {
            return new BotProfileLoadResult(false, null, $"The profile could not be loaded from Discord. {ex.Message}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to load the bot profile");
            return new BotProfileLoadResult(false, null, "The profile could not be loaded from Discord. Check the logs for details.");
        }
    }

    /// <inheritdoc/>
    public async Task<SettingsSectionResult> SaveAsync(BotProfileUpdateRequest request, string userId, CancellationToken cancellationToken = default)
    {
        if (!_gateway.IsAvailable)
        {
            return Failure(NotConnectedMessage, 409);
        }

        var errors = new List<string>();
        var username = request.Username?.Trim();
        var aboutMe = request.AboutMe == null ? null : NormalizeAboutMe(request.AboutMe);

        if (username != null && (username.Length < MinUsernameLength || username.Length > MaxUsernameLength))
        {
            errors.Add($"The username must be {MinUsernameLength} to {MaxUsernameLength} characters long.");
        }

        if (aboutMe != null && aboutMe.Length > MaxAboutMeLength)
        {
            errors.Add($"About me can be at most {MaxAboutMeLength} characters long. It is {aboutMe.Length}.");
        }

        using var avatar = await ReadImageAsync(request.Avatar, "avatar", errors, cancellationToken);
        using var banner = await ReadImageAsync(request.Banner, "banner", errors, cancellationToken);

        if (errors.Count > 0)
        {
            return new SettingsSectionResult
            {
                Success = false,
                Message = "The profile was not saved.",
                Errors = errors,
                StatusCode = 400
            };
        }

        BotProfile current;
        try
        {
            current = await _gateway.GetAsync(cancellationToken);
        }
        catch (BotProfileGatewayException ex)
        {
            return Failure($"The profile was not saved. {ex.Message}", 502);
        }

        var userChanges = new BotUserChanges
        {
            Username = username != null && username != current.Username ? username : null,
            Avatar = avatar,
            // Removing what is not there is not a change
            RemoveAvatar = avatar == null && request.RemoveAvatar && current.HasCustomAvatar,
            Banner = banner,
            RemoveBanner = banner == null && request.RemoveBanner && current.BannerUrl != null
        };
        var aboutMeChanged = aboutMe != null && aboutMe != NormalizeAboutMe(current.AboutMe);

        if (!userChanges.HasChanges && !aboutMeChanged)
        {
            return new SettingsSectionResult { Success = true, Message = "Nothing changed. The profile already has these values.", ChangeCount = 0 };
        }

        var applied = new List<string>();
        try
        {
            if (userChanges.HasChanges)
            {
                await _gateway.ModifyUserAsync(userChanges, cancellationToken);
                applied.AddRange(Describe(userChanges));
            }

            if (aboutMeChanged)
            {
                await _gateway.SetAboutMeAsync(aboutMe!, cancellationToken);
                applied.Add("about me");
            }
        }
        catch (BotProfileGatewayException ex)
        {
            // The user record and "About me" are separate Discord calls, so the first can have gone through
            if (applied.Count > 0)
            {
                AuditLog(userId, applied, current.Username, userChanges.Username);
            }

            var saved = applied.Count > 0 ? $"Saved the {string.Join(", ", applied)}, but not the rest. " : "The profile was not saved. ";
            return Failure(saved + ex.Message, 400);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to update the bot profile, requested by {UserId}", userId);
            if (applied.Count > 0)
            {
                AuditLog(userId, applied, current.Username, userChanges.Username);
            }

            return Failure("The profile could not be saved. Check the logs for details.", 500);
        }

        _logger.LogInformation("Bot profile updated by {UserId}: {Changes}", userId, string.Join(", ", applied));
        AuditLog(userId, applied, current.Username, userChanges.Username);

        return new SettingsSectionResult
        {
            Success = true,
            Message = $"Bot profile saved: {string.Join(", ", applied)}.",
            ChangeCount = applied.Count
        };
    }

    /// <summary>
    /// The image type of a file, by its first bytes: png, jpeg, gif or webp, or null for anything
    /// else. The file name and content type are whatever the browser said, so they are not consulted.
    /// </summary>
    internal static string? DetectImageFormat(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 8 && header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return "png";
        }

        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return "jpeg";
        }

        if (header.Length >= 6 && (header[..6].SequenceEqual("GIF87a"u8) || header[..6].SequenceEqual("GIF89a"u8)))
        {
            return "gif";
        }

        if (header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return "webp";
        }

        return null;
    }

    /// <summary>Line endings as Discord stores them, without surrounding blank space.</summary>
    private static string NormalizeAboutMe(string value) => value.Replace("\r\n", "\n").Replace('\r', '\n').Trim();

    /// <summary>
    /// Buffers an upload (it is read twice: once to check it, once to send it) after checking its
    /// size and type. Adds to <paramref name="errors"/> and returns null when it is not acceptable.
    /// </summary>
    private static async Task<MemoryStream?> ReadImageAsync(BotProfileImage? image, string label, List<string> errors, CancellationToken cancellationToken)
    {
        if (image == null)
        {
            return null;
        }

        if (image.Length == 0)
        {
            errors.Add($"The {label} file is empty.");
            return null;
        }

        if (image.Length > MaxImageBytes)
        {
            errors.Add($"The {label} file is too large. The limit is {MaxImageBytes / (1024 * 1024)} MB.");
            return null;
        }

        var buffer = new MemoryStream();
        await image.Content.CopyToAsync(buffer, cancellationToken);

        if (buffer.Length > MaxImageBytes)
        {
            await buffer.DisposeAsync();
            errors.Add($"The {label} file is too large. The limit is {MaxImageBytes / (1024 * 1024)} MB.");
            return null;
        }

        var header = buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, 12));
        if (DetectImageFormat(header) == null)
        {
            await buffer.DisposeAsync();
            errors.Add($"The {label} must be a PNG, JPEG, GIF or WebP image.");
            return null;
        }

        buffer.Position = 0;
        return buffer;
    }

    private static IEnumerable<string> Describe(BotUserChanges changes)
    {
        if (changes.Username != null) yield return "username";
        if (changes.Avatar != null) yield return "avatar";
        else if (changes.RemoveAvatar) yield return "avatar (removed)";
        if (changes.Banner != null) yield return "banner";
        else if (changes.RemoveBanner) yield return "banner (removed)";
    }

    private void AuditLog(string userId, IReadOnlyList<string> applied, string previousUsername, string? newUsername)
    {
        _auditLogQueue.Enqueue(new AuditLogCreateDto
        {
            Category = AuditLogCategory.System,
            Action = AuditLogAction.Updated,
            ActorType = AuditLogActorType.User,
            ActorId = userId,
            Details = JsonSerializer.Serialize(new
            {
                Operation = "BotProfileUpdate",
                Description = "Bot profile changed by administrator",
                Changed = applied,
                PreviousUsername = newUsername != null ? previousUsername : null,
                NewUsername = newUsername,
                Timestamp = DateTime.UtcNow
            })
        });
    }

    private static SettingsSectionResult Failure(string message, int statusCode) =>
        new() { Success = false, Message = message, StatusCode = statusCode };
}
