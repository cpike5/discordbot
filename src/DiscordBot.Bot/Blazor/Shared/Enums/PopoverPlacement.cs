namespace DiscordBot.Bot.Blazor.Shared;

/// <summary>Which side of the trigger <see cref="PreviewPopover{TModel}"/> opens on. Positioning
/// is pure CSS (<c>position:absolute</c> on the popup, relative to a <c>position:relative</c>
/// wrapper around the trigger) rather than the <c>getBoundingClientRect</c> viewport-edge-flipping
/// math <c>preview-popup.js</c>'s <c>positionPopup()</c> does - see
/// docs/articles/blazor-components.md's Overlays row for this noted as a fidelity deviation
/// (no automatic flip when a fixed placement would overflow the viewport).</summary>
public enum PopoverPlacement
{
    /// <summary>Opens below the trigger. (Default - matches <c>positionPopup()</c>'s usual case.)</summary>
    Below,

    /// <summary>Opens above the trigger.</summary>
    Above
}
