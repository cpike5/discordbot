using Bunit;
using Bunit.TestDoubles;
using Discord.WebSocket;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MessagesTab = DiscordBot.Bot.Blazor.Pages.Admin.Logs.Tabs.MessagesTab;

namespace DiscordBot.ComponentTests.Blazor.Pages.Admin.Logs.Tabs;

/// <summary>
/// Component tests for <see cref="MessagesTab"/> - the "messages" tab of the unified Logs page
/// (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4d). Query names (<c>AuthorId</c>,
/// <c>MessageGuildId</c>, <c>ChannelId</c>, <c>MessageSource</c>, <c>MessageStartDate</c>,
/// <c>MessageEndDate</c>, <c>MessageSearchTerm</c>, <c>messagePageNumber</c>) are asserted exactly
/// since <c>Services/Search/MessageLogsSearchProvider</c> builds links against them.
/// </summary>
public class MessagesTabTests : BlazorComponentTestContext
{
    private readonly Mock<IMessageLogService> _messageLogService = new();
    private readonly Mock<IMessageLogRepository> _messageLogRepository = new();
    private readonly Mock<IGuildService> _guildService = new();

    public MessagesTabTests()
    {
        Services.AddSingleton(_messageLogService.Object);
        Services.AddSingleton(_messageLogRepository.Object);
        Services.AddSingleton(_guildService.Object);
        Services.AddSingleton(new DiscordSocketClient());

        _messageLogService.Setup(s => s.GetLogsAsync(It.IsAny<MessageLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaginatedResponseDto<MessageLogDto> { Items = [], Page = 1, PageSize = 25, TotalCount = 0 });
        _messageLogRepository.Setup(r => r.GetUserMessagesAsync(It.IsAny<ulong>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _guildService.Setup(s => s.GetGuildByIdAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>())).ReturnsAsync((GuildDto?)null);

        AddAuthorizedAdmin();
        SetInteractiveRendererInfo();
    }

    [Fact]
    public void OnInitialized_DefaultsToLast7Days_WhenNoDateFilters()
    {
        var cut = Render<MessagesTab>();

        cut.WaitForAssertion(() => _messageLogService.Verify(s => s.GetLogsAsync(
            It.Is<MessageLogQueryDto>(q => q.StartDate.HasValue && q.EndDate.HasValue), It.IsAny<CancellationToken>()), Times.Once));
    }

    [Fact]
    public void SupplyParameterFromQuery_BuildsTheExpectedQuery()
    {
        var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        navMan.NavigateTo("/Admin/Logs?tab=messages&AuthorId=1&MessageGuildId=2&ChannelId=3&MessageSource=DirectMessage&MessageSearchTerm=hi&messagePageNumber=2");
        // AuthorId/MessageGuildId/ChannelId are query-bound as string (see MessagesTab.razor.cs's
        // AuthorIdQuery note) - a plain numeric string still parses to the expected ulong?.

        var cut = Render<MessagesTab>();

        cut.WaitForAssertion(() => _messageLogService.Verify(s => s.GetLogsAsync(
            It.Is<MessageLogQueryDto>(q =>
                q.AuthorId == 1UL && q.GuildId == 2UL && q.ChannelId == 3UL &&
                q.Source == DiscordBot.Core.Enums.MessageSource.DirectMessage &&
                q.SearchTerm == "hi" && q.Page == 2),
            It.IsAny<CancellationToken>()), Times.Once));
    }

    [Fact]
    public void FilterForm_Submit_NavigatesWithThePreservedQueryNames()
    {
        var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        var cut = Render<MessagesTab>();

        cut.Find("#messageSearchTerm").Input("hello");
        cut.Find("[data-testid='messages-filter-form']").Submit();

        navMan.Uri.Should().Contain("tab=messages").And.Contain("MessageSearchTerm=hello");
    }

    [Fact]
    public void NoExportLink_IsRendered()
    {
        var cut = Render<MessagesTab>();

        cut.FindAll("a").Should().NotContain(a => a.GetAttribute("href")!.Contains("Export", StringComparison.OrdinalIgnoreCase));
    }
}
