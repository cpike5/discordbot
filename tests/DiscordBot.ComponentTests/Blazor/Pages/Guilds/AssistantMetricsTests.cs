using Bunit;
using Bunit.TestDoubles;
using DiscordBot.Agents;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Bot.Blazor.Pages.Guilds;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.DTOs.Llm.Reporting;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Infrastructure.Abstractions.LLM;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DiscordBot.ComponentTests.Blazor.Pages.Guilds;

/// <summary>
/// Component tests for <see cref="AssistantMetrics"/>, the routable replacement for
/// <c>Pages/Guilds/AssistantMetrics.cshtml</c> + <c>AssistantMetricsModel</c>
/// (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4b): read-only 30-day window - daily
/// table, tool usage, prompt-surface report (including its null branch), and cost-by-user.
/// </summary>
public class AssistantMetricsTests : BlazorComponentTestContext
{
    private const ulong GuildId = 123456789012345678UL;

    private readonly Mock<IGuildContextProvider> _contextProvider = new();
    private readonly Mock<IAssistantService> _assistantService = new();
    private readonly Mock<IAssistantInteractionLogRepository> _interactionLogRepository = new();
    private readonly Mock<ILlmUsageRepository> _usageRepository = new();
    private readonly Mock<IDiscordUserResolver> _userResolver = new();
    private readonly Mock<IPromptSurfaceReporter> _promptSurfaceReporter = new();

    public AssistantMetricsTests()
    {
        Services.AddScoped(_ => _contextProvider.Object);
        Services.AddSingleton(_assistantService.Object);
        Services.AddSingleton(_interactionLogRepository.Object);
        Services.AddSingleton(_usageRepository.Object);
        Services.AddSingleton(_userResolver.Object);
        Services.AddSingleton(_promptSurfaceReporter.Object);

        _assistantService.Setup(s => s.GetUsageMetricsRangeAsync(GuildId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AssistantUsageMetrics>());
        _interactionLogRepository.Setup(r => r.GetToolUsageAsync(GuildId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AssistantToolUsage>());
        _usageRepository.Setup(r => r.GetByUserAsync(It.IsAny<LlmUsageQuery>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<LlmUsageByUser>());
        _userResolver.Setup(r => r.ResolveUsersAsync(It.IsAny<IEnumerable<ulong>>()))
            .ReturnsAsync(new Dictionary<ulong, (string Username, string? AvatarUrl)>());
        _promptSurfaceReporter.Setup(r => r.ReportAsync(It.IsAny<DiscordBot.Core.Enums.ToolScopes>(), It.IsAny<ulong?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PromptSurfaceReport?)null);

        AddBunitPersistentComponentState();
        AddAuthorizedAdmin();

        _contextProvider.Setup(p => p.GetAsync(GuildId, It.IsAny<System.Security.Claims.ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildContextResult.Ok(new GuildContext(
                Guild: new GuildDto { Id = GuildId, Name = "Test Guild" },
                GuildId: GuildId,
                GuildIdString: GuildId.ToString(),
                IsAppAdmin: true,
                IsGuildAdmin: false,
                CanEdit: true,
                AudioEnabled: true,
                RatWatchEnabled: true,
                Tabs: DiscordBot.Bot.Configuration.GuildNavigationConfig.GetTabs())));
    }

    private void NavigateTo(string path) =>
        ((BunitNavigationManager)Services.GetRequiredService<NavigationManager>()).NavigateTo(path);

    private IRenderedComponent<AssistantMetrics> RenderPage()
    {
        NavigateTo($"/Guilds/AssistantMetrics/{GuildId}");
        SetInteractiveRendererInfo();
        return Render<AssistantMetrics>(p => p.Add(x => x.GuildId, (long)GuildId));
    }

    [Fact]
    public void NoData_RendersEmptyStates_AndNullPromptSurfaceBranch()
    {
        var cut = RenderPage();

        cut.Markup.Should().Contain("No usage data yet");
        cut.Markup.Should().Contain("No tool names recorded yet in this window");
        cut.Markup.Should().Contain("there is no tool array to measure");
        cut.Markup.Should().Contain("No per-user usage recorded for this range");
    }

    [Fact]
    public void WithMetrics_RendersDailyTableAndSummaryCards()
    {
        _assistantService.Setup(s => s.GetUsageMetricsRangeAsync(GuildId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new AssistantUsageMetrics
                {
                    GuildId = GuildId,
                    Date = DateTime.UtcNow.Date,
                    TotalQuestions = 10,
                    EstimatedCostUsd = 1.2345m,
                    AverageLatencyMs = 500,
                    TotalCacheHits = 4,
                    TotalCacheMisses = 6,
                    FailedRequests = 1,
                    TotalToolCalls = 3
                }
            });

        var cut = RenderPage();

        cut.Markup.Should().Contain("10");
        cut.Markup.Should().Contain("$1.23");
        cut.Find("table").Should().NotBeNull();
    }

    [Fact]
    public void WithToolUsageAndPromptSurface_RendersBothTables()
    {
        _interactionLogRepository.Setup(r => r.GetToolUsageAsync(GuildId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new AssistantToolUsage("get_guild_info", 5, 5, 0, DateTime.UtcNow) });

        _promptSurfaceReporter.Setup(r => r.ReportAsync(It.IsAny<DiscordBot.Core.Enums.ToolScopes>(), It.IsAny<ulong?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PromptSurfaceReport
            {
                Scope = DiscordBot.Core.Enums.ToolScopes.Guild,
                SurfaceName = "Guild",
                GuildId = GuildId,
                Advertised = new PromptSurfaceMeasurement(
                    [new PromptSurfaceTool("get_guild_info", 100, 0.5)],
                    100,
                    25),
                Tools =
                [
                    new PromptSurfaceToolRow("get_guild_info", "Server info", "Server & Users", 100, 0.5, true, false, [])
                ],
                RegisteredChars = 100
            });

        var cut = RenderPage();

        cut.Markup.Should().Contain("get_guild_info");
        cut.Markup.Should().Contain("Server &amp; Users");
        cut.Markup.Should().Contain("Tools sent");
    }

    [Fact]
    public void PromptSurfaceReporter_Throws_IsCaughtAndPanelOmitted()
    {
        _promptSurfaceReporter.Setup(r => r.ReportAsync(It.IsAny<DiscordBot.Core.Enums.ToolScopes>(), It.IsAny<ulong?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var cut = RenderPage();

        cut.Markup.Should().Contain("there is no tool array to measure");
    }

    [Fact]
    public void UnknownGuild_RendersNotFoundGate()
    {
        _contextProvider.Setup(p => p.GetAsync(GuildId, It.IsAny<System.Security.Claims.ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildContextResult.NotFound());

        var cut = RenderPage();

        cut.Markup.Should().Contain("Server Not Found");
        _assistantService.Verify(s => s.GetUsageMetricsRangeAsync(It.IsAny<ulong>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
