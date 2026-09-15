using System.Security.Claims;
using Bunit;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Bot.Configuration;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using IndexPage = DiscordBot.Bot.Blazor.Pages.Guilds.FlaggedEvents.Index;

namespace DiscordBot.ComponentTests.Blazor.Pages.Guilds.FlaggedEvents;

/// <summary>
/// Component tests for <see cref="IndexPage"/>, the routable replacement for
/// <c>Pages/Guilds/FlaggedEvents/Index.cshtml</c> + <c>IndexModel</c>
/// (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4d). Covers filters, bulk select, and that
/// dismiss/acknowledge call the service with the reviewer id resolved from the
/// <c>discord:user_id</c> claim.
/// </summary>
public class IndexTests : BlazorComponentTestContext
{
    private const ulong GuildId = 123456789012345678UL;

    private readonly Mock<IFlaggedEventService> _service = new();

    public IndexTests()
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

    private static FlaggedEventDto BuildEvent(FlaggedEventStatus status = FlaggedEventStatus.Pending) => new()
    {
        Id = Guid.NewGuid(),
        GuildId = GuildId,
        UserId = 42,
        Username = "suspect",
        RuleType = RuleType.Spam,
        Severity = Severity.High,
        Status = status,
        CreatedAt = DateTime.UtcNow
    };

    private IRenderedComponent<IndexPage> RenderIndex(bool withDiscordClaim = true)
    {
        var claims = new List<Claim> { new(ClaimTypes.Role, "Admin") };
        if (withDiscordClaim)
        {
            claims.Add(new Claim("discord:user_id", "999"));
        }

        AddAuthorization().SetAuthorized("admin").SetClaims(claims.ToArray());
        AddBunitPersistentComponentState();
        RegisterMockProvider();
        SetInteractiveRendererInfo();

        return Render<IndexPage>(p => p.Add(x => x.GuildId, (long)GuildId));
    }

    [Fact]
    public void List_RendersRows_ForResolvedEvents()
    {
        _service.Setup(s => s.GetFilteredEventsAsync(GuildId, It.IsAny<FlaggedEventQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { BuildEvent() }, 1));

        var cut = RenderIndex();

        cut.FindAll("[data-testid='flagged-event-row']").Should().HaveCount(1);
        cut.Markup.Should().Contain("suspect");
    }

    [Fact]
    public void NoEvents_ShowsEmptyState()
    {
        _service.Setup(s => s.GetFilteredEventsAsync(GuildId, It.IsAny<FlaggedEventQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<FlaggedEventDto>(), 0));

        var cut = RenderIndex();

        cut.Markup.Should().Contain("No flagged events found");
    }

    [Fact]
    public void Dismiss_WithDiscordClaim_CallsServiceWithReviewerId()
    {
        var evt = BuildEvent();
        _service.SetupSequence(s => s.GetFilteredEventsAsync(GuildId, It.IsAny<FlaggedEventQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { evt }, 1))
            .ReturnsAsync((Array.Empty<FlaggedEventDto>(), 0));
        _service.Setup(s => s.DismissEventAsync(evt.Id, 999UL, It.IsAny<CancellationToken>())).ReturnsAsync(evt);

        var cut = RenderIndex();
        cut.Find("button[title='Dismiss']").Click();

        cut.WaitForAssertion(() => _service.Verify(s => s.DismissEventAsync(evt.Id, 999UL, It.IsAny<CancellationToken>()), Times.Once));
    }

    [Fact]
    public void Dismiss_WithoutDiscordClaim_ShowsErrorToast_AndDoesNotCallService()
    {
        var evt = BuildEvent();
        _service.Setup(s => s.GetFilteredEventsAsync(GuildId, It.IsAny<FlaggedEventQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { evt }, 1));

        var cut = RenderIndex(withDiscordClaim: false);
        cut.Find("button[title='Dismiss']").Click();

        var toast = Services.GetRequiredService<Bot.Blazor.Services.IToastService>();
        cut.WaitForAssertion(() => toast.Toasts.Should().Contain(t => t.Level == Bot.Blazor.Services.ToastLevel.Error));
        _service.Verify(s => s.DismissEventAsync(It.IsAny<Guid>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void SelectAll_ThenBulkAcknowledge_CallsServiceForEachSelectedEvent()
    {
        var events = new[] { BuildEvent(), BuildEvent() };
        _service.SetupSequence(s => s.GetFilteredEventsAsync(GuildId, It.IsAny<FlaggedEventQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((events, 2))
            .ReturnsAsync((events, 2));
        _service.Setup(s => s.AcknowledgeEventAsync(It.IsAny<Guid>(), 999UL, It.IsAny<CancellationToken>())).ReturnsAsync((FlaggedEventDto?)null);

        var cut = RenderIndex();
        cut.Find("input[aria-label='Select all events']").Change(true);
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Acknowledge Selected").Click();

        cut.WaitForAssertion(() => _service.Verify(s => s.AcknowledgeEventAsync(It.IsAny<Guid>(), 999UL, It.IsAny<CancellationToken>()), Times.Exactly(2)));
    }
}
