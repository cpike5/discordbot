using DiscordBot.Agents;
using DiscordBot.Core.Enums;

namespace DiscordBot.Infrastructure.Abstractions.LLM;

/// <summary>
/// What one assistant surface puts in front of the model, and what it costs.
/// </summary>
/// <remarks>
/// Two sets, and the difference between them is the interesting part: <see cref="Advertised"/> is
/// what a request actually carries, <see cref="Tools"/> is every tool the surface's registry holds —
/// including the ones a guild has turned off and the ones a skill is holding back.
/// </remarks>
public sealed record PromptSurfaceReport
{
    /// <summary>The surface this reports on.</summary>
    public required ToolScopes Scope { get; init; }

    /// <summary>Human-readable surface name, for a log line or a page heading.</summary>
    public required string SurfaceName { get; init; }

    /// <summary>The guild whose allow-list was applied, or null for the house set.</summary>
    public ulong? GuildId { get; init; }

    /// <summary>What the per-request tool array costs, as it would be sent.</summary>
    public required PromptSurfaceMeasurement Advertised { get; init; }

    /// <summary>
    /// Every tool the surface's registry holds, advertised or not, largest first.
    /// </summary>
    public required IReadOnlyList<PromptSurfaceToolRow> Tools { get; init; }

    /// <summary>Characters every registered tool would cost if all of them were advertised.</summary>
    public required int RegisteredChars { get; init; }

    /// <summary>How many tools the surface's registry holds.</summary>
    public int RegisteredCount => Tools.Count;

    /// <summary>How many of them a request actually carries.</summary>
    public int AdvertisedCount => Advertised.ToolCount;

    /// <summary>
    /// Characters kept out of the prefix by skills and, for a guild report, by the allow-list.
    /// </summary>
    public int WithheldChars => Tools.Where(t => !t.Advertised).Sum(t => t.SchemaChars);
}

/// <summary>One tool's row in a prompt-surface report.</summary>
/// <param name="Name">The model-facing tool name.</param>
/// <param name="DisplayName">The catalogue's human-readable label.</param>
/// <param name="Category">The catalogue's category.</param>
/// <param name="SchemaChars">Characters this tool's definition serializes to.</param>
/// <param name="Share">
/// Its fraction of the advertised array, or 0 when it is not advertised — a tool that is not in the
/// prefix has no share of it.
/// </param>
/// <param name="Advertised">Whether a request on this surface actually carries it.</param>
/// <param name="BehindSkill">
/// Whether a skill is holding it back. A tool can be behind a skill and advertised at the same time,
/// on a surface that replayed that skill's activation — so this says why a tool <em>might</em> be
/// absent, and <paramref name="Advertised"/> says whether it is.
/// </param>
/// <param name="SkillKeys">The skills naming it, if any.</param>
public sealed record PromptSurfaceToolRow(
    string Name,
    string DisplayName,
    string Category,
    int SchemaChars,
    double Share,
    bool Advertised,
    bool BehindSkill,
    IReadOnlyList<string> SkillKeys);
