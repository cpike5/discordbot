using System.Text.Json;
using DiscordBot.Agents;
using DiscordBot.Agents.Abstractions;
using DiscordBot.Agents.Contracts;
using FluentAssertions;
using Moq;

namespace DiscordBot.Tests.Agents;

/// <summary>
/// Unit tests for the rule that decides which tools a run advertises: a tool named by any available
/// skill is hidden until one of the skills naming it is loaded.
/// </summary>
/// <remarks>
/// Two properties here are load-bearing rather than incidental. The set is always computed from the
/// registry, so a skill can never surface a tool the registry does not hold — that is what makes
/// skills safe on a surface with a per-guild allow-list. And the result is sorted by name, because
/// the tool array serializes at position 0 of the request and its order is part of every prompt-cache
/// breakpoint behind it.
/// </remarks>
public class SkillToolSetTests
{
    private static readonly JsonElement EmptySchema = JsonDocument.Parse("{}").RootElement.Clone();

    [Fact]
    public void Compose_WithNoRegistry_ReturnsNull()
    {
        SkillToolSet.Compose(null, new SkillSession(Array.Empty<AgentSkill>())).Should().BeNull();
    }

    [Fact]
    public void Compose_WithNoSession_ReturnsTheRegistrySetUnchanged()
    {
        var registry = Registry("alpha", "beta", "load_skill");

        var tools = SkillToolSet.Compose(registry, null);

        Names(tools).Should().Equal("alpha", "beta", "load_skill");
    }

    [Fact]
    public void Compose_WithNoSkillsAvailable_DropsTheLoaderTool()
    {
        // A surface with no skill files should cost exactly what it did before skills existed, and
        // the loader's schema is paid for on every request of the run.
        var registry = Registry("alpha", "load_skill");

        var tools = SkillToolSet.Compose(registry, new SkillSession(Array.Empty<AgentSkill>()));

        Names(tools).Should().Equal("alpha");
    }

    [Fact]
    public void Compose_WithAnUnloadedSkill_HidesItsToolsAndKeepsTheLoader()
    {
        var registry = Registry("alpha", "get_cases", "get_history", "load_skill");
        var session = new SkillSession(new[] { Skill("moderation", "get_cases", "get_history") });

        var tools = SkillToolSet.Compose(registry, session);

        Names(tools).Should().Equal("alpha", "load_skill");
    }

    [Fact]
    public void Compose_AfterLoading_AdvertisesThatSkillsToolsAndNoOthers()
    {
        var registry = Registry("alpha", "get_cases", "get_stats", "load_skill");
        var session = new SkillSession(new[]
        {
            Skill("moderation", "get_cases"),
            Skill("analytics", "get_stats"),
        });

        session.Activate("moderation");
        var tools = SkillToolSet.Compose(registry, session);

        Names(tools).Should().Equal("alpha", "get_cases", "load_skill");
    }

    [Fact]
    public void Compose_WithAPreActivatedSkill_AdvertisesItsToolsFromTheStart()
    {
        var registry = Registry("get_cases", "load_skill");
        var session = new SkillSession(new[] { Skill("moderation", "get_cases") }, new[] { "moderation" });

        Names(SkillToolSet.Compose(registry, session)).Should().Equal("get_cases", "load_skill");
    }

    [Fact]
    public void Compose_WithATwoSkillsSharingATool_UnhidesItWhenEitherLoads()
    {
        var registry = Registry("shared", "load_skill");
        var session = new SkillSession(new[]
        {
            Skill("first", "shared"),
            Skill("second", "shared"),
        });

        session.Activate("second");

        Names(SkillToolSet.Compose(registry, session)).Should().Contain("shared");
    }

    [Fact]
    public void Compose_WithASkillNamingAToolTheRegistryDoesNotHave_CannotSurfaceIt()
    {
        // The guarantee the guild allow-list rests on: the set is built from the registry, so a
        // skill's tool list is a request and never a grant.
        var registry = Registry("alpha", "load_skill");
        var session = new SkillSession(new[] { Skill("moderation", "get_cases") });

        session.Activate("moderation");

        Names(SkillToolSet.Compose(registry, session)).Should().Equal("alpha", "load_skill");
    }

    [Fact]
    public void Compose_WithANameCasedDifferently_StillMatches()
    {
        var registry = Registry("Get_Cases", "load_skill");
        var session = new SkillSession(new[] { Skill("moderation", "get_cases") });

        Names(SkillToolSet.Compose(registry, session)).Should().Equal("load_skill");

        session.Activate("moderation");

        Names(SkillToolSet.Compose(registry, session)).Should().Equal("Get_Cases", "load_skill");
    }

    [Fact]
    public void Compose_WithARegistryInAnyOrder_ReturnsThemSortedByName()
    {
        var registry = Registry("zulu", "alpha", "mike");

        Names(SkillToolSet.Compose(registry, new SkillSession(Array.Empty<AgentSkill>())))
            .Should().Equal("alpha", "mike", "zulu");
    }

    [Fact]
    public void Compose_WithACustomLoaderName_DropsThatNameInstead()
    {
        var registry = Registry("load_skill", "open_skill");

        var tools = SkillToolSet.Compose(
            registry,
            new SkillSession(Array.Empty<AgentSkill>(), preActivated: null, loaderToolName: "open_skill"));

        Names(tools).Should().Equal("load_skill");
    }

    private static AgentSkill Skill(string key, params string[] tools) => new()
    {
        Key = key,
        Summary = $"The {key} skill.",
        Tools = tools,
        Instructions = $"How to use {key}."
    };

    private static IToolRegistry Registry(params string[] toolNames)
    {
        var mock = new Mock<IToolRegistry>();
        mock.Setup(r => r.GetEnabledTools()).Returns(toolNames
            .Select(n => new LlmToolDefinition { Name = n, Description = n, InputSchema = EmptySchema })
            .ToList());
        return mock.Object;
    }

    private static IEnumerable<string> Names(IEnumerable<LlmToolDefinition>? tools) =>
        tools?.Select(t => t.Name) ?? Enumerable.Empty<string>();
}
