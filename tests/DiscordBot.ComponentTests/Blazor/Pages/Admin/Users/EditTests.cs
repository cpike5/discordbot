using System.Reflection;
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
        SetInteractiveRendererInfo();
        return Render<Edit>();
    }

    /// <summary>
    /// <c>CanManageUserAsync</c> is unconfigured-by-default false on this loose mock (Moq's
    /// built-in default for an unconfigured <c>Task&lt;bool&gt;</c> method), so every test that
    /// exercises the "editing someone else" path must opt in explicitly - this is also what
    /// exercises the "actor can manage this target" branch of <see cref="Edit.CanAccessEdit"/>.
    /// </summary>
    private void AllowManage(string targetId) =>
        _service.Setup(s => s.CanManageUserAsync(CurrentUserId, targetId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

    [Fact]
    public void MissingId_ShowsNotFoundEmptyState()
    {
        SetInteractiveRendererInfo();
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
        AllowManage(OtherUserId);

        var cut = RenderWithId(OtherUserId);

        cut.Find("#Input_Role").HasAttribute("disabled").Should().BeFalse();
        cut.Find("#Input_IsActive").HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public void ValidSave_CallsUpdateUserAsync_AndToasts()
    {
        _service.Setup(s => s.GetUserByIdAsync(OtherUserId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildUser(OtherUserId));
        AllowManage(OtherUserId);
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
        AllowManage(OtherUserId);
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
        AllowManage(OtherUserId);
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

    /// <summary>
    /// Closes the gap the deleted legacy <c>EditModel</c> left open (see <see cref="Edit.CanAccessEdit"/>'s
    /// doc comment and docs/plans - the review finding on this cluster): with
    /// <c>CanManageUserAsync</c> refusing (the default on this mock unless <see cref="AllowManage"/>
    /// is called - e.g. an Admin actor against a SuperAdmin target), the whole edit form is
    /// replaced by an access-denied empty state instead of rendering editable fields for a user
    /// the actor has no authority over.
    /// </summary>
    [Fact]
    public void CannotManageTarget_ShowsAccessDeniedInsteadOfTheForm()
    {
        _service.Setup(s => s.GetUserByIdAsync(OtherUserId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildUser(OtherUserId, discordLinked: true));
        // CanManageUserAsync left unconfigured -> false, simulating an actor without authority.

        var cut = RenderWithId(OtherUserId);

        cut.Markup.Should().Contain("Cannot Manage This User");
        cut.FindAll("#Input_Role").Should().BeEmpty();
        var buttonLabels = cut.FindAll("button").Select(b => b.TextContent.Trim()).ToList();
        buttonLabels.Should().NotContain("Reset Password");
        buttonLabels.Should().NotContain("Unlink Discord");
    }

    /// <summary>
    /// Defense in depth for <see cref="CannotManageTarget_ShowsAccessDeniedInsteadOfTheForm"/>:
    /// even if a handler were invoked directly (bypassing the hidden-control UI gate - e.g. a
    /// stale render), the service methods must never be called for a target the actor cannot
    /// manage.
    /// </summary>
    [Fact]
    public async Task CannotManageTarget_HandlersRefuse_EvenIfInvokedDirectly()
    {
        _service.Setup(s => s.GetUserByIdAsync(OtherUserId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildUser(OtherUserId, discordLinked: true));

        var cut = RenderWithId(OtherUserId);

        // RequestResetPassword/RequestUnlinkDiscord are `protected` - reflection stands in for a
        // same-assembly caller (a component in a `RenderFragment`, a subclass) that could still
        // reach them despite the UI hiding their buttons.
        await InvokeProtectedAsync(cut, "RequestResetPassword");
        await InvokeProtectedAsync(cut, "RequestUnlinkDiscord");

        _service.Verify(s => s.ResetPasswordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        _service.Verify(s => s.UnlinkDiscordAccountAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static async Task InvokeProtectedAsync(IRenderedComponent<Edit> cut, string methodName)
    {
        var method = typeof(Edit).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"{methodName} not found on {typeof(Edit)}.");
        await cut.InvokeAsync(async () => await (Task)method.Invoke(cut.Instance, null)!);
    }
}
