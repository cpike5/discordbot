using Bunit;
using DiscordBot.Bot.Blazor.Layout;
using DiscordBot.Bot.Interfaces;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.ComponentTests.Blazor.Layout;

/// <summary>
/// <see cref="MainLayout"/> composes <see cref="MainSidebar"/> and <see cref="MainNavbar"/>, so
/// every dependency either needs (see their own test classes' headers for why each is mocked the
/// way it is) has to be registered here too - bUnit renders a component's whole tree, regardless
/// of the <c>@rendermode</c> annotations on the islands (NotificationBell/ToastHost/LoadingOverlay),
/// so there is no way to render only the static shell without satisfying the islands' DI as well.
/// </summary>
public class MainLayoutTests : BlazorComponentTestContext
{
    public MainLayoutTests()
    {
        Services.AddSingleton(Mock.Of<IVersionService>(v => v.GetVersion() == "v1.2.3"));
        Services.AddSingleton(Options.Create(new ObservabilityOptions()));
        var notificationQueryService = new Mock<IDashboardNotificationQueryService>();
        notificationQueryService
            .Setup(s => s.GetNotificationSummaryAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new NotificationSummaryDto());
        Services.AddSingleton(notificationQueryService.Object);
        Services.AddSingleton<AntiforgeryStateProvider, TestAntiforgeryStateProvider>();

        var userStore = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, new Mock<IPasswordHasher<ApplicationUser>>().Object,
            Array.Empty<IUserValidator<ApplicationUser>>(), Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new Mock<ILookupNormalizer>().Object, new Mock<IdentityErrorDescriber>().Object,
            new Mock<IServiceProvider>().Object, new Mock<ILogger<UserManager<ApplicationUser>>>().Object);
        Services.AddSingleton(userManager.Object);

        AddAuthorization().SetNotAuthorized();
    }

    [Fact]
    public void RendersShellStructure_SidebarNavbarAndMainContent()
    {
        var cut = Render<MainLayout>(parameters => parameters
            .Add(p => p.Body, "<p data-testid=\"layout-body\">Page content</p>"));

        cut.Find("a.skip-link").TextContent.Should().Be("Skip to main content");
        cut.Find("#mobileOverlay").Should().NotBeNull();
        cut.Find("#mobileSearchOverlay").Should().NotBeNull();
        cut.Find("#sidebar").Should().NotBeNull();
        cut.Find("#topbar").Should().NotBeNull();
        cut.Find("#main-content [data-testid='layout-body']").TextContent.Should().Be("Page content");
    }

    [Fact]
    public void RendersToastHostAndLoadingOverlayIslands()
    {
        var cut = Render<MainLayout>(parameters => parameters
            .Add(p => p.Body, "<p>Body</p>"));

        cut.Find("#toastContainer").Should().NotBeNull();
    }

    [Fact]
    public void ErrorBoundary_CatchesRenderException_AndShowsAlert()
    {
        var cut = Render<MainLayout>(parameters => parameters
            .Add(p => p.Body, (Microsoft.AspNetCore.Components.RenderFragment)(builder =>
                throw new InvalidOperationException("boom"))));

        cut.Markup.Should().Contain("Something went wrong");
        cut.FindAll("[role='alert']").Should().NotBeEmpty();
    }
}
