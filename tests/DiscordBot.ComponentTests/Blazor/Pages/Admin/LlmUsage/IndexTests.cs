using Bunit;
using Bunit.TestDoubles;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.DTOs.Llm.Reporting;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using IndexPage = DiscordBot.Bot.Blazor.Pages.Admin.LlmUsage.Index;

namespace DiscordBot.ComponentTests.Blazor.Pages.Admin.LlmUsage;

/// <summary>
/// Component tests for <see cref="IndexPage"/>, the routable replacement for
/// <c>Pages/Admin/LlmUsage.cshtml</c> + <c>LlmUsageModel</c>
/// (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4d): hero totals, the per-user drill-down
/// paging (now a direct <see cref="ILlmUsageRepository.GetRecordsAsync"/> call, replacing
/// <c>llm-usage.js</c>) and the shared range-clamp helper.
/// </summary>
public class IndexTests : BlazorComponentTestContext
{
    private readonly Mock<ILlmUsageRepository> _repository = new();
    private readonly Mock<IDiscordUserResolver> _resolver = new();
    private readonly Mock<IGuildService> _guildService = new();

    public IndexTests()
    {
        Services.AddSingleton(_repository.Object);
        Services.AddSingleton(_resolver.Object);
        Services.AddSingleton(_guildService.Object);

        _guildService.Setup(g => g.GetAllGuildsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _resolver.Setup(r => r.ResolveUsersAsync(It.IsAny<IEnumerable<ulong>>()))
            .ReturnsAsync(new Dictionary<ulong, (string, string?)>());
        _repository.Setup(r => r.GetByModelAsync(It.IsAny<LlmUsageQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _repository.Setup(r => r.GetByModeAsync(It.IsAny<LlmUsageQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _repository.Setup(r => r.GetByDayAsync(It.IsAny<LlmUsageQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        AddAuthorization().SetAuthorized("admin").SetRoles("Admin");
        SetInteractiveRendererInfo();
    }

    [Fact]
    public void HeroTiles_ShowTheTotals()
    {
        _repository.Setup(r => r.GetTotalsAsync(It.IsAny<LlmUsageQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmUsageTotals { MessageCount = 3, InputTokens = 100, OutputTokens = 50, CostUsd = 1.23m });
        _repository.Setup(r => r.GetByUserAsync(It.IsAny<LlmUsageQuery>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var cut = Render<IndexPage>();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("3").And.Contain("$1.23"));
    }

    [Fact]
    public void ClickingUserRow_LoadsDrilldownRecords()
    {
        _repository.Setup(r => r.GetTotalsAsync(It.IsAny<LlmUsageQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(new LlmUsageTotals());
        _repository.Setup(r => r.GetByUserAsync(It.IsAny<LlmUsageQuery>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new LlmUsageByUser { UserId = 42UL, MessageCount = 2, CostUsd = 0.5m, CostShare = 1.0 }]);
        _resolver.Setup(r => r.ResolveUsersAsync(It.IsAny<IEnumerable<ulong>>()))
            .ReturnsAsync(new Dictionary<ulong, (string, string?)> { [42UL] = ("bob", null) });

        var record = new LlmUsageRecord { Id = 1, Timestamp = DateTime.UtcNow, UserId = 42UL, Model = "anthropic/claude-sonnet-4", CostUsd = 0.1m, Success = true };
        _repository.Setup(r => r.GetRecordsAsync(It.Is<LlmUsageQuery>(q => q.UserId == 42UL), 1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmUsagePagedRecords { Records = [record], TotalCount = 1 });

        var cut = Render<IndexPage>();
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='llm-usage-user-row']").Should().HaveCount(1));

        cut.Find("[data-testid='llm-usage-user-row']").Click();

        cut.WaitForAssertion(() => cut.FindAll("[data-testid='llm-usage-drilldown-row']").Should().HaveCount(1));
        _repository.Verify(r => r.GetRecordsAsync(It.Is<LlmUsageQuery>(q => q.UserId == 42UL), 1, It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void RangeOverMaxDays_IsClampedToTheWidestAllowedSpan()
    {
        _repository.Setup(r => r.GetTotalsAsync(It.IsAny<LlmUsageQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(new LlmUsageTotals());
        _repository.Setup(r => r.GetByUserAsync(It.IsAny<LlmUsageQuery>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        var end = DateTime.UtcNow.Date;
        var start = end.AddDays(-500);
        navMan.NavigateTo($"/Admin/LlmUsage?StartDate={start:yyyy-MM-dd}&EndDate={end:yyyy-MM-dd}");

        Render<IndexPage>();

        _repository.Verify(r => r.GetTotalsAsync(It.Is<LlmUsageQuery>(q =>
            (q.To.Date - q.From.Date).Days <= LlmUsageRangeLimits.MaxRangeDays + 1), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }
}
