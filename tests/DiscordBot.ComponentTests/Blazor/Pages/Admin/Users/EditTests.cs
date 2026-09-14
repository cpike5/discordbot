using System.Security.Claims;
using Bunit;
using Bunit.TestDoubles;
using DiscordBot.Bot.Blazor.Pages.Admin.Users;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DiscordBot.ComponentTests.Blazor.Pages.Admin.Users;

/// <summary>
/// Component tests for <see cref="Edit"/>, the routable replacement for
/// <c>Pages/Admin/Users/Edit.cshtml</c> + <c>EditModel</c> (docs/plans/blazor-port-plan.md §5
/// Phase 4, cluster 4a).
/// </summary>
public class EditTests : BlazorComponentTestContext
{
    private const string CurrentUserId = "admin-1";
    private const string OtherUserId = "user-2";

    private readonly Mock<IUserManagementService> _service = new();

    public EditTests()
    {
        Services.AddSingleton(_service.Object);
        _service.Setup(s => s.GetAvailableRolesAsync(CurrentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "Viewer", "Moderator", "Admin", "SuperAdmin" });
        AddAuthorization().SetAuthorized("admin").SetClaims(new Claim(ClaimTypes.NameIdentifier, CurrentUserId));
    }

    private static UserDto BuildUser(string id, string email = "user@example.test", string role = "Admin", bool discordLinked = false) => new()
    {
        Id = id,
        Email = email,
        IsActive = true,
        Roles = new[] { role },
        IsDiscordLinked = discordLinked,
        DiscordUsername = discordLinked ? "userTag" : null
    };

    private IRenderedComponent<Edit> RenderWithId(string id)
    {
        var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        navMan.NavigateTo(navMan.GetUriWithQueryParameter("id", id));
        return Render<Edit>();
    }

    [Fact]
    public void MissingId_ShowsNotFoundEmptyState()
    {
        var cut = Render<Edit>();

        cut.Markup.Should().Contain("User Not Found");
    }

    [Fact]
    public void UnknownId_ShowsNotFoundEmptyState()
    {
        _service.Setup(s => s.GetUserByIdAsync("missing", It.IsAny<CancellationToken>())).ReturnsAsync((UserDto?)null);

        var cut = RenderWithId("missing");

        cut.Markup.Should().Contain("User Not Found");
    }

    [Fact]
    public void SelfEdit_DisablesRoleAndActiveFields()
    {
        _service.Setup(s => s.GetUserByIdAsync(CurrentUserId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildUser(CurrentUserId));

        var cut = RenderWithId(CurrentUserId);

        cut.Find("#Input_Role").HasAttribute("disabled").Should().BeTrue();
        cut.Find("#Input_IsActive").HasAttribute("disabled").Should().BeTrue();
        cut.Markup.Should().Contain("editing your own account");
    }

    [Fact]
    public void OtherUserEdit_LeavesRoleAndActiveFieldsEnabled()
    {
        _service.Setup(s => s.GetUserByIdAsync(OtherUserId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildUser(OtherUserId));

        var cut = RenderWithId(OtherUserId);

        cut.Find("#Input_Role").HasAttribute("disabled").Should().BeFalse();
        cut.Find("#Input_IsActive").HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public void ValidSave_CallsUpdateUserAsync_AndToasts()
    {
        _service.Setup(s => s.GetUserByIdAsync(OtherUserId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildUser(OtherUserId));
        _service.Setup(s => s.UpdateUserAsync(OtherUserId, It.IsAny<UserUpdateDto>(), CurrentUserId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserManagementResult.Success());

        var cut = RenderWithId(OtherUserId);
        cut.Find("#Input_DisplayName").Input("New Name");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => _service.Verify(
            s => s.UpdateUserAsync(OtherUserId, It.Is<UserUpdateDto>(dto => dto.DisplayName == "New Name"), CurrentUserId, null, It.IsAny<CancellationToken>()),
            Times.Once));
        var toast = Services.GetRequiredService<IToastService>();
        cut.WaitForAssertion(() => toast.Toasts.Should().Contain(t => t.Level == ToastLevel.Success));
    }

    [Fact]
    public void ResetPassword_Confirmed_ShowsGeneratedPasswordInAlert()
    {
        _service.Setup(s => s.GetUserByIdAsync(OtherUserId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildUser(OtherUserId));
        _service.Setup(s => s.ResetPasswordAsync(OtherUserId, CurrentUserId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserManagementResult.SuccessWithPassword("Temp1234!", BuildUser(OtherUserId)));

        var cut = RenderWithId(OtherUserId);
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Reset Password").Click();
        cut.Find("#resetPasswordModal").QuerySelectorAll("button").Last().Click();

        cut.WaitForAssertion(() => _service.Verify(
            s => s.ResetPasswordAsync(OtherUserId, CurrentUserId, null, It.IsAny<CancellationToken>()), Times.Once));
        cut.WaitForAssertion(() => cut.Find("[data-testid='generated-password']").TextContent.Should().Contain("Temp1234!"));
        cut.Markup.Should().Contain("temporary password");
    }

    [Fact]
    public void UnlinkDiscord_Confirmed_CallsTheServiceAndReloads()
    {
        _service.Setup(s => s.GetUserByIdAsync(OtherUserId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildUser(OtherUserId, discordLinked: true));
        _service.Setup(s => s.UnlinkDiscordAccountAsync(OtherUserId, CurrentUserId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserManagementResult.Success());

        var cut = RenderWithId(OtherUserId);
        cut.Markup.Should().Contain("userTag");
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Unlink Discord").Click();
        cut.Find("#unlinkDiscordModal").QuerySelectorAll("button").Last().Click();

        cut.WaitForAssertion(() => _service.Verify(
            s => s.UnlinkDiscordAccountAsync(OtherUserId, CurrentUserId, null, It.IsAny<CancellationToken>()), Times.Once));
        var toast = Services.GetRequiredService<IToastService>();
        cut.WaitForAssertion(() => toast.Toasts.Should().Contain(t => t.Level == ToastLevel.Success));
    }
}
