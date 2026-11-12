namespace DiscordBot.Core.DTOs;

/// <summary>
/// A guild the signed-in user may open, as offered by the guild switcher and the sidebar's recent
/// guilds list.
/// </summary>
/// <param name="Id">Discord guild ID (snowflake).</param>
/// <param name="Name">Guild name.</param>
/// <param name="IconUrl">Guild icon URL, or null when the guild has no icon.</param>
public sealed record GuildSwitcherEntryDto(ulong Id, string Name, string? IconUrl);
