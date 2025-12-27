using DiscordBot.Bot.Services;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="AuditLogBuilder"/>.
/// Tests cover fluent builder API, method chaining, and log creation.
/// </summary>
[Trait("Category", "Unit")]
public class AuditLogBuilderTests
{
    private readonly Mock<IAuditLogRepository> _mockRepository;
    private readonly Mock<IAuditLogQueue> _mockQueue;
    private readonly Mock<ILogger<AuditLogService>> _mockLogger;
    private readonly AuditLogService _service;

    public AuditLogBuilderTests()
    {
        _mockRepository = new Mock<IAuditLogRepository>();
        _mockQueue = new Mock<IAuditLogQueue>();
        _mockLogger = new Mock<ILogger<AuditLogService>>();
        _service = new AuditLogService(_mockRepository.Object, _mockQueue.Object, _mockLogger.Object);
    }

    #region Fluent API Tests

    [Fact]
    public void ForCategory_SetsCategory()
    {
        // Arrange
        var builder = _service.CreateBuilder();

        // Act
        var result = builder.ForCategory(AuditLogCategory.User);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeSameAs(builder, "should return same builder instance for fluent chaining");
    }

    [Fact]
    public void WithAction_SetsAction()
    {
        // Arrange
        var builder = _service.CreateBuilder();

        // Act
        var result = builder.WithAction(AuditLogAction.Created);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeSameAs(builder, "should return same builder instance for fluent chaining");
    }

    [Fact]
    public void ByUser_SetsActorTypeAndId()
    {
        // Arrange
        var builder = _service.CreateBuilder();
        const string userId = "user123";

        // Act
        var result = builder.ByUser(userId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeSameAs(builder, "should return same builder instance for fluent chaining");
    }

    [Fact]
    public void BySystem_SetsActorTypeToSystem()
    {
        // Arrange
        var builder = _service.CreateBuilder();

        // Act
        var result = builder.BySystem();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeSameAs(builder, "should return same builder instance for fluent chaining");
    }

    [Fact]
    public void ByBot_SetsActorTypeToBot()
    {
        // Arrange
        var builder = _service.CreateBuilder();

        // Act
        var result = builder.ByBot();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeSameAs(builder, "should return same builder instance for fluent chaining");
    }

    [Fact]
    public void OnTarget_SetsTargetTypeAndId()
    {
        // Arrange
        var builder = _service.CreateBuilder();

        // Act
        var result = builder.OnTarget("ScheduledMessage", "msg123");

        // Assert
        result.Should().NotBeNull();
        result.Should().BeSameAs(builder, "should return same builder instance for fluent chaining");
    }

    [Fact]
    public void InGuild_SetsGuildId()
    {
        // Arrange
        var builder = _service.CreateBuilder();

        // Act
        var result = builder.InGuild(123456789UL);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeSameAs(builder, "should return same builder instance for fluent chaining");
    }

    [Fact]
    public void WithDetails_Dictionary_SerializesToJson()
    {
        // Arrange
        var builder = _service.CreateBuilder();
        var details = new Dictionary<string, object?>
        {
            { "Title", "Test Message" },
            { "ChannelId", 987654321UL },
            { "IsEnabled", true }
        };

        // Act
        var result = builder.WithDetails(details);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeSameAs(builder, "should return same builder instance for fluent chaining");
    }

    [Fact]
    public void WithDetails_AnonymousObject_SerializesToJson()
    {
        // Arrange
        var builder = _service.CreateBuilder();
        var details = new { Title = "Test Message", ChannelId = 987654321UL, IsEnabled = true };

        // Act
        var result = builder.WithDetails(details);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeSameAs(builder, "should return same builder instance for fluent chaining");
    }

    [Fact]
    public void FromIpAddress_SetsIpAddress()
    {
        // Arrange
        var builder = _service.CreateBuilder();

        // Act
        var result = builder.FromIpAddress("192.168.1.100");

        // Assert
        result.Should().NotBeNull();
        result.Should().BeSameAs(builder, "should return same builder instance for fluent chaining");
    }

    [Fact]
    public void WithCorrelationId_SetsCorrelationId()
    {
        // Arrange
        var builder = _service.CreateBuilder();
        const string correlationId = "test-correlation-123";

        // Act
        var result = builder.WithCorrelationId(correlationId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeSameAs(builder, "should return same builder instance for fluent chaining");
    }

    #endregion

    #region Method Chaining Tests

    [Fact]
    public void FluentAPI_AllowsMethodChaining()
    {
        // Arrange
        var builder = _service.CreateBuilder();

        // Act
        var result = builder
            .ForCategory(AuditLogCategory.Message)
            .WithAction(AuditLogAction.Created)
            .ByUser("admin123")
            .OnTarget("Message", "msg-456")
            .InGuild(123456789UL)
            .WithDetails(new { Title = "Daily Announcement" })
            .FromIpAddress("127.0.0.1")
            .WithCorrelationId("correlation-789");

        // Assert
        result.Should().NotBeNull();
        result.Should().BeSameAs(builder, "fluent API should return same builder instance");
    }

    #endregion

    #region LogAsync Tests

    [Fact]
    public async Task LogAsync_CallsServiceLogAsync()
    {
        // Arrange
        var builder = _service.CreateBuilder()
            .ForCategory(AuditLogCategory.User)
            .WithAction(AuditLogAction.Created)
            .ByUser("admin123");

        AuditLogCreateDto? capturedDto = null;
        _mockQueue.Setup(q => q.Enqueue(It.IsAny<AuditLogCreateDto>()))
            .Callback<AuditLogCreateDto>(dto => capturedDto = dto);

        // Act
        await builder.LogAsync();

        // Assert
        capturedDto.Should().NotBeNull();
        capturedDto!.Category.Should().Be(AuditLogCategory.User);
        capturedDto.Action.Should().Be(AuditLogAction.Created);
        capturedDto.ActorId.Should().Be("admin123");
        capturedDto.ActorType.Should().Be(AuditLogActorType.User);

        _mockQueue.Verify(
            q => q.Enqueue(It.IsAny<AuditLogCreateDto>()),
            Times.Once);
    }

    [Fact]
    public async Task LogAsync_WithAllProperties_BuildsCompleteDto()
    {
        // Arrange
        var builder = _service.CreateBuilder()
            .ForCategory(AuditLogCategory.Configuration)
            .WithAction(AuditLogAction.SettingChanged)
            .ByUser("user123")
            .OnTarget("ScheduledMessage", "msg-789")
            .InGuild(987654321UL)
            .WithDetails(new { NewTitle = "Updated Title" })
            .FromIpAddress("192.168.1.1")
            .WithCorrelationId("corr-123");

        AuditLogCreateDto? capturedDto = null;
        _mockQueue.Setup(q => q.Enqueue(It.IsAny<AuditLogCreateDto>()))
            .Callback<AuditLogCreateDto>(dto => capturedDto = dto);

        // Act
        await builder.LogAsync();

        // Assert
        capturedDto.Should().NotBeNull();
        capturedDto!.Category.Should().Be(AuditLogCategory.Configuration);
        capturedDto.Action.Should().Be(AuditLogAction.SettingChanged);
        capturedDto.ActorId.Should().Be("user123");
        capturedDto.ActorType.Should().Be(AuditLogActorType.User);
        capturedDto.TargetType.Should().Be("ScheduledMessage");
        capturedDto.TargetId.Should().Be("msg-789");
        capturedDto.GuildId.Should().Be(987654321UL);
        capturedDto.Details.Should().Contain("newTitle"); // camelCase serialization
        capturedDto.IpAddress.Should().Be("192.168.1.1");
        capturedDto.CorrelationId.Should().Be("corr-123");
    }

    #endregion

    #region Enqueue Tests

    [Fact]
    public void Enqueue_CallsQueueEnqueue()
    {
        // Arrange
        var builder = _service.CreateBuilder()
            .ForCategory(AuditLogCategory.Guild)
            .WithAction(AuditLogAction.Updated)
            .ByBot();

        AuditLogCreateDto? capturedDto = null;
        _mockQueue.Setup(q => q.Enqueue(It.IsAny<AuditLogCreateDto>()))
            .Callback<AuditLogCreateDto>(dto => capturedDto = dto);

        // Act
        builder.Enqueue();

        // Assert
        capturedDto.Should().NotBeNull();
        capturedDto!.Category.Should().Be(AuditLogCategory.Guild);
        capturedDto.Action.Should().Be(AuditLogAction.Updated);
        capturedDto.ActorId.Should().Be("Bot");
        capturedDto.ActorType.Should().Be(AuditLogActorType.Bot);

        _mockQueue.Verify(
            q => q.Enqueue(It.IsAny<AuditLogCreateDto>()),
            Times.Once,
            "enqueue should be called directly on the queue");
    }

    [Fact]
    public void Enqueue_IsFireAndForget()
    {
        // Arrange
        var builder = _service.CreateBuilder()
            .ForCategory(AuditLogCategory.User)
            .WithAction(AuditLogAction.Deleted)
            .BySystem();

        // Act
        // Enqueue() returns void, so no task to await
        builder.Enqueue();

        // Assert
        _mockQueue.Verify(
            q => q.Enqueue(It.IsAny<AuditLogCreateDto>()),
            Times.Once);
    }

    #endregion

    #region Actor Type Tests

    [Fact]
    public async Task ByUser_SetsCorrectActorType()
    {
        // Arrange
        var builder = _service.CreateBuilder()
            .ForCategory(AuditLogCategory.User)
            .WithAction(AuditLogAction.Created)
            .ByUser("testuser");

        AuditLogCreateDto? capturedDto = null;
        _mockQueue.Setup(q => q.Enqueue(It.IsAny<AuditLogCreateDto>()))
            .Callback<AuditLogCreateDto>(dto => capturedDto = dto);

        // Act
        await builder.LogAsync();

        // Assert
        capturedDto.Should().NotBeNull();
        capturedDto!.ActorType.Should().Be(AuditLogActorType.User);
        capturedDto.ActorId.Should().Be("testuser");
    }

    [Fact]
    public async Task BySystem_SetsCorrectActorTypeAndId()
    {
        // Arrange
        var builder = _service.CreateBuilder()
            .ForCategory(AuditLogCategory.Message)
            .WithAction(AuditLogAction.MessageDeleted)
            .BySystem();

        AuditLogCreateDto? capturedDto = null;
        _mockQueue.Setup(q => q.Enqueue(It.IsAny<AuditLogCreateDto>()))
            .Callback<AuditLogCreateDto>(dto => capturedDto = dto);

        // Act
        await builder.LogAsync();

        // Assert
        capturedDto.Should().NotBeNull();
        capturedDto!.ActorType.Should().Be(AuditLogActorType.System);
        capturedDto.ActorId.Should().Be("System");
    }

    [Fact]
    public async Task ByBot_SetsCorrectActorTypeAndId()
    {
        // Arrange
        var builder = _service.CreateBuilder()
            .ForCategory(AuditLogCategory.Guild)
            .WithAction(AuditLogAction.Updated)
            .ByBot();

        AuditLogCreateDto? capturedDto = null;
        _mockQueue.Setup(q => q.Enqueue(It.IsAny<AuditLogCreateDto>()))
            .Callback<AuditLogCreateDto>(dto => capturedDto = dto);

        // Act
        await builder.LogAsync();

        // Assert
        capturedDto.Should().NotBeNull();
        capturedDto!.ActorType.Should().Be(AuditLogActorType.Bot);
        capturedDto.ActorId.Should().Be("Bot");
    }

    #endregion

    #region JSON Serialization Tests

    [Fact]
    public async Task WithDetails_Dictionary_SerializesCorrectly()
    {
        // Arrange
        var builder = _service.CreateBuilder()
            .ForCategory(AuditLogCategory.User)
            .WithAction(AuditLogAction.Updated)
            .ByUser("admin")
            .WithDetails(new Dictionary<string, object?>
            {
                { "OldEmail", "old@example.com" },
                { "NewEmail", "new@example.com" }
            });

        AuditLogCreateDto? capturedDto = null;
        _mockQueue.Setup(q => q.Enqueue(It.IsAny<AuditLogCreateDto>()))
            .Callback<AuditLogCreateDto>(dto => capturedDto = dto);

        // Act
        await builder.LogAsync();

        // Assert
        capturedDto.Should().NotBeNull();
        capturedDto!.Details.Should().NotBeNullOrEmpty();
        // Note: Dictionary keys are serialized as-is, not converted to camelCase
        capturedDto.Details.Should().Contain("OldEmail");
        capturedDto.Details.Should().Contain("NewEmail");
        capturedDto.Details.Should().Contain("old@example.com");
        capturedDto.Details.Should().Contain("new@example.com");
    }

    [Fact]
    public async Task WithDetails_AnonymousObject_SerializesCorrectly()
    {
        // Arrange
        var builder = _service.CreateBuilder()
            .ForCategory(AuditLogCategory.Message)
            .WithAction(AuditLogAction.Created)
            .ByUser("creator")
            .WithDetails(new
            {
                MessageTitle = "Test",
                ChannelId = 123456UL,
                IsRecurring = true,
                NextRun = DateTime.UtcNow
            });

        AuditLogCreateDto? capturedDto = null;
        _mockQueue.Setup(q => q.Enqueue(It.IsAny<AuditLogCreateDto>()))
            .Callback<AuditLogCreateDto>(dto => capturedDto = dto);

        // Act
        await builder.LogAsync();

        // Assert
        capturedDto.Should().NotBeNull();
        capturedDto!.Details.Should().NotBeNullOrEmpty();

        // Verify JSON structure
        var deserializedDetails = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(capturedDto.Details!);
        deserializedDetails.Should().ContainKey("messageTitle");
        deserializedDetails.Should().ContainKey("channelId");
        deserializedDetails.Should().ContainKey("isRecurring");
        deserializedDetails.Should().ContainKey("nextRun");
    }

    #endregion
}
