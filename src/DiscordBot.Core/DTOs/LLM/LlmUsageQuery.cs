using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs.LLM;

/// <summary>
/// Filter shared by every grouped <c>ILlmUsageRepository</c> read: a date range plus optional
/// guild/mode/user narrowing.
/// </summary>
public class LlmUsageQuery
{
    public required DateTime From { get; init; }
    public required DateTime To { get; init; }
    public ulong? GuildId { get; init; }
    public LlmMode? Mode { get; init; }
    public ulong? UserId { get; init; }
}
