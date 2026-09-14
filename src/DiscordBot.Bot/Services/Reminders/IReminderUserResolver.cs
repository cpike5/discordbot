namespace DiscordBot.Bot.Services.Reminders;

/// <summary>
/// Resolves a Discord user's display name and avatar for the Reminders admin list
/// (<c>Blazor/Pages/Guilds/Reminders/Index.razor</c>), reproducing
/// <c>Pages/Guilds/Reminders/Index.cshtml.cs</c>'s inline lookup: guild member cache first, a REST
/// fallback on a cache miss, "Unknown (id)" when both fail. Pulled out into its own interface (a
/// small injectable seam over <see cref="Discord.WebSocket.DiscordSocketClient"/>, which bUnit
/// cannot fake directly) purely so the page's bUnit tests can mock user resolution without a real
/// gateway connection.
/// </summary>
public interface IReminderUserResolver
{
    /// <summary>
    /// Resolves <paramref name="userId"/>'s display name and avatar within <paramref name="guildId"/>.
    /// Never throws: a REST failure or missing guild/user falls back to
    /// <c>("Unknown (id)", null)</c>.
    /// </summary>
    Task<ReminderUserInfo> ResolveAsync(ulong guildId, ulong userId, CancellationToken cancellationToken = default);
}

/// <summary>The display name and avatar URL resolved for a reminder's owner.</summary>
/// <param name="Username">The resolved display name, or <c>"Unknown ({userId})"</c> if it could not be resolved.</param>
/// <param name="AvatarUrl">The resolved avatar URL, or <see langword="null"/> if unavailable.</param>
public sealed record ReminderUserInfo(string Username, string? AvatarUrl);
