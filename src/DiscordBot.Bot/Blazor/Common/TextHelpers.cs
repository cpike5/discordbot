namespace DiscordBot.Bot.Blazor.Common;

/// <summary>
/// Small text-formatting helpers shared across Blazor pages. Currently just the avatar-initial
/// helper `Admin/Users/Index.razor` and `Details.razor` both need - a bare
/// <c>user.Email.Substring(0, 1)</c> throws <see cref="ArgumentOutOfRangeException"/> for an
/// empty string, which <see cref="DiscordBot.Core.DTOs.UserDto.Email"/>'s non-nullable-but-not-
/// guaranteed-non-empty contract doesn't rule out.
/// </summary>
public static class TextHelpers
{
    /// <summary>
    /// The first character of <paramref name="value"/>, upper-cased, or <c>"?"</c> when
    /// <paramref name="value"/> is <see langword="null"/> or empty - used as a monogram-avatar
    /// fallback initial.
    /// </summary>
    public static string FirstCharUpper(string? value) =>
        string.IsNullOrEmpty(value) ? "?" : char.ToUpperInvariant(value[0]).ToString();
}
