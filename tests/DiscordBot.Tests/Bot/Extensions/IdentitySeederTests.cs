using DiscordBot.Bot.Extensions;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Bot.Extensions;

/// <summary>
/// Unit tests for IdentitySeeder.
/// </summary>
public class IdentitySeederTests
{
    private readonly Mock<RoleManager<IdentityRole>> _mockRoleManager;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<ILogger> _mockLogger;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IOptions<IdentityConfigOptions>> _mockIdentityOptions;
    private readonly IdentityConfigOptions _identityConfigOptions;

    public IdentitySeederTests()
    {
        // Setup RoleManager mock
        var roleStore = new Mock<IRoleStore<IdentityRole>>();
        _mockRoleManager = new Mock<RoleManager<IdentityRole>>(
            roleStore.Object,
            Array.Empty<IRoleValidator<IdentityRole>>(),
            new Mock<ILookupNormalizer>().Object,
            new Mock<IdentityErrorDescriber>().Object,
            new Mock<ILogger<RoleManager<IdentityRole>>>().Object);

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

        // Setup logger mock
        _mockLogger = new Mock<ILogger>();

        // Setup identity options mock
        _identityConfigOptions = new IdentityConfigOptions();
        _mockIdentityOptions = new Mock<IOptions<IdentityConfigOptions>>();
        _mockIdentityOptions.Setup(x => x.Value).Returns(_identityConfigOptions);

        // Setup service provider mock
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockServiceProvider.Setup(sp => sp.GetService(typeof(RoleManager<IdentityRole>)))
            .Returns(_mockRoleManager.Object);
        _mockServiceProvider.Setup(sp => sp.GetService(typeof(UserManager<ApplicationUser>)))
            .Returns(_mockUserManager.Object);
        _mockServiceProvider.Setup(sp => sp.GetService(typeof(IOptions<IdentityConfigOptions>)))
            .Returns(_mockIdentityOptions.Object);
    }

    [Fact]
    public async Task SeedIdentityAsync_CreatesAllFourRoles()
    {
        // Arrange
        var createdRoles = new List<string>();

        _mockRoleManager.Setup(rm => rm.RoleExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        _mockRoleManager.Setup(rm => rm.CreateAsync(It.IsAny<IdentityRole>()))
            .Callback<IdentityRole>(role => createdRoles.Add(role.Name!))
            .ReturnsAsync(IdentityResult.Success);

        // No default admin configured
        _identityConfigOptions.DefaultAdmin = null;

        // Act
        await IdentitySeeder.SeedIdentityAsync(_mockServiceProvider.Object, _mockLogger.Object);

        // Assert
        createdRoles.Should().HaveCount(4, "there are four roles to create");
        createdRoles.Should().Contain(IdentitySeeder.Roles.SuperAdmin);
        createdRoles.Should().Contain(IdentitySeeder.Roles.Admin);
        createdRoles.Should().Contain(IdentitySeeder.Roles.Moderator);
        createdRoles.Should().Contain(IdentitySeeder.Roles.Viewer);
    }

    [Fact]
    public async Task SeedIdentityAsync_IsIdempotent_DoesNotDuplicateRoles()
    {
        // Arrange
        _mockRoleManager.Setup(rm => rm.RoleExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(true); // All roles already exist

        // No default admin configured
        _identityConfigOptions.DefaultAdmin = null;

        // Act
        await IdentitySeeder.SeedIdentityAsync(_mockServiceProvider.Object, _mockLogger.Object);

        // Assert
        _mockRoleManager.Verify(
            rm => rm.CreateAsync(It.IsAny<IdentityRole>()),
            Times.Never,
            "no roles should be created if they already exist");
    }

    [Fact]
    public async Task SeedIdentityAsync_WithSomeExistingRoles_OnlyCreatesNewRoles()
    {
        // Arrange
        var createdRoles = new List<string>();

        // SuperAdmin and Admin already exist
        _mockRoleManager.Setup(rm => rm.RoleExistsAsync(IdentitySeeder.Roles.SuperAdmin))
            .ReturnsAsync(true);
        _mockRoleManager.Setup(rm => rm.RoleExistsAsync(IdentitySeeder.Roles.Admin))
            .ReturnsAsync(true);

        // Moderator and Viewer do not exist
        _mockRoleManager.Setup(rm => rm.RoleExistsAsync(IdentitySeeder.Roles.Moderator))
            .ReturnsAsync(false);
        _mockRoleManager.Setup(rm => rm.RoleExistsAsync(IdentitySeeder.Roles.Viewer))
            .ReturnsAsync(false);

        _mockRoleManager.Setup(rm => rm.CreateAsync(It.IsAny<IdentityRole>()))
            .Callback<IdentityRole>(role => createdRoles.Add(role.Name!))
            .ReturnsAsync(IdentityResult.Success);

        // No default admin configured
        _identityConfigOptions.DefaultAdmin = null;

        // Act
        await IdentitySeeder.SeedIdentityAsync(_mockServiceProvider.Object, _mockLogger.Object);

        // Assert
        createdRoles.Should().HaveCount(2, "only two roles need to be created");
        createdRoles.Should().Contain(IdentitySeeder.Roles.Moderator);
        createdRoles.Should().Contain(IdentitySeeder.Roles.Viewer);
        createdRoles.Should().NotContain(IdentitySeeder.Roles.SuperAdmin);
        createdRoles.Should().NotContain(IdentitySeeder.Roles.Admin);
    }

    [Fact]
    public async Task SeedDefaultAdminAsync_WithConfigProvided_CreatesAdminUser()
    {
        // Arrange
        const string adminEmail = "admin@example.com";
        const string adminPassword = "SecurePassword123!";
        ApplicationUser? createdUser = null;
        string? assignedRole = null;

        _mockRoleManager.Setup(rm => rm.RoleExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(true); // All roles exist

        _identityConfigOptions.DefaultAdmin = new DefaultAdminOptions
        {
            Email = adminEmail,
            Password = adminPassword
        };

        _mockUserManager.Setup(um => um.FindByEmailAsync(adminEmail))
            .ReturnsAsync((ApplicationUser?)null); // User does not exist

        _mockUserManager.Setup(um => um.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .Callback<ApplicationUser, string>((user, password) =>
            {
                createdUser = user;
            })
            .ReturnsAsync(IdentityResult.Success);

        _mockUserManager.Setup(um => um.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .Callback<ApplicationUser, string>((user, role) =>
            {
                assignedRole = role;
            })
            .ReturnsAsync(IdentityResult.Success);

        // Act
        await IdentitySeeder.SeedIdentityAsync(_mockServiceProvider.Object, _mockLogger.Object);

        // Assert
        createdUser.Should().NotBeNull("admin user should be created");
        createdUser!.Email.Should().Be(adminEmail);
        createdUser.UserName.Should().Be(adminEmail);
        createdUser.EmailConfirmed.Should().BeTrue("seeded admin user should have confirmed email");
        createdUser.IsActive.Should().BeTrue("seeded admin user should be active");
        createdUser.DisplayName.Should().Be("System Administrator");

        assignedRole.Should().Be(IdentitySeeder.Roles.SuperAdmin,
            "admin user should be assigned SuperAdmin role");

        _mockUserManager.Verify(
            um => um.CreateAsync(It.IsAny<ApplicationUser>(), adminPassword),
            Times.Once,
            "user should be created with provided password");
    }

    [Fact]
    public async Task SeedDefaultAdminAsync_WithNoEmailConfig_DoesNotCreateUser()
    {
        // Arrange
        _mockRoleManager.Setup(rm => rm.RoleExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(true);

        // No email configured
        _identityConfigOptions.DefaultAdmin = new DefaultAdminOptions
        {
            Email = null,
            Password = "SomePassword123!"
        };

        // Act
        await IdentitySeeder.SeedIdentityAsync(_mockServiceProvider.Object, _mockLogger.Object);

        // Assert
        _mockUserManager.Verify(
            um => um.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never,
            "user should not be created when email is not configured");
    }

    [Fact]
    public async Task SeedDefaultAdminAsync_WithNoPasswordConfig_DoesNotCreateUser()
    {
        // Arrange
        _mockRoleManager.Setup(rm => rm.RoleExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(true);

        // No password configured
        _identityConfigOptions.DefaultAdmin = new DefaultAdminOptions
        {
            Email = "admin@example.com",
            Password = null
        };

        // Act
        await IdentitySeeder.SeedIdentityAsync(_mockServiceProvider.Object, _mockLogger.Object);

        // Assert
        _mockUserManager.Verify(
            um => um.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never,
            "user should not be created when password is not configured");
    }

    [Fact]
    public async Task SeedDefaultAdminAsync_WithEmptyEmailConfig_DoesNotCreateUser()
    {
        // Arrange
        _mockRoleManager.Setup(rm => rm.RoleExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(true);

        // Whitespace-only email
        _identityConfigOptions.DefaultAdmin = new DefaultAdminOptions
        {
            Email = "   ",
            Password = "SomePassword123!"
        };

        // Act
        await IdentitySeeder.SeedIdentityAsync(_mockServiceProvider.Object, _mockLogger.Object);

        // Assert
        _mockUserManager.Verify(
            um => um.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never,
            "user should not be created when email is whitespace");
    }

    [Fact]
    public async Task SeedDefaultAdminAsync_WithExistingAdmin_DoesNotDuplicateUser()
    {
        // Arrange
        const string adminEmail = "admin@example.com";
        var existingUser = new ApplicationUser
        {
            Email = adminEmail,
            UserName = adminEmail
        };

        _mockRoleManager.Setup(rm => rm.RoleExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(true);

        _identityConfigOptions.DefaultAdmin = new DefaultAdminOptions
        {
            Email = adminEmail,
            Password = "SecurePassword123!"
        };

        _mockUserManager.Setup(um => um.FindByEmailAsync(adminEmail))
            .ReturnsAsync(existingUser); // User already exists

        // Act
        await IdentitySeeder.SeedIdentityAsync(_mockServiceProvider.Object, _mockLogger.Object);

        // Assert
        _mockUserManager.Verify(
            um => um.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never,
            "user should not be created if admin already exists");

        _mockUserManager.Verify(
            um => um.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never,
            "role should not be assigned if admin already exists");
    }

    [Fact]
    public async Task SeedDefaultAdminAsync_WhenUserCreationFails_DoesNotAssignRole()
    {
        // Arrange
        const string adminEmail = "admin@example.com";

        _mockRoleManager.Setup(rm => rm.RoleExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(true);

        _identityConfigOptions.DefaultAdmin = new DefaultAdminOptions
        {
            Email = adminEmail,
            Password = "SecurePassword123!"
        };

        _mockUserManager.Setup(um => um.FindByEmailAsync(adminEmail))
            .ReturnsAsync((ApplicationUser?)null);

        _mockUserManager.Setup(um => um.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError
            {
                Code = "DuplicateUserName",
                Description = "Username already exists"
            }));

        // Act
        await IdentitySeeder.SeedIdentityAsync(_mockServiceProvider.Object, _mockLogger.Object);

        // Assert
        _mockUserManager.Verify(
            um => um.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never,
            "role should not be assigned when user creation fails");
    }

    [Fact]
    public void Roles_ContainsAllExpectedRoleNames()
    {
        // Assert
        IdentitySeeder.Roles.SuperAdmin.Should().Be("SuperAdmin");
        IdentitySeeder.Roles.Admin.Should().Be("Admin");
        IdentitySeeder.Roles.Moderator.Should().Be("Moderator");
        IdentitySeeder.Roles.Viewer.Should().Be("Viewer");
    }
}
