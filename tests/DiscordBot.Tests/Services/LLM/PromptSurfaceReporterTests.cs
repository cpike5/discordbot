using System.Text.Json;
using DiscordBot.Agents;
using DiscordBot.Agents.Abstractions;
using DiscordBot.Agents.Contracts;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Infrastructure.Abstractions.LLM;
using DiscordBot.Infrastructure.Services.LLM;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Services.LLM;

/// <summary>
/// Unit tests for the prompt-surface report.
/// </summary>
/// <remarks>
/// The load-bearing property is what gets counted. A report that measured
/// <c>IToolRegistry.GetEnabledTools()</c> would be easy to write and would be wrong: it would bill a
/// guild for tools it has turned off and for tools a skill is holding back, neither of which is in
/// the request. So most of what is asserted here is the gap between registered and advertised.
/// </remarks>
public class PromptSurfaceReporterTests
{
    private const string SkillsDirectory = "docs/agents/skills/test";

    private static readonly JsonElement Schema = JsonDocument.Parse(
        """{"type":"object","properties":{"id":{"type":"string","description":"An id."}}}""")
        .RootElement.Clone();

    [Fact]
    public async Task ReportAsync_WithNoAssistantConfigured_ReturnsNull()
    {
        // No API key means no ISkillSessionFactory, which is the cheapest honest test for "there is
        // no assistant here" - and the metrics page renders a plain panel rather than failing.
        var reporter = Build(services => { });

        (await reporter.ReportAsync(ToolScopes.Guild)).Should().BeNull();
        (await reporter.ReportAsync(ToolScopes.Dm)).Should().BeNull();
    }

    [Fact]
    public async Task ReportAsync_ForFeatureRequests_ReturnsNull()
    {
        var reporter = Build(services =>
        {
            services.AddSingleton(SkillFactory());
            services.AddSingleton(Registry("alpha"));
        });

        (await reporter.ReportAsync(ToolScopes.FeatureRequests)).Should().BeNull();
    }

    [Fact]
    public async Task ReportAsync_ForTheGuildHouseSet_CountsEveryRegisteredTool()
    {
        var reporter = Build(services =>
        {
            services.AddSingleton(SkillFactory());
            services.AddSingleton(Registry("alpha", "beta"));
        });

        var report = await reporter.ReportAsync(ToolScopes.Guild);

        report.Should().NotBeNull();
        report!.AdvertisedCount.Should().Be(2);
        report.RegisteredCount.Should().Be(2);
        report.Advertised.TotalChars.Should().BeGreaterThan(0);
        report.WithheldChars.Should().Be(0);
    }

    [Fact]
    public async Task ReportAsync_ForAGuild_AppliesThatGuildsAllowList()
    {
        var reporter = Build(
            services =>
            {
                services.AddSingleton(SkillFactory());
                services.AddSingleton(Registry("alpha", "beta"));
            },
            allowed: new[] { "alpha" });

        var report = await reporter.ReportAsync(ToolScopes.Guild, guildId: 42);

        report!.GuildId.Should().Be(42);
        report.AdvertisedCount.Should().Be(1);
        report.RegisteredCount.Should().Be(2);

        var beta = report.Tools.Single(t => t.Name == "beta");
        beta.Advertised.Should().BeFalse();
        beta.BehindSkill.Should().BeFalse();
        beta.Share.Should().Be(0);
        report.WithheldChars.Should().Be(beta.SchemaChars);
    }

    [Fact]
    public async Task ReportAsync_WithASkill_DoesNotBillForTheToolsItHoldsBack()
    {
        // The whole reason this does not measure GetEnabledTools(): get_cases is registered, callable
        // once the skill loads, and absent from the prefix until then.
        var reporter = Build(services =>
        {
            services.AddSingleton(SkillFactory(Skill("moderation", "get_cases")));
            services.AddSingleton(Registry("alpha", "get_cases", "load_skill"));
        });

        var report = await reporter.ReportAsync(ToolScopes.Guild);

        report!.Tools.Select(t => t.Name).Should().BeEquivalentTo("alpha", "get_cases", "load_skill");
        report.Advertised.Tools.Select(t => t.Name).Should().BeEquivalentTo("alpha", "load_skill");

        var gated = report.Tools.Single(t => t.Name == "get_cases");
        gated.Advertised.Should().BeFalse();
        gated.BehindSkill.Should().BeTrue();
        gated.SkillKeys.Should().Equal("moderation");
        report.WithheldChars.Should().Be(gated.SchemaChars);
    }

    [Fact]
    public async Task ReportAsync_WithNoSkillFiles_DoesNotBillForTheLoaderEither()
    {
        // A surface with an empty skill directory must cost exactly what it did before skills.
        var reporter = Build(services =>
        {
            services.AddSingleton(SkillFactory());
            services.AddSingleton(Registry("alpha", "load_skill"));
        });

        var report = await reporter.ReportAsync(ToolScopes.Guild);

        report!.Advertised.Tools.Select(t => t.Name).Should().Equal("alpha");
        report.Tools.Single(t => t.Name == "load_skill").Advertised.Should().BeFalse();
    }

    [Fact]
    public async Task ReportAsync_ForTheDmSurface_MeasuresTheDmProvidersRatherThanTheGuildRegistry()
    {
        var reporter = Build(
            services =>
            {
                services.AddSingleton(SkillFactory());
                services.AddSingleton(Registry("guild_only"));
            },
            dmTools: new[] { "save_note", "search_notes" });

        var report = await reporter.ReportAsync(ToolScopes.Dm);

        report!.SurfaceName.Should().Be("DM assistant");
        report.Tools.Select(t => t.Name).Should().BeEquivalentTo("save_note", "search_notes");
    }

    [Fact]
    public async Task ReportAsync_ForTheDmSurfaceWithNoProviders_ReturnsNull()
    {
        var reporter = Build(services =>
        {
            services.AddSingleton(SkillFactory());
            services.AddSingleton(Registry("alpha"));
        });

        (await reporter.ReportAsync(ToolScopes.Dm)).Should().BeNull();
    }

    [Fact]
    public async Task ReportAsync_OrdersToolsBySchemaSizeSoTheExpensiveOnesAreFirst()
    {
        var reporter = Build(services =>
        {
            services.AddSingleton(SkillFactory());
            services.AddSingleton(Registry(
                ("small", "Short."),
                ("large", new string('x', 400))));
        });

        var report = await reporter.ReportAsync(ToolScopes.Guild);

        report!.Tools.Select(t => t.Name).Should().Equal("large", "small");
        report.Tools[0].Share.Should().BeGreaterThan(report.Tools[1].Share);
    }

    [Fact]
    public async Task ReportAsync_WithDocumentationToolsDisabled_ReportsTheHouseSetUnnarrowed()
    {
        // The guild context factory only builds a FilteredToolRegistry when the tool gate is on; the
        // report has to make the same decision or it would show an allow-list that is not applied.
        var reporter = Build(
            services =>
            {
                services.AddSingleton(SkillFactory());
                services.AddSingleton(Registry("alpha", "beta"));
            },
            allowed: new[] { "alpha" },
            enableTools: false);

        var report = await reporter.ReportAsync(ToolScopes.Guild, guildId: 42);

        report!.AdvertisedCount.Should().Be(2);
    }

    private static IPromptSurfaceReporter Build(
        Action<IServiceCollection> configure,
        IEnumerable<string>? allowed = null,
        IEnumerable<string>? dmTools = null,
        bool enableTools = true)
    {
        var services = new ServiceCollection();
        configure(services);

        var toolAccess = new Mock<IToolAccessResolver>();
        toolAccess
            .Setup(r => r.ResolveAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(allowed ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase));

        var assistantOptions = new AssistantOptions();
        assistantOptions.Tools.EnableDocumentationTools = enableTools;
        assistantOptions.Tools.SkillsPath = SkillsDirectory;

        var dmOptions = new DmAssistantOptions { SkillsPath = SkillsDirectory };

        if (dmTools is not null)
        {
            services.AddSingleton(DmProvider(dmTools));
        }

        return new PromptSurfaceReporter(
            services.BuildServiceProvider(),
            toolAccess.Object,
            NullLoggerFactory.Instance,
            Options.Create(assistantOptions),
            Options.Create(dmOptions),
            NullLogger<PromptSurfaceReporter>.Instance);
    }

    /// <summary>
    /// The real session factory over a library that returns <paramref name="skills"/>, so the
    /// narrowing this depends on is the production one rather than a stand-in.
    /// </summary>
    private static ISkillSessionFactory SkillFactory(params AgentSkill[] skills)
    {
        var library = new Mock<ISkillLibrary>();
        library
            .Setup(l => l.LoadAsync(SkillsDirectory, It.IsAny<CancellationToken>()))
            .ReturnsAsync(skills);

        return new SkillSessionFactory(library.Object, NullLogger<SkillSessionFactory>.Instance);
    }

    private static AgentSkill Skill(string key, params string[] tools) => new()
    {
        Key = key,
        Summary = $"The {key} skill.",
        Tools = tools,
        Instructions = $"How to use {key}."
    };

    private static IToolRegistry Registry(params string[] toolNames) =>
        Registry(toolNames.Select(n => (n, $"The {n} tool.")).ToArray());

    private static IToolRegistry Registry(params (string Name, string Description)[] tools)
    {
        var registry = new Mock<IToolRegistry>();
        registry.Setup(r => r.GetEnabledTools()).Returns(tools
            .Select(t => new LlmToolDefinition { Name = t.Name, Description = t.Description, InputSchema = Schema })
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList());
        return registry.Object;
    }

    private static IDmToolProvider DmProvider(IEnumerable<string> toolNames)
    {
        var provider = new Mock<IDmToolProvider>();
        provider.SetupGet(p => p.Name).Returns("DmTools");
        provider.SetupGet(p => p.Description).Returns("DM tools");
        provider.Setup(p => p.GetTools()).Returns(toolNames
            .Select(n => new LlmToolDefinition { Name = n, Description = $"The {n} tool.", InputSchema = Schema })
            .ToList());
        return provider.Object;
    }
}
