using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DiscordBot.Core.Entities;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace DiscordBot.Tests.Integration;

/// <summary>
/// Phase 9 of the UX polish plan against the real app (offline mode, PostgreSQL): the member
/// portal opens for the seeded development member (D15), the voice panel's endpoints serve a
/// member who has no Identity role (D10), and EnableMemberPortal is enforced independently of
/// AudioEnabled (issue #947).
/// </summary>
[Collection(OfflineAppHostCollection.Name)]
public class MemberPortalTests : IClassFixture<MemberPortalTests.AppFixture>
{
    private const ulong GuildId = OfflineAppHost.GuildId;

    private readonly AppFixture _app;

    public MemberPortalTests(AppFixture app)
    {
        _app = app;
    }

    [Theory]
    [InlineData("/Portal/Soundboard/{0}")]
    [InlineData("/Portal/TTS/{0}")]
    [InlineData("/Portal/VOX/{0}")]
    public async Task SeededMember_OpensEveryPortalPage(string urlFormat)
    {
        var html = await _app.Member.GetStringAsync(string.Format(urlFormat, GuildId));

        html.Should().Contain("Smoke Test Guild Portal", "the portal header is what an authorized member sees");
        html.Should().NotContain("Verify Your Membership");
        html.Should().NotContain("sign in with Discord", "a member is signed in");
    }

    [Theory]
    [InlineData("/Portal/Soundboard/{0}")]
    [InlineData("/Portal/TTS/{0}")]
    [InlineData("/Portal/VOX/{0}")]
    public async Task SeededAdmin_OpensEveryPortalPage(string urlFormat)
    {
        var html = await _app.Host.Client.GetStringAsync(string.Format(urlFormat, GuildId));

        html.Should().Contain("Smoke Test Guild Portal");
    }

    [Fact]
    public async Task Anonymous_SeesTheLandingPage()
    {
        using var anonymous = _app.Host.CreateAnonymousClient();

        var response = await anonymous.GetAsync($"/Portal/Soundboard/{GuildId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Sign in with Discord");
    }

    [Fact]
    public async Task VoicePanel_OnPortalPages_UsesThePortalEndpoints()
    {
        foreach (var page in new[] { "Soundboard", "TTS", "VOX" })
        {
            var html = await _app.Member.GetStringAsync($"/Portal/{page}/{GuildId}");

            html.Should().Contain($"data-api-base=\"/api/portal/soundboard/{GuildId}\"", $"{page} must not use the Viewer-gated admin endpoints");
        }
    }

    [Fact]
    public async Task Member_CanReadVoiceStatusAndChannels_WithoutAnIdentityRole()
    {
        var status = await _app.Member.GetAsync($"/api/portal/soundboard/{GuildId}/status");
        status.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var json = JsonDocument.Parse(await status.Content.ReadAsStringAsync()))
        {
            json.RootElement.GetProperty("isConnected").GetBoolean().Should().BeFalse();
        }

        var channels = await _app.Member.GetAsync($"/api/portal/soundboard/{GuildId}/channels");
        channels.StatusCode.Should().Be(HttpStatusCode.OK);
        using var list = JsonDocument.Parse(await channels.Content.ReadAsStringAsync());
        list.RootElement.EnumerateArray().Select(c => c.GetProperty("name").GetString())
            .Should().Equal("General", "Gaming", "Music Lounge");
    }

    [Fact]
    public async Task Member_JoiningOffline_GetsAReadableRefusal_NotForbidden()
    {
        var response = await _app.Member.PostAsJsonAsync(
            $"/api/portal/soundboard/{GuildId}/channel",
            new { channelId = 900000000000000201UL });

        // There is no gateway offline, so the join cannot succeed; what matters is that the
        // member is let in (not 401/403) and told why in plain language.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().Contain("channel_not_found");
    }

    [Theory]
    [InlineData("/api/portal/tts/presets")]
    [InlineData("/api/portal/tts/voices/en-US-JennyNeural/capabilities")]
    public async Task Member_CanReachTheGuildLessTtsReferenceEndpoints(string url)
    {
        // Reference data with no {guildId} in the route: the page needs these, so a 403 here would
        // silently break presets and voice styles for every member
        var response = await _app.Member.GetAsync(url);

        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Member_IsNotGivenTheViewerOnlyAdminAudioEndpoints()
    {
        var response = await _app.Member.PostAsync($"/api/guilds/{GuildId}/audio/stop", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, "D10: the portal never grants members the Viewer role");
    }

    [Fact]
    public async Task PortalSwitchedOff_ShowsAFriendlyPage_ToMemberAndAdminAlike()
    {
        foreach (var client in new[] { _app.Member, _app.Host.Client })
        {
            foreach (var page in new[] { "Soundboard", "TTS", "VOX" })
            {
                var response = await client.GetAsync($"/Portal/{page}/{AppFixture.PortalOffGuildId}");

                response.StatusCode.Should().Be(HttpStatusCode.OK);
                var html = await response.Content.ReadAsStringAsync();
                html.Should().Contain("The member portal is off");
                html.Should().NotContain("Verify Your Membership");
            }
        }
    }

    [Fact]
    public async Task PortalSwitchedOff_RefusesTheApi_WithAReason()
    {
        var response = await _app.Member.GetAsync($"/api/portal/soundboard/{AppFixture.PortalOffGuildId}/status");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).Should().Contain("switched off");
    }

    [Fact]
    public async Task PortalSwitchedOff_AlsoRefusesAdminsOnTheApi()
    {
        var response = await _app.Host.Client.GetAsync($"/api/portal/soundboard/{AppFixture.PortalOffGuildId}/status");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AudioOffButPortalOn_StillOpens_WithANotice()
    {
        var html = await _app.Member.GetStringAsync($"/Portal/Soundboard/{AppFixture.AudioOffGuildId}");

        html.Should().Contain("Audio is switched off for this server");
        html.Should().NotContain("The member portal is off");
    }

    /// <summary>The app plus a guild whose portal is off and one whose audio is off.</summary>
    public sealed class AppFixture : IAsyncLifetime
    {
        public const ulong PortalOffGuildId = 223456789012345678UL;
        public const ulong AudioOffGuildId = 323456789012345678UL;

        public OfflineAppHost Host { get; private set; } = null!;

        public HttpClient Member { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            Host = await OfflineAppHost.StartAsync(db =>
            {
                db.Guilds.Add(new Guild { Id = PortalOffGuildId, Name = "Portal Off Guild", JoinedAt = DateTime.UtcNow, IsActive = true });
                db.GuildAudioSettings.Add(new GuildAudioSettings
                {
                    GuildId = PortalOffGuildId,
                    AudioEnabled = true,
                    EnableMemberPortal = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });

                db.Guilds.Add(new Guild { Id = AudioOffGuildId, Name = "Audio Off Guild", JoinedAt = DateTime.UtcNow, IsActive = true });
                db.GuildAudioSettings.Add(new GuildAudioSettings
                {
                    GuildId = AudioOffGuildId,
                    AudioEnabled = false,
                    EnableMemberPortal = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                return Task.CompletedTask;
            });
            Member = await Host.CreatePortalMemberClientAsync();
        }

        public async Task DisposeAsync()
        {
            Member.Dispose();
            await Host.DisposeAsync();
        }
    }
}
