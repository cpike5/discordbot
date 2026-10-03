// src/DiscordBot.Bot/ViewModels/Components/FormInputViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// View model for <c>_FormInput</c>, a thin wrapper over <c>.form-input</c>.
/// </summary>
public record FormInputViewModel
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Label { get; init; }
    public string Type { get; init; } = "text"; // text, email, password, search, url, tel, number, date...
    public string? Placeholder { get; init; }
    public string? Value { get; init; }
    public string? HelpText { get; init; }
    public InputSize Size { get; init; } = InputSize.Medium;
    public ValidationState ValidationState { get; init; } = ValidationState.None;
    public string? ValidationMessage { get; init; }
    public bool IsRequired { get; init; } = false;
    public bool IsDisabled { get; init; } = false;
    public bool IsReadOnly { get; init; } = false;
    public string? IconLeft { get; init; }   // SVG icon path
    public string? IconRight { get; init; }
    public int? MaxLength { get; init; }
    public bool ShowCharacterCount { get; init; } = false;

    /// <summary>
    /// The <c>autocomplete</c> token: <c>email</c>, <c>username</c>, <c>current-password</c>,
    /// <c>new-password</c>, <c>one-time-code</c>, <c>off</c>... Passwords and sign-in names need it
    /// so password managers and browsers behave.
    /// </summary>
    public string? Autocomplete { get; init; }

    /// <summary>
    /// The <c>inputmode</c> hint that picks the on-screen keyboard (<c>numeric</c>, <c>decimal</c>,
    /// <c>email</c>, <c>tel</c>, <c>url</c>, <c>search</c>). Use <c>numeric</c> on <c>type="text"</c>
    /// for IDs and counts that are not quantities.
    /// </summary>
    public string? InputMode { get; init; }

    /// <summary>Lower bound for <c>number</c> and date inputs. A string so decimals and dates pass through unchanged.</summary>
    public string? Min { get; init; }

    /// <summary>Upper bound for <c>number</c> and date inputs.</summary>
    public string? Max { get; init; }

    /// <summary>Step for <c>number</c> inputs (<c>1</c>, <c>0.01</c>, <c>any</c>).</summary>
    public string? Step { get; init; }

    /// <summary>A validation regular expression (<c>pattern</c> attribute).</summary>
    public string? Pattern { get; init; }

    /// <summary>
    /// Extra element IDs (space separated) for <c>aria-describedby</c>, added after the
    /// component's own help and message IDs. Use it to tie the field to a nearby hint or counter.
    /// </summary>
    public string? DescribedBy { get; init; }

    public Dictionary<string, string>? AdditionalAttributes { get; init; }
}

public enum InputSize
{
    Small,      // form-input-sm
    Medium,     // default
    Large       // form-input-lg
}

public enum ValidationState
{
    None,
    Success,
    Warning,
    Error
}
