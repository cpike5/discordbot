namespace DiscordBot.Core.Interfaces.LLM;

/// <summary>
/// Remembers which skills the DM assistant has loaded for a user, so a skill loaded on one turn is
/// still in force on the next.
/// </summary>
/// <remarks>
/// <para>
/// This is what makes a skill nearly free on the DM surface. Turn 1 pays a round to load one; every
/// turn after that replays the activation, so the skill's tools are advertised from the first call
/// and its instructions are already in the prompt. The guild assistant is single-turn and has no
/// equivalent — there, a skill costs its round every time it is used.
/// </para>
/// <para>
/// Deliberately not a table. It is the same posture as the DM assistant's active guild: a
/// conversation-scoped convenience, and losing it on restart costs one extra round rather than
/// anything a user would notice.
/// </para>
/// </remarks>
public interface IDmSkillActivationStore
{
    /// <summary>The skill keys active for this user, oldest activation first. Empty when none are.</summary>
    /// <param name="userId">The Discord user id.</param>
    IReadOnlyList<string> Get(ulong userId);

    /// <summary>Replaces the user's active skill keys. An empty list clears them.</summary>
    /// <param name="userId">The Discord user id.</param>
    /// <param name="skillKeys">The keys now active.</param>
    void Set(ulong userId, IEnumerable<string> skillKeys);

    /// <summary>Forgets the user's active skills — what clearing a conversation should do.</summary>
    /// <param name="userId">The Discord user id.</param>
    void Clear(ulong userId);
}
