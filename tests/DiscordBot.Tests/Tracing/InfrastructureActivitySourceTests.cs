using System.Diagnostics;
using DiscordBot.Bot.Tracing;
using DiscordBot.Infrastructure.Tracing;
using FluentAssertions;
using OpenTelemetry.Trace;

namespace DiscordBot.Tests.Tracing;

/// <summary>
/// Unit tests for <see cref="InfrastructureActivitySource"/>.
/// Tests distributed tracing functionality for infrastructure-level operations (repositories, database operations).
/// </summary>
public class InfrastructureActivitySourceTests : IDisposable
{
    private readonly ActivityListener _listener;
    private Activity? _capturedActivity;

    public InfrastructureActivitySourceTests()
    {
        // Set up ActivityListener to capture activities created by InfrastructureActivitySource
        // ActivitySource only creates activities when there's a listener
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == InfrastructureActivitySource.SourceName,
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
        var sourceName = InfrastructureActivitySource.SourceName;

        // Assert
        sourceName.Should().Be("DiscordBot.Infrastructure", "source name should match the infrastructure namespace");
    }

    [Fact]
    public void Version_ShouldNotBeNull()
    {
        // Arrange & Act
        var version = InfrastructureActivitySource.Version;

        // Assert
        version.Should().NotBeNullOrEmpty("version should be extracted from assembly or default to 1.0.0");
    }

    [Fact]
    public void Source_ShouldBeInitialized()
    {
        // Arrange & Act
        var source = InfrastructureActivitySource.Source;

        // Assert
        source.Should().NotBeNull("ActivitySource should be initialized");
        source.Name.Should().Be(InfrastructureActivitySource.SourceName);
        source.Version.Should().Be(InfrastructureActivitySource.Version);
    }

    [Fact]
    public void Attributes_ShouldHaveCorrectKeys()
    {
        // Arrange & Act & Assert
        InfrastructureActivitySource.Attributes.DbSystem.Should().Be("db.system");
        InfrastructureActivitySource.Attributes.DbOperation.Should().Be("db.operation");
        InfrastructureActivitySource.Attributes.DbEntityType.Should().Be("db.entity.type");
        InfrastructureActivitySource.Attributes.DbEntityId.Should().Be("db.entity.id");
        InfrastructureActivitySource.Attributes.DbDurationMs.Should().Be("db.duration.ms");
    }

    [Fact]
    public void StartRepositoryActivity_ReturnsActivity_WithCorrectName()
    {
        // Arrange
        const string operationName = "GetByIdAsync";
        const string entityType = "Guild";
        const string dbOperation = "SELECT";
        const string entityId = "123456789012345678";

        // Act
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName,
            entityType,
            dbOperation,
            entityId);

        // Assert
        activity.Should().NotBeNull("activity should be created when listener is active");
        activity!.DisplayName.Should().Be("db.select Guild", "activity name should include operation and entity type");
    }

    [Fact]
    public void StartRepositoryActivity_ReturnsActivity_WithCorrectTags()
    {
        // Arrange
        const string operationName = "GetByIdAsync";
        const string entityType = "Guild";
        const string dbOperation = "SELECT";
        const string entityId = "123456789012345678";

        // Act
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName,
            entityType,
            dbOperation,
            entityId);

        // Assert
        activity.Should().NotBeNull();
        activity!.Tags.Should().Contain(tag =>
            tag.Key == InfrastructureActivitySource.Attributes.DbOperation && tag.Value == dbOperation);
        activity.Tags.Should().Contain(tag =>
            tag.Key == InfrastructureActivitySource.Attributes.DbEntityType && tag.Value == entityType);
        activity.Tags.Should().Contain(tag =>
            tag.Key == InfrastructureActivitySource.Attributes.DbEntityId && tag.Value == entityId);
    }

    [Fact]
    public void StartRepositoryActivity_WithoutEntityId_OmitsEntityIdTag()
    {
        // Arrange
        const string operationName = "GetAllAsync";
        const string entityType = "Guild";
        const string dbOperation = "SELECT";

        // Act
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName,
            entityType,
            dbOperation,
            entityId: null);

        // Assert
        activity.Should().NotBeNull();
        activity!.Tags.Should().NotContain(tag =>
            tag.Key == InfrastructureActivitySource.Attributes.DbEntityId,
            "entity ID tag should be omitted when entityId is null");
    }

    [Fact]
    public void StartRepositoryActivity_WithEmptyEntityId_OmitsEntityIdTag()
    {
        // Arrange
        const string operationName = "CountAsync";
        const string entityType = "CommandLog";
        const string dbOperation = "COUNT";

        // Act
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName,
            entityType,
            dbOperation,
            entityId: string.Empty);

        // Assert
        activity.Should().NotBeNull();
        activity!.Tags.Should().NotContain(tag =>
            tag.Key == InfrastructureActivitySource.Attributes.DbEntityId,
            "entity ID tag should be omitted when entityId is empty");
    }

    [Fact]
    public void StartRepositoryActivity_UsesCorrectSpanKind()
    {
        // Arrange
        const string operationName = "AddAsync";
        const string entityType = "Guild";
        const string dbOperation = "INSERT";

        // Act
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName,
            entityType,
            dbOperation);

        // Assert
        activity.Should().NotBeNull();
        activity!.Kind.Should().Be(ActivityKind.Client,
            "repository operations are client operations calling external systems (database)");
    }

    [Theory]
    [InlineData("SELECT", "db.select")]
    [InlineData("INSERT", "db.insert")]
    [InlineData("UPDATE", "db.update")]
    [InlineData("DELETE", "db.delete")]
    [InlineData("COUNT", "db.count")]
    [InlineData("EXISTS", "db.exists")]
    public void StartRepositoryActivity_NormalizesOperationNameToLowercase(string dbOperation, string expectedPrefix)
    {
        // Arrange
        const string operationName = "TestOperation";
        const string entityType = "TestEntity";

        // Act
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName,
            entityType,
            dbOperation);

        // Assert
        activity.Should().NotBeNull();
        activity!.DisplayName.Should().StartWith(expectedPrefix,
            $"operation '{dbOperation}' should be normalized to lowercase in activity name");
    }

    [Fact]
    public void StartRepositoryActivity_InheritsCorrelationIdFromParentBaggage()
    {
        // Arrange
        const string correlationId = "parent-correlation-id";
        const string operationName = "GetByIdAsync";
        const string entityType = "Guild";
        const string dbOperation = "SELECT";

        // Create a parent activity with correlation ID in baggage
        using var parentActivity = new Activity("parent-operation");
        parentActivity.AddBaggage(TracingConstants.Baggage.CorrelationId, correlationId);
        parentActivity.Start();

        // Act
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName,
            entityType,
            dbOperation);

        // Assert
        activity.Should().NotBeNull();
        activity!.Tags.Should().Contain(tag =>
            tag.Key == "correlation.id" && tag.Value == correlationId,
            "correlation ID should be inherited from parent activity baggage");
    }

    [Fact]
    public void StartRepositoryActivity_WithoutParentBaggage_DoesNotAddCorrelationIdTag()
    {
        // Arrange
        const string operationName = "GetByIdAsync";
        const string entityType = "Guild";
        const string dbOperation = "SELECT";

        // Act
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName,
            entityType,
            dbOperation);

        // Assert
        activity.Should().NotBeNull();
        activity!.Tags.Should().NotContain(tag =>
            tag.Key == "correlation.id",
            "correlation ID tag should not be added when no parent baggage exists");
    }

    [Fact]
    public void CompleteActivity_SetsDurationAndOkStatus()
    {
        // Arrange
        const string operationName = "GetByIdAsync";
        const string entityType = "Guild";
        const string dbOperation = "SELECT";
        const double durationMs = 42.5;

        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName,
            entityType,
            dbOperation);

        // Act
        InfrastructureActivitySource.CompleteActivity(activity, durationMs);

        // Assert
        activity.Should().NotBeNull();
        activity!.TagObjects.Should().Contain(tag =>
            tag.Key == InfrastructureActivitySource.Attributes.DbDurationMs && tag.Value != null && tag.Value.ToString() == "42.5",
            "duration should be set as a tag");
        activity.Status.Should().Be(ActivityStatusCode.Ok, "activity status should be Ok after completion");
    }

    [Fact]
    public void CompleteActivity_WithNullActivity_DoesNotThrow()
    {
        // Arrange
        Activity? nullActivity = null;
        const double durationMs = 42.5;

        // Act
        var act = () => InfrastructureActivitySource.CompleteActivity(nullActivity, durationMs);

        // Assert
        act.Should().NotThrow("CompleteActivity should handle null activity gracefully");
    }

    [Fact]
    public void RecordException_SetsDurationErrorStatusAndException()
    {
        // Arrange
        const string operationName = "UpdateAsync";
        const string entityType = "Guild";
        const string dbOperation = "UPDATE";
        const double durationMs = 123.45;

        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName,
            entityType,
            dbOperation);

        var exception = new InvalidOperationException("Database connection failed");

        // Act
        InfrastructureActivitySource.RecordException(activity, exception, durationMs);

        // Assert
        activity.Should().NotBeNull();
        activity!.TagObjects.Should().Contain(tag =>
            tag.Key == InfrastructureActivitySource.Attributes.DbDurationMs && tag.Value != null && tag.Value.ToString() == "123.45",
            "duration should be set as a tag");
        activity.Status.Should().Be(ActivityStatusCode.Error, "activity status should be Error");
        activity.StatusDescription.Should().Be("Database connection failed", "status description should match exception message");

        // Verify exception event was added
        var exceptionEvent = activity.Events.FirstOrDefault(e => e.Name == "exception");
        exceptionEvent.Should().NotBeNull("activity should have an exception event");
        exceptionEvent.Tags.Should().Contain(tag => tag.Key == "exception.type" && tag.Value != null && tag.Value.ToString() == "System.InvalidOperationException");
        exceptionEvent.Tags.Should().Contain(tag => tag.Key == "exception.message" && tag.Value != null && tag.Value.ToString() == "Database connection failed");
    }

    [Fact]
    public void RecordException_WithNullActivity_DoesNotThrow()
    {
        // Arrange
        Activity? nullActivity = null;
        var exception = new InvalidOperationException("Test exception");
        const double durationMs = 100.0;

        // Act
        var act = () => InfrastructureActivitySource.RecordException(nullActivity, exception, durationMs);

        // Assert
        act.Should().NotThrow("RecordException should handle null activity gracefully");
    }

    [Fact]
    public void StartRepositoryActivity_WithoutListener_ReturnsNull()
    {
        // Arrange
        // Dispose the listener to stop sampling
        _listener.Dispose();

        const string operationName = "GetByIdAsync";
        const string entityType = "Guild";
        const string dbOperation = "SELECT";

        // Act
        var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName,
            entityType,
            dbOperation);

        // Assert
        activity.Should().BeNull("activity should be null when no listener is active");
    }

    [Fact]
    public void StartRepositoryActivity_IntegrationScenario_WithCommandAsParent()
    {
        // Arrange
        // Set up a listener for the Bot activity source as well
        using var botListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == BotActivitySource.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(botListener);

        const string commandName = "guilds";
        const ulong guildId = 123456789012345678UL;
        const ulong userId = 987654321098765432UL;
        const ulong interactionId = 111222333444555666UL;
        const string correlationId = "integration-test-correlation-id";

        // Act
        // Simulate a command execution that calls a repository
        using var commandActivity = BotActivitySource.StartCommandActivity(
            commandName,
            guildId,
            userId,
            interactionId,
            correlationId);

        using var repositoryActivity = InfrastructureActivitySource.StartRepositoryActivity(
            "GetAllAsync",
            "Guild",
            "SELECT");

        // Assert
        commandActivity.Should().NotBeNull();
        repositoryActivity.Should().NotBeNull();

        // Repository activity should be a child of the command activity
        repositoryActivity!.ParentId.Should().Be(commandActivity!.Id,
            "repository activity should be a child of the command activity");

        // Correlation ID should flow from command to repository via baggage
        repositoryActivity.Tags.Should().Contain(tag =>
            tag.Key == "correlation.id" && tag.Value == correlationId,
            "correlation ID should propagate from parent command activity");
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(1.5)]
    [InlineData(10.0)]
    [InlineData(100.5)]
    [InlineData(1000.123)]
    public void CompleteActivity_WithVariousDurations_RecordsDurationCorrectly(double durationMs)
    {
        // Arrange
        const string operationName = "TestOperation";
        const string entityType = "TestEntity";
        const string dbOperation = "SELECT";

        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName,
            entityType,
            dbOperation);

        // Act
        InfrastructureActivitySource.CompleteActivity(activity, durationMs);

        // Assert
        activity.Should().NotBeNull();
        activity!.TagObjects.Should().Contain(tag =>
            tag.Key == InfrastructureActivitySource.Attributes.DbDurationMs &&
            tag.Value != null && tag.Value.ToString() == durationMs.ToString(System.Globalization.CultureInfo.InvariantCulture),
            $"duration {durationMs}ms should be recorded correctly");
    }
}
