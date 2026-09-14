using Bunit;
using Discord.WebSocket;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using IndexPage = DiscordBot.Bot.Blazor.Pages.Admin.Logs.Index;

namespace DiscordBot.ComponentTests.Blazor.Pages.Admin.Logs;

/// <summary>
/// Component tests for <see cref="IndexPage"/>, the unified replacement for
/// <c>Pages/Admin/Logs/Index.cshtml</c> + <c>IndexModel</c> (docs/plans/blazor-port-plan.md §5
/// Phase 4, cluster 4d): only the tab-selection logic - each tab's own filters/paging are covered
/// by <c>Tabs/MessagesTabTests</c>/<c>Tabs/AuditTabTests</c>.
/// </summary>
public class IndexTests : BlazorComponentTestContext
{
    private readonly Mock<IMessageLogService> _messageLogService = new();
    private readonly Mock<IAuditLogService> _auditLogService = new();
    private readonly Mock<IGuildService> _guildService = new();
    private readonly Mock<IMessageLogRepository> _messageLogRepository = new();

    public IndexTests()
    {
        Services.AddSingleton(_messageLogService.Object);
        Services.AddSingleton(_auditLogService.Object);
        Services.AddSingleton(_guildService.Object);
        Services.AddSingleton(_messageLogRepository.Object);
        Services.AddSingleton(new DiscordSocketClient());

        _messageLogService.Setup(s => s.GetLogsAsync(It.IsAny<MessageLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaginatedResponseDto<MessageLogDto> { Items = [], Page = 1, PageSize = 25, TotalCount = 0 });
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
    public void NoTabQuery_DefaultsToMessagesTab()
    {
        var cut = Render<IndexPage>();

        cut.Find("[data-testid='messages-filter-form']").Should().NotBeNull();
        cut.FindAll("[data-testid='audit-filter-form']").Should().BeEmpty();
    }

    [Fact]
    public void TabQueryAudit_SelectsAuditTab()
    {
        var navMan = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        navMan.NavigateTo("/Admin/Logs?tab=audit");

        var cut = Render<IndexPage>();

        cut.Find("[data-testid='audit-filter-form']").Should().NotBeNull();
        cut.FindAll("[data-testid='messages-filter-form']").Should().BeEmpty();
    }

    [Fact]
    public void TabQueryApplication_ShowsComingSoon()
    {
        var navMan = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        navMan.NavigateTo("/Admin/Logs?tab=application");

        var cut = Render<IndexPage>();

        cut.Markup.Should().Contain("Coming soon");
    }

    [Fact]
    public void TabNav_LinksCarryTheTabQueryName()
    {
        var cut = Render<IndexPage>();

        cut.Markup.Should().Contain("/Admin/Logs?tab=messages").And.Contain("/Admin/Logs?tab=audit");
    }
}
