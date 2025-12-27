using System.Security.Claims;
using DiscordBot.Bot.Services;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="UserManagementService"/>.
/// Tests cover self-protection, role hierarchy, CRUD operations, and activity logging.
/// </summary>
public class UserManagementServiceTests : IDisposable
{
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<RoleManager<IdentityRole>> _mockRoleManager;
    private readonly Mock<ILogger<UserManagementService>> _mockLogger;
    private readonly Mock<IAuditLogService> _mockAuditLogService;
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly BotDbContext _dbContext;
    private readonly UserManagementService _service;
    private readonly SqliteConnection _connection;

    public UserManagementServiceTests()
    {
        // Setup SQLite in-memory database
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<BotDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new BotDbContext(options);
        _dbContext.Database.EnsureCreated();

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

        // Setup RoleManager mock
        var roleStore = new Mock<IRoleStore<IdentityRole>>();
        _mockRoleManager = new Mock<RoleManager<IdentityRole>>(
            roleStore.Object,
            Array.Empty<IRoleValidator<IdentityRole>>(),
            new Mock<ILookupNormalizer>().Object,
            new Mock<IdentityErrorDescriber>().Object,
            new Mock<ILogger<RoleManager<IdentityRole>>>().Object);

        _mockLogger = new Mock<ILogger<UserManagementService>>();
        _mockAuditLogService = new Mock<IAuditLogService>();
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

        // Setup audit log service to return a builder that returns itself for fluent API
        var mockBuilder = new Mock<IAuditLogBuilder>();
        mockBuilder.Setup(x => x.ForCategory(It.IsAny<AuditLogCategory>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.WithAction(It.IsAny<AuditLogAction>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.ByUser(It.IsAny<string>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.BySystem()).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.ByBot()).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.OnTarget(It.IsAny<string>(), It.IsAny<string>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.InGuild(It.IsAny<ulong>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.WithDetails(It.IsAny<Dictionary<string, object?>>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.WithDetails(It.IsAny<object>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.FromIpAddress(It.IsAny<string>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.WithCorrelationId(It.IsAny<string>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.LogAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        _mockAuditLogService.Setup(x => x.CreateBuilder()).Returns(mockBuilder.Object);

        _service = new UserManagementService(
            _mockUserManager.Object,
            _mockRoleManager.Object,
            _dbContext,
            _mockLogger.Object,
            _mockAuditLogService.Object,
            _mockHttpContextAccessor.Object);
    }

    #region Self-Protection Tests

    [Fact]
    public async Task SetUserActiveStatus_WhenTargetIsSelf_ReturnsError()
    {
        // Arrange
        const string userId = "user123";
        const string actorUserId = "user123"; // Same user

        // Act
        var result = await _service.SetUserActiveStatusAsync(
            userId,
            isActive: false,
            actorUserId);

        // Assert
        result.Succeeded.Should().BeFalse("user cannot disable their own account");
        result.ErrorCode.Should().Be(UserManagementResult.SelfModificationDenied);
        result.ErrorMessage.Should().Contain("cannot disable your own account");
    }

    [Fact]
    public async Task AssignRole_WhenTargetIsSelf_ReturnsError()
    {
        // Arrange
        const string userId = "user123";
        const string actorUserId = "user123"; // Same user

        // Act
        var result = await _service.AssignRoleAsync(
            userId,
            "Viewer",
            actorUserId);

        // Assert
        result.Succeeded.Should().BeFalse("user cannot change their own role");
        result.ErrorCode.Should().Be(UserManagementResult.SelfModificationDenied);
        result.ErrorMessage.Should().Contain("cannot change your own role");
    }

    [Fact]
    public async Task RemoveRole_WhenTargetIsSelf_ReturnsError()
    {
        // Arrange
        const string userId = "user123";
        const string actorUserId = "user123"; // Same user

        // Act
        var result = await _service.RemoveRoleAsync(
            userId,
            "Admin",
            actorUserId);

        // Assert
        result.Succeeded.Should().BeFalse("user cannot remove their own role");
        result.ErrorCode.Should().Be(UserManagementResult.SelfModificationDenied);
        result.ErrorMessage.Should().Contain("cannot remove your own role");
    }

    [Fact]
    public async Task UpdateUserAsync_WhenChangingOwnActiveStatus_ReturnsError()
    {
        // Arrange
        const string userId = "user123";
        const string actorUserId = "user123"; // Same user

        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@example.com",
            UserName = "user@example.com",
            IsActive = true
        };

        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync(user);

        var updateRequest = new UserUpdateDto
        {
            IsActive = false
        };

        // Act
        var result = await _service.UpdateUserAsync(userId, updateRequest, actorUserId);

        // Assert
        result.Succeeded.Should().BeFalse("user cannot change their own active status");
        result.ErrorCode.Should().Be(UserManagementResult.SelfModificationDenied);
        result.ErrorMessage.Should().Contain("cannot change your own active status");
    }

    #endregion

    #region Role Hierarchy Tests

    [Fact]
    public async Task AssignRole_AdminAssigningSuperAdmin_ReturnsError()
    {
        // Arrange
        const string actorUserId = "admin123";
        const string targetUserId = "user456";

        var actorUser = new ApplicationUser
        {
            Id = actorUserId,
            Email = "admin@example.com",
            UserName = "admin@example.com"
        };

        var targetUser = new ApplicationUser
        {
            Id = targetUserId,
            Email = "target@example.com",
            UserName = "target@example.com"
        };

        _mockUserManager.Setup(um => um.FindByIdAsync(actorUserId))
            .ReturnsAsync(actorUser);
        _mockUserManager.Setup(um => um.GetRolesAsync(actorUser))
            .ReturnsAsync(new List<string> { "Admin" });
        _mockUserManager.Setup(um => um.FindByIdAsync(targetUserId))
            .ReturnsAsync(targetUser);
        _mockRoleManager.Setup(rm => rm.RoleExistsAsync("SuperAdmin"))
            .ReturnsAsync(true);

        // Act
        var result = await _service.AssignRoleAsync(
            targetUserId,
            "SuperAdmin",
            actorUserId);

        // Assert
        result.Succeeded.Should().BeFalse("admin cannot assign SuperAdmin role");
        result.ErrorCode.Should().Be(UserManagementResult.InsufficientPermissions);
        result.ErrorMessage.Should().Contain("do not have permission to assign the role: SuperAdmin");
    }

    [Fact]
    public async Task AssignRole_SuperAdminAssigningAnyRole_Succeeds()
    {
        // Arrange
        const string actorUserId = "superadmin123";
        const string targetUserId = "user456";

        var actorUser = new ApplicationUser
        {
            Id = actorUserId,
            Email = "superadmin@example.com",
            UserName = "superadmin@example.com"
        };

        var targetUser = new ApplicationUser
        {
            Id = targetUserId,
            Email = "user@example.com",
            UserName = "user@example.com"
        };

        _mockUserManager.Setup(um => um.FindByIdAsync(actorUserId))
            .ReturnsAsync(actorUser);
        _mockUserManager.Setup(um => um.GetRolesAsync(actorUser))
            .ReturnsAsync(new List<string> { "SuperAdmin" });
        _mockUserManager.Setup(um => um.FindByIdAsync(targetUserId))
            .ReturnsAsync(targetUser);
        _mockUserManager.Setup(um => um.GetRolesAsync(targetUser))
            .ReturnsAsync(new List<string>());
        _mockRoleManager.Setup(rm => rm.RoleExistsAsync("Admin"))
            .ReturnsAsync(true);
        _mockUserManager.Setup(um => um.RemoveFromRolesAsync(targetUser, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(um => um.AddToRoleAsync(targetUser, "Admin"))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _service.AssignRoleAsync(
            targetUserId,
            "Admin",
            actorUserId);

        // Assert
        result.Succeeded.Should().BeTrue("SuperAdmin can assign any role");
        _mockUserManager.Verify(
            um => um.AddToRoleAsync(targetUser, "Admin"),
            Times.Once,
            "role should be assigned");
    }

    [Fact]
    public async Task GetAvailableRoles_ForAdmin_ExcludesSuperAdmin()
    {
        // Arrange
        const string adminUserId = "admin123";
        var adminUser = new ApplicationUser
        {
            Id = adminUserId,
            Email = "admin@example.com",
            UserName = "admin@example.com"
        };

        _mockUserManager.Setup(um => um.FindByIdAsync(adminUserId))
            .ReturnsAsync(adminUser);
        _mockUserManager.Setup(um => um.GetRolesAsync(adminUser))
            .ReturnsAsync(new List<string> { "Admin" });

        // Act
        var result = await _service.GetAvailableRolesAsync(adminUserId);

        // Assert
        result.Should().HaveCount(3, "admin can assign Admin, Moderator, Viewer");
        result.Should().Contain("Admin");
        result.Should().Contain("Moderator");
        result.Should().Contain("Viewer");
        result.Should().NotContain("SuperAdmin", "admin cannot assign SuperAdmin role");
    }

    [Fact]
    public async Task GetAvailableRoles_ForSuperAdmin_ReturnsAllRoles()
    {
        // Arrange
        const string superAdminUserId = "superadmin123";
        var superAdminUser = new ApplicationUser
        {
            Id = superAdminUserId,
            Email = "superadmin@example.com",
            UserName = "superadmin@example.com"
        };

        _mockUserManager.Setup(um => um.FindByIdAsync(superAdminUserId))
            .ReturnsAsync(superAdminUser);
        _mockUserManager.Setup(um => um.GetRolesAsync(superAdminUser))
            .ReturnsAsync(new List<string> { "SuperAdmin" });

        // Act
        var result = await _service.GetAvailableRolesAsync(superAdminUserId);

        // Assert
        result.Should().HaveCount(4, "SuperAdmin can assign all roles");
        result.Should().Contain("SuperAdmin");
        result.Should().Contain("Admin");
        result.Should().Contain("Moderator");
        result.Should().Contain("Viewer");
    }

    [Fact]
    public async Task GetAvailableRoles_ForModerator_ReturnsEmpty()
    {
        // Arrange
        const string moderatorUserId = "mod123";
        var moderatorUser = new ApplicationUser
        {
            Id = moderatorUserId,
            Email = "mod@example.com",
            UserName = "mod@example.com"
        };

        _mockUserManager.Setup(um => um.FindByIdAsync(moderatorUserId))
            .ReturnsAsync(moderatorUser);
        _mockUserManager.Setup(um => um.GetRolesAsync(moderatorUser))
            .ReturnsAsync(new List<string> { "Moderator" });

        // Act
        var result = await _service.GetAvailableRolesAsync(moderatorUserId);

        // Assert
        result.Should().BeEmpty("moderator cannot assign any roles");
    }

    #endregion

    #region User Query Operations Tests

    [Fact(Skip = "Cannot mock IQueryable with EF async operations - requires integration testing")]
    public async Task GetUsersAsync_WithSearchTerm_FiltersResults()
    {
        // This test documents the expected behavior of GetUsersAsync with search filtering.
        // The actual implementation uses EF Core's IQueryable with async operations which
        // cannot be effectively mocked in unit tests. Proper testing requires integration
        // tests with a real database.
        //
        // Expected behavior:
        // - Filters users by email, display name, or Discord username containing search term (case-insensitive)
        // - Returns paginated results
        // - Maps to UserDto with roles
        await Task.CompletedTask;
    }

    [Fact(Skip = "Cannot mock IQueryable with EF async operations - requires integration testing")]
    public async Task GetUsersAsync_WithPagination_ReturnsCorrectPage()
    {
        // This test documents the expected behavior of GetUsersAsync with pagination.
        // The actual implementation uses EF Core's IQueryable with async operations which
        // cannot be effectively mocked in unit tests. Proper testing requires integration
        // tests with a real database.
        //
        // Expected behavior:
        // - Applies Skip/Take for pagination
        // - Returns correct page number and page size in result
        // - Returns correct total count
        await Task.CompletedTask;
    }

    [Fact(Skip = "Cannot mock IQueryable with EF async operations - requires integration testing")]
    public async Task GetUsersAsync_WithActiveStatusFilter_FiltersCorrectly()
    {
        // This test documents the expected behavior of GetUsersAsync with active status filter.
        // The actual implementation uses EF Core's IQueryable with async operations which
        // cannot be effectively mocked in unit tests. Proper testing requires integration
        // tests with a real database.
        //
        // Expected behavior:
        // - Filters users by IsActive property
        // - Returns only users matching the specified active status
        await Task.CompletedTask;
    }

    [Fact(Skip = "Cannot mock IQueryable with EF async operations - requires integration testing")]
    public async Task GetUsersAsync_WithDiscordLinkedFilter_FiltersCorrectly()
    {
        // This test documents the expected behavior of GetUsersAsync with Discord linked filter.
        // The actual implementation uses EF Core's IQueryable with async operations which
        // cannot be effectively mocked in unit tests. Proper testing requires integration
        // tests with a real database.
        //
        // Expected behavior:
        // - Filters users by presence of DiscordUserId
        // - Returns only users with/without Discord accounts based on filter
        await Task.CompletedTask;
    }

    [Fact]
    public async Task GetUserByIdAsync_WithExistingUser_ReturnsUserDto()
    {
        // Arrange
        const string userId = "user123";
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "test@example.com",
            DisplayName = "Test User",
            IsActive = true
        };

        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockUserManager.Setup(um => um.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "Admin" });

        // Act
        var result = await _service.GetUserByIdAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(userId);
        result.Email.Should().Be("test@example.com");
        result.DisplayName.Should().Be("Test User");
        result.Roles.Should().Contain("Admin");
    }

    [Fact]
    public async Task GetUserByIdAsync_WithNonExistentUser_ReturnsNull()
    {
        // Arrange
        const string userId = "nonexistent";

        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _service.GetUserByIdAsync(userId);

        // Assert
        result.Should().BeNull("user does not exist");
    }

    #endregion

    #region User Create Tests

    [Fact]
    public async Task CreateUserAsync_WithValidData_CreatesUser()
    {
        // Arrange
        const string actorUserId = "admin123";
        const string email = "newuser@example.com";
        const string password = "SecurePassword123!";

        var actorUser = new ApplicationUser
        {
            Id = actorUserId,
            Email = "admin@example.com",
            UserName = "admin@example.com"
        };

        var createdUser = new ApplicationUser
        {
            Id = "newuser123",
            Email = email,
            UserName = email,
            DisplayName = "New User",
            EmailConfirmed = true,
            IsActive = true
        };

        _mockUserManager.Setup(um => um.FindByIdAsync(actorUserId))
            .ReturnsAsync(actorUser);
        _mockUserManager.Setup(um => um.GetRolesAsync(actorUser))
            .ReturnsAsync(new List<string> { "Admin" });
        _mockUserManager.Setup(um => um.FindByEmailAsync(email))
            .ReturnsAsync((ApplicationUser?)null);
        _mockUserManager.Setup(um => um.CreateAsync(It.IsAny<ApplicationUser>(), password))
            .Callback<ApplicationUser, string>((user, _) =>
            {
                // Simulate Identity setting the Id
                user.Id = createdUser.Id;
            })
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(um => um.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Viewer"))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(um => um.GetRolesAsync(It.Is<ApplicationUser>(u => u.Id == createdUser.Id)))
            .ReturnsAsync(new List<string> { "Viewer" });

        var request = new UserCreateDto
        {
            Email = email,
            DisplayName = "New User",
            Password = password,
            ConfirmPassword = password,
            Role = "Viewer"
        };

        // Act
        var result = await _service.CreateUserAsync(request, actorUserId);

        // Assert
        result.Succeeded.Should().BeTrue("user creation should succeed");
        result.User.Should().NotBeNull();
        result.User!.Email.Should().Be(email);
        result.User.DisplayName.Should().Be("New User");
        result.User.Roles.Should().Contain("Viewer");

        _mockUserManager.Verify(
            um => um.CreateAsync(
                It.Is<ApplicationUser>(u =>
                    u.Email == email &&
                    u.UserName == email &&
                    u.EmailConfirmed == true &&
                    u.IsActive == true),
                password),
            Times.Once,
            "user should be created with correct properties");
    }

    [Fact]
    public async Task CreateUserAsync_WithDuplicateEmail_ReturnsError()
    {
        // Arrange
        const string actorUserId = "admin123";
        const string email = "existing@example.com";

        var existingUser = new ApplicationUser
        {
            Email = email
        };

        _mockUserManager.Setup(um => um.FindByEmailAsync(email))
            .ReturnsAsync(existingUser);

        var request = new UserCreateDto
        {
            Email = email,
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            Role = "Viewer"
        };

        // Act
        var result = await _service.CreateUserAsync(request, actorUserId);

        // Assert
        result.Succeeded.Should().BeFalse("duplicate email should be rejected");
        result.ErrorCode.Should().Be(UserManagementResult.EmailAlreadyExists);
        result.ErrorMessage.Should().Contain("user with this email already exists");

        _mockUserManager.Verify(
            um => um.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never,
            "user should not be created");
    }

    [Fact]
    public async Task CreateUserAsync_WithPasswordMismatch_ReturnsError()
    {
        // Arrange
        const string actorUserId = "admin123";

        var request = new UserCreateDto
        {
            Email = "newuser@example.com",
            Password = "Password123!",
            ConfirmPassword = "DifferentPassword123!",
            Role = "Viewer"
        };

        // Act
        var result = await _service.CreateUserAsync(request, actorUserId);

        // Assert
        result.Succeeded.Should().BeFalse("password mismatch should be rejected");
        result.ErrorCode.Should().Be(UserManagementResult.PasswordValidationFailed);
        result.ErrorMessage.Should().Contain("Passwords do not match");
    }

    [Fact]
    public async Task CreateUserAsync_WithUnauthorizedRole_ReturnsError()
    {
        // Arrange
        const string actorUserId = "admin123";
        var actorUser = new ApplicationUser
        {
            Id = actorUserId,
            Email = "admin@example.com"
        };

        _mockUserManager.Setup(um => um.FindByIdAsync(actorUserId))
            .ReturnsAsync(actorUser);
        _mockUserManager.Setup(um => um.GetRolesAsync(actorUser))
            .ReturnsAsync(new List<string> { "Admin" }); // Admin cannot assign SuperAdmin

        var request = new UserCreateDto
        {
            Email = "newuser@example.com",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            Role = "SuperAdmin"
        };

        // Act
        var result = await _service.CreateUserAsync(request, actorUserId);

        // Assert
        result.Succeeded.Should().BeFalse("admin cannot assign SuperAdmin role");
        result.ErrorCode.Should().Be(UserManagementResult.InsufficientPermissions);
        result.ErrorMessage.Should().Contain("do not have permission to assign the role: SuperAdmin");
    }

    #endregion

    #region User Update Tests

    [Fact]
    public async Task UpdateUserAsync_WithValidData_UpdatesUser()
    {
        // Arrange
        const string userId = "user123";
        const string actorUserId = "admin456";

        var user = new ApplicationUser
        {
            Id = userId,
            Email = "old@example.com",
            UserName = "old@example.com",
            DisplayName = "Old Name",
            IsActive = true
        };

        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockUserManager.Setup(um => um.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(um => um.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "Viewer" });

        var request = new UserUpdateDto
        {
            DisplayName = "New Name",
            Email = "new@example.com"
        };

        // Act
        var result = await _service.UpdateUserAsync(userId, request, actorUserId);

        // Assert
        result.Succeeded.Should().BeTrue("update should succeed");
        result.User.Should().NotBeNull();

        _mockUserManager.Verify(
            um => um.UpdateAsync(It.Is<ApplicationUser>(u =>
                u.DisplayName == "New Name" &&
                u.Email == "new@example.com")),
            Times.Once,
            "user should be updated");
    }

    [Fact]
    public async Task UpdateUserAsync_WithDuplicateEmail_ReturnsError()
    {
        // Arrange
        const string userId = "user123";
        const string actorUserId = "admin456";
        const string newEmail = "taken@example.com";

        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@example.com"
        };

        var existingUser = new ApplicationUser
        {
            Id = "other999",
            Email = newEmail
        };

        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockUserManager.Setup(um => um.FindByEmailAsync(newEmail))
            .ReturnsAsync(existingUser);

        var request = new UserUpdateDto
        {
            Email = newEmail
        };

        // Act
        var result = await _service.UpdateUserAsync(userId, request, actorUserId);

        // Assert
        result.Succeeded.Should().BeFalse("duplicate email should be rejected");
        result.ErrorCode.Should().Be(UserManagementResult.EmailAlreadyExists);
    }

    [Fact]
    public async Task UpdateUserAsync_WithNonExistentUser_ReturnsError()
    {
        // Arrange
        const string userId = "nonexistent";
        const string actorUserId = "admin456";

        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync((ApplicationUser?)null);

        var request = new UserUpdateDto
        {
            DisplayName = "New Name"
        };

        // Act
        var result = await _service.UpdateUserAsync(userId, request, actorUserId);

        // Assert
        result.Succeeded.Should().BeFalse("user not found");
        result.ErrorCode.Should().Be(UserManagementResult.UserNotFound);
    }

    #endregion

    #region Password Reset Tests

    [Fact]
    public async Task ResetPasswordAsync_GeneratesNewPassword()
    {
        // Arrange
        const string userId = "user123";
        const string actorUserId = "admin456";

        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@example.com"
        };

        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockUserManager.Setup(um => um.GeneratePasswordResetTokenAsync(user))
            .ReturnsAsync("reset-token");
        _mockUserManager.Setup(um => um.ResetPasswordAsync(user, "reset-token", It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(um => um.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "Viewer" });

        // Act
        var result = await _service.ResetPasswordAsync(userId, actorUserId);

        // Assert
        result.Succeeded.Should().BeTrue("password reset should succeed");
        result.GeneratedPassword.Should().NotBeNullOrWhiteSpace("temporary password should be generated");
        result.GeneratedPassword!.Length.Should().Be(16, "password should be 16 characters");

        _mockUserManager.Verify(
            um => um.ResetPasswordAsync(user, "reset-token", It.IsAny<string>()),
            Times.Once,
            "password should be reset");
    }

    [Fact]
    public async Task ResetPasswordAsync_WithNonExistentUser_ReturnsError()
    {
        // Arrange
        const string userId = "nonexistent";
        const string actorUserId = "admin456";

        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _service.ResetPasswordAsync(userId, actorUserId);

        // Assert
        result.Succeeded.Should().BeFalse("user not found");
        result.ErrorCode.Should().Be(UserManagementResult.UserNotFound);
    }

    #endregion

    #region Discord Unlink Tests

    [Fact]
    public async Task UnlinkDiscordAccountAsync_WhenLinked_UnlinksSuccessfully()
    {
        // Arrange
        const string userId = "user123";
        const string actorUserId = "admin456";

        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@example.com",
            DiscordUserId = 123456789UL,
            DiscordUsername = "testuser#1234",
            DiscordAvatarUrl = "https://cdn.discord.com/avatar.png"
        };

        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockUserManager.Setup(um => um.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(um => um.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "Viewer" });

        // Act
        var result = await _service.UnlinkDiscordAccountAsync(userId, actorUserId);

        // Assert
        result.Succeeded.Should().BeTrue("Discord unlink should succeed");
        result.User.Should().NotBeNull();
        result.User!.DiscordUserId.Should().BeNull("Discord ID should be cleared");
        result.User.DiscordUsername.Should().BeNull("Discord username should be cleared");

        _mockUserManager.Verify(
            um => um.UpdateAsync(It.Is<ApplicationUser>(u =>
                u.DiscordUserId == null &&
                u.DiscordUsername == null &&
                u.DiscordAvatarUrl == null)),
            Times.Once,
            "Discord data should be cleared");
    }

    [Fact]
    public async Task UnlinkDiscordAccountAsync_WhenNotLinked_ReturnsError()
    {
        // Arrange
        const string userId = "user123";
        const string actorUserId = "admin456";

        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@example.com",
            DiscordUserId = null // Not linked
        };

        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync(user);

        // Act
        var result = await _service.UnlinkDiscordAccountAsync(userId, actorUserId);

        // Assert
        result.Succeeded.Should().BeFalse("cannot unlink when not linked");
        result.ErrorCode.Should().Be(UserManagementResult.DiscordNotLinked);
        result.ErrorMessage.Should().Contain("does not have a linked Discord account");
    }

    #endregion

    #region Activity Logging Tests

    [Fact]
    public async Task CreateUserAsync_LogsActivity()
    {
        // Arrange
        const string actorUserId = "admin123";
        const string newUserId = "newuser123";
        const string email = "newuser@example.com";

        var actorUser = new ApplicationUser
        {
            Id = actorUserId,
            Email = "admin@example.com",
            UserName = "admin@example.com"
        };

        // Add actor to DbContext so foreign key constraint is satisfied
        _dbContext.Set<ApplicationUser>().Add(actorUser);
        await _dbContext.SaveChangesAsync();

        _mockUserManager.Setup(um => um.FindByIdAsync(actorUserId))
            .ReturnsAsync(actorUser);
        _mockUserManager.Setup(um => um.GetRolesAsync(actorUser))
            .ReturnsAsync(new List<string> { "Admin" });
        _mockUserManager.Setup(um => um.FindByEmailAsync(email))
            .ReturnsAsync((ApplicationUser?)null);
        _mockUserManager.Setup(um => um.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .Callback<ApplicationUser, string>((user, _) =>
            {
                // Simulate Identity setting the Id and add to DbContext for FK
                user.Id = newUserId;
                _dbContext.Set<ApplicationUser>().Add(user);
                _dbContext.SaveChanges();
            })
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(um => um.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(um => um.GetRolesAsync(It.Is<ApplicationUser>(u => u.Id == newUserId)))
            .ReturnsAsync(new List<string> { "Viewer" });

        var request = new UserCreateDto
        {
            Email = email,
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            Role = "Viewer"
        };

        // Act
        await _service.CreateUserAsync(request, actorUserId, "127.0.0.1");

        // Assert - Activity logging is synchronous in the implementation
        var logs = await _dbContext.UserActivityLogs.ToListAsync();
        logs.Should().HaveCount(1, "user creation should be logged");

        var log = logs.First();
        log.ActorUserId.Should().Be(actorUserId);
        log.TargetUserId.Should().Be(newUserId, "target should be the newly created user");
        log.Action.Should().Be(UserActivityAction.UserCreated);
        log.IpAddress.Should().Be("127.0.0.1");
        log.Details.Should().Contain(email);
    }

    [Fact]
    public async Task SetUserActiveStatus_LogsActivity()
    {
        // Arrange
        const string userId = "user123";
        const string actorUserId = "admin456";

        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@example.com",
            UserName = "user@example.com",
            IsActive = true
        };

        var actor = new ApplicationUser
        {
            Id = actorUserId,
            Email = "admin@example.com",
            UserName = "admin@example.com"
        };

        // Add users to DbContext so foreign key constraints are satisfied
        _dbContext.Set<ApplicationUser>().Add(actor);
        _dbContext.Set<ApplicationUser>().Add(user);
        await _dbContext.SaveChangesAsync();

        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockUserManager.Setup(um => um.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(um => um.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "Viewer" });

        // Act
        await _service.SetUserActiveStatusAsync(userId, false, actorUserId, "127.0.0.1");

        // Assert - Activity logging is synchronous in the implementation
        var logs = await _dbContext.UserActivityLogs.ToListAsync();
        logs.Should().HaveCount(1, "status change should be logged");

        var log = logs.First();
        log.ActorUserId.Should().Be(actorUserId);
        log.TargetUserId.Should().Be(userId);
        log.Action.Should().Be(UserActivityAction.UserDisabled);
        log.IpAddress.Should().Be("127.0.0.1");
    }

    [Fact]
    public async Task AssignRole_LogsActivity()
    {
        // Arrange
        const string userId = "user123";
        const string actorUserId = "admin456";

        var actorUser = new ApplicationUser
        {
            Id = actorUserId,
            Email = "admin@example.com",
            UserName = "admin@example.com"
        };

        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@example.com",
            UserName = "user@example.com"
        };

        // Add users to DbContext so foreign key constraints are satisfied
        _dbContext.Set<ApplicationUser>().Add(actorUser);
        _dbContext.Set<ApplicationUser>().Add(user);
        await _dbContext.SaveChangesAsync();

        _mockUserManager.Setup(um => um.FindByIdAsync(actorUserId))
            .ReturnsAsync(actorUser);
        _mockUserManager.Setup(um => um.GetRolesAsync(actorUser))
            .ReturnsAsync(new List<string> { "Admin" });
        _mockUserManager.Setup(um => um.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockUserManager.Setup(um => um.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "Viewer" });
        _mockRoleManager.Setup(rm => rm.RoleExistsAsync("Moderator"))
            .ReturnsAsync(true);
        _mockUserManager.Setup(um => um.RemoveFromRolesAsync(user, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(um => um.AddToRoleAsync(user, "Moderator"))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        await _service.AssignRoleAsync(userId, "Moderator", actorUserId, "127.0.0.1");

        // Assert - Activity logging is synchronous in the implementation
        var logs = await _dbContext.UserActivityLogs.ToListAsync();
        logs.Should().HaveCount(1, "role assignment should be logged");

        var log = logs.First();
        log.ActorUserId.Should().Be(actorUserId);
        log.TargetUserId.Should().Be(userId);
        log.Action.Should().Be(UserActivityAction.RoleAssigned);
        log.Details.Should().Contain("Viewer");
        log.Details.Should().Contain("Moderator");
    }

    #endregion

    #region CanManageUser Tests

    [Fact]
    public async Task CanManageUserAsync_WhenActorIsSelf_ReturnsFalse()
    {
        // Arrange
        const string userId = "user123";

        // Act
        var result = await _service.CanManageUserAsync(userId, userId);

        // Assert
        result.Should().BeFalse("user cannot manage themselves");
    }

    [Fact]
    public async Task CanManageUserAsync_WhenAdminManagesViewer_ReturnsTrue()
    {
        // Arrange
        const string actorUserId = "admin123";
        const string targetUserId = "user456";

        var actor = new ApplicationUser { Id = actorUserId, Email = "admin@example.com" };
        var target = new ApplicationUser { Id = targetUserId, Email = "user@example.com" };

        _mockUserManager.Setup(um => um.FindByIdAsync(actorUserId))
            .ReturnsAsync(actor);
        _mockUserManager.Setup(um => um.FindByIdAsync(targetUserId))
            .ReturnsAsync(target);
        _mockUserManager.Setup(um => um.GetRolesAsync(actor))
            .ReturnsAsync(new List<string> { "Admin" });
        _mockUserManager.Setup(um => um.GetRolesAsync(target))
            .ReturnsAsync(new List<string> { "Viewer" });

        // Act
        var result = await _service.CanManageUserAsync(actorUserId, targetUserId);

        // Assert
        result.Should().BeTrue("admin can manage viewer");
    }

    [Fact]
    public async Task CanManageUserAsync_WhenAdminManagesSuperAdmin_ReturnsFalse()
    {
        // Arrange
        const string actorUserId = "admin123";
        const string targetUserId = "superadmin456";

        var actor = new ApplicationUser { Id = actorUserId, Email = "admin@example.com" };
        var target = new ApplicationUser { Id = targetUserId, Email = "superadmin@example.com" };

        _mockUserManager.Setup(um => um.FindByIdAsync(actorUserId))
            .ReturnsAsync(actor);
        _mockUserManager.Setup(um => um.FindByIdAsync(targetUserId))
            .ReturnsAsync(target);
        _mockUserManager.Setup(um => um.GetRolesAsync(actor))
            .ReturnsAsync(new List<string> { "Admin" });
        _mockUserManager.Setup(um => um.GetRolesAsync(target))
            .ReturnsAsync(new List<string> { "SuperAdmin" });

        // Act
        var result = await _service.CanManageUserAsync(actorUserId, targetUserId);

        // Assert
        result.Should().BeFalse("admin cannot manage SuperAdmin");
    }

    [Fact]
    public async Task CanManageUserAsync_WhenSuperAdminManagesSuperAdmin_ReturnsTrue()
    {
        // Arrange
        const string actorUserId = "superadmin123";
        const string targetUserId = "superadmin456";

        var actor = new ApplicationUser { Id = actorUserId, Email = "superadmin1@example.com" };
        var target = new ApplicationUser { Id = targetUserId, Email = "superadmin2@example.com" };

        _mockUserManager.Setup(um => um.FindByIdAsync(actorUserId))
            .ReturnsAsync(actor);
        _mockUserManager.Setup(um => um.FindByIdAsync(targetUserId))
            .ReturnsAsync(target);
        _mockUserManager.Setup(um => um.GetRolesAsync(actor))
            .ReturnsAsync(new List<string> { "SuperAdmin" });
        _mockUserManager.Setup(um => um.GetRolesAsync(target))
            .ReturnsAsync(new List<string> { "SuperAdmin" });

        // Act
        var result = await _service.CanManageUserAsync(actorUserId, targetUserId);

        // Assert
        result.Should().BeTrue("SuperAdmin can manage other SuperAdmin users");
    }

    [Fact]
    public async Task CanManageUserAsync_WhenModeratorManagesViewer_ReturnsFalse()
    {
        // Arrange
        const string actorUserId = "mod123";
        const string targetUserId = "user456";

        var actor = new ApplicationUser { Id = actorUserId, Email = "mod@example.com" };
        var target = new ApplicationUser { Id = targetUserId, Email = "user@example.com" };

        _mockUserManager.Setup(um => um.FindByIdAsync(actorUserId))
            .ReturnsAsync(actor);
        _mockUserManager.Setup(um => um.FindByIdAsync(targetUserId))
            .ReturnsAsync(target);
        _mockUserManager.Setup(um => um.GetRolesAsync(actor))
            .ReturnsAsync(new List<string> { "Moderator" });
        _mockUserManager.Setup(um => um.GetRolesAsync(target))
            .ReturnsAsync(new List<string> { "Viewer" });

        // Act
        var result = await _service.CanManageUserAsync(actorUserId, targetUserId);

        // Assert
        result.Should().BeFalse("moderator cannot manage users");
    }

    #endregion

    #region Activity Log Query Tests

    [Fact]
    public async Task GetActivityLogAsync_WithNoFilter_ReturnsAllLogs()
    {
        // Arrange
        var actor = new ApplicationUser
        {
            Id = "actor1",
            Email = "actor@example.com",
            UserName = "actor@example.com"
        };
        var target = new ApplicationUser
        {
            Id = "target1",
            Email = "target@example.com",
            UserName = "target@example.com"
        };

        _dbContext.Set<ApplicationUser>().Add(actor);
        _dbContext.Set<ApplicationUser>().Add(target);
        await _dbContext.SaveChangesAsync();

        await _dbContext.UserActivityLogs.AddRangeAsync(
            new UserActivityLog
            {
                Id = Guid.NewGuid(),
                ActorUserId = actor.Id,
                TargetUserId = target.Id,
                Action = UserActivityAction.UserCreated,
                Timestamp = DateTime.UtcNow
            },
            new UserActivityLog
            {
                Id = Guid.NewGuid(),
                ActorUserId = actor.Id,
                TargetUserId = target.Id,
                Action = UserActivityAction.RoleAssigned,
                Timestamp = DateTime.UtcNow
            }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetActivityLogAsync(null, page: 1, pageSize: 10);

        // Assert
        result.Items.Should().HaveCount(2, "should return all logs");
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetActivityLogAsync_WithUserIdFilter_ReturnsFilteredLogs()
    {
        // Arrange
        var actor = new ApplicationUser
        {
            Id = "actor1",
            Email = "actor@example.com",
            UserName = "actor@example.com"
        };
        var target1 = new ApplicationUser
        {
            Id = "target1",
            Email = "target1@example.com",
            UserName = "target1@example.com"
        };
        var target2 = new ApplicationUser
        {
            Id = "target2",
            Email = "target2@example.com",
            UserName = "target2@example.com"
        };

        _dbContext.Set<ApplicationUser>().Add(actor);
        _dbContext.Set<ApplicationUser>().Add(target1);
        _dbContext.Set<ApplicationUser>().Add(target2);
        await _dbContext.SaveChangesAsync();

        await _dbContext.UserActivityLogs.AddRangeAsync(
            new UserActivityLog
            {
                Id = Guid.NewGuid(),
                ActorUserId = actor.Id,
                TargetUserId = target1.Id,
                Action = UserActivityAction.UserCreated,
                Timestamp = DateTime.UtcNow
            },
            new UserActivityLog
            {
                Id = Guid.NewGuid(),
                ActorUserId = actor.Id,
                TargetUserId = target2.Id,
                Action = UserActivityAction.UserCreated,
                Timestamp = DateTime.UtcNow
            }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetActivityLogAsync(target1.Id, page: 1, pageSize: 10);

        // Assert
        result.Items.Should().HaveCount(1, "should only return logs for target1");
        result.Items.First().TargetUserId.Should().Be(target1.Id);
    }

    #endregion

    public void Dispose()
    {
        _dbContext?.Dispose();
        _connection?.Dispose();
    }
}
