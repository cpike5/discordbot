using DiscordBot.Bot.Services;
using DiscordBot.Core.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="InteractionStateService"/>.
/// </summary>
public class InteractionStateServiceTests
{
    private readonly Mock<ILogger<InteractionStateService>> _mockLogger;
    private readonly Mock<IOptions<CachingOptions>> _mockCachingOptions;
    private readonly InteractionStateService _service;

    public InteractionStateServiceTests()
    {
        _mockLogger = new Mock<ILogger<InteractionStateService>>();
        _mockCachingOptions = new Mock<IOptions<CachingOptions>>();
        _mockCachingOptions.Setup(x => x.Value).Returns(new CachingOptions());
        _service = new InteractionStateService(_mockLogger.Object, _mockCachingOptions.Object);
    }

    [Fact]
    public void CreateState_ReturnsUniqueCorrelationId()
    {
        // Arrange
        const ulong userId = 123456789UL;
        var data = new TestStateData { Message = "Test data" };

        // Act
        var correlationId1 = _service.CreateState(userId, data);
        var correlationId2 = _service.CreateState(userId, data);
        var correlationId3 = _service.CreateState(userId, data);

        // Assert
        correlationId1.Should().NotBe(correlationId2,
            "each CreateState call should generate a unique correlation ID");
        correlationId2.Should().NotBe(correlationId3,
            "each CreateState call should generate a unique correlation ID");
        correlationId1.Should().NotBe(correlationId3,
            "each CreateState call should generate a unique correlation ID");
    }

    [Fact]
    public void CreateState_GeneratesEightCharacterId()
    {
        // Arrange
        const ulong userId = 123456789UL;
        var data = new TestStateData { Message = "Test data" };

        // Act
        var correlationId = _service.CreateState(userId, data);

        // Assert
        correlationId.Should().HaveLength(8,
            "the correlation ID should be exactly 8 characters long");
        correlationId.Should().MatchRegex("^[a-f0-9]{8}$",
            "the correlation ID should contain only hexadecimal characters");
    }

    [Fact]
    public void CreateState_WithDefaultExpiry_CreatesStateWithFifteenMinuteExpiry()
    {
        // Arrange
        const ulong userId = 123456789UL;
        var data = new TestStateData { Message = "Test data" };
        var beforeCreate = DateTime.UtcNow;

        // Act
        var correlationId = _service.CreateState(userId, data);

        // Assert
        var afterCreate = DateTime.UtcNow;
        _service.TryGetState<TestStateData>(correlationId, out var retrievedData)
            .Should().BeTrue("the state should be immediately retrievable after creation");
        retrievedData.Should().BeEquivalentTo(data,
            "the retrieved data should match the original data");

        // Verify state was created (indirectly by checking active count increased)
        _service.ActiveStateCount.Should().BeGreaterThan(0,
            "active state count should include the newly created state");
    }

    [Fact]
    public void CreateState_WithCustomExpiry_CreatesStateWithSpecifiedExpiry()
    {
        // Arrange
        const ulong userId = 123456789UL;
        var data = new TestStateData { Message = "Test data" };
        var customExpiry = TimeSpan.FromMinutes(5);

        // Act
        var correlationId = _service.CreateState(userId, data, customExpiry);

        // Assert
        _service.TryGetState<TestStateData>(correlationId, out var retrievedData)
            .Should().BeTrue("the state should be retrievable with custom expiry");
        retrievedData.Should().BeEquivalentTo(data);
    }

    [Fact]
    public void TryGetState_ExistingState_ReturnsTrueAndState()
    {
        // Arrange
        const ulong userId = 123456789UL;
        var data = new TestStateData { Message = "Test message", Value = 42 };
        var correlationId = _service.CreateState(userId, data);

        // Act
        var success = _service.TryGetState<TestStateData>(correlationId, out var retrievedData);

        // Assert
        success.Should().BeTrue("the state should be successfully retrieved");
        retrievedData.Should().NotBeNull();
        retrievedData.Should().BeEquivalentTo(data,
            "the retrieved data should match the original data");
    }

    [Fact]
    public void TryGetState_NonExistentState_ReturnsFalse()
    {
        // Arrange
        const string nonExistentCorrelationId = "abcd1234";

        // Act
        var success = _service.TryGetState<TestStateData>(nonExistentCorrelationId, out var retrievedData);

        // Assert
        success.Should().BeFalse("the state should not be found");
        retrievedData.Should().BeNull("no data should be returned for non-existent state");
    }

    [Fact]
    public void TryGetState_ExpiredState_ReturnsFalse()
    {
        // Arrange
        const ulong userId = 123456789UL;
        var data = new TestStateData { Message = "Test data" };
        // Create state with very short expiry
        var correlationId = _service.CreateState(userId, data, TimeSpan.FromMilliseconds(1));

        // Wait for expiration
        Thread.Sleep(50);

        // Act
        var success = _service.TryGetState<TestStateData>(correlationId, out var retrievedData);

        // Assert
        success.Should().BeFalse("expired state should not be retrievable");
        retrievedData.Should().BeNull("no data should be returned for expired state");
    }

    [Fact]
    public void TryGetState_ExpiredState_RemovesFromDictionary()
    {
        // Arrange
        const ulong userId = 123456789UL;
        var data = new TestStateData { Message = "Test data" };
        var correlationId = _service.CreateState(userId, data, TimeSpan.FromMilliseconds(1));
        var initialCount = _service.ActiveStateCount;

        // Wait for expiration
        Thread.Sleep(50);

        // Act
        _service.TryGetState<TestStateData>(correlationId, out _);

        // Assert
        _service.ActiveStateCount.Should().BeLessThan(initialCount,
            "expired state should be removed from the dictionary when accessed");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryGetState_WithNullOrEmptyCorrelationId_ReturnsFalse(string? correlationId)
    {
        // Act
        var success = _service.TryGetState<TestStateData>(correlationId!, out var retrievedData);

        // Assert
        success.Should().BeFalse("null or empty correlation ID should return false");
        retrievedData.Should().BeNull();
    }

    [Fact]
    public void TryGetState_WithWrongType_ReturnsFalse()
    {
        // Arrange
        const ulong userId = 123456789UL;
        var data = new TestStateData { Message = "Test data" };
        var correlationId = _service.CreateState(userId, data);

        // Act - try to retrieve with wrong type
        var success = _service.TryGetState<AlternateStateData>(correlationId, out var retrievedData);

        // Assert
        success.Should().BeFalse("type mismatch should return false");
        retrievedData.Should().BeNull("no data should be returned for type mismatch");
    }

    [Fact]
    public void TryRemoveState_ExistingState_ReturnsTrue()
    {
        // Arrange
        const ulong userId = 123456789UL;
        var data = new TestStateData { Message = "Test data" };
        var correlationId = _service.CreateState(userId, data);

        // Act
        var success = _service.TryRemoveState(correlationId);

        // Assert
        success.Should().BeTrue("removing existing state should succeed");
        _service.TryGetState<TestStateData>(correlationId, out _)
            .Should().BeFalse("state should no longer be retrievable after removal");
    }

    [Fact]
    public void TryRemoveState_NonExistentState_ReturnsFalse()
    {
        // Arrange
        const string nonExistentCorrelationId = "abcd1234";

        // Act
        var success = _service.TryRemoveState(nonExistentCorrelationId);

        // Assert
        success.Should().BeFalse("removing non-existent state should return false");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryRemoveState_WithNullOrEmptyCorrelationId_ReturnsFalse(string? correlationId)
    {
        // Act
        var success = _service.TryRemoveState(correlationId!);

        // Assert
        success.Should().BeFalse("null or empty correlation ID should return false");
    }

    [Fact]
    public void CleanupExpired_RemovesOnlyExpiredStates()
    {
        // Arrange
        const ulong userId = 123456789UL;

        // Create some states with very short expiry
        var expiredId1 = _service.CreateState(userId, new TestStateData { Message = "Expired 1" }, TimeSpan.FromMilliseconds(1));
        var expiredId2 = _service.CreateState(userId, new TestStateData { Message = "Expired 2" }, TimeSpan.FromMilliseconds(1));

        // Wait for expiration
        Thread.Sleep(50);

        // Create some states that won't expire
        var validId1 = _service.CreateState(userId, new TestStateData { Message = "Valid 1" }, TimeSpan.FromMinutes(10));
        var validId2 = _service.CreateState(userId, new TestStateData { Message = "Valid 2" }, TimeSpan.FromMinutes(10));

        // Act
        var removedCount = _service.CleanupExpired();

        // Assert
        removedCount.Should().Be(2, "exactly 2 expired states should be removed");

        // Verify expired states are gone
        _service.TryGetState<TestStateData>(expiredId1, out _)
            .Should().BeFalse("expired state 1 should be removed");
        _service.TryGetState<TestStateData>(expiredId2, out _)
            .Should().BeFalse("expired state 2 should be removed");

        // Verify valid states remain
        _service.TryGetState<TestStateData>(validId1, out _)
            .Should().BeTrue("valid state 1 should remain");
        _service.TryGetState<TestStateData>(validId2, out _)
            .Should().BeTrue("valid state 2 should remain");
    }

    [Fact]
    public void CleanupExpired_LeavesNonExpiredStates()
    {
        // Arrange
        const ulong userId = 123456789UL;
        var data1 = new TestStateData { Message = "Data 1" };
        var data2 = new TestStateData { Message = "Data 2" };

        var correlationId1 = _service.CreateState(userId, data1, TimeSpan.FromHours(1));
        var correlationId2 = _service.CreateState(userId, data2, TimeSpan.FromHours(1));

        // Act
        var removedCount = _service.CleanupExpired();

        // Assert
        removedCount.Should().Be(0, "no states should be removed as none are expired");
        _service.TryGetState<TestStateData>(correlationId1, out _)
            .Should().BeTrue("non-expired state 1 should remain");
        _service.TryGetState<TestStateData>(correlationId2, out _)
            .Should().BeTrue("non-expired state 2 should remain");
    }

    [Fact]
    public void CleanupExpired_WithNoStates_ReturnsZero()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<InteractionStateService>>();
        var mockOptions = new Mock<IOptions<CachingOptions>>();
        mockOptions.Setup(x => x.Value).Returns(new CachingOptions());
        var freshService = new InteractionStateService(mockLogger.Object, mockOptions.Object);

        // Act
        var removedCount = freshService.CleanupExpired();

        // Assert
        removedCount.Should().Be(0, "no states should be removed when dictionary is empty");
    }

    [Fact]
    public void ActiveStateCount_ReturnsCorrectCount()
    {
        // Arrange
        const ulong userId = 123456789UL;
        var initialCount = _service.ActiveStateCount;

        // Act - Add states
        _service.CreateState(userId, new TestStateData { Message = "State 1" });
        _service.CreateState(userId, new TestStateData { Message = "State 2" });
        _service.CreateState(userId, new TestStateData { Message = "State 3" });

        // Assert
        _service.ActiveStateCount.Should().Be(initialCount + 3,
            "active state count should increase by 3");
    }

    [Fact]
    public void ActiveStateCount_DecreasesAfterRemoval()
    {
        // Arrange
        const ulong userId = 123456789UL;
        var correlationId = _service.CreateState(userId, new TestStateData { Message = "Test" });
        var countBeforeRemoval = _service.ActiveStateCount;

        // Act
        _service.TryRemoveState(correlationId);

        // Assert
        _service.ActiveStateCount.Should().Be(countBeforeRemoval - 1,
            "active state count should decrease after removal");
    }

    [Fact]
    public void CreateState_LogsDebugMessage()
    {
        // Arrange
        const ulong userId = 123456789UL;
        var data = new TestStateData { Message = "Test data" };

        // Act
        _service.CreateState(userId, data);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Created interaction state")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "creating state should log a debug message");
    }

    [Fact]
    public void TryGetState_WithExpiredState_LogsDebugMessage()
    {
        // Arrange
        const ulong userId = 123456789UL;
        var correlationId = _service.CreateState(userId, new TestStateData { Message = "Test" }, TimeSpan.FromMilliseconds(1));

        // Wait for expiration
        Thread.Sleep(50);

        // Act
        _service.TryGetState<TestStateData>(correlationId, out _);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("State expired")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "accessing expired state should log a debug message");
    }

    [Fact]
    public void TryRemoveState_LogsDebugMessageOnSuccess()
    {
        // Arrange
        const ulong userId = 123456789UL;
        var correlationId = _service.CreateState(userId, new TestStateData { Message = "Test" });

        // Reset the mock to clear the CreateState log
        _mockLogger.Invocations.Clear();

        // Act
        _service.TryRemoveState(correlationId);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Removed state")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "removing state should log a debug message");
    }

    [Fact]
    public void CleanupExpired_WithExpiredStates_LogsDebugMessage()
    {
        // Arrange
        const ulong userId = 123456789UL;
        _service.CreateState(userId, new TestStateData { Message = "Expired" }, TimeSpan.FromMilliseconds(1));

        // Wait for expiration
        Thread.Sleep(50);

        // Reset the mock to clear the CreateState log
        _mockLogger.Invocations.Clear();

        // Act
        _service.CleanupExpired();

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cleaned up")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "cleaning up expired states should log a debug message");
    }

    [Fact]
    public void CreateState_WithDifferentDataTypes_WorksCorrectly()
    {
        // Arrange
        const ulong userId = 123456789UL;
        var stringData = "Simple string data";
        var intData = 42;
        var complexData = new TestStateData { Message = "Complex", Value = 100 };

        // Act
        var stringId = _service.CreateState(userId, stringData);
        var intId = _service.CreateState(userId, intData);
        var complexId = _service.CreateState(userId, complexData);

        // Assert
        _service.TryGetState<string>(stringId, out var retrievedString)
            .Should().BeTrue();
        retrievedString.Should().Be(stringData);

        _service.TryGetState<int>(intId, out var retrievedInt)
            .Should().BeTrue();
        retrievedInt.Should().Be(intData);

        _service.TryGetState<TestStateData>(complexId, out var retrievedComplex)
            .Should().BeTrue();
        retrievedComplex.Should().BeEquivalentTo(complexData);
    }

    [Fact]
    public void CreateState_StoresCorrectUserId()
    {
        // Arrange
        const ulong userId1 = 111111111UL;
        const ulong userId2 = 222222222UL;
        var data1 = new TestStateData { Message = "User 1 data" };
        var data2 = new TestStateData { Message = "User 2 data" };

        // Act
        var id1 = _service.CreateState(userId1, data1);
        var id2 = _service.CreateState(userId2, data2);

        // Assert
        _service.TryGetState<TestStateData>(id1, out var retrieved1)
            .Should().BeTrue();
        retrieved1.Should().BeEquivalentTo(data1);

        _service.TryGetState<TestStateData>(id2, out var retrieved2)
            .Should().BeTrue();
        retrieved2.Should().BeEquivalentTo(data2);
    }

    // Test helper classes
    private class TestStateData
    {
        public string Message { get; set; } = string.Empty;
        public int Value { get; set; }
    }

    private class AlternateStateData
    {
        public string DifferentProperty { get; set; } = string.Empty;
    }
}
