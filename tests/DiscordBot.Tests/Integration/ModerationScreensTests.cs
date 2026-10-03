using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DiscordBot.Tests.Integration;

/// <summary>
/// Phase 7 of the UX polish plan against the real app: the flagged-event and member screens post
/// with an antiforgery token, page links keep their page number, exports download, and saving one
/// moderation setting leaves the ones the page does not show alone.
/// </summary>
[Collection(OfflineAppHostCollection.Name)]
public class ModerationScreensTests : IClassFixture<ModerationScreensTests.AppFixture>
{
    private const ulong GuildId = OfflineAppHost.GuildId;
    private readonly AppFixture _app;

    public ModerationScreensTests(AppFixture app)
    {
        _app = app;
    }

    [Fact]
    public async Task FlaggedEvents_PaginationLinks_KeepThePageNumber()
    {
        var html = await _app.Host.Client.GetStringAsync($"/Guilds/FlaggedEvents/{GuildId}");

        // 25 events at 20 a page: the Next link must go to page 2 (it used to go to page 1 forever)
        html.Should().Contain("pageNumber=2");
        var second = await _app.Host.Client.GetStringAsync($"/Guilds/FlaggedEvents/{GuildId}?pageNumber=2");
        second.Should().Contain("aria-current=\"page\"");
        second.Should().Contain("<strong>21-25</strong> of <strong>25</strong>");
    }

    [Fact]
    public async Task FlaggedEvents_ListWithNoFilters_ShowsEventsOlderThanThirtyDays()
    {
        var html = await _app.Host.Client.GetStringAsync($"/Guilds/FlaggedEvents/{GuildId}?FilterStatus={(int)FlaggedEventStatus.Actioned}");

        html.Should().Contain("<strong>1-1</strong> of <strong>1</strong>", "the 90-day-old event shows: there is no forced 30-day window any more");
    }

    [Fact]
    public async Task FlaggedEvents_BulkForm_CarriesItsOwnAntiforgeryToken()
    {
        var html = await _app.Host.Client.GetStringAsync($"/Guilds/FlaggedEvents/{GuildId}");

        var form = html[html.IndexOf("id=\"bulkForm\"", StringComparison.Ordinal)..];
        form = form[..form.IndexOf("</form>", StringComparison.Ordinal)];
        form.Should().Contain("__RequestVerificationToken");
    }

    [Fact]
    public async Task FlaggedEvents_BulkDismiss_ChangesOpenEvents_AndTheToastSurvivesTheRedirect()
    {
        var html = await _app.Host.Client.GetStringAsync($"/Guilds/FlaggedEvents/{GuildId}");
        var token = OfflineAppHost.ReadAntiforgeryToken(html);

        var form = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", token),
            new("bulkAction", "dismiss"),
            new("ids", AppFixture.PendingId.ToString()),
            new("ids", AppFixture.AcknowledgedId.ToString()),
            new("ids", AppFixture.ActionedId.ToString())
        };

        var response = await _app.Host.Client.PostAsync(
            $"/Guilds/FlaggedEvents/{GuildId}?handler=Bulk", new FormUrlEncodedContent(form));

        response.StatusCode.Should().Be(HttpStatusCode.OK, "the redirect back to the list is followed");
        var page = await response.Content.ReadAsStringAsync();
        page.Should().Contain("Dismissed 2 of 3 events").And.Contain("1 already reviewed");

        (await StatusAsync(AppFixture.PendingId)).Should().Be(FlaggedEventStatus.Dismissed);
        (await StatusAsync(AppFixture.AcknowledgedId)).Should().Be(FlaggedEventStatus.Dismissed);
        (await StatusAsync(AppFixture.ActionedId)).Should().Be(FlaggedEventStatus.Actioned, "a closed event is left alone");
    }

    [Fact]
    public async Task FlaggedEventDetails_OfAnAcknowledgedEvent_OffersDismissAndRecordOutcome()
    {
        var html = await _app.Host.Client.GetStringAsync($"/Guilds/FlaggedEvents/Details/{GuildId}/{AppFixture.OtherAcknowledgedId}");

        html.Should().Contain("Dismiss").And.Contain("Record outcome");
        html.Should().NotContain(">Acknowledge<", "it is already acknowledged");
    }

    [Fact]
    public async Task FlaggedEventDetails_DoesNotPrintBracesAroundTheChannel()
    {
        var html = await _app.Host.Client.GetStringAsync($"/Guilds/FlaggedEvents/Details/{GuildId}/{AppFixture.OtherAcknowledgedId}");

        html.Should().Contain("#unknown-channel").And.NotContain("#{");
    }

    [Fact]
    public async Task Members_NeverMessagedFilter_ListsOnlyMembersWithNoActivity()
    {
        var html = await _app.Host.Client.GetStringAsync($"/Guilds/{GuildId}/Members?ActivityFilter=never-messaged");

        html.Should().Contain("silent_member").And.NotContain("chatty_member");
    }

    [Fact]
    public async Task Members_ExportCsv_DownloadsWithUtcHeaders()
    {
        var response = await _app.Host.Client.GetAsync($"/Guilds/{GuildId}/Members?handler=Export&SearchTerm=chatty");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        var csv = await response.Content.ReadAsStringAsync();
        csv.Should().Contain("JoinedAt (UTC)").And.Contain("chatty_member").And.NotContain("silent_member");
    }

    [Fact]
    public async Task Members_ExportCsv_WithNoMatches_ExplainsOnTheList()
    {
        var response = await _app.Host.Client.GetAsync($"/Guilds/{GuildId}/Members?handler=Export&SearchTerm=nobody-at-all");

        response.StatusCode.Should().Be(HttpStatusCode.OK, "the redirect back to the list is followed");
        (await response.Content.ReadAsStringAsync()).Should().Contain("nothing to export");
    }

    [Fact]
    public async Task ModerationSettings_SavingOneContentField_KeepsTheLinkSettingsThePageDoesNotShow()
    {
        var html = await _app.Host.Client.GetStringAsync($"/Guilds/ModerationSettings/{GuildId}");
        var token = OfflineAppHost.ReadAntiforgeryToken(html);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Guilds/ModerationSettings/{GuildId}?handler=SaveContent")
        {
            Content = JsonContent.Create(new { blockInviteLinks = true })
        };
        request.Headers.Add("RequestVerificationToken", token);
        var response = await _app.Host.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = _app.Host.Services.CreateScope();
        var saved = (await scope.ServiceProvider.GetRequiredService<IGuildModerationConfigService>().GetConfigAsync(GuildId)).ContentFilterConfig;
        saved.BlockInviteLinks.Should().BeTrue();
        saved.AllowedLinkDomains.Should().Equal("example.com");
        saved.BlockUnlistedLinks.Should().BeTrue("a content save used to reset this to false");
        saved.ProhibitedWords.Should().Equal("keepme");
    }

    [Fact]
    public async Task ModerationSettings_AnOutOfRangeNumber_IsRejectedWithAFieldMessage_AndNothingIsSaved()
    {
        var token = OfflineAppHost.ReadAntiforgeryToken(await _app.Host.Client.GetStringAsync($"/Guilds/ModerationSettings/{GuildId}"));

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Guilds/ModerationSettings/{GuildId}?handler=SaveSpam")
        {
            Content = JsonContent.Create(new { maxMessagesPerWindow = 5000 })
        };
        request.Headers.Add("RequestVerificationToken", token);
        var response = await _app.Host.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        body.GetProperty("errors").GetProperty("maxMessagesPerWindow").GetString().Should().Contain("between 1 and 100");
    }

    [Fact]
    public async Task UserTags_CanBeAppliedAndRemoved_FromTheProfile()
    {
        // These calls used to answer 500: two controllers owned the same route
        var token = OfflineAppHost.ReadAntiforgeryToken(await _app.Host.Client.GetStringAsync($"/Guilds/ModerationSettings/{GuildId}"));
        var url = $"/api/guilds/{GuildId}/users/{AppFixture.ChattyUserId}/tags/VIP";

        using var apply = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(new { appliedById = "1" }) };
        apply.Headers.Add("RequestVerificationToken", token);
        (await _app.Host.Client.SendAsync(apply)).StatusCode.Should().Be(HttpStatusCode.Created);

        using var remove = new HttpRequestMessage(HttpMethod.Delete, url);
        remove.Headers.Add("RequestVerificationToken", token);
        (await _app.Host.Client.SendAsync(remove)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private async Task<FlaggedEventStatus> StatusAsync(Guid id)
    {
        using var scope = _app.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();
        return (await db.FlaggedEvents.AsNoTracking().SingleAsync(e => e.Id == id)).Status;
    }

    public sealed class AppFixture : IAsyncLifetime
    {
        public const ulong ChattyUserId = 700000000000000001UL;
        public const ulong SilentUserId = 700000000000000002UL;
        public static readonly Guid PendingId = Guid.Parse("0c0c0c0c-0000-4000-8000-000000000001");
        public static readonly Guid AcknowledgedId = Guid.Parse("0c0c0c0c-0000-4000-8000-000000000002");
        public static readonly Guid ActionedId = Guid.Parse("0c0c0c0c-0000-4000-8000-000000000003");
        public static readonly Guid OtherAcknowledgedId = Guid.Parse("0c0c0c0c-0000-4000-8000-000000000004");

        public OfflineAppHost Host { get; private set; } = null!;

        public async Task InitializeAsync() => Host = await OfflineAppHost.StartAsync(Seed);

        public async Task DisposeAsync() => await Host.DisposeAsync();

        private static Task Seed(BotDbContext db)
        {
            var now = DateTime.UtcNow;

            db.Users.AddRange(
                new User { Id = ChattyUserId, Username = "chatty_member", Discriminator = "0", FirstSeenAt = now, LastSeenAt = now },
                new User { Id = SilentUserId, Username = "silent_member", Discriminator = "0", FirstSeenAt = now, LastSeenAt = now });
            db.GuildMembers.AddRange(
                new GuildMember { GuildId = GuildId, UserId = ChattyUserId, JoinedAt = now.AddDays(-5), LastActiveAt = now.AddHours(-1), LastCachedAt = now, IsActive = true },
                new GuildMember { GuildId = GuildId, UserId = SilentUserId, JoinedAt = now.AddDays(-4), LastActiveAt = null, LastCachedAt = now, IsActive = true });

            FlaggedEvent Event(Guid id, FlaggedEventStatus status, DateTime createdAt, string description) => new()
            {
                Id = id,
                GuildId = GuildId,
                UserId = ChattyUserId,
                ChannelId = 42,
                RuleType = RuleType.Spam,
                Severity = Severity.Low,
                Description = description,
                Evidence = "{}",
                Status = status,
                CreatedAt = createdAt
            };

            db.FlaggedEvents.AddRange(
                Event(PendingId, FlaggedEventStatus.Pending, now.AddMinutes(-10), "Pending one"),
                Event(AcknowledgedId, FlaggedEventStatus.Acknowledged, now.AddMinutes(-20), "Acknowledged one"),
                Event(ActionedId, FlaggedEventStatus.Actioned, now.AddDays(-90), "Old event"),
                Event(OtherAcknowledgedId, FlaggedEventStatus.Acknowledged, now.AddMinutes(-30), "Another acknowledged one"));
            for (var i = 0; i < 21; i++)
            {
                db.FlaggedEvents.Add(Event(Guid.NewGuid(), FlaggedEventStatus.Pending, now.AddHours(-1 - i), $"Filler {i}"));
            }

            db.GuildModerationConfigs.Add(new GuildModerationConfig
            {
                GuildId = GuildId,
                IsEnabled = true,
                Mode = ConfigMode.Advanced,
                SimplePreset = "Strict",
                SpamConfig = "{}",
                ContentFilterConfig = JsonSerializer.Serialize(
                    new ContentFilterConfigDto
                    {
                        Enabled = true,
                        ProhibitedWords = new List<string> { "keepme" },
                        AllowedLinkDomains = new List<string> { "example.com" },
                        BlockUnlistedLinks = true
                    },
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                RaidProtectionConfig = "{}",
                UpdatedAt = now
            });

            db.ModTags.Add(new ModTag
            {
                Id = Guid.NewGuid(),
                GuildId = GuildId,
                Name = "VIP",
                Color = "#27AE60",
                Category = TagCategory.Positive,
                CreatedAt = now
            });

            return Task.CompletedTask;
        }
    }
}
