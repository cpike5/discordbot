using DiscordBot.Agents;
using DiscordBot.Agents.Abstractions;
using DiscordBot.Agents.Contracts;
using DiscordBot.Infrastructure.Abstractions.LLM;

namespace DiscordBot.Tests.TestHelpers;

/// <summary>
/// An <see cref="ISkillSessionFactory"/> that hands back a session over a fixed skill list, and
/// records what it was asked for.
/// </summary>
/// <remarks>
/// Most assistant tests do not care about skills and want the empty session a surface with no skill
/// files produces; a mock would hand them a null <see cref="Task"/> instead. The recorded arguments
/// are there for the few tests that do care which directory and registry a factory passes in.
/// </remarks>
public sealed class StubSkillSessionFactory : ISkillSessionFactory
{
    private readonly IReadOnlyList<AgentSkill> _skills;

    /// <summary>Creates a stub over <paramref name="skills"/> (none, by default).</summary>
    public StubSkillSessionFactory(params AgentSkill[] skills)
    {
        _skills = skills;
    }

    /// <summary>The directory of the most recent call.</summary>
    public string? LastDirectory { get; private set; }

    /// <summary>The registry of the most recent call.</summary>
    public IToolRegistry? LastRegistry { get; private set; }

    /// <summary>The pre-activated keys of the most recent call.</summary>
    public IReadOnlyList<string> LastPreActivatedKeys { get; private set; } = Array.Empty<string>();

    /// <inheritdoc />
    public Task<SkillSession> CreateAsync(
        string directory,
        IToolRegistry? registry,
        IEnumerable<string>? preActivatedKeys = null,
        CancellationToken cancellationToken = default)
    {
        LastDirectory = directory;
        LastRegistry = registry;
        LastPreActivatedKeys = preActivatedKeys?.ToList() ?? (IReadOnlyList<string>)Array.Empty<string>();

        return Task.FromResult(new SkillSession(_skills, LastPreActivatedKeys));
    }
}
