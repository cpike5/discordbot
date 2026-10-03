using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services.Tts;

/// <summary>
/// The rules for a person's saved ("custom") TTS presets, shared by the member portal endpoint
/// (<c>PortalTtsPresetsController</c>) and the admin one (<c>GuildTtsPresetsController</c>) so the
/// two cannot drift: the same name and voice checks, the same limit, the same ownership test.
/// </summary>
public class CustomTtsPresetService
{
    /// <summary>The most custom presets one person can keep.</summary>
    public const int MaxPresetsPerUser = 20;

    /// <summary>The longest preset name.</summary>
    public const int MaxNameLength = 50;

    private readonly IUserTtsPresetRepository _repository;
    private readonly ILogger<CustomTtsPresetService> _logger;

    public CustomTtsPresetService(IUserTtsPresetRepository repository, ILogger<CustomTtsPresetService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>The person's presets, oldest first as the repository returns them.</summary>
    public Task<IReadOnlyList<UserTtsPreset>> ListAsync(ulong userId, CancellationToken cancellationToken)
        => _repository.GetByUserIdAsync(userId, cancellationToken);

    /// <summary>Validates and saves a preset; the result says why not when it cannot.</summary>
    public async Task<CustomTtsPresetResult> CreateAsync(
        ulong userId, CustomTtsPresetInput input, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            return CustomTtsPresetResult.Invalid("Preset name is required", "Please provide a name for the preset.");
        }

        if (input.Name.Length > MaxNameLength)
        {
            return CustomTtsPresetResult.Invalid("Preset name too long", $"Preset name must be {MaxNameLength} characters or fewer.");
        }

        if (string.IsNullOrWhiteSpace(input.VoiceName))
        {
            return CustomTtsPresetResult.Invalid("Voice name is required", "Please select a voice for the preset.");
        }

        var currentCount = await _repository.GetCountByUserIdAsync(userId, cancellationToken);
        if (currentCount >= MaxPresetsPerUser)
        {
            return CustomTtsPresetResult.Invalid(
                "Maximum presets reached",
                $"You can have at most {MaxPresetsPerUser} custom presets. Please delete an existing preset first.",
                "preset_limit_reached");
        }

        var preset = new UserTtsPreset
        {
            UserId = userId,
            Name = input.Name.Trim(),
            VoiceName = input.VoiceName.Trim(),
            Style = string.IsNullOrWhiteSpace(input.Style) ? null : input.Style.Trim(),
            Speed = (decimal)Math.Clamp(input.Speed, 0.5, 2.0),
            Pitch = (decimal)Math.Clamp(input.Pitch, 0.5, 2.0),
            Icon = string.IsNullOrWhiteSpace(input.Icon) ? null : input.Icon.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        var created = await _repository.AddAsync(preset, cancellationToken);

        _logger.LogInformation("User {UserId} created custom TTS preset '{PresetName}' (ID: {PresetId})",
            userId, created.Name, created.Id);

        return CustomTtsPresetResult.Created(created);
    }

    /// <summary>Deletes the person's own preset; false when there is no such preset of theirs.</summary>
    public async Task<bool> DeleteAsync(ulong userId, int presetId, CancellationToken cancellationToken)
    {
        var preset = await _repository.GetByIdAsync(presetId, cancellationToken);
        if (preset == null || preset.UserId != userId)
        {
            return false;
        }

        await _repository.DeleteAsync(preset, cancellationToken);

        _logger.LogInformation("User {UserId} deleted custom TTS preset '{PresetName}' (ID: {PresetId})",
            userId, preset.Name, preset.Id);

        return true;
    }

    /// <summary>The JSON shape both endpoints return for one preset.</summary>
    public static object ToResponse(UserTtsPreset preset) => new
    {
        preset.Id,
        preset.Name,
        preset.VoiceName,
        preset.Style,
        Speed = (double)preset.Speed,
        Pitch = (double)preset.Pitch,
        preset.Icon,
        preset.CreatedAt
    };
}

/// <summary>What a person asked to save as a custom preset.</summary>
public sealed record CustomTtsPresetInput(
    string? Name, string? VoiceName, string? Style, double Speed, double Pitch, string? Icon);

/// <summary>The outcome of <see cref="CustomTtsPresetService.CreateAsync"/>.</summary>
public sealed record CustomTtsPresetResult
{
    /// <summary>The saved preset, when it was saved.</summary>
    public UserTtsPreset? Preset { get; init; }

    /// <summary>Short reason, when it was not.</summary>
    public string? Message { get; init; }

    /// <summary>One plain sentence of detail, when it was not.</summary>
    public string? Detail { get; init; }

    /// <summary>The machine-readable <c>errorCode</c>, when it was not.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>True when the preset was saved.</summary>
    public bool Succeeded => Preset is not null;

    internal static CustomTtsPresetResult Created(UserTtsPreset preset) => new() { Preset = preset };

    internal static CustomTtsPresetResult Invalid(string message, string detail, string errorCode = "invalid_request")
        => new() { Message = message, Detail = detail, ErrorCode = errorCode };
}
