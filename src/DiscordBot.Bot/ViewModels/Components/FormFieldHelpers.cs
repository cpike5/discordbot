namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// Rules shared by the form partials (<c>_FormInput</c>, <c>_FormTextarea</c>, radio cards), so
/// validation classes and <c>aria-describedby</c> are built one way.
/// </summary>
public static class FormFieldHelpers
{
    /// <summary>
    /// The class that colours a field for its validation state. These are the same names the
    /// ASP.NET tag helpers and jQuery validation use, so server-rendered and live validation look alike.
    /// </summary>
    public static string ValidationClass(ValidationState state) => state switch
    {
        ValidationState.Error => "input-validation-error",
        ValidationState.Warning => "input-validation-warning",
        ValidationState.Success => "input-validation-success",
        _ => string.Empty
    };

    /// <summary>
    /// Builds <c>aria-describedby</c>: the help text while the field has no state, the message
    /// while it has one, then any caller-supplied IDs. Returns null when there is nothing to
    /// point at, so the attribute is left out.
    /// </summary>
    public static string? DescribedBy(
        string id, string? helpText, ValidationState state, string? validationMessage, string? extra)
    {
        var ids = new List<string>();
        if (state == ValidationState.None)
        {
            if (!string.IsNullOrEmpty(helpText))
            {
                ids.Add($"{id}-help");
            }
        }
        else if (!string.IsNullOrEmpty(validationMessage))
        {
            ids.Add(MessageId(id, state));
        }

        if (!string.IsNullOrWhiteSpace(extra))
        {
            ids.Add(extra.Trim());
        }

        return ids.Count > 0 ? string.Join(' ', ids) : null;
    }

    /// <summary>
    /// True when the field renders a single help-or-message paragraph for assistive technology to
    /// point at: the help text while the field has no state, the validation message while it has
    /// one. Help text hidden behind a Success or Warning state is not rendered, so it is not counted.
    /// </summary>
    public static bool HasMessageElement(string? helpText, ValidationState state, string? validationMessage)
        => state == ValidationState.None
            ? !string.IsNullOrEmpty(helpText)
            : !string.IsNullOrEmpty(validationMessage);

    /// <summary>The element ID of the message paragraph for a state.</summary>
    public static string MessageId(string id, ValidationState state) => state switch
    {
        ValidationState.Error => $"{id}-error",
        ValidationState.Warning => $"{id}-warning",
        ValidationState.Success => $"{id}-success",
        _ => $"{id}-help"
    };
}
