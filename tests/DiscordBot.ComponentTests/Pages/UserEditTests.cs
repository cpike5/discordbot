using System.Security.Claims;
using Bunit.TestDoubles;
using DiscordBot.Bot.Blazor.Interop;
using DiscordBot.Bot.Blazor.Pages.Admin.Users;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DiscordBot.ComponentTests.Pages;

/// <summary>
/// Tests for the routed Blazor twin of the old Pages/Admin/Users/Edit.cshtml
/// (Phase F migration). Covers form load, the self-edit restrictions, the ported
/// OnPostAsync save, and the ConfirmModal-gated reset-password/unlink handlers.
/// </summary>
public class UserEditTests : TestContext
{
    private readonly Mock<IUserManagementService> _userService = new();

    private static readonly UserDto TargetUser = new()
    {
        Id = "user-2",
        Email = "alice@example.com",
        DisplayName = "Alice",
        IsActive = true,
        Roles = new[] { "Moderator" },
        IsDiscordLinked = true,
        DiscordUsername = "alice#1",
        DiscordAvatarUrl = "https://cdn.example.com/a.png"
    };

    public UserEditTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        _userService
            .Setup(s => s.GetUserByIdAsync("user-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(TargetUser);
        _userService
            .Setup(s => s.GetAvailableRolesAsync("admin-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "Admin", "Moderator", "Viewer" });

        Services.AddSingleton(_userService.Object);
        Services.AddScoped<ToastInterop>();
        Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        Services.AddScoped<CircuitClientInfoService>();
        Services.AddLogging();

        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("Admin User");
        auth.SetRoles("Admin");
        auth.SetClaims(new Claim(ClaimTypes.NameIdentifier, "admin-1"));
        auth.SetPolicies("RequireAdmin");
    }

    private IRenderedComponent<UserEdit> RenderPage(string id = "user-2")
    {
        var nav = Services.GetRequiredService<FakeNavigationManager>();
        nav.NavigateTo($"/Admin/Users/Edit?id={id}");
        return RenderComponent<UserEdit>();
    }

    [Fact]
    public void LoadsUser_IntoForm_WithDiscordSection()
    {
        var cut = RenderPage();

        cut.Find("#Input_Email").GetAttribute("value").Should().Be("alice@example.com");
        cut.Find("#Input_DisplayName").GetAttribute("value").Should().Be("Alice");
        cut.Find("#Input_Role").HasAttribute("disabled").Should().BeFalse();
        cut.Find("#Input_IsActive").HasAttribute("disabled").Should().BeFalse();

        cut.Markup.Should().Contain("alice#1");
        cut.Markup.Should().Contain("Unlink Discord");
        cut.Markup.Should().NotContain("You are editing your own account");
        cut.Markup.Should().Contain("/Admin/Users/Details?id=user-2");
    }

    [Fact]
    public void SelfEdit_ShowsWarning_AndLocksRoleAndActiveStatus()
    {
        _userService
            .Setup(s => s.GetUserByIdAsync("admin-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserDto
            {
                Id = "admin-1",
                Email = "admin@example.com",
                IsActive = true,
                Roles = new[] { "Admin" }
            });

        var cut = RenderPage("admin-1");

        cut.Markup.Should().Contain("You are editing your own account");
        cut.Find("#Input_Role").HasAttribute("disabled").Should().BeTrue();
        cut.Find("#Input_IsActive").HasAttribute("disabled").Should().BeTrue();
        cut.Markup.Should().Contain("You cannot disable your own account");
    }

    [Fact]
    public void ValidSave_CallsUpdateUser_WithActor()
    {
        UserUpdateDto? captured = null;
        _userService
            .Setup(s => s.UpdateUserAsync("user-2", It.IsAny<UserUpdateDto>(), "admin-1", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, UserUpdateDto, string, string?, CancellationToken>((_, dto, _, _, _) => captured = dto)
            .ReturnsAsync(UserManagementResult.Success());

        var cut = RenderPage();

        cut.Find("#Input_Email").Change("alice.new@example.com");
        cut.Find("#Input_Role").Change("Admin");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            captured.Should().NotBeNull();
            captured!.Email.Should().Be("alice.new@example.com");
            captured.Role.Should().Be("Admin");
            captured.IsActive.Should().BeTrue();
            cut.Markup.Should().Contain("User updated successfully");
        });
    }

    [Fact]
    public void ResetPassword_ConfirmedThroughModal_ShowsGeneratedPassword()
    {
        _userService
            .Setup(s => s.ResetPasswordAsync("user-2", "admin-1", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserManagementResult.SuccessWithPassword("Temp-Pass-123!", TargetUser));

        var cut = RenderPage();

        cut.FindAll("button").First(b => b.TextContent.Trim() == "Reset Password").Click();
        cut.WaitForAssertion(() => cut.Find("[role=alertdialog]"));
        cut.Find("[role=alertdialog]").QuerySelectorAll("button")
            .First(b => b.TextContent.Trim() == "Reset Password")
            .Click();

        cut.WaitForAssertion(() =>
        {
            _userService.Verify(s => s.ResetPasswordAsync(
                "user-2", "admin-1", It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
            cut.Markup.Should().Contain("New temporary password: Temp-Pass-123!");
        });
    }

    [Fact]
    public void UnlinkDiscord_ConfirmedThroughModal_RemovesDiscordSection()
    {
        _userService
            .Setup(s => s.UnlinkDiscordAccountAsync("user-2", "admin-1", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserManagementResult.Success());

        var cut = RenderPage();

        cut.FindAll("button").First(b => b.TextContent.Trim() == "Unlink Discord").Click();
        cut.WaitForAssertion(() => cut.Find("[role=alertdialog]"));
        cut.Find("[role=alertdialog]").QuerySelectorAll("button")
            .First(b => b.TextContent.Trim() == "Unlink Discord")
            .Click();

        cut.WaitForAssertion(() =>
        {
            _userService.Verify(s => s.UnlinkDiscordAccountAsync(
                "user-2", "admin-1", It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
            cut.Markup.Should().Contain("Discord account unlinked successfully");
            cut.Markup.Should().NotContain("Unlink Discord");
        });
    }
}
