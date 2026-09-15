using System.Security.Claims;
using Bunit;
using Bunit.TestDoubles;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using IndexPage = DiscordBot.Bot.Blazor.Pages.Admin.UserPurge.Index;

namespace DiscordBot.ComponentTests.Blazor.Pages.Admin.UserPurge;

/// <summary>
/// Component tests for <see cref="IndexPage"/>, the routable replacement for
/// <c>Pages/Admin/UserPurge.cshtml</c> + <c>UserPurgeModel</c>
/// (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4d).
/// </summary>
public class IndexTests : BlazorComponentTestContext
{
    private const ulong DiscordUserId = 123456789012345678UL;

    private readonly Mock<IUserPurgeService> _service = new();

    public IndexTests()
    {
        Services.AddSingleton(_service.Object);
        AddAuthorization().SetAuthorized("superadmin").SetClaims(new Claim(ClaimTypes.NameIdentifier, "admin-1"), new Claim(ClaimTypes.Role, "SuperAdmin"));
        SetInteractiveRendererInfo();
    }

    private void NavigateToUser()
    {
        var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        navMan.NavigateTo($"/Admin/UserPurge?DiscordUserId={DiscordUserId}");
    }

    [Fact]
    public void CannotPurge_ShowsTheBlockingReason()
    {
        _service.Setup(s => s.CanPurgeUserAsync(DiscordUserId)).ReturnsAsync((false, "User has an admin role"));
        NavigateToUser();

        var cut = Render<IndexPage>();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("User has an admin role"));
    }

    [Fact]
    public void Preview_RendersTable_WithAnonymizedBadgeForAnonymizedCategories()
    {
        _service.Setup(s => s.CanPurgeUserAsync(DiscordUserId)).ReturnsAsync((true, (string?)null));
        _service.Setup(s => s.PreviewPurgeAsync(DiscordUserId)).ReturnsAsync(UserPurgeResultDto.Succeeded(
            new Dictionary<string, int> { ["Messages"] = 3, ["ModerationNotes_Anonymized"] = 2 }, "corr-preview"));
        NavigateToUser();

        var cut = Render<IndexPage>();

        cut.WaitForAssertion(() => cut.FindAll("[data-testid='user-purge-preview-row']").Should().HaveCount(2));
        cut.Markup.Should().Contain("Anonymized");
    }

    [Fact]
    public void TypedConfirm_RequiredTextIsTheLookedUpDiscordUserId_ThenPurges()
    {
        _service.Setup(s => s.CanPurgeUserAsync(DiscordUserId)).ReturnsAsync((true, (string?)null));
        _service.Setup(s => s.PreviewPurgeAsync(DiscordUserId)).ReturnsAsync(UserPurgeResultDto.Succeeded(
            new Dictionary<string, int> { ["Messages"] = 1 }, "corr-preview"));
        _service.Setup(s => s.PurgeUserDataAsync(DiscordUserId, PurgeInitiator.Admin, "admin-1"))
            .ReturnsAsync(UserPurgeResultDto.Succeeded(new Dictionary<string, int> { ["Messages"] = 1 }, "corr-purge"));
        NavigateToUser();

        var cut = Render<IndexPage>();
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='user-purge-preview']").Should().NotBeEmpty());

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Purge This User's Data").Click();

        var confirmButton = cut.FindAll("#userPurgeModal button").Single(b => b.TextContent.Trim() == "Purge User Data");
        confirmButton.HasAttribute("disabled").Should().BeTrue();

        cut.Find("#userPurgeModalInput").Input(DiscordUserId.ToString());
        confirmButton = cut.FindAll("#userPurgeModal button").Single(b => b.TextContent.Trim() == "Purge User Data");
        confirmButton.HasAttribute("disabled").Should().BeFalse();
        confirmButton.Click();

        cut.WaitForAssertion(() => _service.Verify(s => s.PurgeUserDataAsync(DiscordUserId, PurgeInitiator.Admin, "admin-1"), Times.Once));
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("corr-purge"));
    }
}
