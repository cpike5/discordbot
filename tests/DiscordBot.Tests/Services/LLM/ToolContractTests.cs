using System.Text.Json;
using System.Text.RegularExpressions;
using cpike.Agent;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Models.Llm;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;

namespace DiscordBot.Tests.Services.LLM;

/// <summary>
/// The house rules for a tool, asserted over every tool this application registers.
/// </summary>
/// <remarks>
/// <para>
/// Every one of these is a mistake that currently surfaces as "the model behaves oddly in
/// production" — a schema the model cannot fill in, a description too thin to choose by, a tool
/// missing from the catalogue and therefore advertised nowhere — rather than as a red build. This is
/// the file that makes them red builds.
/// </para>
/// <para>
/// It runs over the <em>application's</em> tools, not the engine's, because these are house
/// conventions. cpike.Agent has no opinion about how long a description should be; this
/// repository does.
/// </para>
/// </remarks>
public class ToolContractTests
{
    /// <summary>
    /// Lower-case, underscore-separated, three characters or more. It is the wire name and cannot be
    /// changed without a prompt-cache write, so the shape is worth settling once.
    /// </summary>
    private static readonly Regex NameShape = new("^[a-z][a-z0-9_]{2,63}$", RegexOptions.Compiled);

    /// <summary>
    /// Roughly 50 to 120 words. The floor catches the one-liner the model cannot choose by; the
    /// ceiling catches the essay, which is paid for on every request whether or not it is needed.
    /// </summary>
    private const int MinimumDescription = 40;

    /// <inheritdoc cref="MinimumDescription" />
    private const int MaximumDescription = 600;

    public static TheoryData<string> ToolNames()
    {
        var data = new TheoryData<string>();

        foreach (var name in RegisteredAgentTools.All.Select(t => t.Name).Distinct(StringComparer.Ordinal))
        {
            data.Add(name);
        }

        return data;
    }

    /// <summary>
    /// The catalogued tools that always change data. Named rather than inferred: inferring them from
    /// <c>RequiresEdit</c> would make the rule below assert that a flag is set wherever it is set.
    /// </summary>
    public static TheoryData<string> MutatingToolNames() =>
        new() { "save_note", "delete_note", "clear_conversation", "submit_feature_request" };

    /// <summary>Tools that require at least one argument.</summary>
    public static TheoryData<string> ToolNamesWithRequiredArguments()
    {
        var data = new TheoryData<string>();

        foreach (var tool in RegisteredAgentTools.AgentTools.Where(t => Required(t.Definition).Any()))
        {
            data.Add(tool.Definition.Name);
        }

        return data;
    }

    [Fact]
    public void EveryToolNameIsUniqueAcrossEverySurface()
    {
        // Two tools answering to one name means which one runs depends on registration order. Loud here rather than mysterious in production.
        var duplicates = RegisteredAgentTools.All
            .GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key}: {string.Join(", ", g.Select(t => t.DeclaringType.Name))}")
            .ToList();

        duplicates.Should().BeEmpty("a tool name must identify exactly one implementation");
    }

    [Fact]
    public void TheScanFindsTheToolsItIsSupposedTo()
    {
        // A guard on the guard: if reflection stopped finding anything, every theory below would pass
        // vacuously and the file would be worse than useless.
        RegisteredAgentTools.All.Should().HaveCountGreaterThan(20);
    }

    [Fact]
    public void EveryCatalogueEntryHasAnImplementation()
    {
        // The other direction of the catalogue rule. An entry with nothing behind it puts a tick box
        // on the settings page for a tool that cannot be called, and a row on the metrics page that
        // can only ever read zero.
        var implemented = RegisteredAgentTools.All
            .Select(t => t.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // load_skill is the framework's built-in: catalogued so the portal can list it, implemented
        // by cpike.Agent rather than by this repository.
        ToolCatalog.All.Select(e => e.Name)
            .Where(n => !FrameworkBuiltIns.Contains(n))
            .Where(n => !implemented.Contains(n))
            .Should().BeEmpty("a catalogued tool that nothing implements is a promise the portal cannot keep");
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public void Name_IsLowerCaseAndUnderscored(string name)
    {
        NameShape.IsMatch(name).Should().BeTrue(
            $"'{name}' must match {NameShape} - it is the name the model calls and the key the "
            + "catalogue, the allow-list and the metrics table all join on");
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public void Description_IsLongEnoughToChooseByAndShortEnoughToPayFor(string name)
    {
        var description = Find(name).Definition.Description;

        description.Should().NotBeNullOrWhiteSpace();
        description!.Length.Should().BeInRange(MinimumDescription, MaximumDescription,
            $"{name}'s description is what the model decides by, and it is serialized into every "
            + "request whether or not the question needs the tool");
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public void InputSchema_IsAnObjectSchema(string name)
    {
        var schema = Find(name).Definition.InputSchema;

        schema.Should().NotBeNull($"{name} must declare a JSON-Schema object");
        schema!.Type.ValueKind.Should().Be(JsonValueKind.String, $"{name}'s schema needs a type");
        schema.Type.GetString().Should().Be("object");
    }

    [Fact]
    public void NoHouseToolTakesAFrameworkBuiltInName()
    {
        // The framework registers load_skill and get_current_time itself; a house tool by either name
        // would be a second registration the loop resolves by order.
        RegisteredAgentTools.All.Select(t => t.Name)
            .Where(n => FrameworkBuiltIns.Contains(n) || n == "get_current_time")
            .Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public void InputSchema_RequiredNamesAllExist(string name)
    {
        var definition = Find(name).Definition;
        var properties = Properties(definition).Select(p => p.Key).ToList();

        foreach (var required in Required(definition))
        {
            properties.Should().Contain(required,
                $"{name} requires '{required}' but does not declare it - the model cannot supply a "
                + "property it was never shown");
        }
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public void InputSchema_EveryPropertyIsDescribed(string name)
    {
        foreach (var property in Properties(Find(name).Definition))
        {
            property.Value.TryGetProperty("description", out var description).Should().BeTrue(
                $"{name}.{property.Key} has no description, so the model is guessing at what to put in it");

            description.GetString().Should().NotBeNullOrWhiteSpace();
        }
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public void Catalogue_HasAnEntry(string name)
    {
        // Not a tidiness rule: the catalogue is what routes a tool to a surface. An uncatalogued tool
        // has ToolScopes.None, reaches no assistant, and is only discovered by its absence.
        ToolCatalog.IsCatalogued(name).Should().BeTrue(
            $"{name} needs an entry in Core/Models/Llm/ToolCatalog.cs or it is advertised nowhere");
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public void Catalogue_DescribesTheToolForAHuman(string name)
    {
        var entry = ToolCatalog.Describe(name);

        entry.Category.Should().NotBe(ToolCatalog.OtherCategory);
        entry.DisplayName.Should().NotBeNullOrWhiteSpace().And.NotBe(name,
            $"{name}'s catalogue label is read by an admin choosing whether to allow it");
        entry.Description.Should().NotBeNullOrWhiteSpace();
        entry.Scopes.Should().NotBe(ToolScopes.None);
    }

    [Theory]
    [MemberData(nameof(MutatingToolNames))]
    public void Mutation_IsDeclaredSoTheLoopRefusesItBeforeTheToolIsEntered(string name)
    {
        // RequiresEdit is the whole of the guard: the framework refuses the call for a caller without
        // edit access before the tool body runs. A tool that changes data without it is reachable by
        // anyone the assistant answers.
        ToolCatalog.IsCatalogued(name).Should().BeTrue($"{name} is listed here as a catalogued tool");
        Find(name).Definition.RequiresEdit.Should().BeTrue(
            $"{name} changes data, so its definition must set RequiresEdit");
    }

    [Theory]
    [MemberData(nameof(ToolNamesWithRequiredArguments))]
    public async Task MissingArguments_ReachTheModelAsACountableFailure(string name)
    {
        // The tool is built with mocked dependencies that answer nothing, so a missing argument has to
        // be caught by the tool itself rather than by a service rejecting it. And the answer goes back
        // through ToolResults, so AgentService.FailureCodeOf counts it - an expected failure reported
        // any other way is invisible in the traces, and expected failures are the majority of what is
        // worth seeing there.
        var tool = RegisteredAgentTools.Construct(Find(name).DeclaringType);

        var result = await tool.InvokeAsync(new Dictionary<string, JsonElement>(), CancellationToken.None);

        AgentService.FailureCodeOf(result).Should().NotBeNull(
            $"{name}'s missing-argument answer must carry a top-level error or a false found/success "
            + "flag - ToolResults.Error and ToolResults.NotFound both do - so the framework counts it");
    }

    private static RegisteredTool Find(string name) =>
        RegisteredAgentTools.All.First(t => string.Equals(t.Name, name, StringComparison.Ordinal));

    private static readonly HashSet<string> FrameworkBuiltIns = new(StringComparer.OrdinalIgnoreCase) { "load_skill" };

    private static IEnumerable<KeyValuePair<string, JsonElement>> Properties(AgentToolDefinition definition) =>
        definition.InputSchema?.Properties ?? Enumerable.Empty<KeyValuePair<string, JsonElement>>();

    private static IEnumerable<string> Required(AgentToolDefinition definition) =>
        definition.InputSchema?.Required ?? Enumerable.Empty<string>();
}
