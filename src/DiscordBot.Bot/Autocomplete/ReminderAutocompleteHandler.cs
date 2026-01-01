using Discord;
using Discord.Interactions;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Autocomplete;

/// <summary>
/// Autocomplete handler that provides suggestions for reminder IDs based on the user's pending reminders.
/// </summary>
public class ReminderAutocompleteHandler : AutocompleteHandler
{
    /// <summary>
    /// Generates autocomplete suggestions for reminder IDs.
    /// Returns up to 25 pending reminders for the current user.
    /// </summary>
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        var reminderService = services.GetRequiredService<IReminderService>();
        var input = autocompleteInteraction.Data.Current.Value?.ToString() ?? "";

        // Get the user's pending reminders (max 25 for autocomplete limit)
        var (reminders, _) = await reminderService.GetUserRemindersAsync(
            context.User.Id,
            1,
            25);

        // Filter and format for autocomplete
        var results = reminders
            .Where(r =>
            {
                if (string.IsNullOrEmpty(input))
                {
                    return true;
                }

                // Match on message content or ID prefix
                return r.Message.Contains(input, StringComparison.OrdinalIgnoreCase) ||
                       r.Id.ToString().StartsWith(input, StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(r => r.TriggerAt)
            .Take(25)
            .Select(r =>
            {
                // Format: "Message preview... (in X hours)"
                var messagePreview = r.Message.Length > 60
                    ? r.Message[..57] + "..."
                    : r.Message;

                var timeUntil = r.TriggerAt - DateTime.UtcNow;
                var timeText = FormatTimeUntil(timeUntil);

                var label = $"{messagePreview} ({timeText})";

                // Truncate label if needed (Discord limit is 100 chars)
                if (label.Length > 100)
                {
                    label = label[..97] + "...";
                }

                return new AutocompleteResult(label, r.Id.ToString());
            });

        return AutocompletionResult.FromSuccess(results);
    }

    /// <summary>
    /// Formats a TimeSpan into a human-readable string (e.g., "in 2h", "in 1d 3h").
    /// </summary>
    private static string FormatTimeUntil(TimeSpan timeSpan)
    {
        if (timeSpan.TotalMinutes < 60)
        {
            return $"in {(int)timeSpan.TotalMinutes}m";
        }

        if (timeSpan.TotalHours < 24)
        {
            var hours = (int)timeSpan.TotalHours;
            var minutes = timeSpan.Minutes;
            return minutes > 0 ? $"in {hours}h {minutes}m" : $"in {hours}h";
        }

        var days = (int)timeSpan.TotalDays;
        var remainingHours = timeSpan.Hours;
        return remainingHours > 0 ? $"in {days}d {remainingHours}h" : $"in {days}d";
    }
}
