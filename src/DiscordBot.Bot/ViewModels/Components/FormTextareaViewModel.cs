namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// View model for <c>_FormTextarea</c>, a thin wrapper over <c>.form-textarea</c>. It shares
/// validation states and <c>aria-describedby</c> rules with <see cref="FormInputViewModel"/>.
/// </summary>
public record FormTextareaViewModel
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Label { get; init; }
    public string? Placeholder { get; init; }
    public string? Value { get; init; }
    public string? HelpText { get; init; }
    public int Rows { get; init; } = 4;
    public int? MaxLength { get; init; }
    public ValidationState ValidationState { get; init; } = ValidationState.None;
    public string? ValidationMessage { get; init; }
    public bool IsRequired { get; init; } = false;
    public bool IsDisabled { get; init; } = false;
    public bool IsReadOnly { get; init; } = false;

    /// <summary>The <c>autocomplete</c> token. Free text rarely wants one; leave it null or use <c>off</c>.</summary>
    public string? Autocomplete { get; init; }

    /// <summary>Text direction: <c>auto</c> lets right-to-left text flow correctly for user-written content.</summary>
    public string? Dir { get; init; }

    /// <summary>Extra element IDs for <c>aria-describedby</c>, after the component's own.</summary>
    public string? DescribedBy { get; init; }

    public Dictionary<string, string>? AdditionalAttributes { get; init; }
}
