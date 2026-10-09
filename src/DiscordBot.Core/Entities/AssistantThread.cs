using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Entities;

/// <summary>
/// One conversation the guild assistant holds in a Discord thread it created. The Discord thread id
/// is the key: the thread is the conversation, and its messages are the history.
/// </summary>
public class AssistantThread
{
    /// <summary>The Discord thread's snowflake id (primary key).</summary>
    public ulong ThreadId { get; set; }

    /// <summary>The guild the thread belongs to.</summary>
    public ulong GuildId { get; set; }

    /// <summary>The text channel the thread hangs off; the allowed-channel check runs against it.</summary>
    public ulong ParentChannelId { get; set; }

    /// <summary>The member whose mention opened the thread. 0 once that member's data is purged.</summary>
    public ulong StarterUserId { get; set; }

    /// <summary>When the thread was created (UTC).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>When a turn was last taken (UTC). Retention sweeps on this.</summary>
    public DateTime LastActivityAt { get; set; }

    /// <summary>Turns taken so far; the cap closes the thread.</summary>
    public int TurnCount { get; set; }

    /// <summary>Whether the thread still takes turns.</summary>
    public AssistantThreadStatus Status { get; set; } = AssistantThreadStatus.Active;

    /// <summary>
    /// The skills loaded in this conversation, as a JSON array of keys. Replayed into the next turn
    /// so a skill costs one round per conversation. Kept on the row rather than in memory because
    /// a thread can be picked up days later and after a restart.
    /// </summary>
    public string ActiveSkills { get; set; } = "[]";

    /// <summary>Navigation property for the guild.</summary>
    public Guild? Guild { get; set; }

    /// <summary>Reads <see cref="ActiveSkills"/>.</summary>
    public List<string> GetActiveSkillsList()
    {
        if (string.IsNullOrWhiteSpace(ActiveSkills) || ActiveSkills == "[]")
            return new List<string>();

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(ActiveSkills) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>Writes <see cref="ActiveSkills"/>.</summary>
    public void SetActiveSkillsList(IEnumerable<string> keys)
    {
        ActiveSkills = System.Text.Json.JsonSerializer.Serialize(keys?.ToList() ?? new List<string>());
    }
}
