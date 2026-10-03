using System.Net;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace DiscordBot.Tests.Integration;

/// <summary>
/// UX polish Phase 15: Landing and the Account pages, rendered by the real app
/// (<see cref="OfflineAppHost"/>).
/// </summary>
[Collection(OfflineAppHostCollection.Name)]
public class AccountAndLandingPageTests : IClassFixture<AccountAndLandingPageTests.AppFixture>
{
    private readonly AppFixture _app;

    public AccountAndLandingPageTests(AppFixture app)
    {
        _app = app;
    }

    [Fact]
    public async Task Landing_IsThemed_HasMainAndOneH1_AndASignInLinkAtEveryWidth()
    {
        using var anonymous = _app.Host.CreateAnonymousClient();

        var html = await anonymous.GetStringAsync("/landing");

        html.Should().MatchRegex("<html[^>]*data-theme=\"[a-z-]+\"", "the landing layout resolves the theme like every other layout");
        html.Should().Contain("window.ThemeConfig");
        html.Should().Contain("<main id=\"main-content\"");
        html.Should().Contain("name=\"description\"");
        html.Split("<h1").Length.Should().Be(2, "the page has exactly one h1");
        html.Should().MatchRegex("<header[\\s\\S]*href=\"/Account/Login\"[\\s\\S]*Sign in[\\s\\S]*</header>");
        html.Should().NotContain("hidden lg:flex", "the sign-in link is no longer hidden below 1024px");
        html.Should().Contain("data-theme-toggle");
    }

    [Fact]
    public async Task Landing_HasASingleTitle_NoPlaceholderBoxes_AndNoStaleOrFakeParts()
    {
        using var anonymous = _app.Host.CreateAnonymousClient();

        var html = await anonymous.GetStringAsync("/landing");

        html.Should().Contain("<title>Discord Bot - open source, with a web admin portal</title>");
        html.Should().NotContain("screenshot-placeholder");
        html.Should().NotContainEquivalentOf("coming soon");
        html.Should().NotContain(">MIT<", "the repository has no MIT licence file");
        html.Should().NotMatchRegex("bg-\\[#", "tiles use theme tokens, not hard-coded colours");
    }

    [Fact]
    public async Task Login_HasOneH1_AndAUsernameAutocomplete_AndAFocusScript()
    {
        using var anonymous = _app.Host.CreateAnonymousClient();

        var html = await anonymous.GetStringAsync("/Account/Login");

        html.Split("<h1").Length.Should().Be(2);
        html.Should().Contain("autocomplete=\"username\"");
        html.Should().NotContain("autocomplete=\"email\"");
        html.Should().Contain("data-focus-first-error");
        html.Should().Contain("/js/form-focus.js");
        html.Should().Contain("data-submit-guard");
    }

    [Fact]
    public async Task Lockout_StatesTheConfiguredDuration_NotAFixedFifteenMinutes()
    {
        using var anonymous = _app.Host.CreateAnonymousClient();

        var html = WebUtility.HtmlDecode(await anonymous.GetStringAsync("/Account/Lockout"));

        html.Should().Contain("You can try again in 15 minutes", "the default Identity lockout is 15 minutes");
        html.Should().Contain("Return to sign in");
        html.Should().Contain("<main");
        html.Should().NotContain("Return to Login");
    }

    [Fact]
    public async Task AccessDenied_UsesPlainLanguage_AndSignOutIsAPost()
    {
        var html = WebUtility.HtmlDecode(await _app.Host.Client.GetStringAsync("/Account/AccessDenied?returnUrl=%2FAdmin%2FSettings"));

        html.Should().Contain("You don't have access to this page");
        html.Should().NotContain("403");
        html.Should().NotContain("Forbidden");
        html.Should().Contain("/Admin/Settings");
        html.Should().MatchRegex("<form[^>]*method=\"post\"[^>]*action=\"/Account/Logout\"");
        html.Should().Contain("Sign out");
    }

    [Fact]
    public async Task Privacy_UsesNoLegacyToggleMarkup()
    {
        var response = await _app.Host.Client.GetAsync("/Account/Privacy");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().NotContain("form-toggle");
        html.Should().NotContain("onclick=\"deleteAllData()");
        html.Should().NotContain("onchange=\"window.confirmConsentToggle");
    }

    [Fact]
    public async Task Profile_OffersMatchMySystem_AsTheFirstThemeChoice()
    {
        var html = WebUtility.HtmlDecode(await _app.Host.Client.GetStringAsync("/Account/Profile"));

        html.Should().Contain("Match my system");
        html.IndexOf("Match my system", StringComparison.Ordinal)
            .Should().BeLessThan(html.IndexOf("Graphite (dark)", StringComparison.Ordinal));
        html.Should().Contain("name=\"SelectedThemeId\"");
        html.Should().NotContain("Select a theme...");
    }

    public sealed class AppFixture : IAsyncLifetime
    {
        public OfflineAppHost Host { get; private set; } = null!;

        public async Task InitializeAsync() => Host = await OfflineAppHost.StartAsync();

        public async Task DisposeAsync() => await Host.DisposeAsync();
    }
}
