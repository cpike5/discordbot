using System.Text.Json;
using cpike.Agent;
using cpike.Agent.Prompting;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Core.Models.Llm;
using DiscordBot.Infrastructure.Abstractions.LLM;
using DiscordBot.Infrastructure.Services.LLM;
using DiscordBot.Tests.TestHelpers;
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
/// The load-bearing property is what gets counted. A report that measured every registered tool would
/// be easy to write and would be wrong: it would bill a guild for tools it has turned off and for
/// tools a skill is holding back, neither of which is in the request. So most of what is asserted
/// here is the gap between registered and advertised. The modes come from the shipped prompt files
/// through <see cref="CatalogPromptStore"/>, and the tools are the shipped tool definitions.
/// </remarks>
public class PromptSurfaceReporterTests
{
    [Fact]
    public async Task ReportAsync_WithNoAssistantConfigured_ReturnsNull()
    {
        // No API key means no prompt store, which is the cheapest honest test for "there is no
        // assistant here" - and the metrics page renders a plain panel rather than failing.
        var reporter = Build(services => { });

        (await reporter.ReportAsync(ToolScopes.Guild)).Should().BeNull();
        (await reporter.ReportAsync(ToolScopes.Dm)).Should().BeNull();
    }

    [Fact]
    public async Task ReportAsync_ForFeatureRequests_ReturnsNull()
    {
        var reporter = Build(ShippedAssistant);

        (await reporter.ReportAsync(ToolScopes.FeatureRequests)).Should().BeNull();
    }

    [Fact]
    public async Task ReportAsync_ForTheGuildHouseSet_AdvertisesEveryGuildTool()
    {
        var reporter = Build(ShippedAssistant);

        var report = await reporter.ReportAsync(ToolScopes.Guild);

        var guildTools = GuildToolNames();
        report.Should().NotBeNull();
        report!.Advertised.Tools.Select(t => t.Name).Should().BeEquivalentTo(guildTools);
        report.Tools.Select(t => t.Name).Should().BeEquivalentTo(guildTools);
        report.Advertised.TotalChars.Should().BeGreaterThan(0);
        report.WithheldChars.Should().Be(0);
    }

    [Fact]
    public async Task ReportAsync_ForAGuild_MarksTheToolsItsAllowListLeavesOutAsNotAdvertised()
    {
        var reporter = Build(ShippedAssistant, allowed: new[] { "get_guild_info" });

        var report = await reporter.ReportAsync(ToolScopes.Guild, guildId: 42);

        report!.GuildId.Should().Be(42);
        report.Advertised.Tools.Select(t => t.Name).Should().Equal("get_guild_info");
        report.RegisteredCount.Should().Be(GuildToolNames().Count);

        var roles = report.Tools.Single(t => t.Name == "get_user_roles");
        roles.Advertised.Should().BeFalse();
        roles.BehindSkill.Should().BeFalse();
        roles.Share.Should().Be(0);
        report.WithheldChars.Should().Be(report.Tools.Where(t => t.Name != "get_guild_info").Sum(t => t.SchemaChars));
    }

    [Fact]
    public async Task ReportAsync_WithTheGuildToolsSwitchedOff_AdvertisesNothing()
    {
        var reporter = Build(ShippedAssistant, configureAssistant: o => o.Tools.EnableDocumentationTools = false);

        var report = await reporter.ReportAsync(ToolScopes.Guild, guildId: 42);

        report!.AdvertisedCount.Should().Be(0);
        report.Advertised.Should().Be(PromptSurfaceMeasurement.Empty);
    }

    [Fact]
    public async Task ReportAsync_ForTheDmSurface_ReportsTheSkillToolsAsBehindTheirSkill()
    {
        // The whole reason this does not measure every registered tool: the moderation tools are
        // registered, callable once the skill loads, and absent from the prefix until then. Code
        // execution is on so that the skill tools are the only ones withheld.
        var reporter = Build(ShippedAssistant, configureDm: o => o.EnableCodeExecution = true);

        var report = await reporter.ReportAsync(ToolScopes.Dm);

        var moderation = report!.Tools.Single(t => t.Name == "get_moderation_cases");
        moderation.Advertised.Should().BeFalse();
        moderation.BehindSkill.Should().BeTrue();
        moderation.SkillKeys.Should().Equal("moderation");

        report.Tools.Single(t => t.Name == "get_command_analytics").SkillKeys.Should().Equal("analytics");
        report.Tools.Single(t => t.Name == "save_note").Advertised.Should().BeTrue();
        report.Tools.Single(t => t.Name == "load_skill").Advertised.Should().BeTrue();
        report.WithheldChars.Should().Be(report.Tools.Where(t => t.BehindSkill).Sum(t => t.SchemaChars));
    }

    [Fact]
    public async Task ReportAsync_ForTheDmSurface_WithCodeExecutionOff_DoesNotAdvertiseExecutePython()
    {
        var reporter = Build(ShippedAssistant, configureDm: o => o.EnableCodeExecution = false);

        var report = await reporter.ReportAsync(ToolScopes.Dm);

        var python = report!.Tools.Single(t => t.Name == "execute_python");
        python.Advertised.Should().BeFalse();
        python.BehindSkill.Should().BeFalse();
        report.Advertised.Tools.Select(t => t.Name).Should().NotContain("execute_python");
    }

    [Fact]
    public async Task ReportAsync_ForTheDmSurface_WithCodeExecutionOn_AdvertisesExecutePython()
    {
        var reporter = Build(ShippedAssistant, configureDm: o => o.EnableCodeExecution = true);

        var report = await reporter.ReportAsync(ToolScopes.Dm);

        report!.Advertised.Tools.Select(t => t.Name).Should().Contain("execute_python");
    }

    [Fact]
    public async Task ReportAsync_SharesOfTheAdvertisedArray_SumToUnderOne()
    {
        // The array's brackets and commas belong to no tool, so the shares sum to just under 1.
        var reporter = Build(ShippedAssistant);

        var report = await reporter.ReportAsync(ToolScopes.Guild);

        report!.Advertised.Tools.Sum(t => t.Share).Should().BeLessThan(1).And.BeGreaterThan(0.9);
        report.Advertised.EstimatedTokens.Should().Be(report.Advertised.TotalChars / 4);
    }

    [Fact]
    public async Task ReportAsync_GuildSurface_DoesNotIncludeDmTools()
    {
        var reporter = Build(ShippedAssistant);

        var report = await reporter.ReportAsync(ToolScopes.Guild);

        report!.Tools.Select(t => t.Name).Should().NotContain(new[] { "save_note", "load_skill", "get_moderation_cases" });
    }

    private static List<string> GuildToolNames() =>
        ToolCatalog.ForScope(ToolScopes.Guild).Select(e => e.Name).Where(n => n != "load_skill").ToList();

    /// <summary>The shipped prompt files and tool definitions, plus a stand-in for the framework's loader.</summary>
    private static void ShippedAssistant(IServiceCollection services)
    {
        var skills = PromptFiles.SkillStore();
        services.AddSingleton<IAgentSkillStore>(skills);
        services.AddSingleton<IAgentPromptStore>(PromptFiles.CatalogStore(skills));

        foreach (var tool in RegisteredAgentTools.AgentTools)
        {
            services.AddSingleton(tool);
        }

        services.AddSingleton<IAgentTool>(new NamedTool("load_skill"));
    }

    private static IPromptSurfaceReporter Build(
        Action<IServiceCollection> configure,
        IEnumerable<string>? allowed = null,
        Action<AssistantOptions>? configureAssistant = null,
        Action<DmAssistantOptions>? configureDm = null)
    {
        var services = new ServiceCollection();
        configure(services);
        var provider = services.BuildServiceProvider();

        var access = new Mock<IToolAccessResolver>();
        access.Setup(a => a.ResolveAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((allowed ?? GuildToolNames()).ToHashSet(StringComparer.OrdinalIgnoreCase));

        var assistant = new AssistantOptions();
        configureAssistant?.Invoke(assistant);
        var dm = new DmAssistantOptions();
        configureDm?.Invoke(dm);

        return new PromptSurfaceReporter(
            provider,
            access.Object,
            Options.Create(assistant),
            Options.Create(dm),
            NullLogger<PromptSurfaceReporter>.Instance);
    }

    private sealed class NamedTool(string name) : IAgentTool
    {
        public AgentToolDefinition Definition { get; } = new()
        {
            Name = name,
            Description = $"The {name} tool.",
            InputSchema = new AgentInputSchema
            {
                Type = JsonSerializer.SerializeToElement("object"),
                Properties = new Dictionary<string, JsonElement>(),
                Required = []
            }
        };

        public Task<string> InvokeAsync(IReadOnlyDictionary<string, JsonElement> input, CancellationToken cancellationToken) =>
            Task.FromResult("{}");
    }
}
