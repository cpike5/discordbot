using DiscordBot.Bot.Services;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="ConsentService"/>.
/// Tests cover consent status retrieval, history tracking, granting, and revoking consent.
/// </summary>
public class ConsentServiceTests
{
    private readonly Mock<IUserConsentRepository> _mockRepository;
    private readonly Mock<ILogger<ConsentService>> _mockLogger;
    private readonly ConsentService _service;

    public ConsentServiceTests()
    {
        _mockRepository = new Mock<IUserConsentRepository>();
        _mockLogger = new Mock<ILogger<ConsentService>>();
        _service = new ConsentService(_mockRepository.Object, _mockLogger.Object);
    }

    #region GetConsentStatusAsync Tests

    [Fact]
    public async Task GetConsentStatusAsync_ReturnsAllConsentTypes_IncludingNotGranted()
    {
        // Arrange
        var discordUserId = 123456789UL;
        var userConsents = new List<UserConsent>
        {
            new UserConsent
            {
                Id = 1,
                DiscordUserId = discordUserId,
                ConsentType = ConsentType.MessageLogging,
                GrantedAt = DateTime.UtcNow.AddDays(-5),
                GrantedVia = "SlashCommand",
                RevokedAt = null,
                RevokedVia = null
            }
        };

        _mockRepository
            .Setup(r => r.GetUserConsentsAsync(discordUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userConsents);

        // Act
        var result = await _service.GetConsentStatusAsync(discordUserId);

        // Assert
        var statuses = result.ToList();
        statuses.Should().NotBeNull();
        statuses.Should().HaveCountGreaterThanOrEqualTo(1, "should include at least MessageLogging consent type");

        // Verify MessageLogging is present and granted
        var messageLoggingStatus = statuses.FirstOrDefault(s => s.Type == (int)ConsentType.MessageLogging);
        messageLoggingStatus.Should().NotBeNull();
        messageLoggingStatus!.IsGranted.Should().BeTrue();
        messageLoggingStatus.TypeDisplayName.Should().Be("Message Logging");
        messageLoggingStatus.GrantedAt.Should().NotBeNull();
        messageLoggingStatus.GrantedVia.Should().Be("SlashCommand");

        _mockRepository.Verify(
            r => r.GetUserConsentsAsync(discordUserId, It.IsAny<CancellationToken>()),
            Times.Once,
            "repository should be called once to fetch user consents");
    }

    [Fact]
    public async Task GetConsentStatusAsync_ReturnsCorrectStatus_ForActiveConsent()
    {
        // Arrange
        var discordUserId = 987654321UL;
        var grantedAt = DateTime.UtcNow.AddDays(-10);
        var userConsents = new List<UserConsent>
        {
            new UserConsent
            {
                Id = 1,
                DiscordUserId = discordUserId,
                ConsentType = ConsentType.MessageLogging,
                GrantedAt = grantedAt,
                GrantedVia = "WebUI",
                RevokedAt = null,
                RevokedVia = null
            }
        };

        _mockRepository
            .Setup(r => r.GetUserConsentsAsync(discordUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userConsents);

        // Act
        var result = await _service.GetConsentStatusAsync(discordUserId);

        // Assert
        var statuses = result.ToList();
        var messageLoggingStatus = statuses.First(s => s.Type == (int)ConsentType.MessageLogging);

        messageLoggingStatus.IsGranted.Should().BeTrue("consent is active and not revoked");
        messageLoggingStatus.GrantedAt.Should().Be(grantedAt);
        messageLoggingStatus.GrantedVia.Should().Be("WebUI");
        messageLoggingStatus.TypeDisplayName.Should().Be("Message Logging");
        messageLoggingStatus.Description.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetConsentStatusAsync_ReturnsNotGranted_ForRevokedConsent()
    {
        // Arrange
        var discordUserId = 111222333UL;
        var userConsents = new List<UserConsent>
        {
            new UserConsent
            {
                Id = 1,
                DiscordUserId = discordUserId,
                ConsentType = ConsentType.MessageLogging,
                GrantedAt = DateTime.UtcNow.AddDays(-10),
                GrantedVia = "WebUI",
                RevokedAt = DateTime.UtcNow.AddDays(-2),
                RevokedVia = "WebUI"
            }
        };

        _mockRepository
            .Setup(r => r.GetUserConsentsAsync(discordUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userConsents);

        // Act
        var result = await _service.GetConsentStatusAsync(discordUserId);

        // Assert
        var statuses = result.ToList();
        var messageLoggingStatus = statuses.First(s => s.Type == (int)ConsentType.MessageLogging);

        messageLoggingStatus.IsGranted.Should().BeFalse("consent was revoked");
        messageLoggingStatus.GrantedAt.Should().BeNull("revoked consent should not show granted timestamp");
        messageLoggingStatus.GrantedVia.Should().BeNull("revoked consent should not show granted source");
    }

    [Fact]
    public async Task GetConsentStatusAsync_ReturnsNotGranted_WhenNoConsentExists()
    {
        // Arrange
        var discordUserId = 444555666UL;
        var userConsents = new List<UserConsent>();

        _mockRepository
            .Setup(r => r.GetUserConsentsAsync(discordUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userConsents);

        // Act
        var result = await _service.GetConsentStatusAsync(discordUserId);

        // Assert
        var statuses = result.ToList();
        statuses.Should().NotBeEmpty("should return status for all consent types");

        var messageLoggingStatus = statuses.First(s => s.Type == (int)ConsentType.MessageLogging);
        messageLoggingStatus.IsGranted.Should().BeFalse("no consent exists for this type");
        messageLoggingStatus.GrantedAt.Should().BeNull();
        messageLoggingStatus.GrantedVia.Should().BeNull();
    }

    [Fact]
    public async Task GetConsentStatusAsync_SelectsMostRecentActiveConsent_WhenMultipleExist()
    {
        // Arrange
        var discordUserId = 777888999UL;
        var olderGrantedAt = DateTime.UtcNow.AddDays(-20);
        var newerGrantedAt = DateTime.UtcNow.AddDays(-5);
        var userConsents = new List<UserConsent>
        {
            new UserConsent
            {
                Id = 1,
                DiscordUserId = discordUserId,
                ConsentType = ConsentType.MessageLogging,
                GrantedAt = olderGrantedAt,
                GrantedVia = "SlashCommand",
                RevokedAt = DateTime.UtcNow.AddDays(-10),
                RevokedVia = "SlashCommand"
            },
            new UserConsent
            {
                Id = 2,
                DiscordUserId = discordUserId,
                ConsentType = ConsentType.MessageLogging,
                GrantedAt = newerGrantedAt,
                GrantedVia = "WebUI",
                RevokedAt = null,
                RevokedVia = null
            }
        };

        _mockRepository
            .Setup(r => r.GetUserConsentsAsync(discordUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userConsents);

        // Act
        var result = await _service.GetConsentStatusAsync(discordUserId);

        // Assert
        var statuses = result.ToList();
        var messageLoggingStatus = statuses.First(s => s.Type == (int)ConsentType.MessageLogging);

        messageLoggingStatus.IsGranted.Should().BeTrue();
        messageLoggingStatus.GrantedAt.Should().Be(newerGrantedAt, "should use most recent active consent");
        messageLoggingStatus.GrantedVia.Should().Be("WebUI");
    }

    #endregion

    #region GetConsentHistoryAsync Tests

    [Fact]
    public async Task GetConsentHistoryAsync_ReturnsOrderedHistory_NewestFirst()
    {
        // Arrange
        var discordUserId = 123456789UL;
        var oldestGrantedAt = DateTime.UtcNow.AddDays(-30);
        var middleGrantedAt = DateTime.UtcNow.AddDays(-20);
        var newestGrantedAt = DateTime.UtcNow.AddDays(-10);

        var userConsents = new List<UserConsent>
        {
            new UserConsent
            {
                Id = 1,
                DiscordUserId = discordUserId,
                ConsentType = ConsentType.MessageLogging,
                GrantedAt = oldestGrantedAt,
                GrantedVia = "SlashCommand",
                RevokedAt = DateTime.UtcNow.AddDays(-25),
                RevokedVia = "SlashCommand"
            },
            new UserConsent
            {
                Id = 2,
                DiscordUserId = discordUserId,
                ConsentType = ConsentType.MessageLogging,
                GrantedAt = middleGrantedAt,
                GrantedVia = "WebUI",
                RevokedAt = DateTime.UtcNow.AddDays(-15),
                RevokedVia = "WebUI"
            },
            new UserConsent
            {
                Id = 3,
                DiscordUserId = discordUserId,
                ConsentType = ConsentType.MessageLogging,
                GrantedAt = newestGrantedAt,
                GrantedVia = "WebUI",
                RevokedAt = null,
                RevokedVia = null
            }
        };

        _mockRepository
            .Setup(r => r.GetUserConsentsAsync(discordUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userConsents);

        // Act
        var result = await _service.GetConsentHistoryAsync(discordUserId);

        // Assert
        var history = result.ToList();
        history.Should().NotBeEmpty();

        // First entry should be the most recent action (newest grant)
        history[0].Timestamp.Should().Be(newestGrantedAt);
        history[0].Action.Should().Be("Granted");

        // History should be in descending order by timestamp
        for (int i = 0; i < history.Count - 1; i++)
        {
            history[i].Timestamp.Should().BeOnOrAfter(history[i + 1].Timestamp,
                "history should be ordered newest first");
        }

        _mockRepository.Verify(
            r => r.GetUserConsentsAsync(discordUserId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetConsentHistoryAsync_IncludesBothGrantedAndRevokedEntries()
    {
        // Arrange
        var discordUserId = 987654321UL;
        var grantedAt = DateTime.UtcNow.AddDays(-10);
        var revokedAt = DateTime.UtcNow.AddDays(-5);

        var userConsents = new List<UserConsent>
        {
            new UserConsent
            {
                Id = 1,
                DiscordUserId = discordUserId,
                ConsentType = ConsentType.MessageLogging,
                GrantedAt = grantedAt,
                GrantedVia = "WebUI",
                RevokedAt = revokedAt,
                RevokedVia = "WebUI"
            }
        };

        _mockRepository
            .Setup(r => r.GetUserConsentsAsync(discordUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userConsents);

        // Act
        var result = await _service.GetConsentHistoryAsync(discordUserId);

        // Assert
        var history = result.ToList();
        history.Should().HaveCount(2, "should include both granted and revoked entries");

        // First entry should be the revoke (most recent)
        history[0].Action.Should().Be("Revoked");
        history[0].Timestamp.Should().Be(revokedAt);
        history[0].Source.Should().Be("WebUI");
        history[0].TypeDisplayName.Should().Be("Message Logging");

        // Second entry should be the grant
        history[1].Action.Should().Be("Granted");
        history[1].Timestamp.Should().Be(grantedAt);
        history[1].Source.Should().Be("WebUI");
    }

    [Fact]
    public async Task GetConsentHistoryAsync_ReturnsEmptyList_WhenNoHistory()
    {
        // Arrange
        var discordUserId = 111222333UL;
        var userConsents = new List<UserConsent>();

        _mockRepository
            .Setup(r => r.GetUserConsentsAsync(discordUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userConsents);

        // Act
        var result = await _service.GetConsentHistoryAsync(discordUserId);

        // Assert
        var history = result.ToList();
        history.Should().BeEmpty("no consent history exists for this user");

        _mockRepository.Verify(
            r => r.GetUserConsentsAsync(discordUserId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetConsentHistoryAsync_HandlesUnknownSource_WhenSourceIsNull()
    {
        // Arrange
        var discordUserId = 444555666UL;
        var userConsents = new List<UserConsent>
        {
            new UserConsent
            {
                Id = 1,
                DiscordUserId = discordUserId,
                ConsentType = ConsentType.MessageLogging,
                GrantedAt = DateTime.UtcNow.AddDays(-10),
                GrantedVia = null,
                RevokedAt = null,
                RevokedVia = null
            }
        };

        _mockRepository
            .Setup(r => r.GetUserConsentsAsync(discordUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userConsents);

        // Act
        var result = await _service.GetConsentHistoryAsync(discordUserId);

        // Assert
        var history = result.ToList();
        history.Should().HaveCount(1);
        history[0].Source.Should().Be("Unknown", "null source should be replaced with 'Unknown'");
    }

    #endregion

    #region GrantConsentAsync Tests

    [Fact]
    public async Task GrantConsentAsync_CreatesNewConsent_WhenNoneExists()
    {
        // Arrange
        var discordUserId = 123456789UL;
        var consentType = ConsentType.MessageLogging;

        _mockRepository
            .Setup(r => r.GetActiveConsentAsync(discordUserId, consentType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(It.IsAny<UserConsent>());

        UserConsent? capturedConsent = null;
        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<UserConsent>(), It.IsAny<CancellationToken>()))
            .Callback<UserConsent, CancellationToken>((consent, _) => capturedConsent = consent)
            .ReturnsAsync(It.IsAny<UserConsent>());

        // Act
        var result = await _service.GrantConsentAsync(discordUserId, consentType);

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeTrue();
        result.ErrorCode.Should().BeNull();
        result.ErrorMessage.Should().BeNull();

        capturedConsent.Should().NotBeNull();
        capturedConsent!.DiscordUserId.Should().Be(discordUserId);
        capturedConsent.ConsentType.Should().Be(consentType);
        capturedConsent.GrantedVia.Should().Be("WebUI", "consent granted via service should use WebUI source");
        capturedConsent.RevokedAt.Should().BeNull();
        capturedConsent.RevokedVia.Should().BeNull();
        capturedConsent.GrantedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        _mockRepository.Verify(
            r => r.GetActiveConsentAsync(discordUserId, consentType, It.IsAny<CancellationToken>()),
            Times.Once,
            "should check for existing active consent");

        _mockRepository.Verify(
            r => r.AddAsync(It.IsAny<UserConsent>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "should add new consent record");
    }

    [Fact]
    public async Task GrantConsentAsync_ReturnsAlreadyGranted_WhenActiveConsentExists()
    {
        // Arrange
        var discordUserId = 987654321UL;
        var consentType = ConsentType.MessageLogging;

        var existingConsent = new UserConsent
        {
            Id = 1,
            DiscordUserId = discordUserId,
            ConsentType = consentType,
            GrantedAt = DateTime.UtcNow.AddDays(-5),
            GrantedVia = "SlashCommand",
            RevokedAt = null,
            RevokedVia = null
        };

        _mockRepository
            .Setup(r => r.GetActiveConsentAsync(discordUserId, consentType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingConsent);

        // Act
        var result = await _service.GrantConsentAsync(discordUserId, consentType);

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ConsentUpdateResult.AlreadyGranted);
        result.ErrorMessage.Should().Be("Consent is already granted for this type.");

        _mockRepository.Verify(
            r => r.AddAsync(It.IsAny<UserConsent>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "should not add new consent when active consent already exists");
    }

    [Fact]
    public async Task GrantConsentAsync_ReturnsInvalidConsentType_ForInvalidType()
    {
        // Arrange
        var discordUserId = 111222333UL;
        var invalidConsentType = (ConsentType)999;

        // Act
        var result = await _service.GrantConsentAsync(discordUserId, invalidConsentType);

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ConsentUpdateResult.InvalidConsentType);
        result.ErrorMessage.Should().Be("Invalid consent type.");

        _mockRepository.Verify(
            r => r.GetActiveConsentAsync(It.IsAny<ulong>(), It.IsAny<ConsentType>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "should not check for active consent when type is invalid");

        _mockRepository.Verify(
            r => r.AddAsync(It.IsAny<UserConsent>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "should not add consent when type is invalid");
    }

    [Fact]
    public async Task GrantConsentAsync_ReturnsDatabaseError_WhenRepositoryThrows()
    {
        // Arrange
        var discordUserId = 444555666UL;
        var consentType = ConsentType.MessageLogging;

        _mockRepository
            .Setup(r => r.GetActiveConsentAsync(discordUserId, consentType, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await _service.GrantConsentAsync(discordUserId, consentType);

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ConsentUpdateResult.DatabaseError);
        result.ErrorMessage.Should().Be("An error occurred while granting consent.");

        _mockRepository.Verify(
            r => r.AddAsync(It.IsAny<UserConsent>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "should not attempt to add consent when exception occurs");
    }

    [Fact]
    public async Task GrantConsentAsync_SetsCorrectSource_AsWebUI()
    {
        // Arrange
        var discordUserId = 777888999UL;
        var consentType = ConsentType.MessageLogging;

        _mockRepository
            .Setup(r => r.GetActiveConsentAsync(discordUserId, consentType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(It.IsAny<UserConsent>());

        UserConsent? capturedConsent = null;
        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<UserConsent>(), It.IsAny<CancellationToken>()))
            .Callback<UserConsent, CancellationToken>((consent, _) => capturedConsent = consent)
            .ReturnsAsync(It.IsAny<UserConsent>());

        // Act
        await _service.GrantConsentAsync(discordUserId, consentType);

        // Assert
        capturedConsent.Should().NotBeNull();
        capturedConsent!.GrantedVia.Should().Be("WebUI", "service should always use WebUI as source");
    }

    #endregion

    #region RevokeConsentAsync Tests

    [Fact]
    public async Task RevokeConsentAsync_RevokesExistingActiveConsent()
    {
        // Arrange
        var discordUserId = 123456789UL;
        var consentType = ConsentType.MessageLogging;

        var activeConsent = new UserConsent
        {
            Id = 1,
            DiscordUserId = discordUserId,
            ConsentType = consentType,
            GrantedAt = DateTime.UtcNow.AddDays(-10),
            GrantedVia = "WebUI",
            RevokedAt = null,
            RevokedVia = null
        };

        _mockRepository
            .Setup(r => r.GetActiveConsentAsync(discordUserId, consentType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeConsent);

        UserConsent? updatedConsent = null;
        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<UserConsent>(), It.IsAny<CancellationToken>()))
            .Callback<UserConsent, CancellationToken>((consent, _) => updatedConsent = consent)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.RevokeConsentAsync(discordUserId, consentType);

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeTrue();
        result.ErrorCode.Should().BeNull();
        result.ErrorMessage.Should().BeNull();

        updatedConsent.Should().NotBeNull();
        updatedConsent!.RevokedAt.Should().NotBeNull();
        updatedConsent.RevokedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        updatedConsent.RevokedVia.Should().Be("WebUI", "consent revoked via service should use WebUI source");

        _mockRepository.Verify(
            r => r.GetActiveConsentAsync(discordUserId, consentType, It.IsAny<CancellationToken>()),
            Times.Once,
            "should check for active consent");

        _mockRepository.Verify(
            r => r.UpdateAsync(It.IsAny<UserConsent>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "should update consent to revoke it");
    }

    [Fact]
    public async Task RevokeConsentAsync_ReturnsNotGranted_WhenNoActiveConsent()
    {
        // Arrange
        var discordUserId = 987654321UL;
        var consentType = ConsentType.MessageLogging;

        _mockRepository
            .Setup(r => r.GetActiveConsentAsync(discordUserId, consentType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(It.IsAny<UserConsent>());

        // Act
        var result = await _service.RevokeConsentAsync(discordUserId, consentType);

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ConsentUpdateResult.NotGranted);
        result.ErrorMessage.Should().Be("No active consent found for this type.");

        _mockRepository.Verify(
            r => r.UpdateAsync(It.IsAny<UserConsent>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "should not update when no active consent exists");
    }

    [Fact]
    public async Task RevokeConsentAsync_ReturnsInvalidConsentType_ForInvalidType()
    {
        // Arrange
        var discordUserId = 111222333UL;
        var invalidConsentType = (ConsentType)999;

        // Act
        var result = await _service.RevokeConsentAsync(discordUserId, invalidConsentType);

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ConsentUpdateResult.InvalidConsentType);
        result.ErrorMessage.Should().Be("Invalid consent type.");

        _mockRepository.Verify(
            r => r.GetActiveConsentAsync(It.IsAny<ulong>(), It.IsAny<ConsentType>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "should not check for active consent when type is invalid");

        _mockRepository.Verify(
            r => r.UpdateAsync(It.IsAny<UserConsent>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "should not update when type is invalid");
    }

    [Fact]
    public async Task RevokeConsentAsync_ReturnsDatabaseError_WhenRepositoryThrows()
    {
        // Arrange
        var discordUserId = 444555666UL;
        var consentType = ConsentType.MessageLogging;

        _mockRepository
            .Setup(r => r.GetActiveConsentAsync(discordUserId, consentType, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await _service.RevokeConsentAsync(discordUserId, consentType);

        // Assert
        result.Should().NotBeNull();
        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ConsentUpdateResult.DatabaseError);
        result.ErrorMessage.Should().Be("An error occurred while revoking consent.");

        _mockRepository.Verify(
            r => r.UpdateAsync(It.IsAny<UserConsent>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "should not attempt to update when exception occurs");
    }

    [Fact]
    public async Task RevokeConsentAsync_SetsCorrectSource_AsWebUI()
    {
        // Arrange
        var discordUserId = 777888999UL;
        var consentType = ConsentType.MessageLogging;

        var activeConsent = new UserConsent
        {
            Id = 1,
            DiscordUserId = discordUserId,
            ConsentType = consentType,
            GrantedAt = DateTime.UtcNow.AddDays(-10),
            GrantedVia = "SlashCommand",
            RevokedAt = null,
            RevokedVia = null
        };

        _mockRepository
            .Setup(r => r.GetActiveConsentAsync(discordUserId, consentType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeConsent);

        UserConsent? updatedConsent = null;
        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<UserConsent>(), It.IsAny<CancellationToken>()))
            .Callback<UserConsent, CancellationToken>((consent, _) => updatedConsent = consent)
            .Returns(Task.CompletedTask);

        // Act
        await _service.RevokeConsentAsync(discordUserId, consentType);

        // Assert
        updatedConsent.Should().NotBeNull();
        updatedConsent!.RevokedVia.Should().Be("WebUI", "service should always use WebUI as revoke source");
    }

    #endregion

    #region Logging Tests

    [Fact]
    public async Task GetConsentStatusAsync_LogsDebugMessages()
    {
        // Arrange
        var discordUserId = 123456789UL;
        _mockRepository
            .Setup(r => r.GetUserConsentsAsync(discordUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserConsent>());

        // Act
        await _service.GetConsentStatusAsync(discordUserId);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Retrieving consent status")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log debug message when retrieving consent status");
    }

    [Fact]
    public async Task GrantConsentAsync_LogsInformation_OnSuccess()
    {
        // Arrange
        var discordUserId = 123456789UL;
        var consentType = ConsentType.MessageLogging;

        _mockRepository
            .Setup(r => r.GetActiveConsentAsync(discordUserId, consentType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(It.IsAny<UserConsent>());

        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<UserConsent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(It.IsAny<UserConsent>());

        // Act
        await _service.GrantConsentAsync(discordUserId, consentType);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Consent") && v.ToString()!.Contains("granted")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log information message when consent is successfully granted");
    }

    [Fact]
    public async Task RevokeConsentAsync_LogsInformation_OnSuccess()
    {
        // Arrange
        var discordUserId = 123456789UL;
        var consentType = ConsentType.MessageLogging;

        var activeConsent = new UserConsent
        {
            Id = 1,
            DiscordUserId = discordUserId,
            ConsentType = consentType,
            GrantedAt = DateTime.UtcNow.AddDays(-10),
            GrantedVia = "WebUI",
            RevokedAt = null,
            RevokedVia = null
        };

        _mockRepository
            .Setup(r => r.GetActiveConsentAsync(discordUserId, consentType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeConsent);

        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<UserConsent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.RevokeConsentAsync(discordUserId, consentType);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Consent") && v.ToString()!.Contains("revoked")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log information message when consent is successfully revoked");
    }

    [Fact]
    public async Task GrantConsentAsync_LogsError_OnException()
    {
        // Arrange
        var discordUserId = 123456789UL;
        var consentType = ConsentType.MessageLogging;
        var expectedException = new Exception("Database error");

        _mockRepository
            .Setup(r => r.GetActiveConsentAsync(discordUserId, consentType, It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act
        await _service.GrantConsentAsync(discordUserId, consentType);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to grant consent")),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log error when exception occurs during grant");
    }

    #endregion
}
