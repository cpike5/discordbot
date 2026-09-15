using Bunit;
using Bunit.TestDoubles;
using DiscordBot.Bot.Blazor.Pages.Admin.AuditLogs;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DiscordBot.ComponentTests.Blazor.Pages.Admin.AuditLogs;

/// <summary>
/// Component tests for <see cref="Details"/>, the routable replacement for
/// <c>Pages/Admin/AuditLogs/Details.cshtml</c> + <c>DetailsModel</c>
/// (docs/plans/blazor-port-plan.md Phase 4 cluster 4a). Covers: every card rendering, the
/// not-found EmptyState, related entries, the raw-data expand/collapse toggle, copy-to-clipboard
/// (via <c>JSInterop</c>), and the JSON export (asserts the built JSON and the
/// <c>downloadFile</c> interop call). <c>JSInterop</c> is left in its default loose mode for
/// calls this suite doesn't assert on (e.g. <c>convertLocalTimes</c>), matching
/// <c>SearchTests</c>' approach - only <c>copyToClipboard</c>/<c>downloadFile</c> tests configure
/// the module explicitly, to capture and assert on the invocation.
/// </summary>
public class DetailsTests : BlazorComponentTestContext
{
    private readonly Mock<IAuditLogService> _auditLogService = new();

    public DetailsTests()
    {
        Services.AddSingleton(_auditLogService.Object);
    }

    private static AuditLogDto BuildLog(long id = 1, string? correlationId = null, ulong? guildId = null) => new()
    {
        Id = id,
        Timestamp = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc),
        Category = AuditLogCategory.User,
        CategoryName = "User",
        Action = AuditLogAction.Updated,
        ActionName = "Updated",
        ActorId = "actor-1",
        ActorType = AuditLogActorType.User,
        ActorTypeName = "User",
        ActorDisplayName = "Alice",
        TargetType = "User",
        TargetId = "target-1",
        GuildId = guildId,
        GuildName = guildId.HasValue ? "Test Guild" : null,
        Details = "{\"field\":\"value\"}",
        IpAddress = "127.0.0.1",
        CorrelationId = correlationId
    };

    private IRenderedComponent<Details> RenderDetails(long id, string? returnUrl = null)
    {
        AddBunitPersistentComponentState();
        SetInteractiveRendererInfo();

        // ReturnUrl is [SupplyParameterFromQuery] - bUnit requires setting it via the query
        // string (NavigationManager), not ComponentParameterCollectionBuilder.Add, which only
        // works for a plain [Parameter]. Id is a route parameter ({id:long}), so it's still set
        // directly below.
        if (returnUrl is not null)
        {
            var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
            navMan.NavigateTo(navMan.GetUriWithQueryParameter("returnUrl", returnUrl));
        }

        return Render<Details>(p => p.Add(c => c.Id, id));
    }

    [Fact]
    public void EntryFound_RendersActorTargetAndMetadataCards()
    {
        _auditLogService.Setup(s => s.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(BuildLog(1, guildId: 42));

        var cut = RenderDetails(1);

        cut.Markup.Should().Contain("Alice");
        cut.Markup.Should().Contain("Actor Information");
        cut.Markup.Should().Contain("Target Resource");
        cut.Markup.Should().Contain("Request Metadata");
        cut.Markup.Should().Contain("Test Guild");
        cut.Markup.Should().Contain("127.0.0.1");
    }

    [Fact]
    public void EntryNotFound_RendersEmptyState()
    {
        _auditLogService.Setup(s => s.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((AuditLogDto?)null);

        var cut = RenderDetails(99);

        cut.Markup.Should().Contain("Audit Entry Not Found");
        cut.Markup.Should().NotContain("Actor Information");
    }

    [Fact]
    public void EntryWithCorrelationId_RendersRelatedEntries()
    {
        var log = BuildLog(1, correlationId: "corr-1");
        var related = BuildLog(2, correlationId: "corr-1");
        _auditLogService.Setup(s => s.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(log);
        _auditLogService.Setup(s => s.GetByCorrelationIdAsync("corr-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { log, related });

        var cut = RenderDetails(1);

        cut.Markup.Should().Contain("Related Entries");
        cut.Markup.Should().Contain("/Admin/AuditLogs/Details/2");
    }

    [Fact]
    public void RawDataToggle_ExpandsAndCollapses()
    {
        _auditLogService.Setup(s => s.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(BuildLog(1));

        var cut = RenderDetails(1);

        cut.Find("[aria-expanded]").GetAttribute("aria-expanded").Should().Be("false");
        cut.Find("[aria-expanded]").Click();
        cut.Find("[aria-expanded]").GetAttribute("aria-expanded").Should().Be("true");
    }

    [Fact]
    public void CopyEntryId_InvokesClipboardInterop()
    {
        _auditLogService.Setup(s => s.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(BuildLog(1));
        var copyHandler = JSInterop.SetupModule("./js/blazor/browser.js").Setup<bool>("copyToClipboard", _ => true);
        copyHandler.SetResult(true);

        var cut = RenderDetails(1);
        cut.Find("[aria-label='Copy entry ID to clipboard']").Click();

        copyHandler.Invocations.Should().ContainSingle(inv => (string)inv.Arguments[0]! == "1");
    }

    [Fact]
    public void ExportJson_InvokesDownloadFile_WithExpectedFileNameAndEntryId()
    {
        _auditLogService.Setup(s => s.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(BuildLog(1, guildId: 42));
        var downloadHandler = JSInterop.SetupModule("./js/blazor/browser.js").SetupVoid("downloadFile", _ => true);

        var cut = RenderDetails(1);
        cut.Find("[aria-label='Export this entry as JSON']").Click();

        downloadHandler.Invocations.Should().ContainSingle();
        var invocation = downloadHandler.Invocations.Single();
        invocation.Arguments.Should().HaveCount(3);
        invocation.Arguments[0].Should().Be("audit-entry-1.json");
        var json = (string)invocation.Arguments[1]!;
        json.Should().Contain("\"entryId\": 1");
        json.Should().Contain("\"guild\"");
        json.Should().Contain("Test Guild");
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://evil.example")]
    [InlineData("//evil.example")]
    public void UnsafeReturnUrl_FallsBackToLogsPage(string unsafeReturnUrl)
    {
        _auditLogService.Setup(s => s.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(BuildLog(1));

        var cut = RenderDetails(1, unsafeReturnUrl);

        cut.Markup.Should().NotContain(unsafeReturnUrl);
        cut.Markup.Should().Contain("/Admin/Logs?tab=audit");
    }

    [Fact]
    public void SafeReturnUrl_IsRendered()
    {
        _auditLogService.Setup(s => s.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(BuildLog(1));

        var cut = RenderDetails(1, "/Admin/Logs?tab=audit&page=2");

        cut.Markup.Should().Contain("/Admin/Logs?tab=audit&amp;page=2");
    }

    /// <summary>
    /// Declared route authorization - a component-level <c>[Authorize]</c> attribute only takes
    /// effect through the real router's <c>AuthorizeRouteView</c>, which a direct bUnit render
    /// bypasses, so this asserts the policy declaration itself rather than simulating routing.
    /// Reachability for a real Moderator vs. Admin is covered end to end by
    /// <c>Test_S_LogDetails_RenderForSeededRows</c> (E2E, logs in as SuperAdmin) and the sibling
    /// <c>CommandLogs.DetailsTests</c> asserts the contrasting <c>RequireModerator</c> policy on
    /// that page.
    /// </summary>
    [Fact]
    public void Page_RequiresAdminPolicy()
    {
        var attribute = typeof(Details).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Should().ContainSingle().Subject;

        attribute.Policy.Should().Be("RequireAdmin");
    }
}
