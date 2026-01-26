// src/DiscordBot.Bot/ViewModels/Components/SortDropdownViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// ViewModel for the reusable SortDropdown component.
/// Provides a dropdown UI for selecting sort options with keyboard navigation and accessibility support.
/// </summary>
public record SortDropdownViewModel
{
    /// <summary>
    /// Unique identifier for this dropdown instance. Used for generating
    /// element IDs and managing multiple dropdowns on the same page.
    /// </summary>
    public string Id { get; init; } = "sortDropdown";

    /// <summary>
    /// Collection of sort options to display in the dropdown.
    /// </summary>
    public List<SortOption> SortOptions { get; init; } = new();

    /// <summary>
    /// The value of the currently selected sort option.
    /// </summary>
    public string CurrentSort { get; init; } = string.Empty;

    /// <summary>
    /// The query parameter name to use when constructing sort URLs.
    /// Default is "sort".
    /// </summary>
    public string ParameterName { get; init; } = "sort";
}

/// <summary>
/// Represents a single sort option in the dropdown.
/// </summary>
public record SortOption
{
    /// <summary>
    /// The value to use in the query parameter when this option is selected.
    /// </summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>
    /// The display label shown to the user for this sort option.
    /// </summary>
    public string Label { get; init; } = string.Empty;
}
