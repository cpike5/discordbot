using cpike.Agent.Prompting;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Models.Llm;

namespace DiscordBot.Infrastructure.Services.LLM;

/// <summary>
/// The assistant's prompt store: the framework's markdown store (<c>docs/agents/prompts/</c>, copied to
/// <c>Prompts/</c> in the build output) with each surface mode's tool list taken from
/// <see cref="ToolCatalog"/> rather than from the file.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ToolCatalog"/> is the one place that says which tool belongs to which assistant; the
/// settings page, the per-guild allow-list and the tool contract tests all read it. A <c>tools:</c>
/// line in a mode file would be a second copy that drifts, and a blank one means "every registered
/// tool", which would put DM tools in front of a guild. So a surface mode's file carries no
/// <c>tools:</c> line and this store fills it in: every tool the catalogue scopes to that surface,
/// minus the tools only one of the mode's skills brings (those stay out of the prefix until the
/// skill loads), plus <c>load_skill</c> only when the mode has skills to load.
/// </para>
/// <para>
/// It also renders <c>{{BASE_URL}}</c>, which is fixed per deployment and so safe to put in the
/// cached system prompt. Anything per-turn goes in a turn-context part instead.
/// </para>
/// </remarks>
public sealed class CatalogPromptStore : IAgentPromptStore
{
    /// <summary>The surface modes and the catalogue scope each takes its tools from.</summary>
    public static readonly IReadOnlyDictionary<string, ToolScopes> SurfaceModes =
        new Dictionary<string, ToolScopes>(StringComparer.OrdinalIgnoreCase)
        {
            [GuildAssistantContext.ModeName] = ToolScopes.Guild,
            [DmAssistantContext.ModeName] = ToolScopes.Dm,
            [FeatureRequestModeName] = ToolScopes.FeatureRequests,
        };

    /// <summary>The framework mode (and prompt file) the feature-request agent runs in.</summary>
    public const string FeatureRequestModeName = "feature-requests";

    private const string LoadSkillToolName = "load_skill";

    private readonly IAgentPromptStore _inner;
    private readonly IAgentSkillStore _skills;
    private readonly string _baseUrl;

    /// <summary>Creates the store.</summary>
    /// <param name="inner">The markdown store the prompt files load through.</param>
    /// <param name="skills">The skill store, to leave skill-only tools out of a mode's list.</param>
    /// <param name="baseUrl">The portal's base URL, rendered into <c>{{BASE_URL}}</c>.</param>
    public CatalogPromptStore(IAgentPromptStore inner, IAgentSkillStore skills, string? baseUrl)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
        _baseUrl = (baseUrl ?? string.Empty).TrimEnd('/');
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> Keys => _inner.Keys;

    /// <inheritdoc />
    public AgentDefinition GetDefault() => Shape(_inner.GetDefault());

    /// <inheritdoc />
    public AgentDefinition Get(string key) => Shape(_inner.Get(key));

    /// <inheritdoc />
    public bool TryGet(string key, out AgentDefinition definition)
    {
        if (_inner.TryGet(key, out var raw))
        {
            definition = Shape(raw);
            return true;
        }

        definition = null!;
        return false;
    }

    private AgentDefinition Shape(AgentDefinition definition)
    {
        var shaped = definition with { Template = definition.Template.Replace("{{BASE_URL}}", _baseUrl, StringComparison.Ordinal) };

        if (!SurfaceModes.TryGetValue(definition.Key, out var scope))
        {
            return shaped;
        }

        var skillOnly = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in definition.Skills)
        {
            if (_skills.TryGet(key, out var skill))
            {
                skillOnly.UnionWith(skill.Tools);
            }
        }

        var tools = ToolCatalog.ForScope(scope)
            .Select(entry => entry.Name)
            .Where(name => !skillOnly.Contains(name))
            .Where(name => definition.Skills.Count > 0 || !string.Equals(name, LoadSkillToolName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (definition.Skills.Count > 0 && !tools.Contains(LoadSkillToolName, StringComparer.OrdinalIgnoreCase))
        {
            tools.Add(LoadSkillToolName);
        }

        return shaped with { Tools = tools };
    }
}
