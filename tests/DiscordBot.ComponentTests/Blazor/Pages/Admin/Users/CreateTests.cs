using System.Security.Claims;
using Bunit;
using Bunit.TestDoubles;
using DiscordBot.Bot.Blazor.Pages.Admin.Users;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DiscordBot.ComponentTests.Blazor.Pages.Admin.Users;

/// <summary>
/// Component tests for <see cref="Create"/>, the routable replacement for
/// <c>Pages/Admin/Users/Create.cshtml</c> + <c>CreateModel</c> (docs/plans/blazor-port-plan.md §5
/// Phase 4, cluster 4a).
/// </summary>
public class CreateTests : BlazorComponentTestContext
{
    private const string CurrentUserId = "admin-1";

    private readonly Mock<IUserManagementService> _service = new();

    public CreateTests()
    {
        Services.AddSingleton(_service.Object);
        _service.Setup(s => s.GetAvailableRolesAsync(CurrentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "Viewer", "Moderator", "Admin" });
        AddAuthorization().SetAuthorized("admin").SetClaims(new Claim(ClaimTypes.NameIdentifier, CurrentUserId));
    }

    private static void Fill(IRenderedComponent<Create> cut, string email = "new@example.test", string password = "Password123!", string confirm = "Password123!")
    {
        cut.Find("#Input_Email").Input(email);
        cut.Find("#Input_Password").Input(password);
        cut.Find("#Input_ConfirmPassword").Input(confirm);
    }

    [Fact]
    public void EmptyEmail_BlocksSubmit_AndDoesNotCallTheService()
    {
        SetInteractiveRendererInfo();
        var cut = Render<Create>();

        cut.Find("#Input_Password").Input("Password123!");
        cut.Find("#Input_ConfirmPassword").Input("Password123!");
        cut.Find("form").Submit();

        cut.Markup.Should().Contain("Email is required");
        _service.Verify(s => s.CreateUserAsync(It.IsAny<UserCreateDto>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void MismatchedPasswords_BlocksSubmit_AndDoesNotCallTheService()
    {
        SetInteractiveRendererInfo();
        var cut = Render<Create>();

        cut.Find("#Input_Email").Input("new@example.test");
        cut.Find("#Input_Password").Input("Password123!");
        cut.Find("#Input_ConfirmPassword").Input("Different123!");
        cut.Find("form").Submit();

        cut.Markup.Should().Contain("Passwords do not match");
        _service.Verify(s => s.CreateUserAsync(It.IsAny<UserCreateDto>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void ValidSubmit_CallsCreateUserAsync_WithTheRightDto_ThenNavigatesToIndex()
    {
        _service.Setup(s => s.CreateUserAsync(It.IsAny<UserCreateDto>(), CurrentUserId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserManagementResult.Success(new UserDto { Id = "new-1", Email = "new@example.test" }));
        var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();

        SetInteractiveRendererInfo();
        var cut = Render<Create>();
        Fill(cut);
        cut.Find("#Input_Role").Change("Admin");
        cut.Find("form").Submit();

        _service.Verify(s => s.CreateUserAsync(
            It.Is<UserCreateDto>(dto => dto.Email == "new@example.test" && dto.Password == "Password123!" && dto.ConfirmPassword == "Password123!" && dto.Role == "Admin" && dto.SendWelcomeEmail),
            CurrentUserId, null, It.IsAny<CancellationToken>()), Times.Once);
        navMan.Uri.Should().EndWith("/Admin/Users");
    }

    [Fact]
    public void ServiceFailure_ShowsInlineError_AndDoesNotNavigate()
    {
        _service.Setup(s => s.CreateUserAsync(It.IsAny<UserCreateDto>(), CurrentUserId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserManagementResult.Failure(UserManagementResult.EmailAlreadyExists, "A user with this email already exists"));
        var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        var startUri = navMan.Uri;

        SetInteractiveRendererInfo();
        var cut = Render<Create>();
        Fill(cut);
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("A user with this email already exists"));
        navMan.Uri.Should().Be(startUri);
    }
}
