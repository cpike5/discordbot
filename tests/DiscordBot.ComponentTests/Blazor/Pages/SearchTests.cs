using Bunit;
using Bunit.TestDoubles;
using DiscordBot.Bot.Blazor.Pages;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DiscordBot.ComponentTests.Blazor.Pages;

/// <summary>
/// Component tests for <see cref="Search"/>, the routable replacement for
/// <c>Pages/Search.cshtml</c> + <c>SearchModel</c> (docs/plans/blazor-port-plan.md §5 Phase 3).
/// Covers: the empty/validation/no-results/results states, admin-only section gating via
/// <c>IAuthorizationService</c> (bUnit's fake auth backs the page's own
/// <c>AuthorizeAsync(user, "RequireAdmin")</c> call - see the <see cref="BlazorComponentTestContext"/>
/// XML doc and <c>RestartBannerTests</c> for the same pattern), per-call-site <c>Highlight</c>
/// parameters, the <see cref="DiscordBot.Bot.Blazor.Shared.GuildContextSelector"/> a
/// <c>RequiresGuildContext</c> page result renders, the search form's navigation, and the
/// persist/restore round trip across a simulated prerender-to-circuit boundary (mirroring
/// <c>GuildPageBaseTests</c>) in place of a same-process double-render test bUnit cannot express.
/// </summary>
public class SearchTests : BlazorComponentTestContext
{
    private readonly Mock<ISearchService> _searchService = new();
    private readonly Mock<IUserGuildSelectorService> _guildSelectorService = new();

    public SearchTests()
    {
        Services.AddSingleton(_searchService.Object);
        Services.AddSingleton(_guildSelectorService.Object);
        _guildSelectorService
            .Setup(s => s.GetUserGuildsAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GuildSelectorItem>());
    }

    private static UnifiedSearchResultDto EmptyResult(string term) => new() { SearchTerm = term };

    private static SearchCategoryResult Category(params SearchResultItemDto[] items) => new()
    {
        Items = items.ToList(),
        TotalCount = items.Length
    };

    /// <summary>Builds a result with one item in every category, enough to exercise every section and every <c>Highlight</c> call site.</summary>
    private static UnifiedSearchResultDto BuildFullResult(string term) => new()
    {
        SearchTerm = term,
        Guilds = Category(new SearchResultItemDto { Id = "123456789012345678", Title = "Test Guild", BadgeText = "Active" }),
        CommandLogs = Category(new SearchResultItemDto { Id = Guid.NewGuid().ToString(), Title = "/ping", Subtitle = "alice in Test Guild", BadgeText = "Success" }),
        Users = Category(new SearchResultItemDto { Id = "user-1", Title = "Alice", Subtitle = "alice@example.com", BadgeText = "Admin" }),
        Commands = Category(new SearchResultItemDto { Id = "cmd-1", Title = "/ping", Description = "Replies with pong", Url = "/Commands" }),
        Pages = Category(
            new SearchResultItemDto { Id = "page-1", Title = "Settings", Description = "Configure the bot", Url = "/Admin/Settings" },
            new SearchResultItemDto { Id = "page-2", Title = "Soundboard", Description = "Manage sounds", RequiresGuildContext = true, RouteTemplate = "/Guilds/{guildId}/Soundboard" }),
        AuditLogs = Category(new SearchResultItemDto { Id = "audit-1", Title = "Settings changed", Subtitle = "by Alice", Url = "/Admin/AuditLogs/audit-1" }),
        MessageLogs = Category(new SearchResultItemDto { Id = "msg-1", Title = "Hello world this is a long message body used to test truncation", Subtitle = "#general", Url = "/Admin/MessageLogs/msg-1" }),
        Reminders = Category(new SearchResultItemDto { Id = "rem-1", Title = "Stand-up reminder", Url = "/Guilds/1/Reminders" }),
        ScheduledMessages = Category(new SearchResultItemDto { Id = "sched-1", Title = "Weekly announcement", Url = "/Guilds/1/ScheduledMessages" })
    };

    private void SetupSearch(string term, UnifiedSearchResultDto result) =>
        _searchService
            .Setup(s => s.SearchAsync(It.Is<SearchQueryDto>(q => q.SearchTerm == term), It.IsAny<System.Security.Claims.ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    /// <summary>
    /// Renders <see cref="Search"/> with <c>?q=</c> set to <paramref name="term"/> (or omitted
    /// entirely when null) - bUnit refuses to set a <c>[SupplyParameterFromQuery]</c> parameter
    /// via its usual <c>ComponentParameterCollectionBuilder.Add</c> ("To pass a value to a
    /// SupplyParameterFromQuery parameter, use the NavigationManager and navigate to the URI"),
    /// so the query string has to be set on bUnit's fake <see cref="NavigationManager"/> first.
    /// </summary>
    private IRenderedComponent<Search> RenderWithQuery(string? term)
    {
        var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        navMan.NavigateTo(term is null ? "/Search" : navMan.GetUriWithQueryParameter("q", term));
        return Render<Search>();
    }

    [Fact]
    public void NoQuery_ShowsStartSearchingEmptyState_AndDoesNotSearch()
    {
        AddAuthorizedAdmin().SetPolicies("RequireAdmin");
        AddBunitPersistentComponentState();

        var cut = RenderWithQuery(null);

        cut.Find("[data-testid='search-empty-start']").TextContent.Should().Contain("Start searching");
        cut.FindAll("[data-testid='search-validation']").Should().BeEmpty();
        _searchService.Verify(
            s => s.SearchAsync(It.IsAny<SearchQueryDto>(), It.IsAny<System.Security.Claims.ClaimsPrincipal>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void ShortQuery_ShowsValidationMessage_AndDoesNotSearch()
    {
        AddAuthorizedAdmin().SetPolicies("RequireAdmin");
        AddBunitPersistentComponentState();

        var cut = RenderWithQuery("a");

        cut.Find("[data-testid='search-validation']").TextContent.Should().Contain("at least 2 characters");
        _searchService.Verify(
            s => s.SearchAsync(It.IsAny<SearchQueryDto>(), It.IsAny<System.Security.Claims.ClaimsPrincipal>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void NoResults_ShowsNoResultsFoundEmptyState()
    {
        AddAuthorizedAdmin().SetPolicies("RequireAdmin");
        AddBunitPersistentComponentState();
        SetupSearch("nomatch", EmptyResult("nomatch"));

        var cut = RenderWithQuery("nomatch");

        cut.Find("[data-testid='search-empty-none']").TextContent.Should().Contain("No results found");
    }

    [Fact]
    public void Results_RendersEverySectionWithCounts_ForAdmin()
    {
        AddAuthorizedAdmin().SetPolicies("RequireAdmin");
        AddBunitPersistentComponentState();
        SetupSearch("test", BuildFullResult("test"));

        var cut = RenderWithQuery("test");

        foreach (var testId in new[]
                 {
                     "search-section-servers", "search-section-command-logs", "search-section-users",
                     "search-section-commands", "search-section-pages", "search-section-audit-logs",
                     "search-section-message-logs", "search-section-reminders", "search-section-scheduled-messages"
                 })
        {
            cut.Find($"[data-testid='{testId}']").Should().NotBeNull();
        }

        // Each section's Badge count reflects one item per category in BuildFullResult.
        cut.FindComponents<DiscordBot.Bot.Blazor.Shared.Badge>()
            .Select(b => b.Instance.Text)
            .Should().Contain("1");
    }

    [Fact]
    public void Results_CommandLogAndAuditLogRows_RenderLocalTime()
    {
        AddAuthorizedAdmin().SetPolicies("RequireAdmin");
        AddBunitPersistentComponentState();
        var result = BuildFullResult("test");
        result.AuditLogs.Items[0].Timestamp = new DateTime(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);
        SetupSearch("test", result);

        var cut = RenderWithQuery("test");

        // One <LocalTime> for the command log row's ExecutedAt, one for the audit log row's
        // Timestamp - see Search.razor's "Command Log Results List"/"Audit Log Results List"
        // sections, ported off the plain data-utc spans (docs/plans/blazor-port-plan.md Phase 4
        // cluster 4a).
        cut.FindComponents<DiscordBot.Bot.Blazor.Shared.LocalTime>().Should().HaveCount(2);
    }

    [Fact]
    public void Results_HidesAdminOnlySections_ForViewer()
    {
        // No SetPolicies("RequireAdmin") - bUnit's fake authorization fails closed on a policy it
        // was never told about, so AuthorizeAsync(user, "RequireAdmin") resolves to false, same as
        // a real Viewer.
        AddAuthorization().SetAuthorized("viewer").SetRoles("Viewer");
        AddBunitPersistentComponentState();
        SetupSearch("test", BuildFullResult("test"));

        var cut = RenderWithQuery("test");

        // Non-admin sections still render.
        cut.Find("[data-testid='search-section-servers']").Should().NotBeNull();
        cut.Find("[data-testid='search-section-commands']").Should().NotBeNull();
        cut.Find("[data-testid='search-section-pages']").Should().NotBeNull();

        // Admin-only sections (Users, Audit Logs, Message Logs, Reminders, Scheduled Messages) are gone.
        foreach (var testId in new[]
                 {
                     "search-section-users", "search-section-audit-logs",
                     "search-section-message-logs", "search-section-reminders", "search-section-scheduled-messages"
                 })
        {
            cut.FindAll($"[data-testid='{testId}']").Should().BeEmpty();
        }
    }

    [Fact]
    public void Highlight_ReceivesTheConfiguredMaxLengthPerCallSite()
    {
        AddAuthorizedAdmin().SetPolicies("RequireAdmin");
        AddBunitPersistentComponentState();
        SetupSearch("test", BuildFullResult("test"));

        var cut = RenderWithQuery("test");

        var highlights = cut.FindComponents<DiscordBot.Bot.Blazor.Shared.Highlight>();

        // Commands section description: max-length="100".
        highlights.Should().Contain(h => h.Instance.Text == "Replies with pong" && h.Instance.MaxLength == 100 && !h.Instance.ShowContext);

        // Non-guild-scoped page title: no max-length.
        highlights.Should().Contain(h => h.Instance.Text == "Settings" && h.Instance.MaxLength == null);

        // Non-guild-scoped page description: max-length="100".
        highlights.Should().Contain(h => h.Instance.Text == "Configure the bot" && h.Instance.MaxLength == 100);

        // Guild-scoped page title: no max-length.
        highlights.Should().Contain(h => h.Instance.Text == "Soundboard" && h.Instance.MaxLength == null);

        // Audit log title/subtitle: no max-length.
        highlights.Should().Contain(h => h.Instance.Text == "Settings changed" && h.Instance.MaxLength == null);
        highlights.Should().Contain(h => h.Instance.Text == "by Alice" && h.Instance.MaxLength == null);

        // Message log title: max-length="60" show-context="true"; subtitle: no max-length.
        highlights.Should().Contain(h =>
            h.Instance.Text == "Hello world this is a long message body used to test truncation"
            && h.Instance.MaxLength == 60
            && h.Instance.ShowContext);
        highlights.Should().Contain(h => h.Instance.Text == "#general" && h.Instance.MaxLength == null);

        // Reminder title: max-length="50".
        highlights.Should().Contain(h => h.Instance.Text == "Stand-up reminder" && h.Instance.MaxLength == 50);

        // Scheduled message title: max-length="50".
        highlights.Should().Contain(h => h.Instance.Text == "Weekly announcement" && h.Instance.MaxLength == 50);
    }

    [Fact]
    public void PageResult_RequiringGuildContext_RendersGuildContextSelector_WithTheResolvedGuilds()
    {
        AddAuthorizedAdmin().SetPolicies("RequireAdmin");
        AddBunitPersistentComponentState();
        SetupSearch("test", BuildFullResult("test"));
        var guilds = new[] { new GuildSelectorItem { GuildId = "1", GuildName = "My Guild" } };
        _guildSelectorService
            .Setup(s => s.GetUserGuildsAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(guilds);

        var cut = RenderWithQuery("test");

        cut.Find("[data-testid='search-page-result-guild-scoped']").Should().NotBeNull();
        var selector = cut.FindComponent<DiscordBot.Bot.Blazor.Shared.GuildContextSelector>();
        selector.Instance.RouteTemplate.Should().Be("/Guilds/{guildId}/Soundboard");
        selector.Instance.Guilds.Should().BeEquivalentTo(guilds);
    }

    [Fact]
    public void PageResults_WithNoGuildScopedResult_NeverCallsTheGuildSelectorService()
    {
        AddAuthorizedAdmin().SetPolicies("RequireAdmin");
        AddBunitPersistentComponentState();
        SetupSearch("test", new UnifiedSearchResultDto
        {
            SearchTerm = "test",
            Pages = Category(new SearchResultItemDto { Id = "page-1", Title = "Settings", Url = "/Admin/Settings" })
        });

        RenderWithQuery("test");

        _guildSelectorService.Verify(
            s => s.GetUserGuildsAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void SearchForm_Submit_NavigatesToSearchWithTheTypedQuery()
    {
        AddAuthorizedAdmin().SetPolicies("RequireAdmin");
        AddBunitPersistentComponentState();
        // bUnit updates SupplyParameterFromQuery parameters reactively on NavigationManager
        // changes even without a Router in the tree, so submitting re-triggers a real search for
        // the new term - stub it so that second search resolves instead of null-reffing on an
        // unconfigured mock.
        SetupSearch("discord bot", EmptyResult("discord bot"));
        var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();

        var cut = Render<Search>();

        cut.Find("#SearchTerm").Input("discord bot");
        cut.Find("[data-testid='search-form']").Submit();

        navMan.History.Last().Uri.Should().Be("/Search?q=discord%20bot");
    }

    [Fact]
    public void PersistedResult_IsReused_AcrossASimulatedPrerenderToCircuitBoundary()
    {
        var persistentState = AddBunitPersistentComponentState();
        AddAuthorizedAdmin().SetPolicies("RequireAdmin");
        SetupSearch("test", BuildFullResult("test"));

        // First pass ("prerender"): resolves via ISearchService and registers its persisting callback.
        var prerendered = RenderWithQuery("test");
        prerendered.Find("[data-testid='search-section-servers']").Should().NotBeNull();

        // Simulates the end of the prerender phase, where the framework invokes every
        // RegisterOnPersisting callback to serialize state into the page.
        persistentState.TriggerOnPersisting();

        // Second pass ("circuit reconnect"): a fresh component instance, same DI scope - should
        // find the persisted result via TryTakeFromJson instead of calling ISearchService again.
        var reconnected = RenderWithQuery("test");
        reconnected.Find("[data-testid='search-section-servers']").Should().NotBeNull();

        _searchService.Verify(
            s => s.SearchAsync(It.IsAny<SearchQueryDto>(), It.IsAny<System.Security.Claims.ClaimsPrincipal>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "the reconnected instance should restore from persisted state, not search again");
    }
}
