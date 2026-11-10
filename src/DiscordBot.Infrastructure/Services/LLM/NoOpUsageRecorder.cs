using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces.LLM;

namespace DiscordBot.Infrastructure.Services.LLM;

/// <summary>
/// Fallback <see cref="ILlmUsageRecorder"/> used when a context is constructed without one
/// (direct-construction tests that pre-date the usage ledger). Drops every record silently -
/// the same "no recorder means no recording" posture as passing no resolved pricing.
/// </summary>
internal sealed class NoOpUsageRecorder : ILlmUsageRecorder
{
    public static readonly NoOpUsageRecorder Instance = new();

    private NoOpUsageRecorder()
    {
    }

    public void Record(LlmUsageRecord record)
    {
    }
}
