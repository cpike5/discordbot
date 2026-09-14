using System.Security.Claims;
using Bunit;
using DiscordBot.Bot.Blazor.Layout;
using DiscordBot.Bot.Interfaces;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.ComponentTests.Blazor.Layout;

/// <summary>
/// <see cref="MainNavbar"/> ports Pages/Shared/_Navbar.cshtml (see the component's own header
/// comment for the confirmed Logout route/handler and the AntiforgeryToken rationale).
/// <c>UserManager&lt;ApplicationUser&gt;</c> is mocked the same way
/// <c>UserManagementServiceTests</c> does elsewhere in the solution (a real, non-sealed class with
/// virtual members, constructed via its public constructor with every dependency stubbed).
/// <see cref="TestAntiforgeryStateProvider"/> stands in for the token provider
/// <c>AddRazorComponents()</c> registers on the real host (see its own remarks for why).
/// </summary>
public class MainNavbarTests : BlazorComponentTestContext
{
    private const string UserId = "user-123";

    private readonly Mock<UserManager<ApplicationUser>> _userManager;

    public MainNavbarTests()
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
        Services.AddSingleton<AntiforgeryStateProvider, TestAntiforgeryStateProvider>();

        // NotificationBell (an island this component always renders) awaits this on
        // OnInitializedAsync whenever a user id is present - an unconfigured Mock.Of<> would
        // return a null Task for GetNotificationSummaryAsync and NullReferenceException on await.
        var notificationQueryService = new Mock<IDashboardNotificationQueryService>();
        notificationQueryService
            .Setup(s => s.GetNotificationSummaryAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new NotificationSummaryDto());
        Services.AddSingleton(notificationQueryService.Object);
    }

    [Fact]
    public void Anonymous_ShowsSignInLink_NotUserMenu()
    {
        AddAuthorization().SetNotAuthorized();

        var cut = Render<MainNavbar>();

        cut.Markup.Should().Contain("Sign In");
        cut.FindAll("#userMenuButton").Should().BeEmpty();
    }

    [Fact]
    public void Authenticated_ShowsUserMenu_WithNameAndEmail()
    {
        _userManager
            .Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(new ApplicationUser { DisplayName = "Ada Lovelace", Email = "ada@example.test" });
        AddAuthorization().SetAuthorized("ada").SetClaims(new Claim(ClaimTypes.NameIdentifier, UserId));

        var cut = Render<MainNavbar>();

        cut.Find(".topbar-user-name").TextContent.Should().Be("Ada Lovelace");
        cut.Find(".user-menu-name").TextContent.Should().Be("Ada Lovelace");
        cut.Find(".user-menu-email").TextContent.Should().Be("ada@example.test");
        cut.FindAll("#userMenuButton").Should().HaveCount(1);
    }

    [Fact]
    public void Authenticated_WithNoAvatarUrl_ShowsInitials()
    {
        _userManager
            .Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(new ApplicationUser { DisplayName = "Ada Lovelace", Email = "ada@example.test" });
        AddAuthorization().SetAuthorized("ada").SetClaims(new Claim(ClaimTypes.NameIdentifier, UserId));

        var cut = Render<MainNavbar>();

        cut.FindAll(".topbar-avatar img").Should().BeEmpty();
        cut.Find("span.topbar-avatar").TextContent.Should().Be("AL");
    }

    [Fact]
    public void Authenticated_WithAvatarUrl_RendersImage()
    {
        _userManager
            .Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(new ApplicationUser
            {
                DisplayName = "Ada Lovelace",
                Email = "ada@example.test",
                DiscordAvatarUrl = "https://cdn.example.test/avatar.png"
            });
        AddAuthorization().SetAuthorized("ada").SetClaims(new Claim(ClaimTypes.NameIdentifier, UserId));

        var cut = Render<MainNavbar>();

        cut.Find("img.topbar-avatar").GetAttribute("src").Should().Be("https://cdn.example.test/avatar.png");
    }

    [Fact]
    public void LogoutForm_PostsToPlainAccountLogout_WithAntiforgeryToken()
    {
        _userManager
            .Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(new ApplicationUser { DisplayName = "Ada Lovelace" });
        AddAuthorization().SetAuthorized("ada").SetClaims(new Claim(ClaimTypes.NameIdentifier, UserId));

        var cut = Render<MainNavbar>();

        var form = cut.Find(".user-menu-item.danger").ParentElement;
        form!.TagName.Should().Be("FORM");
        form.GetAttribute("method").Should().Be("post");
        form.GetAttribute("action").Should().Be("/Account/Logout");
    }

    [Fact]
    public void NotificationBell_IsRendered()
    {
        AddAuthorization().SetNotAuthorized();

        var cut = Render<MainNavbar>();

        cut.FindAll("button[aria-label='Notifications']").Should().HaveCount(1);
    }
}
