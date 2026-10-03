using System.Net;
using System.Text.RegularExpressions;
using DiscordBot.Core.Entities;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DiscordBot.Tests.Integration;

/// <summary>
/// UX plan Phase 6: the Commands tab endpoints answer in the shape the page script reads
/// (problem JSON for a refused request, deep-linkable pagination), and Search shows its
/// validation message, the true total and only links the viewer can open. Runs against the
/// real app on seeded command logs.
/// </summary>
[Collection(OfflineAppHostCollection.Name)]
public class CommandsAndSearchTests : IClassFixture<CommandsAndSearchTests.AppFixture>
{
    private const ulong UserId = 222222222222222222UL;
    private readonly AppFixture _app;

    public CommandsAndSearchTests(AppFixture app)
    {
        _app = app;
    }

    [Fact]
    public async Task Logs_RangeOver90Days_IsRefusedWithProblemJsonTheScriptCanShow()
    {
        var response = await _app.Host.Client.GetAsync("/api/commands/logs?startDate=2026-01-01&endDate=2026-10-01");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"detail\":\"Choose a date range of 90 days or less.\"");
        body.Should().NotContain("<", "the message is plain text, not markup");
    }

    [Fact]
    public async Task Analytics_RangeOver90Days_IsRefusedToo()
    {
        var response = await _app.Host.Client.GetAsync("/api/commands/analytics?startDate=2026-01-01&endDate=2026-10-01");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("90 days or less");
    }

    [Fact]
    public async Task Logs_StartAfterEnd_IsRefused()
    {
        var response = await _app.Host.Client.GetAsync("/api/commands/logs?startDate=2026-10-02&endDate=2026-10-01");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("start date must be on or before the end date");
    }

    [Fact]
    public async Task Logs_PaginationLinksAreRealDeepLinksCarryingTheFilters()
    {
        var html = await _app.Host.Client.GetStringAsync("/api/commands/logs?searchTerm=ping&pageSize=5");

        html.Should().Contain("data-log-id=");
        var next = Regex.Match(html, "<a[^>]*href=\"([^\"]+)\"[^>]*rel=\"next\"|<a[^>]*rel=\"next\"[^>]*href=\"([^\"]+)\"");
        next.Success.Should().BeTrue("there are more logs than one page of 5");
        var href = WebUtility.HtmlDecode(next.Groups[1].Success ? next.Groups[1].Value : next.Groups[2].Value);
        href.Should().StartWith("/Commands?");
        href.Should().Contain("tab=execution-logs").And.Contain("SearchTerm=ping").And.Contain("pageNumber=2");
    }

    [Fact]
    public async Task Logs_PastTheLastPage_ShowsTheLastPageNotNothing()
    {
        var html = await _app.Host.Client.GetStringAsync("/api/commands/logs?searchTerm=ping&pageSize=5&pageNumber=99");

        html.Should().Contain("data-log-id=");
    }

    [Fact]
    public async Task Logs_NothingMatching_ShowsFilteredEmptyStateWithClearFilters()
    {
        var html = await _app.Host.Client.GetStringAsync("/api/commands/logs?searchTerm=zzzznothing");

        html.Should().Contain("No command logs match these filters");
        html.Should().Contain("data-commands-action=\"clear-filters\"");
    }

    [Fact]
    public async Task Commands_Page_RestoresFilterValuesFromTheUrl()
    {
        var html = await _app.Host.Client.GetStringAsync("/Commands?tab=execution-logs&StartDate=2026-09-01&EndDate=2026-09-30&StatusFilter=false&SearchTerm=bob");

        html.Should().Contain("id=\"logs-StartDate\"").And.Contain("value=\"2026-09-01\"");
        html.Should().Contain("value=\"2026-09-30\"");
        html.Should().Contain("value=\"bob\"");
        html.Should().MatchRegex("<option value=\"false\" selected");
    }

    [Fact]
    public async Task Commands_Page_UsesTheQueryStringForTheTab_NotTheHash()
    {
        var html = await _app.Host.Client.GetStringAsync("/Commands?tab=analytics");

        html.Should().Contain("data-persistence-mode=\"none\"");
        html.Should().MatchRegex("data-tab-id=\"analytics\"[^>]*aria-selected=\"true\"|aria-selected=\"true\"[^>]*data-tab-id=\"analytics\"");
    }

    [Fact]
    public async Task Search_ShortTerm_ShowsTheValidationMessage()
    {
        var html = await _app.Host.Client.GetStringAsync("/Search?q=a");

        html.Should().Contain("Please enter at least 2 characters to search.");
        html.Should().Contain("aria-invalid=\"true\"");
        html.Should().NotContain("No results found");
    }

    [Fact]
    public async Task Search_Header_CountsEveryMatch_NotJustTheFirstFivePerSection()
    {
        var html = await _app.Host.Client.GetStringAsync("/Search?q=ping");

        var match = Regex.Match(html, @"([\d,]+) results? for");
        match.Success.Should().BeTrue();
        int.Parse(match.Groups[1].Value.Replace(",", "")).Should().BeGreaterThanOrEqualTo(12, "12 command logs match and a section lists only 5");
    }

    [Fact]
    public async Task Search_CommandLogLinks_AdminOpensTheDetailsPage_ViewerOpensTheLogDialog()
    {
        var adminHtml = await _app.Host.Client.GetStringAsync("/Search?q=ping");
        adminHtml.Should().MatchRegex("href=\"/CommandLogs/Details/[0-9a-f-]{36}\\?returnUrl=%2FSearch%3Fq%3Dping\"");

        using var viewer = await _app.Host.CreateSignedInClientAsync("viewer-search@example.com", "Viewer");
        var viewerHtml = await viewer.GetStringAsync("/Search?q=ping");
        viewerHtml.Should().NotContain("/CommandLogs/Details/", "a viewer cannot open that page");
        viewerHtml.Should().MatchRegex("href=\"/Commands\\?tab=execution-logs&amp;log=[0-9a-f-]{36}\"");
    }

    [Fact]
    public async Task CommandLogDetails_BackLink_ReturnsToTheViewTheUserCameFrom()
    {
        var id = await FirstLogIdAsync();

        var withReturn = await _app.Host.Client.GetStringAsync($"/CommandLogs/Details/{id}?returnUrl=%2FCommands%3Ftab%3Dexecution-logs%26pageNumber%3D3");
        withReturn.Should().Contain("href=\"/Commands?tab=execution-logs&amp;pageNumber=3\"");

        var foreign = await _app.Host.Client.GetStringAsync($"/CommandLogs/Details/{id}?returnUrl=https%3A%2F%2Fevil.example%2F");
        foreign.Should().NotContain("evil.example");
        foreign.Should().Contain("href=\"/Commands?tab=execution-logs\" class=\"btn btn-secondary\"");
    }

    [Fact]
    public async Task LogDetailsPartial_UsesPlainIdsForThePreviewTriggers()
    {
        var id = await FirstLogIdAsync();

        var html = await _app.Host.Client.GetStringAsync($"/api/commands/log-details/{id}");

        html.Should().Contain($"data-user-id=\"{UserId}\"");
        html.Should().NotContain("data-user-id=\"'", "snowflakes are plain strings, not quoted literals");
        html.Should().Contain("data-log-details-link");
    }

    private async Task<Guid> FirstLogIdAsync()
    {
        using var scope = _app.Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BotDbContext>();
        return context.CommandLogs.OrderBy(l => l.ExecutedAt).Select(l => l.Id).First();
    }

    public sealed class AppFixture : IAsyncLifetime
    {
        public OfflineAppHost Host { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            Host = await OfflineAppHost.StartAsync(async context =>
            {
                context.Users.Add(new User
                {
                    Id = UserId,
                    Username = "bob",
                    FirstSeenAt = DateTime.UtcNow,
                    LastSeenAt = DateTime.UtcNow
                });
                await context.SaveChangesAsync();

                for (var i = 0; i < 12; i++)
                {
                    context.CommandLogs.Add(new CommandLog
                    {
                        Id = Guid.NewGuid(),
                        GuildId = OfflineAppHost.GuildId,
                        UserId = UserId,
                        CommandName = "ping",
                        ExecutedAt = DateTime.UtcNow.AddMinutes(-i),
                        ResponseTimeMs = 40 + i,
                        Success = i % 4 != 0
                    });
                }
            });
        }

        public async Task DisposeAsync()
        {
            await Host.DisposeAsync();
        }
    }
}
