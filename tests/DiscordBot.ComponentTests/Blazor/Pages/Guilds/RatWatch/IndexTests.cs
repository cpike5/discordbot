using Bunit;
using Bunit.TestDoubles;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Bot.Blazor.Pages.Guilds.RatWatch;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DiscordBot.ComponentTests.Blazor.Pages.Guilds.RatWatch;

/// <summary>
/// Component tests for <see cref="Index"/> (Guilds/RatWatch), the routable replacement for
/// <c>Pages/Guilds/RatWatch/Index.cshtml</c> + <c>IndexModel</c>
/// (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4b) - parity coverage for the behaviours
/// the deleted <c>IndexModelTests</c> unit-tested (guild lookup, settings/leaderboard/analytics
/// fetch, not-found short-circuit), plus the cancel/end-vote confirm-modal flows and the settings
/// validation rules that only exist as component methods now.
/// </summary>
public class IndexTests : BlazorComponentTestContext
{
    private const ulong GuildId = 123456789012345678UL;

    private readonly Mock<IGuildContextProvider> _contextProvider = new();
    private readonly Mock<IRatWatchService> _ratWatchService = new();
    private readonly Mock<IRatWatchRepository> _ratWatchRepository = new();

    public IndexTests()
    {
        Services.AddScoped(_ => _contextProvider.Object);
        Services.AddSingleton(_ratWatchService.Object);
        Services.AddSingleton(_ratWatchRepository.Object);

        _ratWatchService.Setup(s => s.GetGuildSettingsAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildRatWatchSettings { GuildId = GuildId, IsEnabled = true, Timezone = "UTC", MaxAdvanceHours = 24, VotingDurationMinutes = 5 });
        _ratWatchService.Setup(s => s.GetByGuildAsync(GuildId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<RatWatchDto>(), 0));
        _ratWatchService.Setup(s => s.GetLeaderboardAsync(GuildId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RatLeaderboardEntryDto>());
        _ratWatchRepository.Setup(r => r.GetAnalyticsSummaryAsync(GuildId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RatWatchAnalyticsSummaryDto());

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

    private IRenderedComponent<Bot.Blazor.Pages.Guilds.RatWatch.Index> RenderPage(string? path = null)
    {
        NavigateTo(path ?? $"/Guilds/RatWatch/{GuildId}");
        SetInteractiveRendererInfo();
        return Render<Bot.Blazor.Pages.Guilds.RatWatch.Index>(p => p.Add(x => x.GuildId, (long)GuildId));
    }

    [Fact]
    public void Ok_FetchesSettingsLeaderboardAndAnalytics_RendersEmptyState()
    {
        var cut = RenderPage();

        _ratWatchService.Verify(s => s.GetGuildSettingsAsync(GuildId, It.IsAny<CancellationToken>()), Times.Once);
        _ratWatchRepository.Verify(r => r.GetAnalyticsSummaryAsync(GuildId, null, null, It.IsAny<CancellationToken>()), Times.Once);
        cut.Markup.Should().Contain("No Rat Watches Yet");
        cut.Markup.Should().Contain("UTC");
    }

    [Fact]
    public void UnknownGuild_RendersNotFoundGate_WithoutCallingRatWatchService()
    {
        _contextProvider.Setup(p => p.GetAsync(GuildId, It.IsAny<System.Security.Claims.ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildContextResult.NotFound());

        var cut = RenderPage();

        cut.Markup.Should().Contain("Server Not Found");
        _ratWatchService.Verify(s => s.GetGuildSettingsAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Covers docs review finding 8: pagination is now <see cref="Bot.Blazor.Common.PagedQuery"/> +
    /// link-mode <c>Pagination</c>, so a page 2 link is a real <c>&lt;a href&gt;</c> carrying
    /// <c>pageNumber=2</c> (browser back/forward works) rather than a callback-mode
    /// <c>&lt;button&gt;</c> that only mutated in-memory state.
    /// </summary>
    [Fact]
    public void MultiplePages_RendersPageLinkWithPageNumberQueryString()
    {
        _ratWatchService.Setup(s => s.GetByGuildAsync(GuildId, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Enumerable.Range(0, 20).Select(i => MakeWatch(Guid.NewGuid())).ToArray(), 25));

        var cut = RenderPage();

        var link = cut.FindAll("a").SingleOrDefault(a => a.GetAttribute("href")?.Contains("pageNumber=2") == true);
        link.Should().NotBeNull("page 2 should render as a real link, not a callback-mode button");
    }

    /// <summary>Navigating straight to a <c>?pageNumber=</c> URL (a bookmark, or browser back/forward
    /// after a page-link click) loads that page directly - no client-side callback state needed.</summary>
    [Fact]
    public void NavigatingToPageNumberQuery_LoadsThatPage()
    {
        _ratWatchService.Setup(s => s.GetByGuildAsync(GuildId, 2, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { MakeWatch(Guid.NewGuid()) }, 25));

        var cut = RenderPage($"/Guilds/RatWatch/{GuildId}?pageNumber=2");

        cut.WaitForAssertion(() =>
            _ratWatchService.Verify(s => s.GetByGuildAsync(GuildId, 2, 20, It.IsAny<CancellationToken>()), Times.Once));
    }

    /// <summary>A failed load renders the "couldn't load" alert instead of a misleading empty state.</summary>
    [Fact]
    public void LoadThrows_RendersCouldNotLoadAlert_NotEmptyState()
    {
        _ratWatchService.Setup(s => s.GetGuildSettingsAsync(GuildId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var cut = RenderPage();

        cut.Markup.Should().Contain("Couldn't load Rat Watch data");
        cut.Markup.Should().NotContain("No Rat Watches Yet");
    }

    private static RatWatchDto MakeWatch(Guid id) => new()
    {
        Id = id,
        GuildId = GuildId,
        AccusedUserId = 1,
        AccusedUsername = "accused",
        InitiatorUserId = 2,
        InitiatorUsername = "initiator",
        Status = RatWatchStatus.Pending,
        ScheduledAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow
    };

    [Fact]
    public void EditSettings_InvalidTimezone_ShowsErrorAndDoesNotSave()
    {
        var cut = RenderPage();

        cut.Find("button").Click(); // "Edit Settings"
        cut.Find("#timezone").Change("");
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Save Settings").Click();

        cut.Markup.Should().Contain("Timezone is required");
        _ratWatchService.Verify(s => s.UpdateGuildSettingsAsync(It.IsAny<ulong>(), It.IsAny<Action<GuildRatWatchSettings>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void EditSettings_ValidValues_UpdatesAndReturnsToDisplayMode()
    {
        _ratWatchService.Setup(s => s.UpdateGuildSettingsAsync(GuildId, It.IsAny<Action<GuildRatWatchSettings>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildRatWatchSettings());

        var cut = RenderPage();

        cut.Find("button").Click(); // "Edit Settings"
        cut.Find("#maxAdvanceHours").Change("48");
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Save Settings").Click();

        _ratWatchService.Verify(s => s.UpdateGuildSettingsAsync(GuildId, It.IsAny<Action<GuildRatWatchSettings>>(), It.IsAny<CancellationToken>()), Times.Once);
        var toast = Services.GetRequiredService<IToastService>();
        cut.WaitForAssertion(() => toast.Toasts.Should().Contain(t => t.Level == ToastLevel.Success));
        cut.FindAll("#timezone").Should().BeEmpty("saving returns to display mode");
    }

    [Fact]
    public void CancelWatch_Confirmed_CallsCancelWatchAsync_AndReloads()
    {
        var watchId = Guid.NewGuid();
        _ratWatchService.SetupSequence(s => s.GetByGuildAsync(GuildId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { BuildWatch(watchId, RatWatchStatus.Pending) }, 1))
            .ReturnsAsync((Array.Empty<RatWatchDto>(), 0));
        _ratWatchService.Setup(s => s.CancelWatchAsync(watchId, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var cut = RenderPage();

        cut.Find("button[title='Cancel Watch']").Click();
        cut.Find("#cancelWatchModal").QuerySelectorAll("button").Last().Click();

        // ConfirmModal.ShowAsync()'s TaskCompletionSource uses RunContinuationsAsynchronously, so
        // the caller's continuation (ConfirmCancelAsync -> CancelWatchAsync) resumes on a separate
        // scheduled continuation rather than inline on this second Click() - wait for it instead of
        // asserting immediately after.
        cut.WaitForAssertion(() => _ratWatchService.Verify(s => s.CancelWatchAsync(watchId, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once));
        var toast = Services.GetRequiredService<IToastService>();
        cut.WaitForAssertion(() => toast.Toasts.Should().Contain(t => t.Level == ToastLevel.Success));
    }

    [Fact]
    public void EndVote_Confirmed_CallsFinalizeVotingAsync()
    {
        var watchId = Guid.NewGuid();
        _ratWatchService.Setup(s => s.GetByGuildAsync(GuildId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { BuildWatch(watchId, RatWatchStatus.Voting, votingStartedAt: DateTime.UtcNow) }, 1));
        _ratWatchService.Setup(s => s.FinalizeVotingAsync(watchId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var cut = RenderPage();

        cut.Find("button[title='End Vote']").Click();
        cut.Find("#endVoteModal").QuerySelectorAll("button").Last().Click();

        cut.WaitForAssertion(() => _ratWatchService.Verify(s => s.FinalizeVotingAsync(watchId, It.IsAny<CancellationToken>()), Times.Once));
    }

    [Fact]
    public void CancelWatch_ServiceReturnsFalse_ShowsErrorToast()
    {
        var watchId = Guid.NewGuid();
        _ratWatchService.Setup(s => s.GetByGuildAsync(GuildId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { BuildWatch(watchId, RatWatchStatus.Pending) }, 1));
        _ratWatchService.Setup(s => s.CancelWatchAsync(watchId, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var cut = RenderPage();

        cut.Find("button[title='Cancel Watch']").Click();
        cut.Find("#cancelWatchModal").QuerySelectorAll("button").Last().Click();

        var toast = Services.GetRequiredService<IToastService>();
        cut.WaitForAssertion(() => toast.Toasts.Should().Contain(t => t.Level == ToastLevel.Error));
    }

    private static RatWatchDto BuildWatch(Guid id, RatWatchStatus status, DateTime? votingStartedAt = null) => new()
    {
        Id = id,
        GuildId = GuildId,
        AccusedUserId = 111,
        AccusedUsername = "Accused",
        InitiatorUserId = 222,
        InitiatorUsername = "Initiator",
        ScheduledAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
        Status = status,
        VotingStartedAt = votingStartedAt
    };
}
