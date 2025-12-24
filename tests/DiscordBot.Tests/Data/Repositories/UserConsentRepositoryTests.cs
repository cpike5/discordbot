using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Data.Repositories;

/// <summary>
/// Unit tests for UserConsentRepository.
/// </summary>
public class UserConsentRepositoryTests : IDisposable
{
    private readonly BotDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly UserConsentRepository _repository;
    private readonly Mock<ILogger<UserConsentRepository>> _mockLogger;
    private readonly Mock<ILogger<Repository<UserConsent>>> _mockBaseLogger;

    public UserConsentRepositoryTests()
    {
        (_context, _connection) = TestDbContextFactory.CreateContext();
        _mockLogger = new Mock<ILogger<UserConsentRepository>>();
        _mockBaseLogger = new Mock<ILogger<Repository<UserConsent>>>();
        _repository = new UserConsentRepository(_context, _mockLogger.Object, _mockBaseLogger.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private async Task SeedUserAsync(ulong userId)
    {
        var user = new User
        {
            Id = userId,
            Username = $"TestUser{userId}",
            Discriminator = "0000",
            FirstSeenAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();
    }

    #region GetActiveConsentAsync Tests

    [Fact]
    public async Task GetActiveConsentAsync_WithActiveConsent_ReturnsConsent()
    {
        // Arrange
        var userId = 123456789UL;
        await SeedUserAsync(userId);

        var consentType = ConsentType.MessageLogging;
        var activeConsent = new UserConsent
        {
            DiscordUserId = userId,
            ConsentType = consentType,
            GrantedAt = DateTime.UtcNow.AddDays(-30),
            RevokedAt = null,
            GrantedVia = "SlashCommand"
        };

        await _context.UserConsents.AddAsync(activeConsent);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetActiveConsentAsync(userId, consentType);

        // Assert
        result.Should().NotBeNull();
        result!.DiscordUserId.Should().Be(userId);
        result.ConsentType.Should().Be(consentType);
        result.RevokedAt.Should().BeNull();
        result.IsActive.Should().BeTrue();
        result.GrantedVia.Should().Be("SlashCommand");
    }

    [Fact]
    public async Task GetActiveConsentAsync_WithNoConsent_ReturnsNull()
    {
        // Arrange
        var userId = 123456789UL;
        var consentType = ConsentType.MessageLogging;

        // Act
        var result = await _repository.GetActiveConsentAsync(userId, consentType);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveConsentAsync_WithRevokedConsent_ReturnsNull()
    {
        // Arrange
        var userId = 123456789UL;
        await SeedUserAsync(userId);

        var consentType = ConsentType.MessageLogging;
        var revokedConsent = new UserConsent
        {
            DiscordUserId = userId,
            ConsentType = consentType,
            GrantedAt = DateTime.UtcNow.AddDays(-30),
            RevokedAt = DateTime.UtcNow.AddDays(-5),
            GrantedVia = "SlashCommand",
            RevokedVia = "WebUI"
        };

        await _context.UserConsents.AddAsync(revokedConsent);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetActiveConsentAsync(userId, consentType);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveConsentAsync_WithMultipleConsents_ReturnsMostRecent()
    {
        // Arrange
        var userId = 123456789UL;
        await SeedUserAsync(userId);

        var consentType = ConsentType.MessageLogging;

        var oldConsent = new UserConsent
        {
            DiscordUserId = userId,
            ConsentType = consentType,
            GrantedAt = DateTime.UtcNow.AddDays(-60),
            RevokedAt = DateTime.UtcNow.AddDays(-30),
            GrantedVia = "SlashCommand"
        };

        var recentConsent = new UserConsent
        {
            DiscordUserId = userId,
            ConsentType = consentType,
            GrantedAt = DateTime.UtcNow.AddDays(-10),
            RevokedAt = null,
            GrantedVia = "WebUI"
        };

        await _context.UserConsents.AddRangeAsync(oldConsent, recentConsent);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetActiveConsentAsync(userId, consentType);

        // Assert
        result.Should().NotBeNull();
        result!.GrantedVia.Should().Be("WebUI");
        result.GrantedAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(-10), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetActiveConsentAsync_WithDifferentConsentType_ReturnsNull()
    {
        // Arrange
        var userId = 123456789UL;
        await SeedUserAsync(userId);

        var consent = new UserConsent
        {
            DiscordUserId = userId,
            ConsentType = ConsentType.MessageLogging,
            GrantedAt = DateTime.UtcNow.AddDays(-30),
            RevokedAt = null,
            GrantedVia = "SlashCommand"
        };

        await _context.UserConsents.AddAsync(consent);
        await _context.SaveChangesAsync();

        // Act - Query for a different consent type (using value 99 as a different type)
        var result = await _repository.GetActiveConsentAsync(userId, (ConsentType)99);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveConsentAsync_WithDifferentUser_ReturnsNull()
    {
        // Arrange
        var userId1 = 123456789UL;
        var userId2 = 987654321UL;
        await SeedUserAsync(userId1);

        var consentType = ConsentType.MessageLogging;

        var consent = new UserConsent
        {
            DiscordUserId = userId1,
            ConsentType = consentType,
            GrantedAt = DateTime.UtcNow.AddDays(-30),
            RevokedAt = null,
            GrantedVia = "SlashCommand"
        };

        await _context.UserConsents.AddAsync(consent);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetActiveConsentAsync(userId2, consentType);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetUserConsentsAsync Tests

    [Fact]
    public async Task GetUserConsentsAsync_WithMultipleConsents_ReturnsAllConsents()
    {
        // Arrange
        var userId = 123456789UL;
        await SeedUserAsync(userId);

        var activeConsent = new UserConsent
        {
            DiscordUserId = userId,
            ConsentType = ConsentType.MessageLogging,
            GrantedAt = DateTime.UtcNow.AddDays(-10),
            RevokedAt = null,
            GrantedVia = "SlashCommand"
        };

        var revokedConsent = new UserConsent
        {
            DiscordUserId = userId,
            ConsentType = ConsentType.MessageLogging,
            GrantedAt = DateTime.UtcNow.AddDays(-60),
            RevokedAt = DateTime.UtcNow.AddDays(-30),
            GrantedVia = "SlashCommand",
            RevokedVia = "WebUI"
        };

        await _context.UserConsents.AddRangeAsync(activeConsent, revokedConsent);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetUserConsentsAsync(userId);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(c => c.RevokedAt == null);
        result.Should().Contain(c => c.RevokedAt != null);
    }

    [Fact]
    public async Task GetUserConsentsAsync_WithNoConsents_ReturnsEmptyList()
    {
        // Arrange
        var userId = 123456789UL;

        // Act
        var result = await _repository.GetUserConsentsAsync(userId);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserConsentsAsync_OrdersByGrantedAtDescending()
    {
        // Arrange
        var userId = 123456789UL;
        await SeedUserAsync(userId);

        var oldestConsent = new UserConsent
        {
            DiscordUserId = userId,
            ConsentType = ConsentType.MessageLogging,
            GrantedAt = DateTime.UtcNow.AddDays(-60),
            RevokedAt = DateTime.UtcNow.AddDays(-30),
            GrantedVia = "SlashCommand"
        };

        var middleConsent = new UserConsent
        {
            DiscordUserId = userId,
            ConsentType = ConsentType.MessageLogging,
            GrantedAt = DateTime.UtcNow.AddDays(-30),
            RevokedAt = DateTime.UtcNow.AddDays(-10),
            GrantedVia = "WebUI"
        };

        var newestConsent = new UserConsent
        {
            DiscordUserId = userId,
            ConsentType = ConsentType.MessageLogging,
            GrantedAt = DateTime.UtcNow.AddDays(-5),
            RevokedAt = null,
            GrantedVia = "SlashCommand"
        };

        await _context.UserConsents.AddRangeAsync(oldestConsent, middleConsent, newestConsent);
        await _context.SaveChangesAsync();

        // Act
        var result = (await _repository.GetUserConsentsAsync(userId)).ToList();

        // Assert
        result.Should().HaveCount(3);
        result[0].GrantedVia.Should().Be("SlashCommand"); // Newest
        result[1].GrantedVia.Should().Be("WebUI"); // Middle
        result[2].GrantedVia.Should().Be("SlashCommand"); // Oldest
    }

    [Fact]
    public async Task GetUserConsentsAsync_WithMultipleUsers_ReturnsOnlySpecifiedUser()
    {
        // Arrange
        var userId1 = 123456789UL;
        var userId2 = 987654321UL;
        await SeedUserAsync(userId1);
        await SeedUserAsync(userId2);

        var consent1 = new UserConsent
        {
            DiscordUserId = userId1,
            ConsentType = ConsentType.MessageLogging,
            GrantedAt = DateTime.UtcNow.AddDays(-30),
            RevokedAt = null,
            GrantedVia = "SlashCommand"
        };

        var consent2 = new UserConsent
        {
            DiscordUserId = userId2,
            ConsentType = ConsentType.MessageLogging,
            GrantedAt = DateTime.UtcNow.AddDays(-30),
            RevokedAt = null,
            GrantedVia = "WebUI"
        };

        await _context.UserConsents.AddRangeAsync(consent1, consent2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetUserConsentsAsync(userId1);

        // Assert
        result.Should().HaveCount(1);
        result.First().DiscordUserId.Should().Be(userId1);
        result.First().GrantedVia.Should().Be("SlashCommand");
    }

    [Fact]
    public async Task GetUserConsentsAsync_IncludesAllConsentTypes()
    {
        // Arrange
        var userId = 123456789UL;
        await SeedUserAsync(userId);

        var messageLoggingConsent = new UserConsent
        {
            DiscordUserId = userId,
            ConsentType = ConsentType.MessageLogging,
            GrantedAt = DateTime.UtcNow.AddDays(-30),
            RevokedAt = null,
            GrantedVia = "SlashCommand"
        };

        // Add another consent type if it exists (using same type for now since only MessageLogging exists)
        var anotherConsent = new UserConsent
        {
            DiscordUserId = userId,
            ConsentType = ConsentType.MessageLogging,
            GrantedAt = DateTime.UtcNow.AddDays(-10),
            RevokedAt = DateTime.UtcNow.AddDays(-5),
            GrantedVia = "WebUI"
        };

        await _context.UserConsents.AddRangeAsync(messageLoggingConsent, anotherConsent);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetUserConsentsAsync(userId);

        // Assert
        result.Should().HaveCount(2);
    }

    #endregion

    #region HasActiveConsentAsync Tests

    [Fact]
    public async Task HasActiveConsentAsync_WithActiveConsent_ReturnsTrue()
    {
        // Arrange
        var userId = 123456789UL;
        await SeedUserAsync(userId);

        var consentType = ConsentType.MessageLogging;
        var activeConsent = new UserConsent
        {
            DiscordUserId = userId,
            ConsentType = consentType,
            GrantedAt = DateTime.UtcNow.AddDays(-30),
            RevokedAt = null,
            GrantedVia = "SlashCommand"
        };

        await _context.UserConsents.AddAsync(activeConsent);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.HasActiveConsentAsync(userId, consentType);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasActiveConsentAsync_WithNoConsent_ReturnsFalse()
    {
        // Arrange
        var userId = 123456789UL;
        var consentType = ConsentType.MessageLogging;

        // Act
        var result = await _repository.HasActiveConsentAsync(userId, consentType);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasActiveConsentAsync_WithRevokedConsent_ReturnsFalse()
    {
        // Arrange
        var userId = 123456789UL;
        await SeedUserAsync(userId);

        var consentType = ConsentType.MessageLogging;
        var revokedConsent = new UserConsent
        {
            DiscordUserId = userId,
            ConsentType = consentType,
            GrantedAt = DateTime.UtcNow.AddDays(-30),
            RevokedAt = DateTime.UtcNow.AddDays(-5),
            GrantedVia = "SlashCommand",
            RevokedVia = "WebUI"
        };

        await _context.UserConsents.AddAsync(revokedConsent);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.HasActiveConsentAsync(userId, consentType);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasActiveConsentAsync_WithDifferentUser_ReturnsFalse()
    {
        // Arrange
        var userId1 = 123456789UL;
        var userId2 = 987654321UL;
        await SeedUserAsync(userId1);

        var consentType = ConsentType.MessageLogging;

        var consent = new UserConsent
        {
            DiscordUserId = userId1,
            ConsentType = consentType,
            GrantedAt = DateTime.UtcNow.AddDays(-30),
            RevokedAt = null,
            GrantedVia = "SlashCommand"
        };

        await _context.UserConsents.AddAsync(consent);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.HasActiveConsentAsync(userId2, consentType);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasActiveConsentAsync_WithDifferentConsentType_ReturnsFalse()
    {
        // Arrange
        var userId = 123456789UL;
        await SeedUserAsync(userId);

        var consent = new UserConsent
        {
            DiscordUserId = userId,
            ConsentType = ConsentType.MessageLogging,
            GrantedAt = DateTime.UtcNow.AddDays(-30),
            RevokedAt = null,
            GrantedVia = "SlashCommand"
        };

        await _context.UserConsents.AddAsync(consent);
        await _context.SaveChangesAsync();

        // Act - Query for a different consent type (using value 99 as a different type)
        var result = await _repository.HasActiveConsentAsync(userId, (ConsentType)99);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasActiveConsentAsync_WithMixedActiveAndRevoked_ReturnsTrueOnlyForActive()
    {
        // Arrange
        var userId = 123456789UL;
        await SeedUserAsync(userId);

        var consentType = ConsentType.MessageLogging;

        var revokedConsent = new UserConsent
        {
            DiscordUserId = userId,
            ConsentType = consentType,
            GrantedAt = DateTime.UtcNow.AddDays(-60),
            RevokedAt = DateTime.UtcNow.AddDays(-30),
            GrantedVia = "SlashCommand"
        };

        var activeConsent = new UserConsent
        {
            DiscordUserId = userId,
            ConsentType = consentType,
            GrantedAt = DateTime.UtcNow.AddDays(-10),
            RevokedAt = null,
            GrantedVia = "WebUI"
        };

        await _context.UserConsents.AddRangeAsync(revokedConsent, activeConsent);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.HasActiveConsentAsync(userId, consentType);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region Edge Cases and Integration Tests

    [Fact]
    public async Task GetActiveConsentAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var userId = 123456789UL;
        await SeedUserAsync(userId);

        var consentType = ConsentType.MessageLogging;
        var activeConsent = new UserConsent
        {
            DiscordUserId = userId,
            ConsentType = consentType,
            GrantedAt = DateTime.UtcNow.AddDays(-30),
            RevokedAt = null,
            GrantedVia = "SlashCommand"
        };

        await _context.UserConsents.AddAsync(activeConsent);
        await _context.SaveChangesAsync();

        using var cts = new CancellationTokenSource();

        // Act
        var result = await _repository.GetActiveConsentAsync(userId, consentType, cts.Token);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetUserConsentsAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var userId = 123456789UL;
        await SeedUserAsync(userId);

        var activeConsent = new UserConsent
        {
            DiscordUserId = userId,
            ConsentType = ConsentType.MessageLogging,
            GrantedAt = DateTime.UtcNow.AddDays(-30),
            RevokedAt = null,
            GrantedVia = "SlashCommand"
        };

        await _context.UserConsents.AddAsync(activeConsent);
        await _context.SaveChangesAsync();

        using var cts = new CancellationTokenSource();

        // Act
        var result = await _repository.GetUserConsentsAsync(userId, cts.Token);

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task HasActiveConsentAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var userId = 123456789UL;
        await SeedUserAsync(userId);

        var consentType = ConsentType.MessageLogging;
        var activeConsent = new UserConsent
        {
            DiscordUserId = userId,
            ConsentType = consentType,
            GrantedAt = DateTime.UtcNow.AddDays(-30),
            RevokedAt = null,
            GrantedVia = "SlashCommand"
        };

        await _context.UserConsents.AddAsync(activeConsent);
        await _context.SaveChangesAsync();

        using var cts = new CancellationTokenSource();

        // Act
        var result = await _repository.HasActiveConsentAsync(userId, consentType, cts.Token);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task UserConsent_IsActiveProperty_ReflectsRevokedStatus()
    {
        // Arrange
        var activeConsent = new UserConsent
        {
            DiscordUserId = 123456789UL,
            ConsentType = ConsentType.MessageLogging,
            GrantedAt = DateTime.UtcNow.AddDays(-30),
            RevokedAt = null,
            GrantedVia = "SlashCommand"
        };

        var revokedConsent = new UserConsent
        {
            DiscordUserId = 987654321UL,
            ConsentType = ConsentType.MessageLogging,
            GrantedAt = DateTime.UtcNow.AddDays(-30),
            RevokedAt = DateTime.UtcNow.AddDays(-5),
            GrantedVia = "SlashCommand"
        };

        // Act & Assert
        activeConsent.IsActive.Should().BeTrue();
        revokedConsent.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Repository_SupportsMultipleConcurrentOperations()
    {
        // Arrange
        var userId1 = 111111111UL;
        var userId2 = 222222222UL;
        var userId3 = 333333333UL;
        await SeedUserAsync(userId1);
        await SeedUserAsync(userId2);
        await SeedUserAsync(userId3);

        var consent1 = new UserConsent
        {
            DiscordUserId = userId1,
            ConsentType = ConsentType.MessageLogging,
            GrantedAt = DateTime.UtcNow.AddDays(-30),
            RevokedAt = null,
            GrantedVia = "SlashCommand"
        };

        var consent2 = new UserConsent
        {
            DiscordUserId = userId2,
            ConsentType = ConsentType.MessageLogging,
            GrantedAt = DateTime.UtcNow.AddDays(-30),
            RevokedAt = null,
            GrantedVia = "WebUI"
        };

        var consent3 = new UserConsent
        {
            DiscordUserId = userId3,
            ConsentType = ConsentType.MessageLogging,
            GrantedAt = DateTime.UtcNow.AddDays(-30),
            RevokedAt = DateTime.UtcNow.AddDays(-5),
            GrantedVia = "SlashCommand"
        };

        await _context.UserConsents.AddRangeAsync(consent1, consent2, consent3);
        await _context.SaveChangesAsync();

        // Act - Perform multiple operations
        var hasConsent1 = await _repository.HasActiveConsentAsync(userId1, ConsentType.MessageLogging);
        var activeConsent2 = await _repository.GetActiveConsentAsync(userId2, ConsentType.MessageLogging);
        var allConsents3 = await _repository.GetUserConsentsAsync(userId3);
        var hasConsent3 = await _repository.HasActiveConsentAsync(userId3, ConsentType.MessageLogging);

        // Assert
        hasConsent1.Should().BeTrue();
        activeConsent2.Should().NotBeNull();
        activeConsent2!.GrantedVia.Should().Be("WebUI");
        allConsents3.Should().HaveCount(1);
        hasConsent3.Should().BeFalse();
    }

    #endregion
}
