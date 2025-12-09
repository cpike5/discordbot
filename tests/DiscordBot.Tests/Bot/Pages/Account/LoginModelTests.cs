using System.Security.Claims;
using DiscordBot.Bot.Pages.Account;
using DiscordBot.Bot.Services;
using DiscordBot.Core.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Pages.Account;

/// <summary>
/// Unit tests for LoginModel Razor Page.
/// </summary>
public class LoginModelTests
{
    private readonly Mock<SignInManager<ApplicationUser>> _mockSignInManager;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<ILogger<LoginModel>> _mockLogger;
    private readonly DiscordOAuthSettings _discordOAuthSettings;
    private readonly LoginModel _loginModel;
    private readonly Mock<IAuthenticationService> _mockAuthService;
    private readonly Mock<IServiceProvider> _mockServiceProvider;

    public LoginModelTests()
    {
        // Setup UserManager mock
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object,
            null!,
            new Mock<IPasswordHasher<ApplicationUser>>().Object,
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new Mock<ILookupNormalizer>().Object,
            new Mock<IdentityErrorDescriber>().Object,
            new Mock<IServiceProvider>().Object,
            new Mock<ILogger<UserManager<ApplicationUser>>>().Object);

        // Setup SignInManager mock
        var contextAccessor = new Mock<IHttpContextAccessor>();
        var claimsFactory = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
        _mockSignInManager = new Mock<SignInManager<ApplicationUser>>(
            _mockUserManager.Object,
            contextAccessor.Object,
            claimsFactory.Object,
            null!,
            new Mock<ILogger<SignInManager<ApplicationUser>>>().Object,
            new Mock<IAuthenticationSchemeProvider>().Object,
            null!);

        // Setup logger mock
        _mockLogger = new Mock<ILogger<LoginModel>>();

        // Setup Discord OAuth settings (configured by default for tests)
        _discordOAuthSettings = new DiscordOAuthSettings { IsConfigured = true };

        // Setup authentication service mock
        _mockAuthService = new Mock<IAuthenticationService>();

        // Setup service provider mock
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockServiceProvider.Setup(sp => sp.GetService(typeof(IAuthenticationService)))
            .Returns(_mockAuthService.Object);

        // Create LoginModel instance
        _loginModel = new LoginModel(
            _mockSignInManager.Object,
            _mockUserManager.Object,
            _mockLogger.Object,
            _discordOAuthSettings);

        // Setup HttpContext
        var httpContext = new DefaultHttpContext
        {
            RequestServices = _mockServiceProvider.Object
        };

        _loginModel.PageContext = new PageContext
        {
            HttpContext = httpContext
        };

        // Setup URL helper
        var urlHelper = new Mock<IUrlHelper>();
        urlHelper.Setup(u => u.Content(It.IsAny<string>()))
            .Returns<string>(path => path);
        _loginModel.Url = urlHelper.Object;

        // Setup TempData
        _loginModel.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
    }

    [Fact]
    public void OnGet_SetsReturnUrl()
    {
        // Arrange
        const string expectedReturnUrl = "/dashboard";

        // Act
        var result = _loginModel.OnGet(expectedReturnUrl);

        // Assert
        result.Should().BeOfType<PageResult>();
        _loginModel.ReturnUrl.Should().Be(expectedReturnUrl);
    }

    [Fact]
    public void OnGet_DefaultsReturnUrlToHome()
    {
        // Act
        var result = _loginModel.OnGet();

        // Assert
        result.Should().BeOfType<PageResult>();
        _loginModel.ReturnUrl.Should().Be("~/");
    }

    [Fact]
    public void OnGet_WhenAuthenticated_RedirectsToReturnUrl()
    {
        // Arrange
        var claims = new List<Claim> { new Claim(ClaimTypes.Name, "testuser") };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = _mockServiceProvider.Object,
            User = principal
        };
        _loginModel.PageContext = new PageContext { HttpContext = httpContext };

        // Act
        var result = _loginModel.OnGet("/dashboard");

        // Assert
        result.Should().BeOfType<LocalRedirectResult>();
        var redirectResult = (LocalRedirectResult)result;
        redirectResult.Url.Should().Be("/dashboard");
    }

    [Fact]
    public async Task OnPostAsync_WithInvalidModelState_ReturnsPageResult()
    {
        // Arrange
        _loginModel.ModelState.AddModelError("Email", "Email is required");

        // Act
        var result = await _loginModel.OnPostAsync();

        // Assert
        result.Should().BeOfType<PageResult>();
        _mockSignInManager.Verify(
            sm => sm.PasswordSignInAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()),
            Times.Never,
            "sign-in should not be attempted with invalid model state");
    }

    [Fact]
    public async Task OnPostAsync_WithInactiveUser_ReturnsPageWithError()
    {
        // Arrange
        const string email = "inactive@example.com";
        var inactiveUser = new ApplicationUser
        {
            Email = email,
            UserName = email,
            IsActive = false
        };

        _loginModel.Input = new LoginModel.InputModel
        {
            Email = email,
            Password = "Password123!",
            RememberMe = false
        };

        _mockUserManager.Setup(um => um.FindByEmailAsync(email))
            .ReturnsAsync(inactiveUser);

        // Act
        var result = await _loginModel.OnPostAsync();

        // Assert
        result.Should().BeOfType<PageResult>();
        _loginModel.ModelState.IsValid.Should().BeFalse();
        _loginModel.ModelState[string.Empty]!.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Contain("deactivated");

        _mockSignInManager.Verify(
            sm => sm.PasswordSignInAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()),
            Times.Never,
            "sign-in should not be attempted for inactive users");
    }

    [Fact]
    public async Task OnPostAsync_WithValidCredentials_UpdatesLastLoginAndRedirects()
    {
        // Arrange
        const string email = "user@example.com";
        const string password = "Password123!";
        const string returnUrl = "/dashboard";

        var user = new ApplicationUser
        {
            Email = email,
            UserName = email,
            IsActive = true,
            LastLoginAt = null
        };

        _loginModel.Input = new LoginModel.InputModel
        {
            Email = email,
            Password = password,
            RememberMe = true
        };

        _mockUserManager.Setup(um => um.FindByEmailAsync(email))
            .ReturnsAsync(user);

        _mockSignInManager.Setup(sm => sm.PasswordSignInAsync(
            email,
            password,
            true,
            true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

        _mockUserManager.Setup(um => um.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _loginModel.OnPostAsync(returnUrl);

        // Assert
        result.Should().BeOfType<LocalRedirectResult>()
            .Which.Url.Should().Be(returnUrl);

        user.LastLoginAt.Should().NotBeNull();
        user.LastLoginAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));

        _mockUserManager.Verify(
            um => um.UpdateAsync(It.Is<ApplicationUser>(u => u.LastLoginAt != null)),
            Times.Once,
            "LastLoginAt should be updated on successful login");
    }

    [Fact]
    public async Task OnPostAsync_WithInvalidCredentials_ReturnsPageWithError()
    {
        // Arrange
        const string email = "user@example.com";
        const string password = "WrongPassword";

        var user = new ApplicationUser
        {
            Email = email,
            UserName = email,
            IsActive = true
        };

        _loginModel.Input = new LoginModel.InputModel
        {
            Email = email,
            Password = password,
            RememberMe = false
        };

        _mockUserManager.Setup(um => um.FindByEmailAsync(email))
            .ReturnsAsync(user);

        _mockSignInManager.Setup(sm => sm.PasswordSignInAsync(
            email,
            password,
            false,
            true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        // Act
        var result = await _loginModel.OnPostAsync();

        // Assert
        result.Should().BeOfType<PageResult>();
        _loginModel.ModelState.IsValid.Should().BeFalse();
        _loginModel.ModelState[string.Empty]!.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Contain("Invalid email or password");
    }

    [Fact]
    public async Task OnPostAsync_WithLockedOutUser_RedirectsToLockoutPage()
    {
        // Arrange
        const string email = "locked@example.com";
        const string password = "Password123!";

        var user = new ApplicationUser
        {
            Email = email,
            UserName = email,
            IsActive = true
        };

        _loginModel.Input = new LoginModel.InputModel
        {
            Email = email,
            Password = password,
            RememberMe = false
        };

        _mockUserManager.Setup(um => um.FindByEmailAsync(email))
            .ReturnsAsync(user);

        _mockSignInManager.Setup(sm => sm.PasswordSignInAsync(
            email,
            password,
            false,
            true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.LockedOut);

        // Act
        var result = await _loginModel.OnPostAsync();

        // Assert
        result.Should().BeOfType<RedirectToPageResult>("locked out users should be redirected");
        var redirectResult = result as RedirectToPageResult;
        redirectResult!.PageName.Should().Be("./Lockout");
    }

    [Fact]
    public async Task OnPostAsync_WithTwoFactorRequired_RedirectsToTwoFactorPage()
    {
        // Arrange
        const string email = "2fa@example.com";
        const string password = "Password123!";
        const string returnUrl = "/dashboard";

        var user = new ApplicationUser
        {
            Email = email,
            UserName = email,
            IsActive = true
        };

        _loginModel.Input = new LoginModel.InputModel
        {
            Email = email,
            Password = password,
            RememberMe = true
        };

        _mockUserManager.Setup(um => um.FindByEmailAsync(email))
            .ReturnsAsync(user);

        _mockSignInManager.Setup(sm => sm.PasswordSignInAsync(
            email,
            password,
            true,
            true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.TwoFactorRequired);

        // Act
        var result = await _loginModel.OnPostAsync(returnUrl);

        // Assert
        result.Should().BeOfType<RedirectToPageResult>("users requiring 2FA should be redirected");
        var redirectResult = result as RedirectToPageResult;
        redirectResult!.PageName.Should().Be("./LoginWith2fa");
    }

    [Fact]
    public async Task OnPostAsync_PassesLockoutParameterCorrectly()
    {
        // Arrange
        const string email = "user@example.com";
        const string password = "Password123!";

        var user = new ApplicationUser
        {
            Email = email,
            UserName = email,
            IsActive = true
        };

        _loginModel.Input = new LoginModel.InputModel
        {
            Email = email,
            Password = password,
            RememberMe = false
        };

        _mockUserManager.Setup(um => um.FindByEmailAsync(email))
            .ReturnsAsync(user);

        _mockSignInManager.Setup(sm => sm.PasswordSignInAsync(
            email,
            password,
            false,
            true)) // lockoutOnFailure should be true
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        // Act
        await _loginModel.OnPostAsync();

        // Assert
        _mockSignInManager.Verify(
            sm => sm.PasswordSignInAsync(email, password, false, true),
            Times.Once,
            "lockoutOnFailure should be enabled");
    }

    [Fact]
    public void IsDiscordOAuthConfigured_ReturnsSettingsValue()
    {
        // Arrange - settings is configured by default in test constructor

        // Assert
        _loginModel.IsDiscordOAuthConfigured.Should().BeTrue();
    }

    [Fact]
    public void OnPostDiscordLogin_WhenOAuthNotConfigured_ReturnsPageWithError()
    {
        // Arrange
        var oauthSettings = new DiscordOAuthSettings { IsConfigured = false };
        var loginModel = new LoginModel(
            _mockSignInManager.Object,
            _mockUserManager.Object,
            _mockLogger.Object,
            oauthSettings);

        // Setup URL helper for the new instance
        var urlHelper = new Mock<IUrlHelper>();
        urlHelper.Setup(u => u.Content(It.IsAny<string>()))
            .Returns<string>(path => path);
        loginModel.Url = urlHelper.Object;

        // Setup HttpContext
        var httpContext = new DefaultHttpContext
        {
            RequestServices = _mockServiceProvider.Object
        };
        loginModel.PageContext = new PageContext
        {
            HttpContext = httpContext
        };

        // Act
        var result = loginModel.OnPostDiscordLogin("/dashboard");

        // Assert
        result.Should().BeOfType<PageResult>();
        loginModel.ModelState.IsValid.Should().BeFalse();
        loginModel.ModelState[string.Empty]!.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Contain("Discord login is not available");
    }

    // Note: Full Discord OAuth flow tests are skipped due to complexity of mocking IUrlHelper extension methods.
    // Discord OAuth functionality should be tested via integration tests instead.

    [Fact]
    public void InputModel_EmailIsRequired()
    {
        // Arrange
        var input = new LoginModel.InputModel
        {
            Email = "",
            Password = "Password123!"
        };

        // Act
        var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(input);
        var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            input, validationContext, validationResults, validateAllProperties: true);

        // Assert
        isValid.Should().BeFalse();
        validationResults.Should().ContainSingle(vr => vr.MemberNames.Contains("Email"));
    }

    [Fact]
    public void InputModel_PasswordIsRequired()
    {
        // Arrange
        var input = new LoginModel.InputModel
        {
            Email = "user@example.com",
            Password = ""
        };

        // Act
        var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(input);
        var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            input, validationContext, validationResults, validateAllProperties: true);

        // Assert
        isValid.Should().BeFalse();
        validationResults.Should().ContainSingle(vr => vr.MemberNames.Contains("Password"));
    }

    [Fact]
    public void InputModel_ValidEmail_PassesValidation()
    {
        // Arrange
        var input = new LoginModel.InputModel
        {
            Email = "user@example.com",
            Password = "Password123!",
            RememberMe = true
        };

        // Act
        var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(input);
        var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            input, validationContext, validationResults, validateAllProperties: true);

        // Assert
        isValid.Should().BeTrue();
        validationResults.Should().BeEmpty();
    }
}
