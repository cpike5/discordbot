namespace DiscordBot.Core.Enums;

/// <summary>Whether an assistant thread still takes turns.</summary>
public enum AssistantThreadStatus
{
    /// <summary>Messages in the thread are answered.</summary>
    Active = 0,

    /// <summary>The turn cap was reached; messages in the thread are ignored.</summary>
    Closed = 1
}
