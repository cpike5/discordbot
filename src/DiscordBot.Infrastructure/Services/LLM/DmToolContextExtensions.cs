using DiscordBot.Agents.Contracts;

namespace DiscordBot.Infrastructure.Services.LLM;

/// <summary>
/// Typed access to the DM assistant's entries in <see cref="ToolContext.Items"/>.
/// </summary>
/// <remarks>
/// "Active guild" is a DM-assistant concept, not an agent-engine one, so it rides in the context's
/// application bag rather than on the engine's contract. These helpers keep the call sites as
/// readable as the property they replaced, and keep the key spelled in exactly one place.
/// </remarks>
public static class DmToolContextExtensions
{
    /// <summary>The <see cref="ToolContext.Items"/> key holding the DM assistant's active guild.</summary>
    public const string ActiveGuildIdKey = "dm.activeGuildId";

    /// <summary>
    /// The active guild selected via the <c>set_active_guild</c> tool, or <c>null</c> when none is set.
    /// Tools that need guild context use it as a fallback when no explicit guild_id was supplied.
    /// </summary>
    public static ulong? GetActiveGuildId(this ToolContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Items.TryGetValue(ActiveGuildIdKey, out var value) && value is ulong guildId
            ? guildId
            : null;
    }

    /// <summary>
    /// Sets — or, with <c>null</c>, clears — the DM assistant's active guild.
    /// </summary>
    public static void SetActiveGuildId(this ToolContext context, ulong? guildId)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (guildId.HasValue)
            context.Items[ActiveGuildIdKey] = guildId.Value;
        else
            context.Items.Remove(ActiveGuildIdKey);
    }
}
