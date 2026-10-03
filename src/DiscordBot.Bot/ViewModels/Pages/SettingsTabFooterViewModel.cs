namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for the footer of a Settings tab (<c>_SettingsTabFooter</c>): the error area, the
/// "Unsaved changes" hint and the Reset and Save buttons.
/// </summary>
public record SettingsTabFooterViewModel
{
    /// <summary>The tab's category id (General, Features, Advanced, AiModels, Commands, Appearance).</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>Text of the Save button.</summary>
    public string SaveText { get; init; } = "Save changes";

    /// <summary>Id of the confirm modal that resets this tab, or null when the tab has no reset.</summary>
    public string? ResetModalId { get; init; }

    /// <summary>Text of the Reset button.</summary>
    public string ResetText { get; init; } = "Reset to defaults";
}
