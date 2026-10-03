using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DiscordBot.Bot.Helpers;

/// <summary>
/// Glue between a page's <see cref="ModelStateDictionary"/> and the form partials
/// (<c>_FormInput</c>, <c>_FormTextarea</c>, <c>_FormSelect</c>), which take their error as view-model
/// properties rather than reading ModelState themselves.
/// </summary>
public static class FormFieldState
{
    /// <summary>The first error recorded for a field (key like <c>Input.Title</c>), or null.</summary>
    public static string? FieldError(this ModelStateDictionary modelState, string key)
    {
        return modelState.TryGetValue(key, out var entry)
            ? entry.Errors.Select(e => e.ErrorMessage).FirstOrDefault(m => !string.IsNullOrEmpty(m))
            : null;
    }

    /// <summary><see cref="ValidationState.Error"/> when the field has an error, otherwise none.</summary>
    public static ValidationState StateOf(string? error) =>
        string.IsNullOrEmpty(error) ? ValidationState.None : ValidationState.Error;

    /// <summary>The label shown beside a channel in a picker: a type glyph and the name.</summary>
    public static string ChannelLabel(ChannelSelectItem channel)
    {
        var prefix = channel.Type switch
        {
            ChannelDisplayType.Voice => "\U0001F50A",
            ChannelDisplayType.Announcement => "\U0001F4E2",
            ChannelDisplayType.Stage => "\U0001F3AD",
            ChannelDisplayType.Forum => "\U0001F4AC",
            _ => "#"
        };
        return $"{prefix} {channel.Name}";
    }

    /// <summary>
    /// The options of a channel picker. When the saved channel is not among the channels the bot can
    /// see (deleted, renamed out of view, or the bot is offline), it is still listed and selected, so
    /// opening the page and saving does not silently clear it.
    /// </summary>
    /// <param name="channels">The channels the bot can currently see.</param>
    /// <param name="selectedId">The channel that is saved or was just submitted.</param>
    /// <param name="noneText">Text for the "no channel" choice.</param>
    /// <param name="selectedIsMissing">True when <paramref name="selectedId"/> was not found.</param>
    public static List<SelectOption> ChannelOptions(
        IEnumerable<ChannelSelectItem> channels,
        ulong? selectedId,
        string noneText,
        out bool selectedIsMissing)
    {
        var list = channels.ToList();
        var options = new List<SelectOption> { new() { Value = string.Empty, Text = noneText } };
        selectedIsMissing = selectedId.HasValue && list.All(c => c.Id != selectedId.Value);

        if (selectedIsMissing)
        {
            options.Add(new SelectOption
            {
                Value = selectedId!.Value.ToString(),
                Text = $"Unknown channel ({selectedId.Value})"
            });
        }

        options.AddRange(list.Select(c => new SelectOption { Value = c.Id.ToString(), Text = ChannelLabel(c) }));
        return options;
    }
}
