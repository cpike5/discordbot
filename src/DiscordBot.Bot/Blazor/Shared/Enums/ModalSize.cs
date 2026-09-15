namespace DiscordBot.Bot.Blazor.Shared;

/// <summary>
/// Width steps for <see cref="Modal"/>. The source partial (<c>_ConfirmationModal.cshtml</c>) had
/// no size concept - it was always <c>max-w-md</c> (<see cref="Medium"/>, the default here) - this
/// is a genuine addition for the generic <see cref="Modal"/> shell Tier 3 builds on top of it.
/// </summary>
public enum ModalSize
{
    /// <summary><c>max-w-sm</c>.</summary>
    Small,

    /// <summary><c>max-w-md</c> - matches every existing confirmation/typed-confirmation modal.
    /// (Default.)</summary>
    Medium,

    /// <summary><c>max-w-lg</c>.</summary>
    Large,

    /// <summary><c>max-w-2xl</c>.</summary>
    XLarge
}
