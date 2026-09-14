using Bunit;
using Bunit.TestDoubles;
using DiscordBot.Bot.Blazor.Pages.Account;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.ComponentTests.Blazor.Pages.Account;

/// <summary>
/// Covers the static SSR port of Pages/Account/Privacy.cshtml + PrivacyModel
/// (docs/plans/blazor-port-plan.md Phase 4 cluster 4c): the not-linked callout, consent cards
/// against mocked statuses, consent history, and the delete-all-data form's typed-confirmation
/// validation. The delete form's happy path (typed "DELETE", purge succeeds, sign-out + redirect
/// to /landing) is exercised here directly - bUnit's fake <c>SignInManager</c> mock lets the whole
/// handler run without a real circuit, unlike a real static-SSR POST (Playwright's job for the
/// end-to-end redirect mechanics: <c>Test_Z4</c>).
/// </summary>
public class PrivacyTests : BlazorComponentTestContext
{
    private static readonly ApplicationUser LinkedUser = new()
    {
        Id = "user-1",
        DiscordUserId = 123456789012345678UL,
        DiscordUsername = "ada"
    };

    private static readonly ApplicationUser UnlinkedUser = new() { Id = "user-2" };

    private readonly Mock<UserManager<ApplicationUser>> _userManager;
    private readonly Mock<SignInManager<ApplicationUser>> _signInManager;
    private readonly Mock<IConsentService> _consentService = new();
    private readonly Mock<IUserDataExportService> _exportService = new();
    private readonly Mock<IUserPurgeService> _purgeService = new();
    private readonly DefaultHttpContext _httpContext = new();

    public PrivacyTests()
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

        var contextAccessor = new Mock<IHttpContextAccessor>();
        var claimsFactory = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
        _signInManager = new Mock<SignInManager<ApplicationUser>>(
            _userManager.Object,
            contextAccessor.Object,
            claimsFactory.Object,
            null!,
            new Mock<ILogger<SignInManager<ApplicationUser>>>().Object,
            new Mock<IAuthenticationSchemeProvider>().Object,
            null!);

        Services.AddSingleton(_userManager.Object);
        Services.AddSingleton(_signInManager.Object);
        Services.AddSingleton(_consentService.Object);
        Services.AddSingleton(_exportService.Object);
        Services.AddSingleton(_purgeService.Object);
        Services.AddSingleton<AntiforgeryStateProvider, TestAntiforgeryStateProvider>();
    }

    private void SetUser(ApplicationUser user)
        => _userManager.Setup(m => m.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>())).ReturnsAsync(user);

    private IRenderedComponent<Privacy> RenderPage(string? status = null)
    {
        if (status is not null)
        {
            var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
            navMan.NavigateTo(navMan.GetUriWithQueryParameter("status", status));
        }

        return Render<Privacy>(parameters => parameters.AddCascadingValue(_httpContext));
    }

    [Fact]
    public void NotLinked_RendersCallout_LinkingToLinkDiscord()
    {
        SetUser(UnlinkedUser);

        var cut = RenderPage();

        cut.Markup.Should().Contain("Discord Account Required");
        cut.FindAll("a").Should().Contain(a => a.GetAttribute("href") == "/Account/LinkDiscord");
    }

    [Fact]
    public void Linked_RendersConsentCards_WithGrantOrRevokeButtons()
    {
        SetUser(LinkedUser);
        _consentService.Setup(s => s.GetConsentStatusAsync(LinkedUser.DiscordUserId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ConsentStatusDto { Type = (int)ConsentType.MessageLogging, TypeDisplayName = "Message Logging", IsGranted = true, GrantedAt = DateTime.UtcNow },
                new ConsentStatusDto { Type = (int)ConsentType.AssistantUsage, TypeDisplayName = "Assistant Usage", IsGranted = false }
            });
        _consentService.Setup(s => s.GetConsentHistoryAsync(LinkedUser.DiscordUserId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ConsentHistoryEntryDto>());

        var cut = RenderPage();

        cut.Markup.Should().Contain("Message Logging").And.Contain("Assistant Usage");
        cut.FindAll("button").Should().Contain(b => b.TextContent.Contains("Revoke"));
        cut.FindAll("button").Should().Contain(b => b.TextContent.Contains("Grant"));
    }

    [Fact]
    public async Task Linked_SubmittingGrant_CallsConsentService_AndRedirectsWithStatus()
    {
        SetUser(LinkedUser);
        _consentService.Setup(s => s.GetConsentStatusAsync(LinkedUser.DiscordUserId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ConsentStatusDto { Type = (int)ConsentType.AssistantUsage, TypeDisplayName = "Assistant Usage", IsGranted = false } });
        _consentService.Setup(s => s.GetConsentHistoryAsync(LinkedUser.DiscordUserId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ConsentHistoryEntryDto>());
        _consentService.Setup(s => s.GrantConsentAsync(LinkedUser.DiscordUserId!.Value, ConsentType.AssistantUsage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConsentUpdateResult.Success());
        var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();

        var cut = RenderPage();
        var form = cut.FindAll("form").First(f => f.QuerySelector("button")?.TextContent.Contains("Grant") == true);
        await cut.InvokeAsync(() => form.Submit());

        navMan.Uri.Should().Contain("status=consent-updated");
        _consentService.Verify(s => s.GrantConsentAsync(LinkedUser.DiscordUserId!.Value, ConsentType.AssistantUsage, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Linked_RendersConsentHistoryTimeline()
    {
        SetUser(LinkedUser);
        _consentService.Setup(s => s.GetConsentStatusAsync(LinkedUser.DiscordUserId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ConsentStatusDto>());
        _consentService.Setup(s => s.GetConsentHistoryAsync(LinkedUser.DiscordUserId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ConsentHistoryEntryDto { Type = (int)ConsentType.MessageLogging, TypeDisplayName = "Message Logging", Action = "Granted", Timestamp = DateTime.UtcNow, Source = "WebUI" }
            });

        var cut = RenderPage();

        cut.Markup.Should().Contain("Consent History");
        cut.Markup.Should().Contain("Granted");
        cut.Markup.Should().Contain("via WebUI");
    }

    [Fact]
    public void Linked_DeleteForm_SubmittingWithoutTypingDelete_ShowsValidationError_AndDoesNotPurge()
    {
        SetUser(LinkedUser);
        _consentService.Setup(s => s.GetConsentStatusAsync(LinkedUser.DiscordUserId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ConsentStatusDto>());
        _consentService.Setup(s => s.GetConsentHistoryAsync(LinkedUser.DiscordUserId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ConsentHistoryEntryDto>());

        var cut = RenderPage();
        var deleteForm = cut.FindAll("form").First(f => f.QuerySelector("button")?.TextContent.Contains("Delete All Data") == true);
        deleteForm.QuerySelector("input[placeholder='Type DELETE to confirm']")!.Change("delete"); // wrong case, must be exactly "DELETE"
        deleteForm.Submit();

        cut.Markup.Should().Contain("Type DELETE to confirm.");
        _purgeService.Verify(s => s.CanPurgeUserAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Linked_DeleteForm_SubmittingWithDelete_PurgesSignsOut_AndNavigatesToLanding()
    {
        SetUser(LinkedUser);
        _consentService.Setup(s => s.GetConsentStatusAsync(LinkedUser.DiscordUserId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ConsentStatusDto>());
        _consentService.Setup(s => s.GetConsentHistoryAsync(LinkedUser.DiscordUserId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ConsentHistoryEntryDto>());
        _purgeService.Setup(s => s.CanPurgeUserAsync(LinkedUser.DiscordUserId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, (string?)null));
        _purgeService.Setup(s => s.PurgeUserDataAsync(LinkedUser.DiscordUserId!.Value, PurgeInitiator.User, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserPurgeResultDto.Succeeded(new Dictionary<string, int> { ["Messages"] = 3 }, "corr-1"));
        _signInManager.Setup(s => s.SignOutAsync()).Returns(Task.CompletedTask);
        var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();

        var cut = RenderPage();
        var deleteForm = cut.FindAll("form").First(f => f.QuerySelector("button")?.TextContent.Contains("Delete All Data") == true);
        deleteForm.QuerySelector("input[placeholder='Type DELETE to confirm']")!.Change("DELETE");
        await cut.InvokeAsync(() => deleteForm.Submit());

        _purgeService.Verify(s => s.PurgeUserDataAsync(LinkedUser.DiscordUserId!.Value, PurgeInitiator.User, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _signInManager.Verify(s => s.SignOutAsync(), Times.Once);
        navMan.Uri.Should().Contain("/landing");
    }

    [Fact]
    public void StatusKey_ExportSuccess_RendersDynamicDetailAsMessage()
    {
        SetUser(LinkedUser);
        _consentService.Setup(s => s.GetConsentStatusAsync(LinkedUser.DiscordUserId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ConsentStatusDto>());
        _consentService.Setup(s => s.GetConsentHistoryAsync(LinkedUser.DiscordUserId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ConsentHistoryEntryDto>());
        var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        navMan.NavigateTo("/Account/Privacy?status=export-success&detail=" + Uri.EscapeDataString("12 records were exported."));

        var cut = Render<Privacy>(parameters => parameters.AddCascadingValue(_httpContext));

        cut.Markup.Should().Contain("12 records were exported.");
    }

    [Fact]
    public void UserNotFound_ShowsErrorAlert()
    {
        _userManager.Setup(m => m.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>())).ReturnsAsync((ApplicationUser?)null);

        var cut = RenderPage();

        cut.Markup.Should().Contain("User not found");
    }
}
