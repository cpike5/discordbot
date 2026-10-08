namespace DiscordBot.Core.Configuration;

/// <summary>
/// Thread-mode settings for the guild assistant. Binds under "Assistant:Threads".
/// </summary>
public class AssistantThreadOptions
{
    /// <summary>
    /// How many of a thread's most recent turns (user and assistant messages together) seed the
    /// next turn. Same meaning as the DM assistant's window.
    /// </summary>
    public int MaxConversationMessages { get; set; } = 20;

    /// <summary>Turns after which a thread is closed and later messages in it are ignored.</summary>
    public int MaxTurnsPerThread { get; set; } = 40;

    /// <summary>
    /// Days of inactivity after which a thread and its history are deleted by the retention sweep.
    /// 0 or less disables the sweep for threads.
    /// </summary>
    public int HistoryRetentionDays { get; set; } = 30;

    /// <summary>
    /// How long Discord waits before archiving an idle assistant thread, in minutes. Mapped to the
    /// nearest archive duration Discord offers (60, 1440, 4320, 10080).
    /// </summary>
    public int AutoArchiveMinutes { get; set; } = 1440;
}
