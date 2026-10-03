using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace DiscordBot.Tests.Integration;

/// <summary>
/// The Admin Logs page-size selector must name the parameter the page binds, keep the chosen size in
/// the page links, and not turn the default date range into a user filter on page 2.
/// </summary>
[Collection(OfflineAppHostCollection.Name)]
public class LogsPaginationTests : IClassFixture<LogsPaginationTests.AppFixture>
{
    private readonly AppFixture _app;

    public LogsPaginationTests(AppFixture app)
    {
        _app = app;
    }

    [Fact]
    public async Task AuditTab_PageSizeSelector_SubmitsTheParameterThePageBinds()
    {
        var html = await _app.Host.Client.GetStringAsync("/Admin/Logs?tab=audit");

        html.Should().Contain("name=\"AuditPageSize\"");
        html.Should().NotContain("name=\"pageSize\"");
    }

    [Fact]
    public async Task AuditTab_PageLinks_KeepThePageSizeAndLeaveTheDefaultDatesOut()
    {
        var html = await _app.Host.Client.GetStringAsync("/Admin/Logs?tab=audit&AuditPageSize=10");

        var link = LinkTo(html, "auditPageNumber=2");
        link.Should().Contain("AuditPageSize=10");
        link.Should().NotContain("AuditStartDate").And.NotContain("AuditEndDate",
            "the default range is not a filter the user chose, and writing it into the link would make it one");
    }

    [Fact]
    public async Task AuditTab_PageSize_ChangesHowManyRowsShow()
    {
        var small = await _app.Host.Client.GetStringAsync("/Admin/Logs?tab=audit&AuditPageSize=10");
        var large = await _app.Host.Client.GetStringAsync("/Admin/Logs?tab=audit&AuditPageSize=50");

        small.Should().Contain("auditPageNumber=2");
        large.Should().NotContain("auditPageNumber=2", "all 30 entries fit on one page of 50");
    }

    [Fact]
    public async Task AuditTab_TooSmallPageSize_IsRaisedToTheMinimumNotRejected()
    {
        // Unclamped, a page size of 1 would make 30+ pages (the 30 seeded entries plus the sign-in's own)
        var response = await _app.Host.Client.GetAsync("/Admin/Logs?tab=audit&AuditPageSize=1");

        response.IsSuccessStatusCode.Should().BeTrue();
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("auditPageNumber=3").And.NotContain("auditPageNumber=5");
        html.Should().Contain("<option value=\"10\" selected");
    }

    [Fact]
    public async Task AuditTab_AbsurdPageSize_IsClampedNotRejected()
    {
        var response = await _app.Host.Client.GetAsync("/Admin/Logs?tab=audit&AuditPageSize=100000");

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task MessagesTab_PageSizeSelector_SubmitsTheParameterThePageBinds()
    {
        var html = await _app.Host.Client.GetStringAsync("/Admin/Logs?tab=messages");

        html.Should().NotContain("name=\"pageSize\"");
    }

    /// <summary>The first anchor whose href contains <paramref name="fragment"/>.</summary>
    private static string LinkTo(string html, string fragment)
    {
        var at = html.IndexOf(fragment, StringComparison.Ordinal);
        at.Should().BeGreaterThan(-1, $"a link containing {fragment} must be on the page");
        var start = html.LastIndexOf("href=\"", at, StringComparison.Ordinal);
        var end = html.IndexOf('"', start + 6);
        return System.Net.WebUtility.HtmlDecode(html[(start + 6)..end]);
    }

    public sealed class AppFixture : IAsyncLifetime
    {
        public OfflineAppHost Host { get; private set; } = null!;

        public async Task InitializeAsync() => Host = await OfflineAppHost.StartAsync(Seed);

        public async Task DisposeAsync() => await Host.DisposeAsync();

        private static Task Seed(BotDbContext db)
        {
            var now = DateTime.UtcNow;
            for (var i = 0; i < 30; i++)
            {
                db.AuditLogs.Add(new AuditLog
                {
                    Timestamp = now.AddMinutes(-i),
                    Category = AuditLogCategory.User,
                    Action = AuditLogAction.Created,
                    ActorType = AuditLogActorType.User,
                    ActorId = "actor-1",
                    TargetType = "Thing",
                    TargetId = i.ToString(),
                    GuildId = OfflineAppHost.GuildId,
                    Details = $"Entry {i}"
                });
            }

            return db.SaveChangesAsync();
        }
    }
}
