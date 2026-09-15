using Bunit;
using Bunit.TestDoubles;
using DiscordBot.Bot.Blazor.Pages.Account;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.ComponentTests.Blazor.Pages.Account;

/// <summary>
/// Covers the static SSR port of Pages/Account/Profile.cshtml + ProfileModel
/// (docs/plans/blazor-port-plan.md Phase 4 cluster 4a). <c>UserManager&lt;ApplicationUser&gt;</c>
/// is mocked the same way <c>MainNavbarTests</c> does; <c>HttpContext</c> is supplied as a
/// cascading value the same way <c>NotFoundTests</c>/<c>ServerErrorTests</c> do for the other
/// static SSR pages that inject it directly. The theme-save path (<c>SaveThemeAsync</c>) is
/// exercised by submitting the rendered <c>&lt;EditForm&gt;</c> - bUnit invokes the same
/// <c>OnValidSubmit</c> delegate a real static SSR POST would, and
/// <c>NavigationManager.NavigateTo</c> from inside it updates the fake
/// <c>NavigationManager</c>'s history exactly as it does for <c>Search</c>'s own submit handler
/// (see <c>SearchTests.SearchForm_Submit_NavigatesToSearchWithTheTypedQuery</c>) - so the redirect
/// itself is covered here; only the real HTTP 302 mechanics behind a static-SSR
/// <c>NavigationException</c> are Playwright's job (<c>Test_P_Profile_RendersAndSavesTheme</c>).
/// </summary>
public class ProfileTests : BlazorComponentTestContext
{
    private static readonly ApplicationUser LinkedUser = new()
    {
        Id = "user-1",
        DisplayName = "Ada Lovelace",
        Email = "ada@example.test",
        DiscordUserId = 123456789012345678UL,
        DiscordUsername = "ada",
        DiscordAvatarUrl = "https://cdn.example.test/avatar.png",
        CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        LastLoginAt = new DateTime(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc)
    };

    private static readonly ApplicationUser UnlinkedUser = new()
    {
        Id = "user-2",
        DisplayName = "Bob",
        Email = "bob@example.test",
        CreatedAt = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    private static readonly ThemeDto DarkTheme = new() { Id = 1, ThemeKey = "discord-dark", DisplayName = "Discord Dark", IsActive = true };
    private static readonly ThemeDto LightTheme = new() { Id = 2, ThemeKey = "light", DisplayName = "Light", IsActive = true };

    private readonly Mock<UserManager<ApplicationUser>> _userManager;
    private readonly Mock<IThemeService> _themeService = new();
    private readonly DefaultHttpContext _httpContext = new();

    public ProfileTests()
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        _userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object,
            null!,
            new Mock<IPasswordHasher<ApplicationUser>>().Object,
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new Mock<ILookupNormalizer>().Object,
            new Mock<IdentityErrorDescriber>().Object,
            new Mock<IServiceProvider>().Object,
            new Mock<ILogger<UserManager<ApplicationUser>>>().Object);

        Services.AddSingleton(_userManager.Object);
        Services.AddSingleton(_themeService.Object);
        Services.AddSingleton<AntiforgeryStateProvider, TestAntiforgeryStateProvider>();

        _themeService.Setup(s => s.GetActiveThemesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { DarkTheme, LightTheme });
    }

    private void SetUser(ApplicationUser user, ThemeDto? currentTheme = null, ThemeSource source = ThemeSource.User)
    {
        _userManager.Setup(m => m.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>())).ReturnsAsync(user);
        _userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Admin" });
        _themeService.Setup(s => s.GetUserThemeAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CurrentThemeDto { Theme = currentTheme ?? DarkTheme, Source = source });
    }

    private IRenderedComponent<Profile> RenderProfile(string? status = null)
    {
        if (status is not null)
        {
            var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
            navMan.NavigateTo(navMan.GetUriWithQueryParameter("status", status));
        }

        return Render<Profile>(parameters => parameters.AddCascadingValue(_httpContext));
    }

    [Fact]
    public void UserNotFound_ShowsErrorAlert()
    {
        _userManager.Setup(m => m.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>())).ReturnsAsync((ApplicationUser?)null);

        var cut = RenderProfile();

        cut.Markup.Should().Contain("User not found");
    }

    [Fact]
    public void RendersDisplayNameEmailAndRoleBadge()
    {
        SetUser(LinkedUser);

        var cut = RenderProfile();

        cut.Find("h2").TextContent.Should().Be("Ada Lovelace");
        cut.Markup.Should().Contain("ada@example.test");
        cut.Markup.Should().Contain("Administrator");
    }

    [Fact]
    public void DiscordNotLinked_ShowsNotLinkedBadgeAndLinkDiscordLink()
    {
        SetUser(UnlinkedUser);

        var cut = RenderProfile();

        cut.Markup.Should().Contain("Not Linked");
        cut.Markup.Should().Contain("Not linked");
        cut.FindAll("a").Should().Contain(a => a.GetAttribute("href") == "/Account/LinkDiscord");
    }

    [Fact]
    public void DiscordLinked_ShowsAvatarAndUsername_NotLinkDiscordLink()
    {
        SetUser(LinkedUser);

        var cut = RenderProfile();

        cut.Find("img[alt='Ada Lovelace']").GetAttribute("src").Should().Be("https://cdn.example.test/avatar.png");
        cut.Markup.Should().Contain("ada");
        cut.Markup.Should().Contain("Linked");
        cut.FindAll("a").Should().NotContain(a => a.GetAttribute("href") == "/Account/LinkDiscord");
    }

    [Fact]
    public void ThemeSelect_ListsActiveThemes_WithCurrentThemeSelected()
    {
        SetUser(LinkedUser, currentTheme: LightTheme);

        var cut = RenderProfile();

        var select = cut.Find("select#SelectedThemeId");
        select.Children.Should().Contain(o => o.TextContent == "Discord Dark");
        select.Children.Should().Contain(o => o.TextContent == "Light");
        cut.Markup.Should().Contain("Light");
        cut.Markup.Should().Contain("Current theme:");
    }

    [Fact]
    public void StatusSaved_ShowsSuccessBanner()
    {
        SetUser(LinkedUser);

        var cut = RenderProfile(status: "saved");

        cut.Markup.Should().Contain("Success").And.Contain("Theme preference saved successfully.");
    }

    [Fact]
    public void StatusError_ShowsErrorBanner()
    {
        SetUser(LinkedUser);

        var cut = RenderProfile(status: "error");

        cut.Markup.Should().Contain("Failed to save theme preference");
    }

    [Fact]
    public void SubmittingTheForm_WithAValidTheme_SavesAndRedirectsToSaved()
    {
        SetUser(LinkedUser, currentTheme: DarkTheme);
        _themeService.Setup(s => s.GetThemeByIdAsync(LightTheme.Id, It.IsAny<CancellationToken>())).ReturnsAsync(LightTheme);
        _themeService.Setup(s => s.SetUserThemeAsync(LinkedUser.Id, LightTheme.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();

        var cut = RenderProfile();
        cut.Find("select#SelectedThemeId").Change(LightTheme.Id.ToString());
        cut.Find("form").Submit();

        navMan.History.Last().Uri.Should().Be("/Account/Profile?status=saved");
        _themeService.Verify(s => s.SetUserThemeAsync(LinkedUser.Id, LightTheme.Id, It.IsAny<CancellationToken>()), Times.Once);
        _httpContext.Response.Headers.SetCookie.ToString().Should().Contain(IThemeService.ThemePreferenceCookieName);
    }

    [Fact]
    public void SubmittingTheForm_WithAnInactiveTheme_RedirectsToError_AndDoesNotSetTheCookie()
    {
        SetUser(LinkedUser, currentTheme: DarkTheme);
        var inactiveTheme = LightTheme with { IsActive = false };
        _themeService.Setup(s => s.GetThemeByIdAsync(LightTheme.Id, It.IsAny<CancellationToken>())).ReturnsAsync(inactiveTheme);
        var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();

        var cut = RenderProfile();
        cut.Find("select#SelectedThemeId").Change(LightTheme.Id.ToString());
        cut.Find("form").Submit();

        navMan.History.Last().Uri.Should().Be("/Account/Profile?status=error");
        _httpContext.Response.Headers.SetCookie.Should().BeEmpty();
    }
}
