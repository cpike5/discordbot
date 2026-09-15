using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Core.Enums;

namespace DiscordBot.Bot.Blazor.Common;

/// <summary>
/// The <see cref="FeatureRequestStatus"/> -&gt; badge label/variant mapping duplicated three times
/// in the legacy <c>Pages/Guilds/FeatureRequests/{Index,Details}.cshtml</c> (the desktop table row,
/// the mobile card, and the details page header) - one shared helper for
/// <c>Blazor/Pages/Guilds/FeatureRequests/{Index,Details}.razor</c> instead.
/// </summary>
public static class FeatureRequestStatusDisplay
{
    public static string Label(FeatureRequestStatus status) => status switch
    {
        FeatureRequestStatus.Submitted => "Submitted",
        FeatureRequestStatus.GeneratingDocs => "Generating Docs",
        FeatureRequestStatus.DocsGenerated => "Docs Generated",
        FeatureRequestStatus.DocGenFailed => "Doc Gen Failed",
        FeatureRequestStatus.Approved => "Approved",
        FeatureRequestStatus.Rejected => "Rejected",
        _ => status.ToString()
    };

    public static BadgeVariant Variant(FeatureRequestStatus status) => status switch
    {
        FeatureRequestStatus.Submitted => BadgeVariant.Info,
        FeatureRequestStatus.GeneratingDocs => BadgeVariant.Warning,
        FeatureRequestStatus.DocsGenerated => BadgeVariant.Success,
        FeatureRequestStatus.DocGenFailed => BadgeVariant.Error,
        FeatureRequestStatus.Approved => BadgeVariant.Success,
        FeatureRequestStatus.Rejected => BadgeVariant.Default,
        _ => BadgeVariant.Default
    };
}
