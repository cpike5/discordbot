using System.Net;
using System.Text.Json;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace DiscordBot.Tests.Integration;

/// <summary>
/// Phase 1 of the UX polish plan: scripts get status codes and JSON rather than the sign-in
/// page's HTML, and every status code has a themed error page that names the failed address.
/// Runs against the real app (<see cref="OfflineAppHost"/>).
/// </summary>
[Collection(OfflineAppHostCollection.Name)]
public class FeedbackPlumbingTests : IClassFixture<FeedbackPlumbingTests.AppFixture>
{
    private readonly AppFixture _app;

    public FeedbackPlumbingTests(AppFixture app)
    {
        _app = app;
    }

    [Fact]
    public async Task ApiRequest_WithoutSession_Gets401Json_NotTheSignInPage()
    {
        using var anonymous = _app.Host.CreateAnonymousClient();

        var response = await anonymous.GetAsync("/api/guilds");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await ReadJsonAsync(response);
        problem.GetProperty("detail").GetString().Should().Contain("Sign in again");
    }

    [Fact]
    public async Task PageHandlerCalledFromScript_WithoutSession_Gets401_NotARedirect()
    {
        using var anonymous = _app.Host.CreateAnonymousClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/Guilds/Details/{OfflineAppHost.GuildId}");
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");

        var response = await anonymous.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task BrowserNavigation_WithoutSession_StillRedirectsToSignIn()
    {
        using var anonymous = _app.Host.CreateAnonymousClient();

        var response = await anonymous.GetAsync($"/Guilds/Details/{OfflineAppHost.GuildId}");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("/Account/Login");
    }

    [Fact]
    public async Task ApiRequest_WithoutPermission_Gets403Json()
    {
        using var viewer = await _app.Host.CreateSignedInClientAsync("feedback-viewer@example.com", "Viewer");

        var response = await viewer.GetAsync("/api/guilds");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Theory]
    [InlineData(400, "This page has expired")]
    [InlineData(403, "You don't have access to this page")]
    [InlineData(404, "Page not found")]
    [InlineData(405, "That action isn't available here")]
    [InlineData(429, "Too many requests")]
    [InlineData(500, "Something went wrong")]
    [InlineData(503, "Temporarily unavailable")]
    public async Task ErrorPage_RendersThemed_WithMainLandmark(int code, string heading)
    {
        var html = await _app.Host.Client.GetStringAsync($"/Error/{code}");

        WebUtility.HtmlDecode(html).Should().Contain(heading);
        html.Should().MatchRegex("<html[^>]*data-theme=\"[a-z-]+\"", "the error layout applies the user's theme");
        html.Should().Contain("<main id=\"main-content\"");
        html.Should().Contain("/css/app.css");
        html.Should().NotContainEquivalentOf("team has been notified");
        html.Should().NotContainEquivalentOf("team notified");
    }

    [Fact]
    public async Task UnknownAddress_Gets404Page_NamingTheAddressThatFailed()
    {
        var response = await _app.Host.Client.GetAsync("/no/such/page?x=1");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Page not found");
        html.Should().Contain("/no/such/page?x=1");
        html.Should().NotContain("/Error/404", "the page shows the address that failed, not its own");
    }

    [Fact]
    public async Task UnknownApiAddress_Gets404Json_NotHtml()
    {
        var response = await _app.Host.Client.GetAsync("/api/no-such-endpoint");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task FormPost_WithStaleAntiforgeryToken_Gets400Page_OfferingAReload()
    {
        var response = await _app.Host.Client.PostAsync(
            $"/Guilds/Edit/{OfflineAppHost.GuildId}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = "stale" }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("This page has expired");
        html.Should().Contain("Reload the page");
        html.Should().Contain($"href=\"/Guilds/Edit/{OfflineAppHost.GuildId}\"");
    }

    [Fact]
    public async Task ScriptPost_WithStaleAntiforgeryToken_Gets400Json()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Guilds/Edit/{OfflineAppHost.GuildId}")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = "stale" })
        };
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");

        var response = await _app.Host.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await ReadJsonAsync(response);
        problem.GetProperty("title").GetString().Should().Be("This page has expired");
    }

    [Fact]
    public async Task ActionResult_SetAsToastBeforeRedirect_RendersOnce_OnTheNextPage()
    {
        var page = $"/Guilds/Reminders/{OfflineAppHost.GuildId}";
        var token = OfflineAppHost.ReadAntiforgeryToken(await _app.Host.Client.GetStringAsync(page));

        var response = await _app.Host.Client.PostAsync(
            $"{page}?handler=Cancel&reminderId={Guid.NewGuid()}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));

        response.RequestMessage!.RequestUri!.AbsolutePath.Should().Be(page, "the handler redirects back to the list");
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("<script type=\"application/json\" id=\"serverToasts\">");
        html.Should().Contain("\"type\":\"error\",\"message\":\"Reminder not found.\"");

        var reload = await _app.Host.Client.GetStringAsync(page);
        reload.Should().NotContain("Reminder not found.", "a toast shows on one page render only");
    }

    [Fact]
    public async Task SignedOut_403Page_OffersSignIn()
    {
        using var anonymous = _app.Host.CreateAnonymousClient();

        var html = await anonymous.GetStringAsync("/Error/403");

        html.Should().Contain("Sign in to continue");
        html.Should().Contain("/Account/Login");
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    public sealed class AppFixture : IAsyncLifetime
    {
        public OfflineAppHost Host { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            Host = await OfflineAppHost.StartAsync();
        }

        public async Task DisposeAsync()
        {
            await Host.DisposeAsync();
        }
    }
}
