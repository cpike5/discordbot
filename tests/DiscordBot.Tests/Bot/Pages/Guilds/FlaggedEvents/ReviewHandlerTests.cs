using DiscordBot.Bot.Pages.Guilds.FlaggedEvents;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Pages.Guilds.FlaggedEvents;

/// <summary>
/// The review actions on the flagged-event list and details pages: they check the event belongs to
/// the guild, skip what is already reviewed, and tell the reader exactly what changed.
/// </summary>
public class ReviewHandlerTests
{
    private const ulong GuildId = 123456789UL;

    private readonly Mock<IFlaggedEventService> _events = new();
    private readonly Mock<IGuildService> _guilds = new();
    private readonly Mock<IGuildMemberService> _members = new();
    private readonly IndexModel _list;
    private readonly DetailsModel _details;

    public ReviewHandlerTests()
    {
        _list = new IndexModel(_events.Object, _guilds.Object, Mock.Of<ILogger<IndexModel>>());
        _details = new DetailsModel(_events.Object, _guilds.Object, _members.Object, Mock.Of<ILogger<DetailsModel>>());
        Prepare(_list);
        Prepare(_details);
    }

    private static void Prepare(PageModel model)
    {
        var http = new DefaultHttpContext();
        model.PageContext = new PageContext(new ActionContext(http, new RouteData(), new PageActionDescriptor(), new ModelStateDictionary()));
        model.TempData = new TempDataDictionary(http, Mock.Of<ITempDataProvider>());
    }

    private FlaggedEventDto Event(FlaggedEventStatus status, ulong guildId = GuildId)
    {
        var dto = new FlaggedEventDto
        {
            Id = Guid.NewGuid(),
            GuildId = guildId,
            UserId = 111UL,
            Username = "someone",
            RuleType = RuleType.Spam,
            Severity = Severity.Medium,
            Description = "Spam detected",
            Evidence = "{}",
            Status = status,
            CreatedAt = DateTime.UtcNow
        };
        _events.Setup(e => e.GetEventAsync(dto.Id, It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        _events.Setup(e => e.DismissEventAsync(dto.Id, It.IsAny<ulong>(), It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        _events.Setup(e => e.AcknowledgeEventAsync(dto.Id, It.IsAny<ulong>(), It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        _events.Setup(e => e.TakeActionAsync(dto.Id, It.IsAny<string>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        return dto;
    }

    #region List

    [Fact]
    public async Task Bulk_Dismiss_OpenEvents_ReportsSuccessAndRedirectsToTheSameList()
    {
        var a = Event(FlaggedEventStatus.Pending);
        var b = Event(FlaggedEventStatus.Acknowledged);
        _list.FilterStatus = FlaggedEventStatus.Pending;
        _list.CurrentPage = 2;

        var result = await _list.OnPostBulkAsync(GuildId, "dismiss", new List<Guid> { a.Id, b.Id }, CancellationToken.None);

        var redirect = result.Should().BeOfType<RedirectToPageResult>().Subject;
        redirect.RouteValues.Should().ContainKey("FilterStatus").WhoseValue.Should().Be(FlaggedEventStatus.Pending);
        redirect.RouteValues.Should().ContainKey("pageNumber").WhoseValue.Should().Be(2);
        _list.TempData["ToastSuccess"].Should().Be("2 events dismissed.");
        _events.Verify(e => e.DismissEventAsync(It.IsAny<Guid>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Bulk_Dismiss_SkipsEventsThatAreAlreadyClosed_AndSaysSo()
    {
        var open = Event(FlaggedEventStatus.Pending);
        var closed = Event(FlaggedEventStatus.Actioned);

        await _list.OnPostBulkAsync(GuildId, "dismiss", new List<Guid> { open.Id, closed.Id }, CancellationToken.None);

        ((string)_list.TempData["ToastWarning"]!).Should().Contain("Dismissed 1 of 2 events").And.Contain("1 already reviewed");
        _events.Verify(e => e.DismissEventAsync(closed.Id, It.IsAny<ulong>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Bulk_Acknowledge_SkipsEventsThatAreAlreadyAcknowledged()
    {
        var pending = Event(FlaggedEventStatus.Pending);
        var seen = Event(FlaggedEventStatus.Acknowledged);

        await _list.OnPostBulkAsync(GuildId, "acknowledge", new List<Guid> { pending.Id, seen.Id }, CancellationToken.None);

        _events.Verify(e => e.AcknowledgeEventAsync(pending.Id, It.IsAny<ulong>(), It.IsAny<CancellationToken>()), Times.Once);
        _events.Verify(e => e.AcknowledgeEventAsync(seen.Id, It.IsAny<ulong>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Bulk_NeverTouchesAnotherGuildsEvent()
    {
        var foreign = Event(FlaggedEventStatus.Pending, guildId: 999UL);

        await _list.OnPostBulkAsync(GuildId, "dismiss", new List<Guid> { foreign.Id }, CancellationToken.None);

        _events.Verify(e => e.DismissEventAsync(It.IsAny<Guid>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()), Times.Never);
        ((string)_list.TempData["ToastError"]!).Should().Contain("No events were dismissed");
    }

    [Fact]
    public async Task Bulk_WhenOneUpdateThrows_ReportsAPartialFailureAndKeepsGoing()
    {
        var good = Event(FlaggedEventStatus.Pending);
        var bad = Event(FlaggedEventStatus.Pending);
        _events.Setup(e => e.DismissEventAsync(bad.Id, It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db"));

        await _list.OnPostBulkAsync(GuildId, "dismiss", new List<Guid> { bad.Id, good.Id }, CancellationToken.None);

        ((string)_list.TempData["ToastWarning"]!).Should().Contain("Dismissed 1 of 2 events").And.Contain("1 could not be updated");
        _events.Verify(e => e.DismissEventAsync(good.Id, It.IsAny<ulong>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Bulk_CountsADuplicatedIdOnce()
    {
        var a = Event(FlaggedEventStatus.Pending);

        await _list.OnPostBulkAsync(GuildId, "dismiss", new List<Guid> { a.Id, a.Id }, CancellationToken.None);

        _list.TempData["ToastSuccess"].Should().Be("Event dismissed.");
        _events.Verify(e => e.DismissEventAsync(a.Id, It.IsAny<ulong>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Bulk_WithNothingSelected_ExplainsInsteadOfDoingNothingSilently()
    {
        await _list.OnPostBulkAsync(GuildId, "dismiss", new List<Guid>(), CancellationToken.None);

        ((string)_list.TempData["ToastError"]!).Should().Contain("Select at least one event");
    }

    [Fact]
    public async Task Bulk_WithAnUnknownAction_ChangesNothing()
    {
        var a = Event(FlaggedEventStatus.Pending);

        await _list.OnPostBulkAsync(GuildId, "delete", new List<Guid> { a.Id }, CancellationToken.None);

        _events.Verify(e => e.DismissEventAsync(It.IsAny<Guid>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()), Times.Never);
        _list.TempData["ToastError"].Should().NotBeNull();
    }

    [Fact]
    public async Task Row_Dismiss_OfAnAcknowledgedEvent_Works()
    {
        var acknowledged = Event(FlaggedEventStatus.Acknowledged);

        await _list.OnPostDismissAsync(GuildId, acknowledged.Id, CancellationToken.None);

        _list.TempData["ToastSuccess"].Should().Be("Event dismissed.");
    }

    #endregion

    #region Details

    [Fact]
    public async Task Details_Dismiss_AnAcknowledgedEvent_Works()
    {
        var acknowledged = Event(FlaggedEventStatus.Acknowledged);

        var result = await _details.OnPostDismissAsync(GuildId, acknowledged.Id, CancellationToken.None);

        result.Should().BeOfType<RedirectToPageResult>();
        _details.TempData["ToastSuccess"].Should().Be("Event dismissed.");
    }

    [Fact]
    public async Task Details_Acknowledge_AnAlreadyDismissedEvent_ExplainsWhyNot()
    {
        var dismissed = Event(FlaggedEventStatus.Dismissed);

        await _details.OnPostAcknowledgeAsync(GuildId, dismissed.Id, CancellationToken.None);

        ((string)_details.TempData["ToastError"]!).Should().Be("This event is already dismissed.");
        _events.Verify(e => e.AcknowledgeEventAsync(It.IsAny<Guid>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Details_ReviewOfAnotherGuildsEvent_IsNotFound()
    {
        var foreign = Event(FlaggedEventStatus.Pending, guildId: 999UL);

        var result = await _details.OnPostDismissAsync(GuildId, foreign.Id, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Details_RecordOutcome_StoresTheTrimmedText()
    {
        var acknowledged = Event(FlaggedEventStatus.Acknowledged);

        await _details.OnPostRecordOutcomeAsync(GuildId, acknowledged.Id, "  Warned user  ", CancellationToken.None);

        _events.Verify(e => e.TakeActionAsync(acknowledged.Id, "Warned user", It.IsAny<ulong>(), It.IsAny<CancellationToken>()), Times.Once);
        _details.TempData["ToastSuccess"].Should().Be("Outcome recorded.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Details_RecordOutcome_WithoutText_ExplainsAndStoresNothing(string? outcome)
    {
        var pending = Event(FlaggedEventStatus.Pending);

        await _details.OnPostRecordOutcomeAsync(GuildId, pending.Id, outcome, CancellationToken.None);

        ((string)_details.TempData["ToastError"]!).Should().Be("Describe the outcome first.");
        _events.Verify(e => e.TakeActionAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Details_RecordOutcome_TooLong_IsRejected()
    {
        var pending = Event(FlaggedEventStatus.Pending);

        await _details.OnPostRecordOutcomeAsync(GuildId, pending.Id, new string('x', DetailsModel.MaxOutcomeLength + 1), CancellationToken.None);

        ((string)_details.TempData["ToastError"]!).Should().Contain("too long");
        _events.Verify(e => e.TakeActionAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Details_RecordOutcome_OnAClosedEvent_IsRefused()
    {
        var actioned = Event(FlaggedEventStatus.Actioned);

        await _details.OnPostRecordOutcomeAsync(GuildId, actioned.Id, "Warned user", CancellationToken.None);

        ((string)_details.TempData["ToastError"]!).Should().Contain("already closed");
        _events.Verify(e => e.TakeActionAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion
}
