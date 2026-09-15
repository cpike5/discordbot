using System.Security.Claims;
using Bunit;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Bot.Configuration;
using DiscordBot.Bot.Interfaces;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using DetailsPage = DiscordBot.Bot.Blazor.Pages.Guilds.Details;

namespace DiscordBot.ComponentTests.Blazor.Pages.Guilds;

/// <summary>
/// Component tests for <see cref="DetailsPage"/>, the routable replacement for
/// <c>Pages/Guilds/Details.cshtml</c> + <c>DetailsModel</c> (docs/plans/blazor-port-plan.md §5
/// Phase 4, cluster 4d). Covers the six dashboard widgets' populated/empty states, the
/// <c>CanEdit</c>-gated action bar, the sync toast, and the aggregator-null not-found state.
/// </summary>
public class DetailsTests : BlazorComponentTestContext
{
    private const ulong GuildId = 123456789012345678UL;

    private readonly Mock<IGuildDetailsAggregator> _aggregator = new();
    private readonly Mock<IGuildService> _guildService = new();

    public DetailsTests()
    {
        Services.AddSingleton(_aggregator.Object);
        Services.AddSingleton(_guildService.Object);
    }

    private static GuildContext Context(bool canEdit = true) => new(
        Guild: new GuildDto { Id = GuildId, Name = "Test Guild", MemberCount = 50, IsActive = true, JoinedAt = DateTime.UtcNow },
        GuildId: GuildId,
        GuildIdString: GuildId.ToString(),
        IsAppAdmin: canEdit,
        IsGuildAdmin: false,
        CanEdit: canEdit,
        AudioEnabled: true,
        RatWatchEnabled: true,
        Tabs: GuildNavigationConfig.GetTabs());

    private void RegisterMockProvider(bool canEdit = true)
    {
        var mock = new Mock<IGuildContextProvider>();
        mock.Setup(p => p.GetAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildContextResult.Ok(Context(canEdit)));
        Services.AddScoped(_ => mock.Object);
    }

    private static GuildDetailsAggregateDto EmptyAggregate() => new()
    {
        Guild = new GuildDto { Id = GuildId, Name = "Test Guild" },
        RecentCommandLogs = Array.Empty<CommandLogDto>()
    };

    private IRenderedComponent<DetailsPage> RenderDetails(GuildDetailsAggregateDto? aggregate, bool canEdit = true)
    {
        AddAuthorization().SetAuthorized("admin").SetClaims(new Claim(ClaimTypes.Role, "Admin"));
        AddBunitPersistentComponentState();
        RegisterMockProvider(canEdit);
        _aggregator.Setup(a => a.BuildAsync(GuildId, 10, It.IsAny<CancellationToken>())).ReturnsAsync(aggregate);
        SetInteractiveRendererInfo();

        return Render<DetailsPage>(p => p.Add(x => x.GuildId, (long)GuildId));
    }

    [Fact]
    public void AggregateNull_ShowsNotFoundEmptyState()
    {
        var cut = RenderDetails(null);

        cut.Markup.Should().Contain("Guild not found");
    }

    [Fact]
    public void EmptyAggregate_ShowsEmptyStatesForEveryWidget()
    {
        var cut = RenderDetails(EmptyAggregate());

        cut.Markup.Should().Contain("No scheduled messages yet");
        cut.Markup.Should().Contain("No reminders yet");
        cut.Markup.Should().Contain("No command activity yet");
    }

    [Fact]
    public void PopulatedAggregate_RendersWidgetStats()
    {
        var aggregate = EmptyAggregate() with
        {
            ScheduledMessagesTotal = 3,
            ScheduledMessagesActive = 2,
            ScheduledMessagesPaused = 1,
            RemindersTotal = 5,
            RemindersPending = 2,
            MembersTotalCount = 40,
            RecentCommandLogs = new[]
            {
                new CommandLogDto { Id = Guid.NewGuid(), CommandName = "ping", Username = "alice", UserId = 1, ExecutedAt = DateTime.UtcNow, Success = true, ResponseTimeMs = 12 }
            }
        };

        var cut = RenderDetails(aggregate);

        cut.Markup.Should().Contain("/ping");
        cut.Markup.Should().Contain("alice");
    }

    [Fact]
    public void CanEdit_ShowsSyncAndEditSettingsActions()
    {
        var cut = RenderDetails(EmptyAggregate(), canEdit: true);

        cut.Markup.Should().Contain("Edit Settings");
        cut.FindAll("button").Should().Contain(b => b.TextContent.Contains("Sync"));
    }

    [Fact]
    public void CannotEdit_HidesSyncAndEditSettingsActions()
    {
        var cut = RenderDetails(EmptyAggregate(), canEdit: false);

        cut.Markup.Should().NotContain("Edit Settings");
    }

    [Fact]
    public void Sync_Success_ShowsToastAndReloads()
    {
        var cut = RenderDetails(EmptyAggregate());
        _guildService.Setup(s => s.SyncGuildAsync(GuildId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Sync").Click();

        cut.WaitForAssertion(() => _guildService.Verify(s => s.SyncGuildAsync(GuildId, It.IsAny<CancellationToken>()), Times.Once));
        var toast = Services.GetRequiredService<Bot.Blazor.Services.IToastService>();
        cut.WaitForAssertion(() => toast.Toasts.Should().Contain(t => t.Level == Bot.Blazor.Services.ToastLevel.Success));
    }

    [Fact]
    public void MoreActions_CopyGuildId_CopiesAndToasts()
    {
        // Must be configured before the first render - Details.razor.cs's OnAfterRenderAsync
        // always calls BrowserInterop.OnClickOutsideAsync for the More Actions dropdown, which
        // caches the module task from whatever "import" call resolves first.
        JSInterop.SetupModule("./js/blazor/browser.js").Setup<bool>("copyToClipboard", _ => true).SetResult(true);
        var cut = RenderDetails(EmptyAggregate());
        cut.FindAll("button").Single(b => b.TextContent.Contains("More Actions")).Click();
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Copy Guild ID").Click();

        var toast = Services.GetRequiredService<Bot.Blazor.Services.IToastService>();
        cut.WaitForAssertion(() => toast.Toasts.Should().Contain(t => t.Level == Bot.Blazor.Services.ToastLevel.Success));
    }
}
