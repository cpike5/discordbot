using Bunit.TestDoubles;
using DiscordBot.Bot.Blazor.Interop;
using DiscordBot.Bot.Blazor.Pages.Account;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DiscordBot.ComponentTests.Pages;

/// <summary>
/// Tests for the routed Blazor twin of the old Pages/Account/Profile.cshtml
/// (Phase E migration). Covers the identity card, theme form rendering, and the
/// save flow (service call + client-side cookie via blazorInterop.applyTheme).
/// </summary>
public class ProfileTests : TestContext
{
    private static readonly ThemeDto DarkTheme = new()
    {
        Id = 1, ThemeKey = "discord-dark", DisplayName = "Discord Dark", IsActive = true
    };

    private static readonly ThemeDto LightTheme = new()
    {
        Id = 2, ThemeKey = "discord-light", DisplayName = "Discord Light", IsActive = true
    };

    private readonly Mock<UserManager<ApplicationUser>> _userManager;
    private readonly Mock<IThemeService> _themeService;
    private readonly ApplicationUser _user;

    public ProfileTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        _user = new ApplicationUser
        {
            Id = "user-1",
            Email = "chris@example.com",
            DisplayName = "Chris Test",
            DiscordUserId = 123456789012345678UL,
            DiscordUsername = "christest",
            CreatedAt = new DateTime(2024, 3, 15, 0, 0, 0, DateTimeKind.Utc),
            LastLoginAt = DateTime.UtcNow.AddMinutes(-5)
        };

        var store = new Mock<IUserStore<ApplicationUser>>();
        _userManager = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        _userManager
            .Setup(m => m.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync(_user);
        _userManager
            .Setup(m => m.GetRolesAsync(_user))
            .ReturnsAsync(new List<string> { "Admin" });

        _themeService = new Mock<IThemeService>();
        _themeService
            .Setup(s => s.GetActiveThemesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ThemeDto> { DarkTheme, LightTheme });
        _themeService
            .Setup(s => s.GetUserThemeAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CurrentThemeDto { Theme = DarkTheme, Source = ThemeSource.User });
        _themeService
            .Setup(s => s.GetThemeByIdAsync(LightTheme.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(LightTheme);
        _themeService
            .Setup(s => s.SetUserThemeAsync("user-1", LightTheme.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // The page resolves these through IServiceScopeFactory (scope per operation).
        Services.AddSingleton(_userManager.Object);
        Services.AddSingleton(_themeService.Object);
        Services.AddScoped<ToastInterop>();
        Services.AddScoped<ThemeInterop>();
        Services.AddLogging();

        this.AddTestAuthorization().SetAuthorized("Chris Test");
    }

    [Fact]
    public void RendersIdentityCardAndThemeForm()
    {
        var cut = RenderComponent<Profile>();

        // Identity card
        cut.Markup.Should().Contain("Chris Test");
        cut.Markup.Should().Contain("chris@example.com");
        cut.Markup.Should().Contain("@christest");
        cut.Markup.Should().Contain("ID: 123456789012345678");
        cut.Markup.Should().Contain("Linked");
        cut.Markup.Should().Contain("Administrator");
        cut.Markup.Should().Contain("March 15, 2024");
        cut.Markup.Should().Contain("minutes ago");

        // Discord already linked: no link-account buttons
        cut.Markup.Should().NotContain("Link Discord Account");

        // Theme form: both options plus current-theme info
        var options = cut.FindAll("select#SelectedThemeId option");
        options.Should().HaveCount(3); // placeholder + 2 themes
        cut.Markup.Should().Contain("Discord Dark");
        cut.Markup.Should().Contain("Discord Light");
        cut.Markup.Should().Contain("Current theme:");
    }

    [Fact]
    public void RendersLinkDiscordActions_WhenNotLinked()
    {
        _user.DiscordUserId = null;
        _user.DiscordUsername = null;

        var cut = RenderComponent<Profile>();

        cut.Markup.Should().Contain("Not linked");
        cut.Markup.Should().Contain("Link Discord Account");
    }

    [Fact]
    public void SavingTheme_PersistsViaServiceAndAppliesClientSide()
    {
        var cut = RenderComponent<Profile>();

        cut.Find("select#SelectedThemeId").Change(LightTheme.Id.ToString());
        cut.Find("form").Submit();

        // Preference persisted through the same service the Razor Page used
        _themeService.Verify(
            s => s.SetUserThemeAsync("user-1", LightTheme.Id, It.IsAny<CancellationToken>()),
            Times.Once);

        // Cookie + instant apply happen client-side (twin of Response.Cookies.Append)
        JSInterop.Invocations.Should().Contain(i =>
            i.Identifier == "blazorInterop.applyTheme" &&
            (string?)i.Arguments[0] == "discord-light");

        // Toast raised and status banner updated; current-theme info reflects the save
        JSInterop.Invocations.Should().Contain(i => i.Identifier == "blazorInterop.toast");
        cut.Markup.Should().Contain("Theme preference saved successfully.");
        cut.Markup.Should().Contain("Discord Light");
    }

    [Fact]
    public void SavingUnavailableTheme_ShowsError()
    {
        _themeService
            .Setup(s => s.GetThemeByIdAsync(LightTheme.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ThemeDto?)null);

        var cut = RenderComponent<Profile>();

        cut.Find("select#SelectedThemeId").Change(LightTheme.Id.ToString());
        cut.Find("form").Submit();

        _themeService.Verify(
            s => s.SetUserThemeAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        cut.Markup.Should().Contain("The selected theme is not available.");
    }
}
