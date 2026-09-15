using Bunit;
using Bunit.TestDoubles;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using AuditTab = DiscordBot.Bot.Blazor.Pages.Admin.Logs.Tabs.AuditTab;

namespace DiscordBot.ComponentTests.Blazor.Pages.Admin.Logs.Tabs;

/// <summary>
/// Component tests for <see cref="AuditTab"/> - the "audit" tab of the unified Logs page
/// (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4d). Query names (<c>Category</c>,
/// <c>Action</c>, <c>ActorId</c>, <c>TargetType</c>, <c>AuditGuildId</c>, <c>AuditStartDate</c>,
/// <c>AuditEndDate</c>, <c>AuditSearchTerm</c>, <c>auditPageNumber</c>) are asserted exactly since
/// <c>Services/Search/AuditLogsSearchProvider</c> and <c>AuditLogCard.razor</c> build links
/// against them.
/// </summary>
public class AuditTabTests : BlazorComponentTestContext
{
    private readonly Mock<IAuditLogService> _auditLogService = new();
    private readonly Mock<IGuildService> _guildService = new();
    private readonly Mock<IMessageLogRepository> _messageLogRepository = new();

    public AuditTabTests()
    {
        Services.AddSingleton(_auditLogService.Object);
        Services.AddSingleton(_guildService.Object);
        Services.AddSingleton(_messageLogRepository.Object);

        _auditLogService.Setup(s => s.GetLogsAsync(It.IsAny<AuditLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<AuditLogDto>(), 0));
        _guildService.Setup(s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _messageLogRepository.Setup(r => r.GetUserMessagesAsync(It.IsAny<ulong>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _messageLogRepository.Setup(r => r.SearchAuthorsAsync(It.IsAny<string>(), It.IsAny<ulong?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        AddAuthorizedAdmin();
        SetInteractiveRendererInfo();
    }

    [Fact]
    public void OnInitialized_DefaultsToLast30Days_WhenNoDateFilters()
    {
        var cut = Render<AuditTab>();

        cut.WaitForAssertion(() => _auditLogService.Verify(s => s.GetLogsAsync(
            It.Is<AuditLogQueryDto>(q => q.StartDate.HasValue && q.EndDate.HasValue), It.IsAny<CancellationToken>()), Times.Once));
    }

    [Fact]
    public void SupplyParameterFromQuery_BuildsTheExpectedQuery()
    {
        var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        // Category/Action/AuditGuildId are query-bound as int?/string (see AuditTab.razor.cs's
        // CategoryQuery/ActionQuery/AuditGuildIdQuery notes) - Security=4, Login=4 by enum value.
        navMan.NavigateTo("/Admin/Logs?tab=audit&Category=4&Action=4&ActorId=abc&TargetType=User&AuditGuildId=42&AuditSearchTerm=x&auditPageNumber=3");

        var cut = Render<AuditTab>();

        cut.WaitForAssertion(() => _auditLogService.Verify(s => s.GetLogsAsync(
            It.Is<AuditLogQueryDto>(q =>
                q.Category == DiscordBot.Core.Enums.AuditLogCategory.Security &&
                q.Action == DiscordBot.Core.Enums.AuditLogAction.Login &&
                q.ActorId == "abc" && q.TargetType == "User" && q.GuildId == 42UL &&
                q.SearchTerm == "x" && q.Page == 3),
            It.IsAny<CancellationToken>()), Times.Once));
    }

    [Fact]
    public void ExportLink_CarriesTheCurrentFilters()
    {
        var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        navMan.NavigateTo("/Admin/Logs?tab=audit&Category=4&AuditSearchTerm=hi");

        var cut = Render<AuditTab>();

        var exportLink = cut.FindAll("a").Single(a => a.TextContent.Contains("Export CSV"));
        var href = exportLink.GetAttribute("href");
        href.Should().StartWith("/api/admin/audit-logs/export").And.Contain("Category=4").And.Contain("AuditSearchTerm=hi");
    }

    [Fact]
    public void RowClick_TogglesTheExpandedDetailPanel()
    {
        _auditLogService.Setup(s => s.GetLogsAsync(It.IsAny<AuditLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<AuditLogDto>
            {
                new() { Id = 1, Timestamp = DateTime.UtcNow, CategoryName = "User", ActionName = "Created", ActorDisplayName = "alice", Details = "{\"a\":1}" }
            }, 1));

        var cut = Render<AuditTab>();
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='audit-row']").Should().HaveCount(1));

        cut.Find("[data-testid='audit-row']").Click();

        cut.Markup.Should().Contain("Correlation ID");
    }
}
