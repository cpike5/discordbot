using System.Diagnostics;
using DiscordBot.Bot.Tracing;
using FluentAssertions;
using OpenTelemetry.Trace;

namespace DiscordBot.Tests.Tracing;

/// <summary>
/// Unit tests for <see cref="BotActivitySource"/>.
/// Tests distributed tracing functionality for Discord bot operations.
/// </summary>
public class BotActivitySourceTests : IDisposable
{
    private readonly ActivityListener _listener;
    private Activity? _capturedActivity;

    public BotActivitySourceTests()
    {
        // Set up ActivityListener to capture activities created by BotActivitySource
        // ActivitySource only creates activities when there's a listener
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == BotActivitySource.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => _capturedActivity = activity
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose()
    {
        _listener?.Dispose();
        _capturedActivity?.Dispose();
    }

    [Fact]
    public void SourceName_ShouldBeCorrect()
    {
        // Arrange & Act
        var sourceName = BotActivitySource.SourceName;

        // Assert
        sourceName.Should().Be("DiscordBot.Bot", "source name should match the bot namespace");
    }

    [Fact]
    public void Version_ShouldNotBeNull()
    {
        // Arrange & Act
        var version = BotActivitySource.Version;

        // Assert
        version.Should().NotBeNullOrEmpty("version should be extracted from assembly or default to 1.0.0");
    }

    [Fact]
    public void Source_ShouldBeInitialized()
    {
        // Arrange & Act
        var source = BotActivitySource.Source;

        // Assert
        source.Should().NotBeNull("ActivitySource should be initialized");
        source.Name.Should().Be(BotActivitySource.SourceName);
        source.Version.Should().Be(BotActivitySource.Version);
    }

    [Fact]
    public void StartCommandActivity_ReturnsActivity_WithCorrectName()
    {
        // Arrange
        const string commandName = "ping";
        const ulong guildId = 123456789012345678UL;
        const ulong userId = 987654321098765432UL;
        const ulong interactionId = 111222333444555666UL;
        const string correlationId = "test-correlation-id";

        // Act
        using var activity = BotActivitySource.StartCommandActivity(
            commandName,
            guildId,
            userId,
            interactionId,
            correlationId);

        // Assert
        activity.Should().NotBeNull("activity should be created when listener is active");
        activity!.DisplayName.Should().Be("discord.command ping", "activity name should include command name");
    }

    [Fact]
    public void StartCommandActivity_ReturnsActivity_WithCorrectTags()
    {
        // Arrange
        const string commandName = "shutdown";
        const ulong guildId = 123456789012345678UL;
        const ulong userId = 987654321098765432UL;
        const ulong interactionId = 111222333444555666UL;
        const string correlationId = "test-correlation-id";

        // Act
        using var activity = BotActivitySource.StartCommandActivity(
            commandName,
            guildId,
            userId,
            interactionId,
            correlationId);

        // Assert
        activity.Should().NotBeNull();
        activity!.Tags.Should().Contain(tag =>
            tag.Key == TracingConstants.Attributes.CommandName && tag.Value == commandName);
        activity.Tags.Should().Contain(tag =>
            tag.Key == TracingConstants.Attributes.GuildId && tag.Value == guildId.ToString());
        activity.Tags.Should().Contain(tag =>
            tag.Key == TracingConstants.Attributes.UserId && tag.Value == userId.ToString());
        activity.Tags.Should().Contain(tag =>
            tag.Key == TracingConstants.Attributes.InteractionId && tag.Value == interactionId.ToString());
        activity.Tags.Should().Contain(tag =>
            tag.Key == TracingConstants.Attributes.CorrelationId && tag.Value == correlationId);
    }

    [Fact]
    public void StartCommandActivity_WithNullGuildId_TagsAsDM()
    {
        // Arrange
        const string commandName = "ping";
        const ulong userId = 987654321098765432UL;
        const ulong interactionId = 111222333444555666UL;
        const string correlationId = "test-correlation-id";

        // Act
        using var activity = BotActivitySource.StartCommandActivity(
            commandName,
            null, // DM command, no guild
            userId,
            interactionId,
            correlationId);

        // Assert
        activity.Should().NotBeNull();
        activity!.Tags.Should().Contain(tag =>
            tag.Key == TracingConstants.Attributes.GuildId && tag.Value == "dm",
            "null guild ID should be tagged as 'dm' for direct messages");
    }

    [Fact]
    public void StartCommandActivity_UsesCorrectSpanKind()
    {
        // Arrange
        const string commandName = "ping";
        const ulong guildId = 123456789012345678UL;
        const ulong userId = 987654321098765432UL;
        const ulong interactionId = 111222333444555666UL;
        const string correlationId = "test-correlation-id";

        // Act
        using var activity = BotActivitySource.StartCommandActivity(
            commandName,
            guildId,
            userId,
            interactionId,
            correlationId);

        // Assert
        activity.Should().NotBeNull();
        activity!.Kind.Should().Be(ActivityKind.Server,
            "command execution is a server operation receiving external requests");
    }

    [Fact]
    public void StartCommandActivity_AddsCorrelationIdToBaggage()
    {
        // Arrange
        const string commandName = "ping";
        const ulong guildId = 123456789012345678UL;
        const ulong userId = 987654321098765432UL;
        const ulong interactionId = 111222333444555666UL;
        const string correlationId = "test-correlation-id";

        // Act
        using var activity = BotActivitySource.StartCommandActivity(
            commandName,
            guildId,
            userId,
            interactionId,
            correlationId);

        // Assert
        activity.Should().NotBeNull();
        activity!.Baggage.Should().Contain(baggage =>
            baggage.Key == TracingConstants.Baggage.CorrelationId && baggage.Value == correlationId,
            "correlation ID should be added as baggage for downstream propagation");
    }

    [Fact]
    public void StartComponentActivity_ReturnsActivity_WithCorrectName()
    {
        // Arrange
        const string componentType = "button";
        const string customId = "shutdown:confirm:123456789:abc123de";
        const ulong guildId = 123456789012345678UL;
        const ulong userId = 987654321098765432UL;
        const ulong interactionId = 111222333444555666UL;
        const string correlationId = "test-correlation-id";

        // Act
        using var activity = BotActivitySource.StartComponentActivity(
            componentType,
            customId,
            guildId,
            userId,
            interactionId,
            correlationId);

        // Assert
        activity.Should().NotBeNull("activity should be created when listener is active");
        activity!.DisplayName.Should().Be("discord.component button", "activity name should include component type");
    }

    [Fact]
    public void StartComponentActivity_ReturnsActivity_WithCorrectTags()
    {
        // Arrange
        const string componentType = "select_menu";
        const string customId = "guilds:filter:123456789:abc123de:active";
        const ulong guildId = 123456789012345678UL;
        const ulong userId = 987654321098765432UL;
        const ulong interactionId = 111222333444555666UL;
        const string correlationId = "test-correlation-id";

        // Act
        using var activity = BotActivitySource.StartComponentActivity(
            componentType,
            customId,
            guildId,
            userId,
            interactionId,
            correlationId);

        // Assert
        activity.Should().NotBeNull();
        activity!.Tags.Should().Contain(tag =>
            tag.Key == TracingConstants.Attributes.ComponentType && tag.Value == componentType);
        activity.Tags.Should().Contain(tag =>
            tag.Key == TracingConstants.Attributes.ComponentId && tag.Value == "guilds:filter",
            "custom ID should be sanitized to only handler:action");
        activity.Tags.Should().Contain(tag =>
            tag.Key == TracingConstants.Attributes.GuildId && tag.Value == guildId.ToString());
        activity.Tags.Should().Contain(tag =>
            tag.Key == TracingConstants.Attributes.UserId && tag.Value == userId.ToString());
        activity.Tags.Should().Contain(tag =>
            tag.Key == TracingConstants.Attributes.InteractionId && tag.Value == interactionId.ToString());
        activity.Tags.Should().Contain(tag =>
            tag.Key == TracingConstants.Attributes.CorrelationId && tag.Value == correlationId);
    }

    [Fact]
    public void StartComponentActivity_WithNullGuildId_TagsAsDM()
    {
        // Arrange
        const string componentType = "button";
        const string customId = "test:action:123456789:abc123de";
        const ulong userId = 987654321098765432UL;
        const ulong interactionId = 111222333444555666UL;
        const string correlationId = "test-correlation-id";

        // Act
        using var activity = BotActivitySource.StartComponentActivity(
            componentType,
            customId,
            null, // DM interaction, no guild
            userId,
            interactionId,
            correlationId);

        // Assert
        activity.Should().NotBeNull();
        activity!.Tags.Should().Contain(tag =>
            tag.Key == TracingConstants.Attributes.GuildId && tag.Value == "dm",
            "null guild ID should be tagged as 'dm' for direct messages");
    }

    [Fact]
    public void StartComponentActivity_UsesCorrectSpanKind()
    {
        // Arrange
        const string componentType = "modal";
        const string customId = "feedback:submit:123456789:abc123de";
        const ulong guildId = 123456789012345678UL;
        const ulong userId = 987654321098765432UL;
        const ulong interactionId = 111222333444555666UL;
        const string correlationId = "test-correlation-id";

        // Act
        using var activity = BotActivitySource.StartComponentActivity(
            componentType,
            customId,
            guildId,
            userId,
            interactionId,
            correlationId);

        // Assert
        activity.Should().NotBeNull();
        activity!.Kind.Should().Be(ActivityKind.Server,
            "component interaction is a server operation receiving external requests");
    }

    [Fact]
    public void StartComponentActivity_AddsCorrelationIdToBaggage()
    {
        // Arrange
        const string componentType = "button";
        const string customId = "test:action:123456789:abc123de";
        const ulong guildId = 123456789012345678UL;
        const ulong userId = 987654321098765432UL;
        const ulong interactionId = 111222333444555666UL;
        const string correlationId = "test-correlation-id";

        // Act
        using var activity = BotActivitySource.StartComponentActivity(
            componentType,
            customId,
            guildId,
            userId,
            interactionId,
            correlationId);

        // Assert
        activity.Should().NotBeNull();
        activity!.Baggage.Should().Contain(baggage =>
            baggage.Key == TracingConstants.Baggage.CorrelationId && baggage.Value == correlationId,
            "correlation ID should be added as baggage for downstream propagation");
    }

    [Theory]
    [InlineData("simple")]
    [InlineData("handler:action")]
    [InlineData("handler:action:user:corr")]
    [InlineData("handler:action:user:corr:data")]
    [InlineData("handler:action:user:corr:data:extra:parts")]
    public void StartComponentActivity_SanitizesCustomId(string customId)
    {
        // Arrange
        const string componentType = "button";
        const ulong guildId = 123456789012345678UL;
        const ulong userId = 987654321098765432UL;
        const ulong interactionId = 111222333444555666UL;
        const string correlationId = "test-correlation-id";

        var parts = customId.Split(':');
        var expectedSanitized = parts.Length >= 2 ? $"{parts[0]}:{parts[1]}" : customId;

        // Act
        using var activity = BotActivitySource.StartComponentActivity(
            componentType,
            customId,
            guildId,
            userId,
            interactionId,
            correlationId);

        // Assert
        activity.Should().NotBeNull();
        activity!.Tags.Should().Contain(tag =>
            tag.Key == TracingConstants.Attributes.ComponentId && tag.Value == expectedSanitized,
            $"custom ID '{customId}' should be sanitized to '{expectedSanitized}'");
    }

    [Fact]
    public void RecordException_AddsExceptionToActivity()
    {
        // Arrange
        const string commandName = "ping";
        const ulong guildId = 123456789012345678UL;
        const ulong userId = 987654321098765432UL;
        const ulong interactionId = 111222333444555666UL;
        const string correlationId = "test-correlation-id";

        using var activity = BotActivitySource.StartCommandActivity(
            commandName,
            guildId,
            userId,
            interactionId,
            correlationId);

        var exception = new InvalidOperationException("Test exception message");

        // Act
        BotActivitySource.RecordException(activity, exception);

        // Assert
        activity.Should().NotBeNull();
        activity!.Status.Should().Be(ActivityStatusCode.Error, "activity status should be Error");
        activity.StatusDescription.Should().Be("Test exception message", "status description should match exception message");

        // Verify exception event was added
        var exceptionEvent = activity.Events.FirstOrDefault(e => e.Name == "exception");
        exceptionEvent.Should().NotBeNull("activity should have an exception event");
        exceptionEvent.Tags.Should().Contain(tag => tag.Key == "exception.type" && tag.Value != null && tag.Value.ToString() == "System.InvalidOperationException");
        exceptionEvent.Tags.Should().Contain(tag => tag.Key == "exception.message" && tag.Value != null && tag.Value.ToString() == "Test exception message");
    }

    [Fact]
    public void RecordException_WithNullActivity_DoesNotThrow()
    {
        // Arrange
        Activity? nullActivity = null;
        var exception = new InvalidOperationException("Test exception");

        // Act
        var act = () => BotActivitySource.RecordException(nullActivity, exception);

        // Assert
        act.Should().NotThrow("RecordException should handle null activity gracefully");
    }

    [Fact]
    public void SetSuccess_SetsOkStatus()
    {
        // Arrange
        const string commandName = "ping";
        const ulong guildId = 123456789012345678UL;
        const ulong userId = 987654321098765432UL;
        const ulong interactionId = 111222333444555666UL;
        const string correlationId = "test-correlation-id";

        using var activity = BotActivitySource.StartCommandActivity(
            commandName,
            guildId,
            userId,
            interactionId,
            correlationId);

        // Act
        BotActivitySource.SetSuccess(activity);

        // Assert
        activity.Should().NotBeNull();
        activity!.Status.Should().Be(ActivityStatusCode.Ok, "activity status should be Ok after SetSuccess");
    }

    [Fact]
    public void SetSuccess_WithNullActivity_DoesNotThrow()
    {
        // Arrange
        Activity? nullActivity = null;

        // Act
        var act = () => BotActivitySource.SetSuccess(nullActivity);

        // Assert
        act.Should().NotThrow("SetSuccess should handle null activity gracefully");
    }

    [Fact(Skip = "Test is environment-dependent; global activity listeners (e.g., Elastic APM) may be active")]
    public void StartCommandActivity_WithoutListener_ReturnsNull()
    {
        // Arrange
        // Dispose the listener to stop sampling
        _listener.Dispose();

        const string commandName = "ping";
        const ulong guildId = 123456789012345678UL;
        const ulong userId = 987654321098765432UL;
        const ulong interactionId = 111222333444555666UL;
        const string correlationId = "test-correlation-id";

        // Act
        var activity = BotActivitySource.StartCommandActivity(
            commandName,
            guildId,
            userId,
            interactionId,
            correlationId);

        // Assert
        activity.Should().BeNull("activity should be null when no listener is active");
    }

    [Fact(Skip = "Test is environment-dependent; global activity listeners (e.g., Elastic APM) may be active")]
    public void StartComponentActivity_WithoutListener_ReturnsNull()
    {
        // Arrange
        // Dispose the listener to stop sampling
        _listener.Dispose();

        const string componentType = "button";
        const string customId = "test:action:123456789:abc123de";
        const ulong guildId = 123456789012345678UL;
        const ulong userId = 987654321098765432UL;
        const ulong interactionId = 111222333444555666UL;
        const string correlationId = "test-correlation-id";

        // Act
        var activity = BotActivitySource.StartComponentActivity(
            componentType,
            customId,
            guildId,
            userId,
            interactionId,
            correlationId);

        // Assert
        activity.Should().BeNull("activity should be null when no listener is active");
    }
}
