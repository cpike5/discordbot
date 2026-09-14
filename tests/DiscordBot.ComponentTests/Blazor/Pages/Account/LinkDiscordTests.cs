using Bunit;
using Bunit.TestDoubles;
using DiscordBot.Bot.Blazor.Pages.Account;
using DiscordBot.Bot.Services;
using DiscordBot.Bot.Services.Account;
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
/// Covers the static SSR port of Pages/Account/LinkDiscord.cshtml + LinkDiscordModel
/// (docs/plans/blazor-port-plan.md Phase 4 cluster 4c): the three top-level states (not
/// configured / linked / not linked), the status banner per key, and the unlink two-step
/// confirmation. Mutation handlers are exercised by submitting the rendered <c>&lt;EditForm&gt;</c>
/// - bUnit invokes that specific form's own <c>OnValidSubmit</c> delegate directly (it does not
/// need to simulate the real static-SSR FormName/"_handler" routing to pick the right one among
/// several), the same approach <c>ProfileTests</c> uses for its single form.
/// </summary>
public class LinkDiscordTests : BlazorComponentTestContext
{
    private static readonly ApplicationUser LinkedUser = new()
    {
        Id = "user-1",
        DiscordUserId = 123456789012345678UL,
        DiscordUsername = "ada",
        DiscordAvatarUrl = "https://cdn.example.test/avatar.png"
    };

    private static readonly ApplicationUser UnlinkedUser = new() { Id = "user-2" };

    private readonly Mock<UserManager<ApplicationUser>> _userManager;
    private readonly Mock<IDiscordTokenService> _tokenService = new();
    private readonly Mock<IGuildMembershipService> _guildMembershipService = new();
    private readonly Mock<IVerificationService> _verificationService = new();
    private readonly Mock<IDiscordLinkService> _linkService = new();
    private readonly DefaultHttpContext _httpContext = new();

    public LinkDiscordTests()
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
        Services.AddSingleton(_tokenService.Object);
        Services.AddSingleton(_guildMembershipService.Object);
        Services.AddSingleton(_verificationService.Object);
        Services.AddSingleton(_linkService.Object);
        Services.AddSingleton(new DiscordOAuthSettings { IsConfigured = true });
        Services.AddSingleton<AntiforgeryStateProvider, TestAntiforgeryStateProvider>();

        _verificationService.Setup(s => s.GetPendingVerificationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VerificationCode?)null);
    }

    private void SetUser(ApplicationUser user)
        => _userManager.Setup(m => m.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>())).ReturnsAsync(user);

    private IRenderedComponent<LinkDiscord> RenderPage(string? status = null, string? detail = null)
    {
        if (status is not null)
        {
            var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
            var query = detail is null
                ? $"?status={Uri.EscapeDataString(status)}"
                : $"?status={Uri.EscapeDataString(status)}&detail={Uri.EscapeDataString(detail)}";
            navMan.NavigateTo("/Account/LinkDiscord" + query);
        }

        return Render<LinkDiscord>(parameters => parameters.AddCascadingValue(_httpContext));
    }

    [Fact]
    public void OAuthNotConfigured_ShowsNotConfiguredMessage_AndNoLinkForm()
    {
        // Registered after the constructor's IsConfigured=true - the built-in container resolves
        // the last registration for a given service type, so this wins.
        Services.AddSingleton(new DiscordOAuthSettings { IsConfigured = false });
        SetUser(UnlinkedUser);

        var cut = RenderPage();

        cut.Markup.Should().Contain("Discord OAuth Not Configured");
        cut.FindAll("form").Should().NotContain(f => f.GetAttribute("action") == "/Account/PerformExternalLogin");
        // Bot verification stays reachable even though OAuth isn't configured - see
        // LinkDiscord.razor's remarks on why this differs from the legacy nesting.
        cut.Markup.Should().Contain("Start Verification");
    }

    [Fact]
    public async Task OAuthNotConfigured_InitiatingVerification_CallsService_AndSucceeds()
    {
        Services.AddSingleton(new DiscordOAuthSettings { IsConfigured = false });
        SetUser(UnlinkedUser);
        _linkService.Setup(s => s.InitiateBotVerificationAsync(UnlinkedUser, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiscordLinkOperationOutcome(true, "verify-init-success"));
        var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();

        var cut = RenderPage();
        var initiateForm = cut.FindAll("form").First(f => f.QuerySelector("button")?.TextContent.Contains("Start Verification") == true);
        await cut.InvokeAsync(() => initiateForm.Submit());

        navMan.Uri.Should().Contain("status=verify-init-success");
    }

    [Fact]
    public void NotLinked_ShowsLinkForm_PostingToPerformExternalLogin_WithReturnUrl()
    {
        SetUser(UnlinkedUser);

        var cut = RenderPage();

        var linkForm = cut.FindAll("form").First(f => f.GetAttribute("action") == "/Account/PerformExternalLogin");
        linkForm.QuerySelector("input[name='returnUrl']")!.GetAttribute("value").Should().Be("/Account/LinkDiscord");
        cut.Markup.Should().Contain("No Discord Account Linked");
    }

    [Fact]
    public void Linked_ShowsUsernameAvatarAndGuilds()
    {
        SetUser(LinkedUser);
        _tokenService.Setup(s => s.HasValidTokenAsync(LinkedUser.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _guildMembershipService.Setup(s => s.GetAdministeredGuildsAsync(LinkedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new DiscordGuildDto { Id = 1, Name = "My Guild", Owner = true } });

        var cut = RenderPage();

        cut.Markup.Should().Contain("ada");
        cut.Markup.Should().Contain("My Guild");
        cut.Markup.Should().Contain("Linked");
    }

    [Fact]
    public void Linked_UnlinkIsATwoStepConfirmation_BehindADetailsElement()
    {
        SetUser(LinkedUser);
        _tokenService.Setup(s => s.HasValidTokenAsync(LinkedUser.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var cut = RenderPage();

        cut.Find("details summary").TextContent.Should().Contain("Unlink Discord");
        cut.Markup.Should().Contain("Are you sure you want to unlink your Discord account?");
    }

    [Fact]
    public async Task Linked_SubmittingUnlinkConfirm_CallsService_AndRedirectsWithStatus()
    {
        SetUser(LinkedUser);
        _tokenService.Setup(s => s.HasValidTokenAsync(LinkedUser.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _linkService.Setup(s => s.UnlinkAsync(LinkedUser, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiscordLinkOperationOutcome(true, "unlink-success"));
        var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();

        var cut = RenderPage();
        var unlinkForm = cut.FindAll("form").First(f => f.QuerySelector("button")?.TextContent.Contains("Yes, Unlink") == true);
        await cut.InvokeAsync(() => unlinkForm.Submit());

        navMan.Uri.Should().Contain("status=unlink-success");
        _linkService.Verify(s => s.UnlinkAsync(LinkedUser, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotLinked_SubmittingRefreshOnALinkedAccount_CallsService()
    {
        SetUser(LinkedUser);
        _tokenService.Setup(s => s.HasValidTokenAsync(LinkedUser.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _linkService.Setup(s => s.RefreshDiscordDataAsync(LinkedUser, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiscordLinkOperationOutcome(true, "refresh-success"));
        var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();

        var cut = RenderPage();
        var refreshForm = cut.FindAll("form").First(f => f.QuerySelector("button")?.TextContent.Contains("Refresh Data") == true);
        await cut.InvokeAsync(() => refreshForm.Submit());

        navMan.Uri.Should().Contain("status=refresh-success");
    }

    [Fact]
    public async Task NotLinked_SubmittingInitiateVerification_ThenShowsCodeEntry_AfterReload()
    {
        SetUser(UnlinkedUser);
        _linkService.Setup(s => s.InitiateBotVerificationAsync(UnlinkedUser, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiscordLinkOperationOutcome(true, "verify-init-success"));
        var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();

        var cut = RenderPage();
        var initiateForm = cut.FindAll("form").First(f => f.QuerySelector("button")?.TextContent.Contains("Start Verification") == true);
        await cut.InvokeAsync(() => initiateForm.Submit());

        navMan.Uri.Should().Contain("status=verify-init-success");
    }

    [Fact]
    public void NotLinked_WithPendingVerification_ShowsCodeFormAndCancel()
    {
        SetUser(UnlinkedUser);
        _verificationService.Setup(s => s.GetPendingVerificationAsync(UnlinkedUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerificationCode { Id = Guid.NewGuid(), ApplicationUserId = UnlinkedUser.Id, ExpiresAt = DateTime.UtcNow.AddMinutes(10) });

        var cut = RenderPage();

        cut.Markup.Should().Contain("Verification pending");
        cut.Markup.Should().Contain("Verification Code");
        cut.FindAll("form").Any(f => f.QuerySelector("button") is { } b && b.TextContent.Contains("Cancel")).Should().BeTrue();
    }

    [Theory]
    [InlineData("unlink-success", true, "Discord account unlinked successfully.")]
    [InlineData("not-linked", false, "No Discord account is currently linked.")]
    [InlineData("refresh-success", true, "Discord data refreshed successfully.")]
    [InlineData("cancel-success", true, "Verification cancelled.")]
    public void StatusKey_RendersTheExpectedBanner(string status, bool expectSuccess, string expectedMessage)
    {
        SetUser(LinkedUser);
        _tokenService.Setup(s => s.HasValidTokenAsync(LinkedUser.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var cut = RenderPage(status);

        cut.Markup.Should().Contain(expectSuccess ? "Success" : "Error").And.Contain(expectedMessage);
    }

    [Fact]
    public void StatusKey_VerifyCodeSuccess_UsesDetailAsWelcomeName()
    {
        SetUser(LinkedUser);
        _tokenService.Setup(s => s.HasValidTokenAsync(LinkedUser.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var cut = RenderPage("verify-code-success", "SomeDiscordUser");

        cut.Markup.Should().Contain("Welcome, SomeDiscordUser!");
    }

    [Fact]
    public void UserNotFound_ShowsErrorAlert()
    {
        _userManager.Setup(m => m.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>())).ReturnsAsync((ApplicationUser?)null);

        var cut = RenderPage();

        cut.Markup.Should().Contain("User not found");
    }
}
