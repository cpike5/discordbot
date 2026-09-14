using System.Security.Claims;
using Bunit;
using Bunit.TestDoubles;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Bot.Configuration;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using DetailsPage = DiscordBot.Bot.Blazor.Pages.Guilds.FlaggedEvents.Details;

namespace DiscordBot.ComponentTests.Blazor.Pages.Guilds.FlaggedEvents;

/// <summary>
/// Component tests for <see cref="DetailsPage"/>, the routable replacement for
/// <c>Pages/Guilds/FlaggedEvents/Details.cshtml</c> + <c>DetailsModel</c>
/// (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4d). Covers Dismiss/Acknowledge/canned and
/// custom Take Action, the fixed post-dismiss redirect, and the guild-mismatch not-found state.
/// </summary>
public class DetailsTests : BlazorComponentTestContext
{
    private const ulong GuildId = 123456789012345678UL;
    private static readonly Guid EventId = Guid.NewGuid();

    private readonly Mock<IFlaggedEventService> _service = new();

    public DetailsTests()
    {
        Services.AddSingleton(_service.Object);
    }

    private static GuildContext Context() => new(
        Guild: new GuildDto { Id = GuildId, Name = "Test Guild" },
        GuildId: GuildId,
        GuildIdString: GuildId.ToString(),
        IsAppAdmin: true,
        IsGuildAdmin: false,
        CanEdit: true,
        AudioEnabled: true,
        RatWatchEnabled: true,
        Tabs: GuildNavigationConfig.GetTabs());

    private void RegisterMockProvider()
    {
        var mock = new Mock<IGuildContextProvider>();
        mock.Setup(p => p.GetAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildContextResult.Ok(Context()));
        Services.AddScoped(_ => mock.Object);
    }

    private static FlaggedEventDto BuildEvent(ulong guildId = GuildId, FlaggedEventStatus status = FlaggedEventStatus.Pending) => new()
    {
        Id = EventId,
        GuildId = guildId,
        UserId = 42,
        Username = "suspect",
        RuleType = RuleType.Spam,
        Severity = Severity.High,
        Status = status,
        Description = "Sent 10 messages in 3 seconds",
        Evidence = "{}",
        CreatedAt = DateTime.UtcNow
    };

    private IRenderedComponent<DetailsPage> RenderDetails()
    {
        AddAuthorization().SetAuthorized("admin").SetClaims(new Claim(ClaimTypes.Role, "Admin"), new Claim("discord:user_id", "999"));
        AddBunitPersistentComponentState();
        RegisterMockProvider();
        _service.Setup(s => s.GetUserEventsAsync(GuildId, 42UL, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<FlaggedEventDto>());
        SetInteractiveRendererInfo();

        return Render<DetailsPage>(p => p
            .Add(x => x.GuildId, (long)GuildId)
            .Add(x => x.Id, EventId));
    }

    [Fact]
    public void EventNotFound_ShowsNotFoundEmptyState()
    {
        _service.Setup(s => s.GetEventAsync(EventId, It.IsAny<CancellationToken>())).ReturnsAsync((FlaggedEventDto?)null);

        var cut = RenderDetails();

        cut.Markup.Should().Contain("Flagged event not found");
    }

    [Fact]
    public void EventFromDifferentGuild_ShowsNotFoundEmptyState()
    {
        _service.Setup(s => s.GetEventAsync(EventId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildEvent(guildId: 999999999999999999UL));

        var cut = RenderDetails();

        cut.Markup.Should().Contain("Flagged event not found");
    }

    [Fact]
    public void PendingEvent_RendersActionsPanel()
    {
        _service.Setup(s => s.GetEventAsync(EventId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildEvent());

        var cut = RenderDetails();

        cut.Markup.Should().Contain("Dismiss").And.Contain("Acknowledge").And.Contain("Take Action");
    }

    [Fact]
    public void Dismiss_Confirmed_CallsServiceAndNavigatesToTheListRoute()
    {
        _service.Setup(s => s.GetEventAsync(EventId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildEvent());
        _service.Setup(s => s.DismissEventAsync(EventId, 999UL, It.IsAny<CancellationToken>())).ReturnsAsync(BuildEvent(status: FlaggedEventStatus.Dismissed));

        var cut = RenderDetails();
        var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Dismiss").Click();
        cut.Find("#flaggedEventConfirmModal").QuerySelectorAll("button").Last().Click();

        cut.WaitForAssertion(() => _service.Verify(s => s.DismissEventAsync(EventId, 999UL, It.IsAny<CancellationToken>()), Times.Once));
        // The legacy inline redirect targeted the nonexistent "/Guilds/FlaggedEvents/Index/{guildId}" -
        // this asserts the fixed, real list route.
        cut.WaitForAssertion(() => navMan.Uri.Should().EndWith($"/Guilds/FlaggedEvents/{GuildId}"));
    }

    [Fact]
    public void TakeAction_Canned_Confirmed_CallsServiceWithTheCannedDescription()
    {
        _service.Setup(s => s.GetEventAsync(EventId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildEvent());
        _service.Setup(s => s.TakeActionAsync(EventId, "Warned user", 999UL, It.IsAny<CancellationToken>())).ReturnsAsync(BuildEvent(status: FlaggedEventStatus.Actioned));

        var cut = RenderDetails();
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Warn User").Click();
        cut.Find("#flaggedEventConfirmModal").QuerySelectorAll("button").Last().Click();

        cut.WaitForAssertion(() => _service.Verify(s => s.TakeActionAsync(EventId, "Warned user", 999UL, It.IsAny<CancellationToken>()), Times.Once));
    }

    [Fact]
    public void TakeAction_Custom_Submit_CallsServiceWithTheTypedDescription()
    {
        _service.Setup(s => s.GetEventAsync(EventId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildEvent());
        _service.Setup(s => s.TakeActionAsync(EventId, "Removed from voice channel", 999UL, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildEvent(status: FlaggedEventStatus.Actioned));

        var cut = RenderDetails();
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Custom Action...").Click();
        cut.Find("#customActionText").Input("Removed from voice channel");
        cut.Find("#customActionModal").QuerySelectorAll("button").Single(b => b.TextContent.Trim() == "Submit").Click();

        cut.WaitForAssertion(() => _service.Verify(s => s.TakeActionAsync(EventId, "Removed from voice channel", 999UL, It.IsAny<CancellationToken>()), Times.Once));
    }

    [Fact]
    public void NonPendingEvent_ShowsReadOnlyStatusMessage_NoActions()
    {
        _service.Setup(s => s.GetEventAsync(EventId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildEvent(status: FlaggedEventStatus.Dismissed));

        var cut = RenderDetails();

        cut.Markup.Should().Contain("This event has been dismissed");
    }
}
