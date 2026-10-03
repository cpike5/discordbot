using System.Net;
using DiscordBot.Core.Entities;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DiscordBot.Tests.Integration;

/// <summary>
/// Review fixes against the real app (offline mode, PostgreSQL): the public leaderboard names no
/// server unless its board is public, guild authorization is decided by the route's guild (and
/// Guilds/Edit has one), and an Admin cannot disable a SuperAdmin.
/// </summary>
[Collection(OfflineAppHostCollection.Name)]
public class GuildAccessAndPrivacyTests : IClassFixture<GuildAccessAndPrivacyTests.AppFixture>
{
    private readonly AppFixture _app;

    public GuildAccessAndPrivacyTests(AppFixture app)
    {
        _app = app;
    }

    // ---- Public leaderboard: no guild data on a state that is not a public board

    [Theory]
    [InlineData(AppFixture.NoSettingsGuildId, AppFixture.NoSettingsGuildName)]
    [InlineData(AppFixture.RatWatchOffGuildId, AppFixture.RatWatchOffGuildName)]
    [InlineData(AppFixture.NotPublicGuildId, AppFixture.NotPublicGuildName)]
    public async Task PublicLeaderboard_ForABoardThatIsNotPublic_NamesNoServer(ulong guildId, string guildName)
    {
        using var anonymous = _app.Host.CreateAnonymousClient();

        var response = await anonymous.GetAsync($"/Guilds/PublicLeaderboard/{guildId}/Leaderboard");
        var html = await response.Content.ReadAsStringAsync();

        html.Should().NotContain(guildName, "an anonymous visitor must not learn which servers exist");
        html.Should().Contain("<title>Hall of Shame</title>", "the title and share cards are generic on these states");
        html.Should().NotContain("og:description\" content=\"View the Hall of Shame leaderboard for");
    }

    [Fact]
    public async Task PublicLeaderboard_ForAnUnknownGuild_LooksTheSameAsARatWatchOffGuild()
    {
        using var anonymous = _app.Host.CreateAnonymousClient();

        var unknown = await anonymous.GetAsync("/Guilds/PublicLeaderboard/999999999999999999/Leaderboard");
        var off = await anonymous.GetAsync($"/Guilds/PublicLeaderboard/{AppFixture.RatWatchOffGuildId}/Leaderboard");

        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        off.StatusCode.Should().Be(unknown.StatusCode);
        (await off.Content.ReadAsStringAsync()).Should().Be(await unknown.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PublicLeaderboard_ForAPublicBoard_StillShowsItsName()
    {
        using var anonymous = _app.Host.CreateAnonymousClient();

        var html = await anonymous.GetStringAsync($"/Guilds/PublicLeaderboard/{AppFixture.PublicGuildId}/Leaderboard");

        html.Should().Contain($"<title>Hall of Shame - {AppFixture.PublicGuildName}</title>");
    }

    // ---- Guild authorization follows the route's guild

    [Fact]
    public void EveryGuildAccessEndpoint_HasAGuildIdRouteParameter()
    {
        var endpoints = _app.Host.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>();

        var withoutGuildId = endpoints
            .Where(e => e.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(a => a.Policy == "GuildAccess"))
            .Where(e => e.RoutePattern.GetParameter("guildId") == null)
            .Select(e => e.RoutePattern.RawText)
            .ToList();

        withoutGuildId.Should().BeEmpty(
            "GuildAccess reads the guild from the route's guildId; a page with another name would be refused for Admins, or worse, decided by the query string");
    }

    [Fact]
    public async Task GuildEdit_IsRoutedByGuildId_AndTheConfigureLinksPointAtIt()
    {
        var html = await _app.Host.Client.GetStringAsync("/Guilds");

        html.Should().Contain($"href=\"/Guilds/Edit/{OfflineAppHost.GuildId}\"");
        (await _app.Host.Client.GetAsync($"/Guilds/Edit/{OfflineAppHost.GuildId}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GuildEdit_CrossGuildPost_WithAnotherGuildInTheQuery_IsDenied_AndChangesNothing()
    {
        using var admin = await _app.Host.CreateSignedInClientAsync("cross-guild-admin@example.com", "Admin");
        var token = OfflineAppHost.ReadAntiforgeryToken(await admin.GetStringAsync("/"));

        // The route names the other guild; the query names one the caller could own
        var response = await admin.PostAsync(
            $"/Guilds/Edit/{AppFixture.OtherGuildId}?guildId={OfflineAppHost.GuildId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Input.IsActive"] = "false",
                ["Input.AudioEnabled"] = "true",
                ["Input.AutoLeaveTimeoutMinutes"] = "5",
                ["__RequestVerificationToken"] = token
            }));

        // Refused: not the save's redirect to the guild's details page (the client follows redirects)
        response.RequestMessage!.RequestUri!.AbsolutePath.Should().NotStartWith("/Guilds/Details");
        await using var scope = _app.Host.Services.CreateAsyncScope();
        var other = await scope.ServiceProvider.GetRequiredService<BotDbContext>()
            .Guilds.AsNoTracking().SingleAsync(g => g.Id == AppFixture.OtherGuildId);
        other.IsActive.Should().BeTrue("the denied request must not deactivate the guild");
    }

    // ---- An Admin cannot disable a SuperAdmin

    [Fact]
    public async Task UsersList_HidesDisable_OnASuperAdminRow_FromAnAdmin_ButNotFromASuperAdmin()
    {
        using var admin = await _app.Host.CreateSignedInClientAsync("list-admin@example.com", "Admin");

        var asAdmin = await admin.GetStringAsync("/Admin/Users");
        var asSuperAdmin = await _app.Host.Client.GetStringAsync("/Admin/Users");

        asAdmin.Should().Contain(OfflineAppHost.AdminEmail, "the SuperAdmin is listed");
        asAdmin.Should().NotContain($"aria-label=\"Disable {OfflineAppHost.AdminEmail}\"");
        asSuperAdmin.Should().Contain("aria-label=\"Disable list-admin@example.com\"");
    }

    [Fact]
    public async Task AnAdmin_PostingToDisableASuperAdmin_IsRefused_AndTheAccountStaysActive()
    {
        using var admin = await _app.Host.CreateSignedInClientAsync("disable-attempt-admin@example.com", "Admin");
        var page = await admin.GetStringAsync("/Admin/Users");
        var superAdminId = await _app.Host.GetAdminUserIdAsync();

        await admin.PostAsync("/Admin/Users?handler=ToggleActive", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["userId"] = superAdminId,
            ["isActive"] = "false",
            ["__RequestVerificationToken"] = OfflineAppHost.ReadAntiforgeryToken(page)
        }));

        await using var scope = _app.Host.Services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<BotDbContext>()
            .Set<ApplicationUser>().AsNoTracking().SingleAsync(u => u.Id == superAdminId);
        stored.IsActive.Should().BeTrue();
    }

    public sealed class AppFixture : IAsyncLifetime
    {
        public const ulong NoSettingsGuildId = 811111111111111111UL;
        public const string NoSettingsGuildName = "Zzyzx Hidden No Settings";
        public const ulong RatWatchOffGuildId = 822222222222222222UL;
        public const string RatWatchOffGuildName = "Zzyzx Hidden Rat Watch Off";
        public const ulong NotPublicGuildId = 833333333333333333UL;
        public const string NotPublicGuildName = "Zzyzx Hidden Not Public";
        public const ulong PublicGuildId = 844444444444444444UL;
        public const string PublicGuildName = "Zzyzx Public Board";
        public const ulong OtherGuildId = 855555555555555555UL;

        public OfflineAppHost Host { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            Host = await OfflineAppHost.StartAsync(Seed);
        }

        public async Task DisposeAsync()
        {
            await Host.DisposeAsync();
        }

        private static Task Seed(BotDbContext db)
        {
            var now = DateTime.UtcNow;

            Guild NewGuild(ulong id, string name) => new()
            {
                Id = id,
                Name = name,
                JoinedAt = now,
                IsActive = true
            };

            GuildRatWatchSettings Settings(ulong id, bool enabled, bool isPublic) => new()
            {
                GuildId = id,
                IsEnabled = enabled,
                PublicLeaderboardEnabled = isPublic,
                CreatedAt = now,
                UpdatedAt = now
            };

            db.Guilds.AddRange(
                NewGuild(NoSettingsGuildId, NoSettingsGuildName),
                NewGuild(RatWatchOffGuildId, RatWatchOffGuildName),
                NewGuild(NotPublicGuildId, NotPublicGuildName),
                NewGuild(PublicGuildId, PublicGuildName),
                NewGuild(OtherGuildId, "Zzyzx Other Guild"));

            db.GuildRatWatchSettings.AddRange(
                Settings(RatWatchOffGuildId, enabled: false, isPublic: true),
                Settings(NotPublicGuildId, enabled: true, isPublic: false),
                Settings(PublicGuildId, enabled: true, isPublic: true));

            return Task.CompletedTask;
        }
    }
}
