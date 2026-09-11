namespace DiscordBot.Bot.Blazor.Shared;

/// <summary>
/// Size steps for <see cref="Icon"/>-hosted icons, mapping to the Tailwind <c>w-*/h-*</c>
/// pairs the ported partials use for their inline stroke-outline SVGs (see
/// <c>docs/articles/blazor-components.md</c> "Component contract"). Not every partial's icon
/// fits one of these five steps (<c>_EmptyState.cshtml</c>'s icon ranges from <c>w-10</c> to
/// <c>w-20</c> depending on <c>EmptyStateSize</c>) - <see cref="Icon"/>'s <c>Size</c>
/// parameter is nullable for exactly that case, so a caller can size the icon entirely through
/// <c>Class</c> instead.
/// </summary>
public enum IconSize
{
    /// <summary>12px (<c>w-3 h-3</c>).</summary>
    XS,

    /// <summary>16px (<c>w-4 h-4</c>) - buttons, alert dismiss, empty-state action icons.</summary>
    SM,

    /// <summary>20px (<c>w-5 h-5</c>) - alert icons, card collapse chevron.</summary>
    MD,

    /// <summary>24px (<c>w-6 h-6</c>).</summary>
    LG,

    /// <summary>32px (<c>w-8 h-8</c>).</summary>
    XL
}
