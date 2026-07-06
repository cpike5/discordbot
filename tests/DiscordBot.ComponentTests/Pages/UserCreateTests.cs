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
/// Tests for the routed Blazor twin of the old Pages/Admin/Users/Create.cshtml
/// (Phase F migration). Covers form rendering, DataAnnotations validation, and the
/// ported OnPostAsync handler (same UserCreateDto + actor arguments to the service).
/// </summary>
public class UserCreateTests : TestContext
{
    private readonly Mock<IUserManagementService> _userService = new();

    public UserCreateTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

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

    [Fact]
    public void RendersForm_WithRoleOptionsFromService()
    {
        var cut = RenderComponent<UserCreate>();

        cut.Find("#Input_Email");
        cut.Find("#Input_Password");
        cut.Find("#Input_ConfirmPassword");
        cut.Find("#Input_SendWelcomeEmail");

        var roleOptions = cut.FindAll("#Input_Role option");
        roleOptions.Select(o => o.TextContent).Should().Contain(new[] { "Admin", "Moderator", "Viewer" });
        cut.Markup.Should().Contain("About User Creation");
    }

    [Fact]
    public void SubmittingEmptyForm_ShowsValidationErrors_AndDoesNotCallService()
    {
        var cut = RenderComponent<UserCreate>();

        cut.Find("form").Submit();

        cut.Markup.Should().Contain("Email is required");
        cut.Markup.Should().Contain("Password is required");
        _userService.Verify(s => s.CreateUserAsync(
            It.IsAny<UserCreateDto>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void ValidSubmit_CreatesUser_WithActor_AndNavigatesToIndex()
    {
        UserCreateDto? captured = null;
        _userService
            .Setup(s => s.CreateUserAsync(It.IsAny<UserCreateDto>(), "admin-1", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<UserCreateDto, string, string?, CancellationToken>((dto, _, _, _) => captured = dto)
            .ReturnsAsync(UserManagementResult.Success());

        var cut = RenderComponent<UserCreate>();

        cut.Find("#Input_Email").Change("new@example.com");
        cut.Find("#Input_DisplayName").Change("New User");
        cut.Find("#Input_Password").Change("Password1!");
        cut.Find("#Input_ConfirmPassword").Change("Password1!");
        cut.Find("#Input_Role").Change("Moderator");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            captured.Should().NotBeNull();
            captured!.Email.Should().Be("new@example.com");
            captured.DisplayName.Should().Be("New User");
            captured.Password.Should().Be("Password1!");
            captured.Role.Should().Be("Moderator");
            captured.SendWelcomeEmail.Should().BeTrue();

            Services.GetRequiredService<FakeNavigationManager>()
                .Uri.Should().EndWith("/Admin/Users");
        });
    }

    [Fact]
    public void FailedCreate_ShowsServiceError_AndStaysOnPage()
    {
        _userService
            .Setup(s => s.CreateUserAsync(It.IsAny<UserCreateDto>(), "admin-1", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserManagementResult.Failure("DuplicateEmail", "A user with this email already exists"));

        var cut = RenderComponent<UserCreate>();

        cut.Find("#Input_Email").Change("dupe@example.com");
        cut.Find("#Input_Password").Change("Password1!");
        cut.Find("#Input_ConfirmPassword").Change("Password1!");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
            cut.Markup.Should().Contain("A user with this email already exists"));
    }
}
