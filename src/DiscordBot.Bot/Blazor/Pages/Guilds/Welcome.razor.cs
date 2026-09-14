using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.RegularExpressions;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Bot.Blazor.Interop;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Components;

namespace DiscordBot.Bot.Blazor.Pages.Guilds;

/// <summary>
/// Code-behind for the routable replacement of <c>Pages/Guilds/Welcome.cshtml</c> +
/// <c>WelcomeModel</c> (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4b). Reproduces the
/// defaults-when-unconfigured behavior, the "channel required when enabled" rule (a manual check
/// in <see cref="HandleValidSubmit"/> rather than a data-annotation, since it depends on another
/// field), and the live Discord-message preview - computed here in C# instead of
/// <c>updatePreview()</c>'s client-side JS, HTML-encoding user input before token substitution so a
/// welcome message can't inject markup into the admin's own preview pane.
/// </summary>
/// <remarks>
/// Token insertion (the toolbar buttons and the token-help table's click-to-insert rows) goes
/// through <see cref="BrowserInterop.InsertAtSelectionAsync"/> against the raw textarea's own
/// <see cref="ElementReference"/> rather than the shared <c>TextArea</c> component, which exposes
/// no element reference to target - see the message field's markup comment in Welcome.razor.
/// </remarks>
public partial class Welcome : GuildPageBase
{
    [Inject]
    private IWelcomeService WelcomeService { get; set; } = default!;

    [Inject]
    private IDiscordChannelResolver ChannelResolver { get; set; } = default!;

    [Inject]
    private IToastService Toast { get; set; } = default!;

    [Inject]
    private BrowserInterop BrowserInterop { get; set; } = default!;

    [Inject]
    private ILogger<Welcome> Logger { get; set; } = default!;

    protected InputModel Input { get; set; } = new();
    protected List<ChannelSelectItem> AvailableChannels { get; set; } = [];
    protected string? ChannelError { get; set; }
    protected bool TokensExpanded { get; set; } = true;

    private ElementReference _messageTextArea;
    private static readonly Regex HexColorPattern = new(@"^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

    protected override async Task OnGuildContextReadyAsync()
    {
        if (Guild is null)
        {
            return;
        }

        var guildId = (ulong)GuildId;
        AvailableChannels = ChannelResolver.GetTextChannels(guildId).Select(ChannelSelectItem.FromChannelInfo).ToList();

        var config = await WelcomeService.GetConfigurationAsync(guildId) ?? new WelcomeConfigurationDto
        {
            GuildId = guildId,
            IsEnabled = false,
            WelcomeMessage = "Welcome to {server}, {user}! You are member #{memberCount}.",
            IncludeAvatar = true,
            UseEmbed = true,
            EmbedColor = "#5865F2"
        };

        Input = new InputModel
        {
            IsEnabled = config.IsEnabled,
            WelcomeChannelId = config.WelcomeChannelId,
            WelcomeMessage = config.WelcomeMessage,
            IncludeAvatar = config.IncludeAvatar,
            UseEmbed = config.UseEmbed,
            EmbedColor = config.EmbedColor
        };
    }

    protected async Task HandleValidSubmit()
    {
        if (Guild is null)
        {
            return;
        }

        ChannelError = null;

        if (Input.IsEnabled && !Input.WelcomeChannelId.HasValue)
        {
            ChannelError = "A welcome channel must be selected when welcome messages are enabled.";
            return;
        }

        var guildId = (ulong)GuildId;
        var updateRequest = new WelcomeConfigurationUpdateDto
        {
            IsEnabled = Input.IsEnabled,
            WelcomeChannelId = Input.WelcomeChannelId,
            WelcomeMessage = Input.WelcomeMessage,
            IncludeAvatar = Input.IncludeAvatar,
            UseEmbed = Input.UseEmbed,
            EmbedColor = Input.EmbedColor
        };

        var result = await WelcomeService.UpdateConfigurationAsync(guildId, updateRequest);

        if (result is null)
        {
            Logger.LogWarning("Failed to update welcome configuration for guild {GuildId} - guild not found", guildId);
            Toast.Error("Guild not found. It may have been removed.");
            return;
        }

        Logger.LogInformation("Successfully updated welcome configuration for guild {GuildId}", guildId);
        Toast.Success("Welcome configuration saved successfully.");
    }

    protected async Task InsertToken(string token)
    {
        await BrowserInterop.InsertAtSelectionAsync(_messageTextArea, token);
    }

    protected void HandleMessageInput(ChangeEventArgs e) => Input.WelcomeMessage = e.Value?.ToString();

    protected void HandleColorPickerChanged(ChangeEventArgs e)
    {
        var value = e.Value?.ToString();
        if (!string.IsNullOrEmpty(value))
        {
            Input.EmbedColor = value.ToUpperInvariant();
        }
    }

    protected void ToggleTokenHelp() => TokensExpanded = !TokensExpanded;

    /// <summary>The channel dropdown's option prefix, matching Welcome.cshtml's per-type icon choice.</summary>
    protected static string ChannelPrefix(ChannelDisplayType type) => type switch
    {
        ChannelDisplayType.Text => "#",
        ChannelDisplayType.Voice => "\U0001F50A",
        ChannelDisplayType.Announcement => "\U0001F4E2",
        ChannelDisplayType.Stage => "\U0001F3AD",
        ChannelDisplayType.Forum => "\U0001F4AC",
        _ => "#"
    };

    /// <summary>
    /// The live preview markup, built server-side from <see cref="Input"/> and the guild name -
    /// the C# equivalent of <c>updatePreview()</c>. HTML-encodes the message and the guild name
    /// before token substitution so neither can inject markup into this pane.
    /// </summary>
    protected MarkupString BuildPreview(string guildName)
    {
        if (!Input.IsEnabled)
        {
            return new MarkupString("<span class=\"text-[#72767d] italic\">Welcome messages are disabled</span>");
        }

        var message = Input.WelcomeMessage ?? string.Empty;
        if (string.IsNullOrEmpty(message))
        {
            return new MarkupString("<span class=\"text-[#72767d] italic\">Enter a message to see preview</span>");
        }

        var encoded = WebUtility.HtmlEncode(message)
            .Replace("{user}", "<span class=\"discord-mention\">@NewMember</span>")
            .Replace("{username}", "<strong>NewMember</strong>")
            .Replace("{server}", $"<strong>{WebUtility.HtmlEncode(guildName)}</strong>")
            .Replace("{memberCount}", "<strong>1,234</strong>")
            .Replace("\n", "<br>");

        return new MarkupString(encoded);
    }

    /// <summary>The preview's embed-border inline style, or empty when no embed border should show.</summary>
    protected string PreviewBorderStyle => Input.IsEnabled && Input.UseEmbed && IsValidHex(Input.EmbedColor)
        ? $"border-left: 4px solid {Input.EmbedColor}; padding-left: 0.75rem;"
        : string.Empty;

    private static bool IsValidHex(string? value) => !string.IsNullOrEmpty(value) && HexColorPattern.IsMatch(value);

    /// <summary>Mirrors <c>WelcomeModel.InputModel</c>.</summary>
    public sealed class InputModel
    {
        [Display(Name = "Enable Welcome Messages")]
        public bool IsEnabled { get; set; }

        [Display(Name = "Welcome Channel")]
        public ulong? WelcomeChannelId { get; set; }

        [StringLength(2000, ErrorMessage = "Welcome message cannot exceed 2000 characters")]
        [Display(Name = "Welcome Message")]
        public string? WelcomeMessage { get; set; }

        [Display(Name = "Include User Avatar")]
        public bool IncludeAvatar { get; set; }

        [Display(Name = "Use Embed")]
        public bool UseEmbed { get; set; }

        [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Embed color must be a valid hex color (e.g., #5865F2)")]
        [Display(Name = "Embed Color")]
        public string? EmbedColor { get; set; }
    }
}
