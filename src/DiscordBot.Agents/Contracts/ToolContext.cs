namespace DiscordBot.Agents.Contracts;

/// <summary>
/// Context information passed to tools during execution.
/// </summary>
public class ToolContext
{
    /// <summary>
    /// The Discord user ID making the request.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// The Discord guild (server) ID for context.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// The Discord channel ID for context.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    /// The Discord message ID that triggered the request.
    /// </summary>
    public ulong MessageId { get; set; }

    /// <summary>
    /// Whether the caller may perform actions that create or change data.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Consulted <em>inside</em> a mutating tool rather than used to filter the advertised tool
    /// list. Filtering per user would give every permission level its own prompt-cache prefix, and
    /// the tool array is the most expensive thing to fragment — it serializes at position 0 of the
    /// request, ahead of everything else. Per-<em>guild</em> filtering is a different matter and is
    /// done at the registry boundary; see <c>FilteredToolRegistry</c>.
    /// </para>
    /// <para>
    /// Defaults to false, so a tool that forgets the check is the only way a caller gets
    /// unintended write access — never an unpopulated context.
    /// </para>
    /// </remarks>
    public bool CanMutate { get; set; }

    /// <summary>
    /// Open-ended, application-specific state for this run, keyed by string. The engine never reads
    /// it; it exists so the hosting application can hand its own tools context the engine has no
    /// concept of, without the contract growing an app-specific property per feature.
    /// </summary>
    public Dictionary<string, object?> Items { get; } = new();
}
