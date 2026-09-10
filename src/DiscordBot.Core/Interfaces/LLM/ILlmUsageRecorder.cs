using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces.LLM;

/// <summary>
/// Non-blocking write path into the <see cref="LlmUsageRecord"/> ledger. Implementations enqueue
/// onto a bounded background channel (matching <c>IAuditLogQueue</c>'s posture) so recording never
/// adds latency to a reply; a full queue drops the oldest entry rather than blocking the caller.
/// </summary>
public interface ILlmUsageRecorder
{
    /// <summary>Enqueues <paramref name="record"/> for background persistence. Returns immediately.</summary>
    void Record(LlmUsageRecord record);
}
