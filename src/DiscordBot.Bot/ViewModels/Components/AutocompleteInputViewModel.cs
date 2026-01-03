// src/DiscordBot.Bot/ViewModels/Components/AutocompleteInputViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// ViewModel for the autocomplete input partial view.
/// Configures the HTML structure and JavaScript behavior of autocomplete inputs.
/// </summary>
public record AutocompleteInputViewModel
{
    /// <summary>
    /// The ID for the hidden input element that stores the selected value.
    /// Also used as the base for generating related element IDs.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// The form name for the hidden input element used during form submission.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The label text displayed above the input.
    /// </summary>
    public string? Label { get; init; }

    /// <summary>
    /// Placeholder text for the search input.
    /// </summary>
    public string? Placeholder { get; init; }

    /// <summary>
    /// The API endpoint for autocomplete search requests.
    /// Example: "/api/autocomplete/users"
    /// </summary>
    public string Endpoint { get; init; } = string.Empty;

    /// <summary>
    /// The initial value (ID) for the hidden input when editing existing data.
    /// </summary>
    public string? InitialValue { get; init; }

    /// <summary>
    /// The initial display text for the search input when editing existing data.
    /// </summary>
    public string? InitialDisplayText { get; init; }

    /// <summary>
    /// Optional ID of an element containing the guild ID to include in search requests.
    /// Used for guild-scoped searches (e.g., searching users within a specific guild).
    /// </summary>
    public string? GuildIdSourceElement { get; init; }

    /// <summary>
    /// Whether this field is required for form submission.
    /// </summary>
    public bool IsRequired { get; init; }

    /// <summary>
    /// Minimum characters before triggering search. Defaults to 2.
    /// </summary>
    public int MinChars { get; init; } = 2;

    /// <summary>
    /// Debounce delay in milliseconds. Defaults to 300.
    /// </summary>
    public int DebounceMs { get; init; } = 300;

    /// <summary>
    /// Maximum number of results to display. Defaults to 25.
    /// </summary>
    public int MaxResults { get; init; } = 25;

    /// <summary>
    /// Message displayed when no results are found.
    /// </summary>
    public string NoResultsMessage { get; init; } = "No results found";

    /// <summary>
    /// Optional help text displayed below the input.
    /// </summary>
    public string? HelpText { get; init; }
}
