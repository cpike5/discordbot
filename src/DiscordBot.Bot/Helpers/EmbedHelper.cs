using Discord;

namespace DiscordBot.Bot.Helpers;

/// <summary>
/// Provides standardized factory methods for building Discord embeds with consistent colors and structure.
/// </summary>
public static class EmbedHelper
{
    /// <summary>
    /// Creates a red error embed.
    /// </summary>
    /// <param name="title">The embed title.</param>
    /// <param name="description">The embed description.</param>
    /// <returns>A built <see cref="Embed"/> with red color and a current timestamp.</returns>
    public static Embed Error(string title, string description)
    {
        return new EmbedBuilder()
            .WithTitle(title)
            .WithDescription(description)
            .WithColor(Color.Red)
            .WithCurrentTimestamp()
            .Build();
    }

    /// <summary>
    /// Creates a green success embed.
    /// </summary>
    /// <param name="title">The embed title.</param>
    /// <param name="description">The embed description.</param>
    /// <returns>A built <see cref="Embed"/> with green color and a current timestamp.</returns>
    public static Embed Success(string title, string description)
    {
        return new EmbedBuilder()
            .WithTitle(title)
            .WithDescription(description)
            .WithColor(Color.Green)
            .WithCurrentTimestamp()
            .Build();
    }

    /// <summary>
    /// Creates a blue empty-state embed, used when a query returns no results.
    /// </summary>
    /// <param name="title">The embed title.</param>
    /// <param name="description">The embed description.</param>
    /// <returns>A built <see cref="Embed"/> with blue color and a current timestamp.</returns>
    public static Embed EmptyState(string title, string description)
    {
        return new EmbedBuilder()
            .WithTitle(title)
            .WithDescription(description)
            .WithColor(Color.Blue)
            .WithCurrentTimestamp()
            .Build();
    }

    /// <summary>
    /// Creates an orange confirmation embed, used when asking the user to confirm a destructive action.
    /// </summary>
    /// <param name="title">The embed title.</param>
    /// <param name="description">The embed description.</param>
    /// <returns>A built <see cref="Embed"/> with orange color and a current timestamp.</returns>
    public static Embed Confirmation(string title, string description)
    {
        return new EmbedBuilder()
            .WithTitle(title)
            .WithDescription(description)
            .WithColor(Color.Orange)
            .WithCurrentTimestamp()
            .Build();
    }

    /// <summary>
    /// Creates a blue informational embed.
    /// </summary>
    /// <param name="title">The embed title.</param>
    /// <param name="description">The embed description.</param>
    /// <returns>A built <see cref="Embed"/> with blue color and a current timestamp.</returns>
    public static Embed Info(string title, string description)
    {
        return new EmbedBuilder()
            .WithTitle(title)
            .WithDescription(description)
            .WithColor(Color.Blue)
            .WithCurrentTimestamp()
            .Build();
    }
}
