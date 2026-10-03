using System.Security.Claims;
using DiscordBot.Bot.Pages.Account;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Bot.Pages.Account;

/// <summary>
/// UX polish Phase 15: the copy and choices the Account pages derive from configuration and state.
/// </summary>
public class AccountPageModelTests
{
    [Theory]
    [InlineData(15, "15 minutes")]
    [InlineData(1, "1 minute")]
    [InlineData(90, "90 minutes")]
    [InlineData(120, "2 hours")]
    [InlineData(60, "1 hour")]
    [InlineData(1440, "1 day")]
    public void Lockout_DescribesTheConfiguredDuration_InWords(int minutes, string expected)
    {
        LockoutModel.Describe(TimeSpan.FromMinutes(minutes)).Should().Be(expected);
    }

    [Fact]
    public void Lockout_ReadsTheDurationFromIdentityOptions_NotAFixedNumber()
    {
        var options = new IdentityOptions { Lockout = { DefaultLockoutTimeSpan = TimeSpan.FromMinutes(45) } };

        var model = new LockoutModel(Options.Create(options));

        model.LockoutDuration.Should().Be("45 minutes");
    }

    [Theory]
    [InlineData("https://localhost:5001/exports/123/abc.zip", "/exports/123/abc.zip")]
    [InlineData("https://example.org/exports/123/abc.zip", "/exports/123/abc.zip")]
    [InlineData("/exports/123/abc.zip", "/exports/123/abc.zip")]
    [InlineData("", null)]
    [InlineData("//evil.example/x.zip", null)]
    [InlineData("javascript:alert(1)", null)]
    public void Privacy_ExportDownloadPath_IsAPathOnThisSite(string url, string? expected)
    {
        // Only the property under test is used, so the services are not needed.
        var model = new PrivacyModel(null!, null!, null!, null!, null!, Mock.Of<ILogger<PrivacyModel>>())
        {
            ExportDownloadUrl = url
        };

        model.ExportDownloadPath.Should().Be(expected);
    }

    private static (ProfileModel Model, Mock<IThemeService> Themes) CreateProfile(ApplicationUser user)
    {
        var users = new Mock<UserManager<ApplicationUser>>(
            new Mock<IUserStore<ApplicationUser>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);
        users.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

        var themes = new Mock<IThemeService>();
        var model = new ProfileModel(users.Object, themes.Object, Mock.Of<ILogger<ProfileModel>>());

        var httpContext = new DefaultHttpContext();
        model.PageContext = new PageContext { HttpContext = httpContext };
        model.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        return (model, themes);
    }

    [Fact]
    public async Task Profile_PostWithNoTheme_ClearsTheSavedChoice_SoTheUserFollowsTheSystem()
    {
        var (model, themes) = CreateProfile(new ApplicationUser { Id = "user-1" });
        themes.Setup(t => t.SetUserThemeAsync("user-1", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        model.SelectedThemeId = null;

        var result = await model.OnPostAsync();

        result.Should().BeOfType<RedirectToPageResult>();
        themes.Verify(t => t.SetUserThemeAsync("user-1", null, It.IsAny<CancellationToken>()), Times.Once);
        model.PageContext.HttpContext.Response.Headers.SetCookie.ToString()
            .Should().Contain(IThemeService.ThemePreferenceCookieName, "the cookie is expired so the server stops rendering the old theme");
        model.TempData["ToastSuccess"].Should().BeOfType<string>().Which.Should().Contain("system");
    }

    [Fact]
    public async Task Profile_PostWithNonNumericTheme_IsRefused_NotTreatedAsMatchMySystem()
    {
        var (model, themes) = CreateProfile(new ApplicationUser { Id = "user-1" });
        model.ModelState.AddModelError(nameof(ProfileModel.SelectedThemeId), "not a number");

        await model.OnPostAsync();

        themes.Verify(t => t.SetUserThemeAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
        model.TempData["ToastError"].Should().NotBeNull();
    }

    [Fact]
    public void Profile_ThemeChoices_StartWithMatchMySystem_AndSelectItWhenNothingIsSaved()
    {
        var (model, _) = CreateProfile(new ApplicationUser { Id = "user-1" });
        model.AvailableThemes = new[]
        {
            new ThemeDto { Id = 1, ThemeKey = "discord-dark", DisplayName = "Graphite (dark)" },
            new ThemeDto { Id = 2, ThemeKey = "purple-dusk", DisplayName = "Purple Dusk (light)" }
        };
        model.SelectedThemeId = null;

        var choices = model.ThemeChoices;

        choices.Options.Select(o => o.Title).Should().Equal("Match my system", "Graphite (dark)", "Purple Dusk (light)");
        choices.Options[0].Value.Should().BeEmpty();
        choices.SelectedValue.Should().BeEmpty();
    }

    [Fact]
    public void Profile_ThemeChoices_SelectTheSavedTheme()
    {
        var (model, _) = CreateProfile(new ApplicationUser { Id = "user-1" });
        model.AvailableThemes = new[] { new ThemeDto { Id = 2, ThemeKey = "purple-dusk", DisplayName = "Purple Dusk (light)" } };
        model.SelectedThemeId = 2;

        model.ThemeChoices.SelectedValue.Should().Be("2");
    }
}
