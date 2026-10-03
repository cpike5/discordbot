using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Core.Enums;

namespace DiscordBot.Bot.Helpers;

/// <summary>How a feature request's status reads on screen, shared by the list and the details page.</summary>
public static class FeatureRequestDisplay
{
    /// <summary>The words for a status.</summary>
    public static string Label(FeatureRequestStatus status) => status switch
    {
        FeatureRequestStatus.Submitted => "Submitted",
        FeatureRequestStatus.GeneratingDocs => "Writing documentation",
        FeatureRequestStatus.DocsGenerated => "Documentation ready",
        FeatureRequestStatus.DocGenFailed => "Documentation failed",
        FeatureRequestStatus.Approved => "Approved",
        FeatureRequestStatus.Rejected => "Rejected",
        _ => "Unknown"
    };

    /// <summary>The badge colour for a status.</summary>
    public static BadgeVariant Variant(FeatureRequestStatus status) => status switch
    {
        FeatureRequestStatus.Submitted => BadgeVariant.Blue,
        FeatureRequestStatus.GeneratingDocs => BadgeVariant.Warning,
        FeatureRequestStatus.DocsGenerated => BadgeVariant.Success,
        FeatureRequestStatus.DocGenFailed => BadgeVariant.Error,
        FeatureRequestStatus.Approved => BadgeVariant.Success,
        _ => BadgeVariant.Default
    };

    /// <summary>The badge for a status.</summary>
    public static BadgeViewModel Badge(FeatureRequestStatus status) => new()
    {
        Text = Label(status),
        Variant = Variant(status),
        Style = BadgeStyle.Subtle,
        Size = BadgeSize.Small,
        IsPill = true
    };

    /// <summary>A short, single-line version of a description for list rows.</summary>
    public static string Excerpt(string description, int maxLength = 80)
    {
        var singleLine = string.Join(' ', (description ?? string.Empty).Split(
            new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return singleLine.Length <= maxLength ? singleLine : singleLine[..maxLength].TrimEnd() + "...";
    }
}
