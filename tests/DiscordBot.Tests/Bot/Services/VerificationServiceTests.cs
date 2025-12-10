using DiscordBot.Bot.Services;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Services;

/// <summary>
/// Unit tests for <see cref="VerificationService"/>.
/// Tests cover verification initiation, code generation, validation, rate limiting, and cleanup.
/// </summary>
public class VerificationServiceTests : IDisposable
{
    private readonly BotDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly Mock<ILogger<VerificationService>> _mockLogger;
    private readonly VerificationService _service;

    public VerificationServiceTests()
    {
        // Setup in-memory database with unique name per test instance
        var options = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new BotDbContext(options);
        _context.Database.EnsureCreated();

        // Setup real UserManager with user store backed by DbContext
        // This is necessary because VerificationService uses UserManager.Users which must support async queries
        var userStore = new Microsoft.AspNetCore.Identity.EntityFrameworkCore.UserStore<ApplicationUser>(_context);
        _userManager = new UserManager<ApplicationUser>(
            userStore,
            null!,
            new PasswordHasher<ApplicationUser>(),
            null!,
            null!,
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            new Mock<ILogger<UserManager<ApplicationUser>>>().Object);

        _mockLogger = new Mock<ILogger<VerificationService>>();

        _service = new VerificationService(_context, _userManager, _mockLogger.Object);
    }

    public void Dispose()
    {
        _userManager?.Dispose();
        _context?.Dispose();
    }

    #region InitiateVerificationAsync Tests

    [Fact]
    public async Task InitiateVerificationAsync_CreatesVerification_WhenUserExists()
    {
        // Arrange
        const string userId = "user123";
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@example.com",
            UserName = "user@example.com",
            DiscordUserId = null // No Discord linked
        };

        // Add user to DbContext so UserManager can find it
        await _userManager.CreateAsync(user);

        // Act
        var result = await _service.InitiateVerificationAsync(userId, "127.0.0.1");

        // Assert
        result.Succeeded.Should().BeTrue("verification should be initiated successfully");
        result.VerificationId.Should().NotBeEmpty("verification ID should be assigned");

        var verification = await _context.VerificationCodes.FindAsync(result.VerificationId);
        verification.Should().NotBeNull("verification should be saved to database");
        verification!.ApplicationUserId.Should().Be(userId);
        verification.Status.Should().Be(VerificationStatus.Pending);
        verification.Code.Should().BeEmpty("code should be empty until Discord user generates it");
        verification.DiscordUserId.Should().BeNull("Discord user ID should be null initially");
        verification.IpAddress.Should().Be("127.0.0.1");
        verification.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task InitiateVerificationAsync_FailsWhenAlreadyLinked()
    {
        // Arrange
        const string userId = "user123";
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@example.com",
            UserName = "user@example.com",
            DiscordUserId = 123456789UL // Already linked
        };

        // Add user to DbContext so UserManager can find it
        await _userManager.CreateAsync(user);

        // Act
        var result = await _service.InitiateVerificationAsync(userId);

        // Assert
        result.Succeeded.Should().BeFalse("cannot initiate verification when Discord already linked");
        result.ErrorCode.Should().Be(VerificationInitiationResult.AlreadyLinked);
        result.ErrorMessage.Should().Contain("already linked");
        result.VerificationId.Should().BeNull();
    }

    [Fact]
    public async Task InitiateVerificationAsync_CancelsPreviousPendingVerifications()
    {
        // Arrange
        const string userId = "user123";
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@example.com",
            UserName = "user@example.com",
            DiscordUserId = null
        };

        // Create existing pending verification
        var existingVerification = new VerificationCode
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = userId,
            Code = string.Empty,
            Status = VerificationStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };
        _context.VerificationCodes.Add(existingVerification);
        await _context.SaveChangesAsync();

        // Add user to DbContext so UserManager can find it
        await _userManager.CreateAsync(user);

        // Act
        var result = await _service.InitiateVerificationAsync(userId);

        // Assert
        result.Succeeded.Should().BeTrue("new verification should be created");

        var cancelledVerification = await _context.VerificationCodes.FindAsync(existingVerification.Id);
        cancelledVerification!.Status.Should().Be(VerificationStatus.Cancelled,
            "previous pending verification should be cancelled");

        var newVerification = await _context.VerificationCodes.FindAsync(result.VerificationId);
        newVerification.Should().NotBeNull("new verification should be created");
        newVerification!.Status.Should().Be(VerificationStatus.Pending);
    }

    [Fact]
    public async Task InitiateVerificationAsync_FailsWhenUserNotFound()
    {
        // Arrange
        const string userId = "nonexistent";

        // Don't create user - it doesn't exist

        // Act
        var result = await _service.InitiateVerificationAsync(userId);

        // Assert
        result.Succeeded.Should().BeFalse("user not found");
        result.ErrorCode.Should().Be(VerificationInitiationResult.UserNotFound);
        result.ErrorMessage.Should().Contain("User not found");
    }

    #endregion

    #region GenerateCodeForDiscordUserAsync Tests

    [Fact]
    public async Task GenerateCodeForDiscordUserAsync_GeneratesUniqueCode()
    {
        // Arrange
        const ulong discordUserId = 987654321UL;
        const string userId = "user123";

        // Create pending verification
        var verification = new VerificationCode
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = userId,
            Code = string.Empty,
            Status = VerificationStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15)
        };
        _context.VerificationCodes.Add(verification);
        await _context.SaveChangesAsync();

        // No users with this Discord ID exist - don't add any to DbContext

        // Act
        var result = await _service.GenerateCodeForDiscordUserAsync(discordUserId);

        // Assert
        result.Succeeded.Should().BeTrue("code generation should succeed");
        result.Code.Should().NotBeNullOrEmpty("code should be generated");
        result.Code!.Length.Should().Be(6, "code should be 6 characters");
        result.Code.Should().MatchRegex("^[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{6}$",
            "code should only contain allowed characters");
        result.FormattedCode.Should().Be($"{result.Code.Substring(0, 3)}-{result.Code.Substring(3, 3)}",
            "formatted code should have dash separator");
        result.ExpiresAt.Should().NotBeNull();
        result.ExpiresAt.Should().BeAfter(DateTime.UtcNow);

        var updatedVerification = await _context.VerificationCodes.FindAsync(verification.Id);
        updatedVerification!.Code.Should().Be(result.Code);
        updatedVerification.DiscordUserId.Should().Be(discordUserId);
    }

    [Fact]
    public async Task GenerateCodeForDiscordUserAsync_FailsWhenRateLimited()
    {
        // Arrange
        const ulong discordUserId = 987654321UL;
        const string userId = "user123";

        // Create 3 recent verifications (rate limit is 3 per hour)
        var recentTime = DateTime.UtcNow.AddMinutes(-30);
        for (int i = 0; i < 3; i++)
        {
            _context.VerificationCodes.Add(new VerificationCode
            {
                Id = Guid.NewGuid(),
                ApplicationUserId = userId,
                Code = $"CODE{i:D2}",
                DiscordUserId = discordUserId,
                Status = VerificationStatus.Pending,
                CreatedAt = recentTime.AddMinutes(i * 5),
                ExpiresAt = recentTime.AddMinutes(i * 5 + 15)
            });
        }
        await _context.SaveChangesAsync();

        // No users with this Discord ID exist - don't add any to DbContext

        // Act
        var result = await _service.GenerateCodeForDiscordUserAsync(discordUserId);

        // Assert
        result.Succeeded.Should().BeFalse("rate limit should be exceeded");
        result.ErrorCode.Should().Be(CodeGenerationResult.RateLimited);
        result.ErrorMessage.Should().Contain("Rate limit exceeded");
        result.ErrorMessage.Should().Contain("3 codes per hour");
    }

    [Fact]
    public async Task GenerateCodeForDiscordUserAsync_FailsWhenDiscordAlreadyLinked()
    {
        // Arrange
        const ulong discordUserId = 987654321UL;
        var existingUser = new ApplicationUser
        {
            Id = "existing123",
            Email = "existing@example.com",
            UserName = "existing@example.com",
            DiscordUserId = discordUserId
        };

        // Add user to DbContext so VerificationService can query it
        _context.Set<ApplicationUser>().Add(existingUser);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GenerateCodeForDiscordUserAsync(discordUserId);

        // Assert
        result.Succeeded.Should().BeFalse("Discord account already linked");
        result.ErrorCode.Should().Be(CodeGenerationResult.AlreadyLinked);
        result.ErrorMessage.Should().Contain("already linked");
    }

    [Fact]
    public async Task GenerateCodeForDiscordUserAsync_FailsWhenNoPendingVerification()
    {
        // Arrange
        const ulong discordUserId = 987654321UL;

        // No pending verifications exist in database
        // No users with this Discord ID exist

        // Act
        var result = await _service.GenerateCodeForDiscordUserAsync(discordUserId);

        // Assert
        result.Succeeded.Should().BeFalse("no pending verification exists");
        result.ErrorCode.Should().Be(CodeGenerationResult.NoPendingVerification);
        result.ErrorMessage.Should().Contain("No pending verification found");
        result.ErrorMessage.Should().Contain("web interface first");
    }

    #endregion

    #region ValidateCodeAsync Tests

    [Fact]
    public async Task ValidateCodeAsync_LinksAccountsOnSuccess()
    {
        // Arrange
        const string userId = "user123";
        const ulong discordUserId = 987654321UL;
        const string code = "ABC123";

        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@example.com",
            UserName = "user@example.com",
            DiscordUserId = null
        };

        var verification = new VerificationCode
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = userId,
            Code = code,
            DiscordUserId = discordUserId,
            Status = VerificationStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };
        _context.VerificationCodes.Add(verification);
        await _context.SaveChangesAsync();

        // Add user to DbContext so UserManager can find it
        await _userManager.CreateAsync(user);
        // No users with this Discord ID exist in database

        // Act
        var result = await _service.ValidateCodeAsync(userId, code);

        // Assert
        result.Succeeded.Should().BeTrue("code validation should succeed");
        result.LinkedDiscordUserId.Should().Be(discordUserId);

        // Verify user was updated with Discord ID
        var updatedUser = await _userManager.FindByIdAsync(userId);
        updatedUser!.DiscordUserId.Should().Be(discordUserId, "user should be updated with Discord ID");

        var updatedVerification = await _context.VerificationCodes.FindAsync(verification.Id);
        updatedVerification!.Status.Should().Be(VerificationStatus.Completed);
        updatedVerification.CompletedAt.Should().NotBeNull();
        updatedVerification.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ValidateCodeAsync_FailsWithInvalidCode()
    {
        // Arrange
        const string userId = "user123";
        const string invalidCode = "INVALID";

        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@example.com",
            DiscordUserId = null
        };

        // Add user to DbContext so UserManager can find it
        await _userManager.CreateAsync(user);

        // Act
        var result = await _service.ValidateCodeAsync(userId, invalidCode);

        // Assert
        result.Succeeded.Should().BeFalse("invalid code should fail");
        result.ErrorCode.Should().Be(CodeValidationResult.InvalidCode);
        result.ErrorMessage.Should().Contain("Invalid verification code");
    }

    [Fact]
    public async Task ValidateCodeAsync_FailsWithExpiredCode()
    {
        // Arrange
        const string userId = "user123";
        const string code = "EXPIRED";

        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@example.com",
            DiscordUserId = null
        };

        var verification = new VerificationCode
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = userId,
            Code = code,
            DiscordUserId = 123456789UL,
            Status = VerificationStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddMinutes(-20),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5) // Expired 5 minutes ago
        };
        _context.VerificationCodes.Add(verification);
        await _context.SaveChangesAsync();

        // Add user to DbContext so UserManager can find it
        await _userManager.CreateAsync(user);

        // Act
        var result = await _service.ValidateCodeAsync(userId, code);

        // Assert
        result.Succeeded.Should().BeFalse("expired code should fail");
        result.ErrorCode.Should().Be(CodeValidationResult.CodeExpired);
        result.ErrorMessage.Should().Contain("expired");

        var updatedVerification = await _context.VerificationCodes.FindAsync(verification.Id);
        updatedVerification!.Status.Should().Be(VerificationStatus.Expired,
            "expired code should be marked as expired");
    }

    [Fact]
    public async Task ValidateCodeAsync_FailsWhenCodeAlreadyUsed()
    {
        // Arrange
        const string userId = "user123";
        const string code = "USED123";

        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@example.com",
            DiscordUserId = null
        };

        var verification = new VerificationCode
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = userId,
            Code = code,
            DiscordUserId = 123456789UL,
            Status = VerificationStatus.Completed, // Already completed
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            CompletedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        _context.VerificationCodes.Add(verification);
        await _context.SaveChangesAsync();

        // Add user to DbContext so UserManager can find it
        await _userManager.CreateAsync(user);

        // Act
        var result = await _service.ValidateCodeAsync(userId, code);

        // Assert
        result.Succeeded.Should().BeFalse("already used code should fail");
        result.ErrorCode.Should().Be(CodeValidationResult.CodeAlreadyUsed);
        result.ErrorMessage.Should().Contain("already been used");
    }

    [Fact]
    public async Task ValidateCodeAsync_NormalizesCodeInput()
    {
        // Arrange
        const string userId = "user123";
        const ulong discordUserId = 987654321UL;
        const string storedCode = "ABC123";
        const string inputCodeWithDashes = "abc-123"; // Lower case with dash

        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@example.com",
            DiscordUserId = null
        };

        var verification = new VerificationCode
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = userId,
            Code = storedCode,
            DiscordUserId = discordUserId,
            Status = VerificationStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };
        _context.VerificationCodes.Add(verification);
        await _context.SaveChangesAsync();

        // Add user to DbContext so UserManager can find it
        await _userManager.CreateAsync(user);
        // No users with this Discord ID exist in database

        // Act - Test with dashes
        var resultWithDashes = await _service.ValidateCodeAsync(userId, inputCodeWithDashes);

        // Assert
        resultWithDashes.Succeeded.Should().BeTrue("code with dashes should be normalized and accepted");
        resultWithDashes.LinkedDiscordUserId.Should().Be(discordUserId);
    }

    [Fact]
    public async Task ValidateCodeAsync_FailsWhenUserAlreadyLinked()
    {
        // Arrange
        const string userId = "user123";
        const string code = "ABC123";

        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@example.com",
            DiscordUserId = 999999999UL // Already linked
        };

        // Add user to DbContext so UserManager can find it
        await _userManager.CreateAsync(user);

        // Act
        var result = await _service.ValidateCodeAsync(userId, code);

        // Assert
        result.Succeeded.Should().BeFalse("user already has Discord linked");
        result.ErrorCode.Should().Be(CodeValidationResult.AlreadyLinked);
        result.ErrorMessage.Should().Contain("already linked");
    }

    [Fact]
    public async Task ValidateCodeAsync_FailsWhenDiscordUserAlreadyLinkedToAnother()
    {
        // Arrange
        const string userId = "user123";
        const ulong discordUserId = 987654321UL;
        const string code = "ABC123";

        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@example.com",
            DiscordUserId = null
        };

        var otherUser = new ApplicationUser
        {
            Id = "other456",
            Email = "other@example.com",
            DiscordUserId = discordUserId // Discord already linked to different user
        };

        var verification = new VerificationCode
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = userId,
            Code = code,
            DiscordUserId = discordUserId,
            Status = VerificationStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };
        _context.VerificationCodes.Add(verification);
        await _context.SaveChangesAsync();

        // Add user to DbContext so UserManager can find it
        await _userManager.CreateAsync(user);

        // Add other user to DbContext so VerificationService can find the conflict
        _context.Set<ApplicationUser>().Add(otherUser);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.ValidateCodeAsync(userId, code);

        // Assert
        result.Succeeded.Should().BeFalse("Discord account already linked to another user");
        result.ErrorCode.Should().Be(CodeValidationResult.DiscordAlreadyLinked);
        result.ErrorMessage.Should().Contain("already linked to another user");
    }

    [Fact]
    public async Task ValidateCodeAsync_FailsWhenCodeNotActivated()
    {
        // Arrange
        const string userId = "user123";
        const string code = "NOTACT";

        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@example.com",
            DiscordUserId = null
        };

        var verification = new VerificationCode
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = userId,
            Code = code,
            DiscordUserId = null, // Not activated by Discord user yet
            Status = VerificationStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };
        _context.VerificationCodes.Add(verification);
        await _context.SaveChangesAsync();

        // Add user to DbContext so UserManager can find it
        await _userManager.CreateAsync(user);

        // Act
        var result = await _service.ValidateCodeAsync(userId, code);

        // Assert
        result.Succeeded.Should().BeFalse("code not yet activated by Discord user");
        result.ErrorCode.Should().Be(CodeValidationResult.InvalidCode);
        result.ErrorMessage.Should().Contain("not yet activated");
        result.ErrorMessage.Should().Contain("/verify-account");
    }

    #endregion

    #region Utility Methods Tests

    [Fact]
    public async Task IsRateLimitedAsync_ReturnsTrueAfterThreeCodes()
    {
        // Arrange
        const ulong discordUserId = 987654321UL;
        const string userId = "user123";

        // Create exactly 3 codes within the last hour
        var recentTime = DateTime.UtcNow.AddMinutes(-30);
        for (int i = 0; i < 3; i++)
        {
            _context.VerificationCodes.Add(new VerificationCode
            {
                Id = Guid.NewGuid(),
                ApplicationUserId = userId,
                Code = $"CODE{i:D2}",
                DiscordUserId = discordUserId,
                Status = VerificationStatus.Pending,
                CreatedAt = recentTime.AddMinutes(i * 10),
                ExpiresAt = recentTime.AddMinutes(i * 10 + 15)
            });
        }
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.IsRateLimitedAsync(discordUserId);

        // Assert
        result.Should().BeTrue("rate limit should be triggered after 3 codes");
    }

    [Fact]
    public async Task IsRateLimitedAsync_ReturnsFalseWithLessThanThreeCodes()
    {
        // Arrange
        const ulong discordUserId = 987654321UL;
        const string userId = "user123";

        // Create only 2 codes within the last hour
        var recentTime = DateTime.UtcNow.AddMinutes(-30);
        for (int i = 0; i < 2; i++)
        {
            _context.VerificationCodes.Add(new VerificationCode
            {
                Id = Guid.NewGuid(),
                ApplicationUserId = userId,
                Code = $"CODE{i:D2}",
                DiscordUserId = discordUserId,
                Status = VerificationStatus.Pending,
                CreatedAt = recentTime.AddMinutes(i * 10),
                ExpiresAt = recentTime.AddMinutes(i * 10 + 15)
            });
        }
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.IsRateLimitedAsync(discordUserId);

        // Assert
        result.Should().BeFalse("rate limit should not be triggered with 2 codes");
    }

    [Fact]
    public async Task IsRateLimitedAsync_IgnoresOldCodes()
    {
        // Arrange
        const ulong discordUserId = 987654321UL;
        const string userId = "user123";

        // Create 3 codes older than 1 hour
        var oldTime = DateTime.UtcNow.AddHours(-2);
        for (int i = 0; i < 3; i++)
        {
            _context.VerificationCodes.Add(new VerificationCode
            {
                Id = Guid.NewGuid(),
                ApplicationUserId = userId,
                Code = $"OLD{i:D2}",
                DiscordUserId = discordUserId,
                Status = VerificationStatus.Expired,
                CreatedAt = oldTime.AddMinutes(i * 10),
                ExpiresAt = oldTime.AddMinutes(i * 10 + 15)
            });
        }

        // Create 1 recent code
        _context.VerificationCodes.Add(new VerificationCode
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = userId,
            Code = "RECENT",
            DiscordUserId = discordUserId,
            Status = VerificationStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.IsRateLimitedAsync(discordUserId);

        // Assert
        result.Should().BeFalse("old codes should be ignored in rate limit calculation");
    }

    [Fact]
    public async Task CleanupExpiredCodesAsync_MarksExpiredCodes()
    {
        // Arrange
        const string userId = "user123";

        // Create expired pending code
        var expiredCode = new VerificationCode
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = userId,
            Code = "EXPIRED",
            Status = VerificationStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddMinutes(-30),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-15) // Expired
        };

        // Create valid pending code
        var validCode = new VerificationCode
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = userId,
            Code = "VALID",
            Status = VerificationStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10) // Still valid
        };

        _context.VerificationCodes.AddRange(expiredCode, validCode);
        await _context.SaveChangesAsync();

        // Act
        var cleanedCount = await _service.CleanupExpiredCodesAsync();

        // Assert
        cleanedCount.Should().BeGreaterThanOrEqualTo(1, "at least the expired code should be cleaned");

        var expiredVerification = await _context.VerificationCodes.FindAsync(expiredCode.Id);
        expiredVerification!.Status.Should().Be(VerificationStatus.Expired,
            "expired pending code should be marked as expired");

        var validVerification = await _context.VerificationCodes.FindAsync(validCode.Id);
        validVerification!.Status.Should().Be(VerificationStatus.Pending,
            "valid code should remain pending");
    }

    [Fact]
    public async Task CleanupExpiredCodesAsync_DeletesOldCodes()
    {
        // Arrange
        const string userId = "user123";

        // Create old completed code (older than 24 hours)
        var oldCode = new VerificationCode
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = userId,
            Code = "OLDCODE",
            Status = VerificationStatus.Completed,
            CreatedAt = DateTime.UtcNow.AddHours(-25),
            ExpiresAt = DateTime.UtcNow.AddHours(-25).AddMinutes(15),
            CompletedAt = DateTime.UtcNow.AddHours(-25).AddMinutes(5)
        };

        // Create recent completed code (less than 24 hours)
        var recentCode = new VerificationCode
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = userId,
            Code = "RECENT",
            Status = VerificationStatus.Completed,
            CreatedAt = DateTime.UtcNow.AddHours(-5),
            ExpiresAt = DateTime.UtcNow.AddHours(-5).AddMinutes(15),
            CompletedAt = DateTime.UtcNow.AddHours(-5).AddMinutes(5)
        };

        _context.VerificationCodes.AddRange(oldCode, recentCode);
        await _context.SaveChangesAsync();

        // Act
        var cleanedCount = await _service.CleanupExpiredCodesAsync();

        // Assert
        cleanedCount.Should().BeGreaterThanOrEqualTo(1, "old code should be deleted");

        var oldVerification = await _context.VerificationCodes.FindAsync(oldCode.Id);
        oldVerification.Should().BeNull("old completed code should be deleted");

        var recentVerification = await _context.VerificationCodes.FindAsync(recentCode.Id);
        recentVerification.Should().NotBeNull("recent completed code should not be deleted");
    }

    [Fact]
    public async Task CleanupExpiredCodesAsync_ReturnsCorrectCount()
    {
        // Arrange
        const string userId = "user123";

        // Create 2 expired pending codes
        var expiredCode1 = new VerificationCode
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = userId,
            Code = "EXP1",
            Status = VerificationStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddMinutes(-30),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-15)
        };

        var expiredCode2 = new VerificationCode
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = userId,
            Code = "EXP2",
            Status = VerificationStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddMinutes(-35),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-20)
        };

        // Create 1 old cancelled code
        var oldCancelledCode = new VerificationCode
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = userId,
            Code = "OLDCANC",
            Status = VerificationStatus.Cancelled,
            CreatedAt = DateTime.UtcNow.AddHours(-26),
            ExpiresAt = DateTime.UtcNow.AddHours(-26).AddMinutes(15)
        };

        _context.VerificationCodes.AddRange(expiredCode1, expiredCode2, oldCancelledCode);
        await _context.SaveChangesAsync();

        // Act
        var cleanedCount = await _service.CleanupExpiredCodesAsync();

        // Assert
        cleanedCount.Should().Be(3, "should count both marked expired codes and deleted old codes");
    }

    [Fact]
    public async Task GetPendingVerificationAsync_ReturnsPendingVerification()
    {
        // Arrange
        const string userId = "user123";

        var pendingVerification = new VerificationCode
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = userId,
            Code = string.Empty,
            Status = VerificationStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };
        _context.VerificationCodes.Add(pendingVerification);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetPendingVerificationAsync(userId);

        // Assert
        result.Should().NotBeNull("pending verification should be found");
        result!.Id.Should().Be(pendingVerification.Id);
        result.Status.Should().Be(VerificationStatus.Pending);
    }

    [Fact]
    public async Task GetPendingVerificationAsync_ReturnsNullWhenNoVerification()
    {
        // Arrange
        const string userId = "user123";

        // Act
        var result = await _service.GetPendingVerificationAsync(userId);

        // Assert
        result.Should().BeNull("no pending verification exists");
    }

    [Fact]
    public async Task GetPendingVerificationAsync_IgnoresExpiredVerifications()
    {
        // Arrange
        const string userId = "user123";

        var expiredVerification = new VerificationCode
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = userId,
            Code = "EXPIRED",
            Status = VerificationStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddMinutes(-30),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-15) // Expired
        };
        _context.VerificationCodes.Add(expiredVerification);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetPendingVerificationAsync(userId);

        // Assert
        result.Should().BeNull("expired verification should be ignored");
    }

    [Fact]
    public async Task CancelPendingVerificationAsync_CancelsAllPendingVerifications()
    {
        // Arrange
        const string userId = "user123";

        var pending1 = new VerificationCode
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = userId,
            Code = string.Empty,
            Status = VerificationStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };

        var pending2 = new VerificationCode
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = userId,
            Code = "CODE123",
            Status = VerificationStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };

        var completed = new VerificationCode
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = userId,
            Code = "DONE",
            Status = VerificationStatus.Completed,
            CreatedAt = DateTime.UtcNow.AddMinutes(-20),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            CompletedAt = DateTime.UtcNow.AddMinutes(-15)
        };

        _context.VerificationCodes.AddRange(pending1, pending2, completed);
        await _context.SaveChangesAsync();

        // Act
        await _service.CancelPendingVerificationAsync(userId);

        // Assert
        var verification1 = await _context.VerificationCodes.FindAsync(pending1.Id);
        verification1!.Status.Should().Be(VerificationStatus.Cancelled);

        var verification2 = await _context.VerificationCodes.FindAsync(pending2.Id);
        verification2!.Status.Should().Be(VerificationStatus.Cancelled);

        var verification3 = await _context.VerificationCodes.FindAsync(completed.Id);
        verification3!.Status.Should().Be(VerificationStatus.Completed,
            "completed verification should not be affected");
    }

    #endregion
}
