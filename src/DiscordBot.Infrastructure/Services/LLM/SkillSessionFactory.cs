using DiscordBot.Agents;
using DiscordBot.Agents.Abstractions;
using DiscordBot.Agents.Contracts;
using DiscordBot.Infrastructure.Abstractions.LLM;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Services.LLM;

/// <inheritdoc cref="ISkillSessionFactory" />
public sealed class SkillSessionFactory : ISkillSessionFactory
{
    private readonly ISkillLibrary _library;
    private readonly ILogger<SkillSessionFactory> _logger;

    /// <summary>Creates the factory.</summary>
    /// <param name="library">Reads the skill files.</param>
    /// <param name="logger">Reports a skill naming a tool this surface does not advertise.</param>
    public SkillSessionFactory(ISkillLibrary library, ILogger<SkillSessionFactory> logger)
    {
        _library = library ?? throw new ArgumentNullException(nameof(library));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<SkillSession> CreateAsync(
        string directory,
        IToolRegistry? registry,
        IEnumerable<string>? preActivatedKeys = null,
        CancellationToken cancellationToken = default)
    {
        var skills = await _library.LoadAsync(directory, cancellationToken);

        if (skills.Count == 0)
        {
            // No registry walk on this path: a surface with no skill files is the common case, and
            // the session still carries the loader's name so the loop knows to drop it.
            return new SkillSession(Array.Empty<AgentSkill>(), preActivated: null, LoaderToolName());
        }

        var advertised = registry is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : registry.GetEnabledTools().Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var narrowed = skills.Select(skill => Narrow(skill, advertised)).ToList();

        return new SkillSession(narrowed, preActivatedKeys, LoaderToolName());
    }

    /// <summary>
    /// The name the loader tool is advertised under, so the loop can drop it when a surface has no
    /// skills. Taken from the tool rather than restated, because the two disagreeing would mean
    /// paying for a schema nothing can use.
    /// </summary>
    private static string LoaderToolName() => Tools.LoadSkillTool.ToolName;

    /// <summary>
    /// <paramref name="skill"/> with its tool list cut down to what this run advertises.
    /// </summary>
    private AgentSkill Narrow(AgentSkill skill, IReadOnlySet<string> advertised)
    {
        if (skill.Tools.Count == 0)
        {
            return skill;
        }

        var kept = skill.Tools.Where(advertised.Contains).ToList();

        if (kept.Count != skill.Tools.Count)
        {
            // Routine on the guild surface, where an allow-list can exclude a tool a skill names;
            // a sign of a typo or a stale skill file anywhere else. Debug either way - it is not a
            // fault, and the model is never told about a tool it cannot call.
            _logger.LogDebug(
                "Skill {SkillKey} names {DroppedCount} tool(s) this surface does not advertise: {DroppedTools}",
                skill.Key,
                skill.Tools.Count - kept.Count,
                string.Join(", ", skill.Tools.Except(kept, StringComparer.OrdinalIgnoreCase)));
        }

        return skill with { Tools = kept };
    }
}
