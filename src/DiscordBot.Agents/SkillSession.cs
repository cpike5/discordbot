using DiscordBot.Agents.Abstractions;
using DiscordBot.Agents.Contracts;

namespace DiscordBot.Agents;

/// <summary>
/// The default <see cref="ISkillActivationState"/>: one run's skills, and which of them it has
/// loaded.
/// </summary>
/// <remarks>
/// Construct one per run. It is deliberately a plain object rather than a service: its lifetime is
/// the run's, and the host hands the same instance to the loop (through
/// <see cref="AgentContext.Skills"/>) and to whatever it uses to reach its loader tool.
/// </remarks>
public sealed class SkillSession : ISkillActivationState
{
    /// <summary>The conventional name of the loader tool.</summary>
    public const string DefaultLoaderToolName = "load_skill";

    private readonly object _lock = new();
    private readonly Dictionary<string, AgentSkill> _byKey;
    private readonly List<AgentSkill> _available;
    private readonly List<AgentSkill> _activated = new();

    /// <summary>
    /// Activation is tracked by key, not by skill. <see cref="AgentSkill"/> is a record whose
    /// <see cref="AgentSkill.Tools"/> is a collection, and a record compares collections by
    /// reference — so two value-identical skills built from separate lists are unequal, which is
    /// not what "is this skill loaded?" means.
    /// </summary>
    private readonly HashSet<string> _activatedKeys = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a session over <paramref name="available"/>.
    /// </summary>
    /// <param name="available">
    /// The skills this run may load. Already narrowed by the host: each skill's
    /// <see cref="AgentSkill.Tools"/> should name only tools the host is willing to advertise, so
    /// the roster and the loader's answer say what the model will actually get.
    /// </param>
    /// <param name="preActivated">
    /// Keys to treat as already loaded — a multi-turn host replays the previous turn's activations
    /// here, which is what makes a skill nearly free from the second turn onward. Unknown keys are
    /// ignored.
    /// </param>
    /// <param name="loaderToolName">The name of the tool that loads a skill.</param>
    public SkillSession(
        IEnumerable<AgentSkill> available,
        IEnumerable<string>? preActivated = null,
        string loaderToolName = DefaultLoaderToolName)
    {
        ArgumentNullException.ThrowIfNull(available);
        ArgumentException.ThrowIfNullOrWhiteSpace(loaderToolName);

        LoaderToolName = loaderToolName;

        // Ordered by key, and de-duplicated first-wins: the roster is prompt surface, so its order
        // has to be the same on every process or the cached prefix behind it is thrown away.
        _byKey = new Dictionary<string, AgentSkill>(StringComparer.OrdinalIgnoreCase);
        foreach (var skill in available)
        {
            // Keys are trimmed on the way in, because lookups are: a key that only matches itself
            // with its whitespace attached is a skill nothing can load.
            if (skill is { Key: not null } && !string.IsNullOrWhiteSpace(skill.Key))
            {
                var key = skill.Key.Trim();
                _byKey.TryAdd(key, key == skill.Key ? skill : skill with { Key = key });
            }
        }

        _available = _byKey.Values.OrderBy(s => s.Key, StringComparer.Ordinal).ToList();

        foreach (var key in preActivated ?? Enumerable.Empty<string>())
        {
            Activate(key);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<AgentSkill> Available => _available;

    /// <inheritdoc />
    public IReadOnlyList<AgentSkill> Activated
    {
        get
        {
            lock (_lock)
            {
                return _activated.ToList();
            }
        }
    }

    /// <inheritdoc />
    public string LoaderToolName { get; }

    /// <inheritdoc />
    public AgentSkill? Find(string key) =>
        !string.IsNullOrWhiteSpace(key) && _byKey.TryGetValue(key.Trim(), out var skill) ? skill : null;

    /// <inheritdoc />
    public bool IsActivated(string key)
    {
        if (Find(key) is not { } skill)
        {
            return false;
        }

        lock (_lock)
        {
            return _activatedKeys.Contains(skill.Key);
        }
    }

    /// <inheritdoc />
    public AgentSkill? Activate(string key)
    {
        var skill = Find(key);
        if (skill is null)
        {
            return null;
        }

        lock (_lock)
        {
            if (_activatedKeys.Add(skill.Key))
            {
                _activated.Add(skill);
            }
        }

        return skill;
    }
}
