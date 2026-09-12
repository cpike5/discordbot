using DiscordBot.Agents.Abstractions;
using DiscordBot.Agents.Contracts;
using DiscordBot.Infrastructure.Services.LLM;
using DiscordBot.Infrastructure.Services.LLM.Tools;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DiscordBot.Tests.Services.LLM;

/// <summary>
/// Unit tests for <see cref="SkillSessionFactory"/>: a surface's skill files, narrowed to the tools
/// that surface actually advertises.
/// </summary>
/// <remarks>
/// The narrowing is the security-relevant half. A skill file <em>names</em> tools; whether the run
/// may use them is the registry's answer, and the guild surface's registry is a
/// <c>FilteredToolRegistry</c> carrying the per-guild allow-list. Narrowing here is what keeps the
/// roster, the loader's answer and the advertised tool array agreeing, and what makes loading a
/// skill unable to reach past that allow-list.
/// </remarks>
public class SkillSessionFactoryTests
{
    private static AgentSkill Skill(string key, params string[] tools) => new()
    {
        Key = key,
        Summary = $"When the request is about {key}.",
        Instructions = $"Instructions for {key}.",
        Tools = tools
    };

    private static SkillSessionFactory BuildFactory(params AgentSkill[] skills)
    {
        var library = new Mock<ISkillLibrary>();
        library
            .Setup(l => l.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(skills);

        return new SkillSessionFactory(library.Object, NullLogger<SkillSessionFactory>.Instance);
    }

    private static IToolRegistry Registry(params string[] advertisedTools)
    {
        var registry = new Mock<IToolRegistry>();
        registry
            .Setup(r => r.GetEnabledTools())
            .Returns(advertisedTools.Select(name => new LlmToolDefinition { Name = name }).ToList());

        return registry.Object;
    }

    [Fact]
    public async Task CreateAsync_DropsToolsTheRegistryDoesNotAdvertise()
    {
        var factory = BuildFactory(Skill("moderation", "get_cases", "ban_user"));

        var session = await factory.CreateAsync("skills/guild", Registry("get_cases", "get_user"));

        session.Available.Should().ContainSingle();
        session.Available[0].Tools.Should().Equal("get_cases");
    }

    [Fact]
    public async Task CreateAsync_WithNullRegistry_KeepsSkillsButNarrowsEveryToolListToEmpty()
    {
        var factory = BuildFactory(Skill("moderation", "get_cases"), Skill("weather", "get_forecast"));

        var session = await factory.CreateAsync("skills/guild", registry: null);

        session.Available.Select(s => s.Key).Should().Equal("moderation", "weather");
        session.Available.Should().OnlyContain(s => s.Tools.Count == 0,
            "a run with no tools still gets the skills' instructions");
    }

    [Fact]
    public async Task CreateAsync_WithToollessSkill_LeavesItUntouched()
    {
        var standingOrders = Skill("standing-orders");
        var factory = BuildFactory(standingOrders);

        var session = await factory.CreateAsync("skills/dm", Registry("get_cases"));

        session.Available.Should().ContainSingle().Which.Should().BeSameAs(standingOrders);
    }

    [Fact]
    public async Task CreateAsync_ActivatesPreActivatedKeys()
    {
        var factory = BuildFactory(Skill("moderation", "get_cases"), Skill("weather"));

        var session = await factory.CreateAsync(
            "skills/dm", Registry("get_cases"), preActivatedKeys: new[] { "weather" });

        session.Activated.Select(s => s.Key).Should().Equal("weather");
        session.IsActivated("moderation").Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_WithEmptyLibrary_ReturnsSessionWithNoSkills()
    {
        var factory = BuildFactory();

        var session = await factory.CreateAsync("skills/none", Registry("get_cases"));

        session.Available.Should().BeEmpty();
        session.Activated.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_SetsLoaderToolNameFromLoadSkillTool()
    {
        var factory = BuildFactory(Skill("weather"));

        var session = await factory.CreateAsync("skills/dm", Registry());

        // The loop drops the loader from the advertised array when a surface has no skills, so the
        // two names disagreeing would mean paying for a schema nothing can use.
        session.LoaderToolName.Should().Be(LoadSkillTool.ToolName);
    }

    [Fact]
    public async Task CreateAsync_CannotSurfaceAToolOutsideTheRegistrysAllowList()
    {
        // The guild registry is an allow-list (FilteredToolRegistry); a skill naming a tool outside
        // it must not be able to advertise that tool by being loaded.
        var factory = BuildFactory(Skill("moderation", "get_cases", "ban_user", "purge_messages"));

        var session = await factory.CreateAsync("skills/guild", Registry("get_cases"));

        var skill = session.Activate("moderation");

        skill!.Tools.Should().Equal("get_cases");
        session.Available.SelectMany(s => s.Tools).Should().NotContain(new[] { "ban_user", "purge_messages" });
    }
}
