using System.Text.Json;
using System.Text.RegularExpressions;
using DiscordBot.Agents;
using DiscordBot.Agents.Abstractions;
using DiscordBot.Agents.Contracts;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Models.Llm;
using DiscordBot.Infrastructure.Services.LLM;
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
/// conventions. <c>DiscordBot.Agents</c> has no opinion about how long a description should be; this
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

    /// <summary>Tools authored as <see cref="IAgentTool"/> that declare a write.</summary>
    public static TheoryData<string> MutatingToolNames()
    {
        var data = new TheoryData<string>();

        foreach (var tool in RegisteredAgentTools.AgentTools.Where(t => t.Mutation is not null))
        {
            data.Add(tool.Definition.Name);
        }

        return data;
    }

    /// <summary>Tools authored as <see cref="IAgentTool"/> that require at least one argument.</summary>
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
        // The registry's walk is first-match, so two tools answering to one name means which one runs
        // depends on provider registration order. Loud here rather than mysterious in production.
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
        RegisteredAgentTools.AgentTools.Should().NotBeEmpty();
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

        ToolCatalog.All.Select(e => e.Name).Where(n => !implemented.Contains(n))
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
        description.Length.Should().BeInRange(MinimumDescription, MaximumDescription,
            $"{name}'s description is what the model decides by, and it is serialized into every "
            + "request whether or not the question needs the tool");
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public void InputSchema_IsAnObjectSchema(string name)
    {
        var schema = Find(name).Definition.InputSchema;

        schema.ValueKind.Should().Be(JsonValueKind.Object, $"{name} must declare a JSON-Schema object");
        schema.TryGetProperty("type", out var type).Should().BeTrue($"{name}'s schema needs a type");
        type.GetString().Should().Be("object");
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public void InputSchema_RequiredNamesAllExist(string name)
    {
        var definition = Find(name).Definition;
        var properties = Properties(definition).Select(p => p.Name).ToList();

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
                $"{name}.{property.Name} has no description, so the model is guessing at what to put in it");

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
    public async Task Mutation_IsRefusedBeforeTheToolIsEntered(string name)
    {
        // The Phase 3 rule, asserted through the real adapter rather than restated: declaring
        // Mutation is the whole of the guard, so a tool that declares one must be unreachable to a
        // caller who may not write. An uninitialized instance is the point - if the refusal did not
        // come first, the tool body would throw on its missing repository.
        var tool = RegisteredAgentTools.AgentTools.Single(t => t.Definition.Name == name);

        var provider = new AgentToolProvider(
            "Contract", "Contract test", new[] { tool }, ToolPermissions.MutationForbidden);

        var result = await provider.ExecuteToolAsync(
            name, EmptyInput(), new ToolContext { UserId = 1, CanMutate = false });

        result.Success.Should().BeTrue("a refusal is a decision to relay, not an error to retry around");
        result.Data.Should().NotBeNull();
        result.Data!.Value.TryGetProperty("forbidden", out var forbidden).Should().BeTrue();
        forbidden.GetBoolean().Should().BeTrue();
        result.Data.Value.GetProperty("message").GetString().Should().Contain(tool.Mutation!);
    }

    [Theory]
    [MemberData(nameof(ToolNamesWithRequiredArguments))]
    public async Task MissingArguments_ReachTheModelAsACountableFailure(string name)
    {
        // Two house rules in one call. Arguments are validated before anything else happens, which is
        // what makes the uninitialized instance below safe and what stops a half-run write. And the
        // answer goes back through ToolResults, so ToolOutcomes.Classify counts it - an expected
        // failure reported any other way is invisible in the traces, and expected failures are the
        // majority of what is worth seeing there.
        var tool = RegisteredAgentTools.AgentTools.Single(t => t.Definition.Name == name);

        var result = await tool.InvokeAsync(EmptyInput(), new ToolContext { UserId = 1, CanMutate = true });

        result.Success.Should().BeTrue(
            $"{name} should explain a missing argument to the model rather than flag a malfunction");
        result.Data.Should().NotBeNull();
        ToolOutcomes.Classify(result.Data!.Value).Should().Be(ToolOutcomes.FailedResult,
            $"{name}'s missing-argument answer must carry a top-level error or a false found/success "
            + "flag - ToolResults.Error and ToolResults.NotFound both do");
    }

    private static RegisteredTool Find(string name) =>
        RegisteredAgentTools.All.First(t => string.Equals(t.Name, name, StringComparison.Ordinal));

    private static JsonElement EmptyInput() => JsonDocument.Parse("{}").RootElement.Clone();

    private static IEnumerable<JsonProperty> Properties(LlmToolDefinition definition) =>
        definition.InputSchema.ValueKind == JsonValueKind.Object
        && definition.InputSchema.TryGetProperty("properties", out var properties)
        && properties.ValueKind == JsonValueKind.Object
            ? properties.EnumerateObject()
            : Enumerable.Empty<JsonProperty>();

    private static IEnumerable<string> Required(LlmToolDefinition definition) =>
        definition.InputSchema.ValueKind == JsonValueKind.Object
        && definition.InputSchema.TryGetProperty("required", out var required)
        && required.ValueKind == JsonValueKind.Array
            ? required.EnumerateArray().Where(r => r.ValueKind == JsonValueKind.String).Select(r => r.GetString()!)
            : Enumerable.Empty<string>();
}
