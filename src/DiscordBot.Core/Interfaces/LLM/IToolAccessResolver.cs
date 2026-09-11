namespace DiscordBot.Core.Interfaces.LLM;

/// <summary>
/// Resolves which tools a guild's assistant may use, from the guild's saved allow-list falling back
/// to the house default set.
/// </summary>
/// <remarks>
/// The unit is the <em>guild</em>, not the user: a guild's tool array is shared by every caller in
/// it, so filtering here still leaves one prompt-cache prefix per guild. Per-caller permission is a
/// different mechanism - see <c>ToolContext.CanMutate</c>, which is checked inside a tool rather
/// than used to filter the advertised list.
/// </remarks>
public interface IToolAccessResolver
{
    /// <summary>
    /// The tool names this guild's assistant may use. Never empty: a guild that has selected
    /// nothing gets the house default set.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlySet<string>> ResolveAsync(ulong guildId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops any cached answer for <paramref name="guildId"/>. Called by
    /// <c>IAssistantGuildSettingsService</c> when settings are saved, so a changed allow-list takes
    /// effect on the next question rather than when a cache entry happens to expire.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    void Invalidate(ulong guildId);
}
