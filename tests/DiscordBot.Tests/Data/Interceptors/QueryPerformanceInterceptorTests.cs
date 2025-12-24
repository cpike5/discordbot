using System.Collections;
using System.Data;
using System.Data.Common;
using System.Reflection;
using DiscordBot.Infrastructure.Configuration;
using DiscordBot.Infrastructure.Data.Interceptors;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;

namespace DiscordBot.Tests.Data.Interceptors;

/// <summary>
/// Unit tests for <see cref="QueryPerformanceInterceptor"/>.
/// </summary>
public class QueryPerformanceInterceptorTests
{
    private readonly Mock<ILogger<QueryPerformanceInterceptor>> _mockLogger;
    private readonly Mock<IOptions<DatabaseSettings>> _mockSettings;
    private readonly DatabaseSettings _settings;

    public QueryPerformanceInterceptorTests()
    {
        _mockLogger = new Mock<ILogger<QueryPerformanceInterceptor>>();
        _settings = new DatabaseSettings
        {
            SlowQueryThresholdMs = 100,
            LogQueryParameters = true
        };
        _mockSettings = new Mock<IOptions<DatabaseSettings>>();
        _mockSettings.Setup(s => s.Value).Returns(_settings);
    }

    #region Normal Query Logging Tests

    [Fact]
    public async Task ReaderExecutedAsync_WithFastQuery_LogsAtDebugLevel()
    {
        // Arrange
        _mockLogger.Setup(l => l.IsEnabled(LogLevel.Debug)).Returns(true);
        var interceptor = new QueryPerformanceInterceptor(_mockLogger.Object, _mockSettings.Object);
        var command = CreateMockCommand("SELECT * FROM Users WHERE Id = @p0", new (string, object?)[] { ("@p0", 123) });
        var eventData = CreateCommandExecutedEventData(command, elapsedMs: 50);
        var reader = Mock.Of<DbDataReader>();

        // Act
        await interceptor.ReaderExecutedAsync(command, eventData, reader);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("EF Query executed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "fast queries should be logged at Debug level");
    }

    [Fact]
    public void ReaderExecuted_WithFastQuery_LogsExecutionTime()
    {
        // Arrange
        _mockLogger.Setup(l => l.IsEnabled(LogLevel.Debug)).Returns(true);
        var interceptor = new QueryPerformanceInterceptor(_mockLogger.Object, _mockSettings.Object);
        var command = CreateMockCommand("SELECT * FROM Users", Array.Empty<(string, object?)>());
        var eventData = CreateCommandExecutedEventData(command, elapsedMs: 25);
        var reader = Mock.Of<DbDataReader>();

        // Act
        interceptor.ReaderExecuted(command, eventData, reader);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("25.00") && v.ToString()!.Contains("ElapsedMs")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "debug log should include execution time");
    }

    [Fact]
    public async Task NonQueryExecutedAsync_WithFastQuery_LogsCommandType()
    {
        // Arrange
        _mockLogger.Setup(l => l.IsEnabled(LogLevel.Debug)).Returns(true);
        var interceptor = new QueryPerformanceInterceptor(_mockLogger.Object, _mockSettings.Object);
        var command = CreateMockCommand("INSERT INTO Users (Name) VALUES (@p0)", new (string, object?)[] { ("@p0", "TestUser") });
        command.CommandType = CommandType.Text;
        var eventData = CreateCommandExecutedEventData(command, elapsedMs: 30);

        // Act
        await interceptor.NonQueryExecutedAsync(command, eventData, 1);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("CommandType=Text")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "debug log should include command type");
    }

    [Fact]
    public async Task ScalarExecutedAsync_WithFastQuery_LogsSqlCommand()
    {
        // Arrange
        _mockLogger.Setup(l => l.IsEnabled(LogLevel.Debug)).Returns(true);
        var interceptor = new QueryPerformanceInterceptor(_mockLogger.Object, _mockSettings.Object);
        var sql = "SELECT COUNT(*) FROM Users";
        var command = CreateMockCommand(sql, Array.Empty<(string, object?)>());
        var eventData = CreateCommandExecutedEventData(command, elapsedMs: 15);

        // Act
        await interceptor.ScalarExecutedAsync(command, eventData, 42);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(sql)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "debug log should include SQL command text");
    }

    #endregion

    #region Slow Query Detection Tests

    [Fact]
    public async Task ReaderExecutedAsync_WithSlowQuery_LogsAtWarningLevel()
    {
        // Arrange
        var interceptor = new QueryPerformanceInterceptor(_mockLogger.Object, _mockSettings.Object);
        var command = CreateMockCommand("SELECT * FROM Users", Array.Empty<(string, object?)>());
        var eventData = CreateCommandExecutedEventData(command, elapsedMs: 150);
        var reader = Mock.Of<DbDataReader>();

        // Act
        await interceptor.ReaderExecutedAsync(command, eventData, reader);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Slow query detected")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "slow queries should be logged at Warning level");
    }

    [Fact]
    public void NonQueryExecuted_WithSlowQuery_IncludesThresholdValue()
    {
        // Arrange
        var interceptor = new QueryPerformanceInterceptor(_mockLogger.Object, _mockSettings.Object);
        var command = CreateMockCommand("UPDATE Users SET Name = @p0", new (string, object?)[] { ("@p0", "NewName") });
        var eventData = CreateCommandExecutedEventData(command, elapsedMs: 200);

        // Act
        interceptor.NonQueryExecuted(command, eventData, 1);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Threshold=100ms")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "warning should include threshold value");
    }

    [Fact]
    public async Task ScalarExecutedAsync_WithSlowQuery_IncludesElapsedTime()
    {
        // Arrange
        var interceptor = new QueryPerformanceInterceptor(_mockLogger.Object, _mockSettings.Object);
        var command = CreateMockCommand("SELECT MAX(Id) FROM Users", Array.Empty<(string, object?)>());
        var eventData = CreateCommandExecutedEventData(command, elapsedMs: 250.75);

        // Act
        await interceptor.ScalarExecutedAsync(command, eventData, 999);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("ElapsedMs=250.75")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "warning should include precise elapsed time");
    }

    [Fact]
    public void ReaderExecuted_WithSlowQuery_LogsBothDebugAndWarning()
    {
        // Arrange
        var interceptor = new QueryPerformanceInterceptor(_mockLogger.Object, _mockSettings.Object);
        var command = CreateMockCommand("SELECT * FROM LargeTable", Array.Empty<(string, object?)>());
        var eventData = CreateCommandExecutedEventData(command, elapsedMs: 500);
        var reader = Mock.Of<DbDataReader>();

        // Enable Debug logging
        _mockLogger.Setup(l => l.IsEnabled(LogLevel.Debug)).Returns(true);

        // Act
        interceptor.ReaderExecuted(command, eventData, reader);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log at Debug level");

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should also log at Warning level for slow queries");
    }

    [Fact]
    public async Task ReaderExecutedAsync_WithCustomThreshold_RespectsConfiguration()
    {
        // Arrange
        _settings.SlowQueryThresholdMs = 500;
        var interceptor = new QueryPerformanceInterceptor(_mockLogger.Object, _mockSettings.Object);
        var command = CreateMockCommand("SELECT * FROM Users", Array.Empty<(string, object?)>());
        var eventData = CreateCommandExecutedEventData(command, elapsedMs: 300);
        var reader = Mock.Of<DbDataReader>();

        // Act
        await interceptor.ReaderExecutedAsync(command, eventData, reader);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never,
            "should not log warning when under custom threshold");
    }

    #endregion

    #region Failed Query Logging Tests

    [Fact]
    public async Task CommandFailedAsync_WithException_LogsAtErrorLevel()
    {
        // Arrange
        var interceptor = new QueryPerformanceInterceptor(_mockLogger.Object, _mockSettings.Object);
        var command = CreateMockCommand("SELECT * FROM NonExistentTable", Array.Empty<(string, object?)>());
        var exception = new InvalidOperationException("Table does not exist");
        var eventData = CreateCommandErrorEventData(command, exception, elapsedMs: 10);

        // Act
        await interceptor.CommandFailedAsync(command, eventData);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("EF Query failed")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "failed queries should be logged at Error level");
    }

    [Fact]
    public void CommandFailed_WithException_IncludesExceptionMessage()
    {
        // Arrange
        var interceptor = new QueryPerformanceInterceptor(_mockLogger.Object, _mockSettings.Object);
        var command = CreateMockCommand("DELETE FROM Users", Array.Empty<(string, object?)>());
        var exception = new SqliteException("Foreign key constraint violation", 19);
        var eventData = CreateCommandErrorEventData(command, exception, elapsedMs: 5);

        // Act
        interceptor.CommandFailed(command, eventData);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Foreign key constraint violation")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "error log should include exception message");
    }

    [Fact]
    public async Task CommandFailedAsync_IncludesExecutionTime()
    {
        // Arrange
        var interceptor = new QueryPerformanceInterceptor(_mockLogger.Object, _mockSettings.Object);
        var command = CreateMockCommand("INSERT INTO Users (Email) VALUES (@p0)", new (string, object?)[] { ("@p0", "invalid") });
        var exception = new SqliteException("Unique constraint violation", 19);
        var eventData = CreateCommandErrorEventData(command, exception, elapsedMs: 75.5);

        // Act
        await interceptor.CommandFailedAsync(command, eventData);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("ElapsedMs=75.50")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "error log should include execution time");
    }

    [Fact]
    public void CommandFailed_IncludesSqlCommand()
    {
        // Arrange
        var interceptor = new QueryPerformanceInterceptor(_mockLogger.Object, _mockSettings.Object);
        var sql = "UPDATE Users SET Email = @p0";
        var command = CreateMockCommand(sql, new (string, object?)[] { ("@p0", "duplicate@test.com") });
        var exception = new SqliteException("Duplicate key", 19);
        var eventData = CreateCommandErrorEventData(command, exception, elapsedMs: 20);

        // Act
        interceptor.CommandFailed(command, eventData);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(sql)),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "error log should include SQL command");
    }

    #endregion

    #region Parameter Sanitization Tests

    [Theory]
    [InlineData("@password")]
    [InlineData("@userPassword")]
    [InlineData("@PasswordHash")]
    [InlineData("@PASSWORD")]
    public async Task ReaderExecutedAsync_WithPasswordParameter_RedactsValue(string parameterName)
    {
        // Arrange
        var interceptor = new QueryPerformanceInterceptor(_mockLogger.Object, _mockSettings.Object);
        var command = CreateMockCommand("SELECT * FROM Users WHERE Password = @password", new (string, object?)[] { (parameterName, "secret123") });
        var eventData = CreateCommandExecutedEventData(command, elapsedMs: 50);
        var reader = Mock.Of<DbDataReader>();

        _mockLogger.Setup(l => l.IsEnabled(LogLevel.Debug)).Returns(true);

        // Act
        await interceptor.ReaderExecutedAsync(command, eventData, reader);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("***REDACTED***")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "password parameters should be redacted");

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("secret123")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never,
            "password value should not appear in logs");
    }

    [Theory]
    [InlineData("@token")]
    [InlineData("@accessToken")]
    [InlineData("@refreshToken")]
    [InlineData("@apiToken")]
    public async Task NonQueryExecutedAsync_WithTokenParameter_RedactsValue(string parameterName)
    {
        // Arrange
        var interceptor = new QueryPerformanceInterceptor(_mockLogger.Object, _mockSettings.Object);
        var command = CreateMockCommand("UPDATE Users SET Token = @token", new (string, object?)[] { (parameterName, "super-secret-token") });
        var eventData = CreateCommandExecutedEventData(command, elapsedMs: 30);

        _mockLogger.Setup(l => l.IsEnabled(LogLevel.Debug)).Returns(true);

        // Act
        await interceptor.NonQueryExecutedAsync(command, eventData, 1);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("***REDACTED***") && !v.ToString()!.Contains("super-secret-token")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "token parameters should be redacted");
    }

    [Theory]
    [InlineData("@secret")]
    [InlineData("@clientSecret")]
    [InlineData("@apiSecret")]
    public async Task ScalarExecutedAsync_WithSecretParameter_RedactsValue(string parameterName)
    {
        // Arrange
        var interceptor = new QueryPerformanceInterceptor(_mockLogger.Object, _mockSettings.Object);
        var command = CreateMockCommand("SELECT Id FROM Config WHERE Secret = @secret", new (string, object?)[] { (parameterName, "my-secret-value") });
        var eventData = CreateCommandExecutedEventData(command, elapsedMs: 20);

        _mockLogger.Setup(l => l.IsEnabled(LogLevel.Debug)).Returns(true);

        // Act
        await interceptor.ScalarExecutedAsync(command, eventData, 1);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("***REDACTED***")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "secret parameters should be redacted");
    }

    [Theory]
    [InlineData("@key")]
    [InlineData("@apiKey")]
    [InlineData("@encryptionKey")]
    public void ReaderExecuted_WithKeyParameter_RedactsValue(string parameterName)
    {
        // Arrange
        var interceptor = new QueryPerformanceInterceptor(_mockLogger.Object, _mockSettings.Object);
        var command = CreateMockCommand("SELECT * FROM Settings WHERE Key = @key", new (string, object?)[] { (parameterName, "encryption-key-12345") });
        var eventData = CreateCommandExecutedEventData(command, elapsedMs: 25);
        var reader = Mock.Of<DbDataReader>();

        _mockLogger.Setup(l => l.IsEnabled(LogLevel.Debug)).Returns(true);

        // Act
        interceptor.ReaderExecuted(command, eventData, reader);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("***REDACTED***")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "key parameters should be redacted");
    }

    [Theory]
    [InlineData("@credential")]
    [InlineData("@credentials")]
    [InlineData("@userCredential")]
    public async Task CommandFailedAsync_WithCredentialParameter_RedactsValue(string parameterName)
    {
        // Arrange
        var interceptor = new QueryPerformanceInterceptor(_mockLogger.Object, _mockSettings.Object);
        var command = CreateMockCommand("SELECT * FROM Auth WHERE Credential = @credential", new (string, object?)[] { (parameterName, "sensitive-credential") });
        var exception = new SqliteException("Auth failed", 1);
        var eventData = CreateCommandErrorEventData(command, exception, elapsedMs: 10);

        // Act
        await interceptor.CommandFailedAsync(command, eventData);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("***REDACTED***") && !v.ToString()!.Contains("sensitive-credential")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "credential parameters should be redacted in error logs");
    }

    [Fact]
    public async Task ReaderExecutedAsync_WithLongParameterValue_TruncatesValue()
    {
        // Arrange
        var interceptor = new QueryPerformanceInterceptor(_mockLogger.Object, _mockSettings.Object);
        var longValue = new string('A', 100);
        var command = CreateMockCommand("SELECT * FROM Users WHERE Description = @p0", new (string, object?)[] { ("@p0", longValue) });
        var eventData = CreateCommandExecutedEventData(command, elapsedMs: 40);
        var reader = Mock.Of<DbDataReader>();

        _mockLogger.Setup(l => l.IsEnabled(LogLevel.Debug)).Returns(true);

        // Act
        await interceptor.ReaderExecutedAsync(command, eventData, reader);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("truncated, length=100")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "long parameter values should be truncated");

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(new string('A', 50))),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should include first 50 characters");
    }

    [Fact]
    public async Task NonQueryExecutedAsync_WithNullParameter_ShowsNull()
    {
        // Arrange
        var interceptor = new QueryPerformanceInterceptor(_mockLogger.Object, _mockSettings.Object);
        var command = CreateMockCommand("UPDATE Users SET DeletedAt = @p0", new (string, object?)[] { ("@p0", null) });
        var eventData = CreateCommandExecutedEventData(command, elapsedMs: 35);

        _mockLogger.Setup(l => l.IsEnabled(LogLevel.Debug)).Returns(true);

        // Act
        await interceptor.NonQueryExecutedAsync(command, eventData, 1);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("@p0=NULL")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "null parameters should be shown as NULL");
    }

    [Fact]
    public async Task ScalarExecutedAsync_WithNoParameters_ShowsNone()
    {
        // Arrange
        var interceptor = new QueryPerformanceInterceptor(_mockLogger.Object, _mockSettings.Object);
        var command = CreateMockCommand("SELECT COUNT(*) FROM Users", Array.Empty<(string, object?)>());
        var eventData = CreateCommandExecutedEventData(command, elapsedMs: 20);

        _mockLogger.Setup(l => l.IsEnabled(LogLevel.Debug)).Returns(true);

        // Act
        await interceptor.ScalarExecutedAsync(command, eventData, 42);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Parameters=(none)")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "queries without parameters should show (none)");
    }

    [Fact]
    public async Task ReaderExecutedAsync_WithMultipleParameters_ShowsAllParameters()
    {
        // Arrange
        var interceptor = new QueryPerformanceInterceptor(_mockLogger.Object, _mockSettings.Object);
        var command = CreateMockCommand(
            "SELECT * FROM Users WHERE Age > @p0 AND Name = @p1",
            new (string, object?)[] { ("@p0", 18), ("@p1", "John") });
        var eventData = CreateCommandExecutedEventData(command, elapsedMs: 45);
        var reader = Mock.Of<DbDataReader>();

        _mockLogger.Setup(l => l.IsEnabled(LogLevel.Debug)).Returns(true);

        // Act
        await interceptor.ReaderExecutedAsync(command, eventData, reader);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("@p0=Int32:'18'") &&
                    v.ToString()!.Contains("@p1=String:'John'")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should include all parameters with their types and values");
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public async Task ReaderExecutedAsync_WithLogParametersDisabled_DoesNotLogParameters()
    {
        // Arrange
        _mockLogger.Setup(l => l.IsEnabled(LogLevel.Debug)).Returns(true);
        _settings.LogQueryParameters = false;
        var interceptor = new QueryPerformanceInterceptor(_mockLogger.Object, _mockSettings.Object);
        var command = CreateMockCommand("SELECT * FROM Users WHERE Name = @name", new (string, object?)[] { ("@name", "John") });
        var eventData = CreateCommandExecutedEventData(command, elapsedMs: 30);
        var reader = Mock.Of<DbDataReader>();

        // Act
        await interceptor.ReaderExecutedAsync(command, eventData, reader);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("(parameter logging disabled)")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should indicate parameter logging is disabled");

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("John")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never,
            "should not include parameter values");
    }

    [Fact]
    public async Task NonQueryExecutedAsync_WithLogParametersDisabled_DoesNotLogParametersInWarning()
    {
        // Arrange
        _settings.LogQueryParameters = false;
        var interceptor = new QueryPerformanceInterceptor(_mockLogger.Object, _mockSettings.Object);
        var command = CreateMockCommand("UPDATE Users SET Name = @p0", new (string, object?)[] { ("@p0", "NewName") });
        var eventData = CreateCommandExecutedEventData(command, elapsedMs: 150);

        // Act
        await interceptor.NonQueryExecutedAsync(command, eventData, 1);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("(parameter logging disabled)")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "slow query warning should also respect parameter logging setting");
    }

    [Fact]
    public async Task CommandFailedAsync_WithLogParametersDisabled_DoesNotLogParameters()
    {
        // Arrange
        _settings.LogQueryParameters = false;
        var interceptor = new QueryPerformanceInterceptor(_mockLogger.Object, _mockSettings.Object);
        var command = CreateMockCommand("DELETE FROM Users WHERE Id = @p0", new (string, object?)[] { ("@p0", 123) });
        var exception = new SqliteException("Delete failed", 1);
        var eventData = CreateCommandErrorEventData(command, exception, elapsedMs: 10);

        // Act
        await interceptor.CommandFailedAsync(command, eventData);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("(parameter logging disabled)")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "error logs should respect parameter logging setting");
    }

    [Fact]
    public void ReaderExecuted_WithDebugLoggingDisabled_DoesNotLogDebug()
    {
        // Arrange
        var interceptor = new QueryPerformanceInterceptor(_mockLogger.Object, _mockSettings.Object);
        var command = CreateMockCommand("SELECT * FROM Users", Array.Empty<(string, object?)>());
        var eventData = CreateCommandExecutedEventData(command, elapsedMs: 50);
        var reader = Mock.Of<DbDataReader>();

        _mockLogger.Setup(l => l.IsEnabled(LogLevel.Debug)).Returns(false);

        // Act
        interceptor.ReaderExecuted(command, eventData, reader);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never,
            "should not log at Debug level when disabled");
    }

    #endregion

    #region SQL Command Truncation Tests

    [Fact]
    public async Task ReaderExecutedAsync_WithLongSql_TruncatesCommandText()
    {
        // Arrange
        var interceptor = new QueryPerformanceInterceptor(_mockLogger.Object, _mockSettings.Object);
        var longSql = "SELECT * FROM Users WHERE " + string.Join(" OR ", Enumerable.Range(1, 100).Select(i => $"Id = {i}"));
        var command = CreateMockCommand(longSql, Array.Empty<(string, object?)>());
        var eventData = CreateCommandExecutedEventData(command, elapsedMs: 30);
        var reader = Mock.Of<DbDataReader>();

        _mockLogger.Setup(l => l.IsEnabled(LogLevel.Debug)).Returns(true);

        // Act
        await interceptor.ReaderExecutedAsync(command, eventData, reader);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("truncated, length=")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "long SQL should be truncated");
    }

    [Fact]
    public async Task NonQueryExecutedAsync_WithEmptySql_ShowsEmpty()
    {
        // Arrange
        var interceptor = new QueryPerformanceInterceptor(_mockLogger.Object, _mockSettings.Object);
        var command = CreateMockCommand("", Array.Empty<(string, object?)>());
        var eventData = CreateCommandExecutedEventData(command, elapsedMs: 10);

        _mockLogger.Setup(l => l.IsEnabled(LogLevel.Debug)).Returns(true);

        // Act
        await interceptor.NonQueryExecutedAsync(command, eventData, 0);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("SQL=(empty)")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "empty SQL should be shown as (empty)");
    }

    [Fact]
    public async Task ScalarExecutedAsync_WithMultilineSql_NormalizesWhitespace()
    {
        // Arrange
        var interceptor = new QueryPerformanceInterceptor(_mockLogger.Object, _mockSettings.Object);
        var multilineSql = @"SELECT
                            COUNT(*)
                            FROM Users
                            WHERE Active = 1";
        var command = CreateMockCommand(multilineSql, Array.Empty<(string, object?)>());
        var eventData = CreateCommandExecutedEventData(command, elapsedMs: 25);

        _mockLogger.Setup(l => l.IsEnabled(LogLevel.Debug)).Returns(true);

        // Act
        await interceptor.ScalarExecutedAsync(command, eventData, 100);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("SELECT COUNT(*) FROM Users WHERE Active = 1") &&
                    !v.ToString()!.Contains("\n") &&
                    !v.ToString()!.Contains("\r")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "multiline SQL should be normalized to single line with single spaces");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ReaderExecutedAsync_WhenLoggingThrows_DoesNotThrow()
    {
        // Arrange
        _mockLogger.Setup(l => l.IsEnabled(LogLevel.Debug))
            .Throws(new InvalidOperationException("Logger error"));

        var interceptor = new QueryPerformanceInterceptor(_mockLogger.Object, _mockSettings.Object);
        var command = CreateMockCommand("SELECT * FROM Users", Array.Empty<(string, object?)>());
        var eventData = CreateCommandExecutedEventData(command, elapsedMs: 50);
        var reader = Mock.Of<DbDataReader>();

        // Act & Assert
        await FluentActions.Invoking(async () =>
            await interceptor.ReaderExecutedAsync(command, eventData, reader))
            .Should().NotThrowAsync("interceptor should never throw exceptions that could break query execution");
    }

    [Fact]
    public void NonQueryExecuted_WhenLoggingThrows_LogsError()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Logging failed");
        _mockLogger.Setup(l => l.IsEnabled(LogLevel.Debug))
            .Throws(expectedException);

        var interceptor = new QueryPerformanceInterceptor(_mockLogger.Object, _mockSettings.Object);
        var command = CreateMockCommand("UPDATE Users SET Name = @p0", new (string, object?)[] { ("@p0", "Test") });
        var eventData = CreateCommandExecutedEventData(command, elapsedMs: 30);

        // Act
        interceptor.NonQueryExecuted(command, eventData, 1);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("QueryPerformanceInterceptor.LogQueryExecution failed")),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log internal errors without throwing");
    }

    [Fact]
    public async Task CommandFailedAsync_WhenLoggingThrows_DoesNotThrow()
    {
        // Arrange
        var interceptor = new QueryPerformanceInterceptor(_mockLogger.Object, _mockSettings.Object);
        var command = CreateMockCommand("SELECT * FROM Users", Array.Empty<(string, object?)>());
        var originalException = new SqliteException("Query failed", 1);
        var eventData = CreateCommandErrorEventData(command, originalException, elapsedMs: 10);

        // Configure the logger to throw during the first Log call, then work normally
        var callCount = 0;
        _mockLogger.Setup(l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new InvalidOperationException("First logging attempt failed");
                }
            });

        // Act & Assert
        await FluentActions.Invoking(async () =>
            await interceptor.CommandFailedAsync(command, eventData))
            .Should().NotThrowAsync("interceptor should handle logging errors gracefully");
    }

    #endregion

    #region Helper Methods

    private static DbCommand CreateMockCommand(string commandText, (string, object?)[] parameters)
    {
        var mockCommand = new Mock<DbCommand>();
        mockCommand.Setup(c => c.CommandText).Returns(commandText);
        mockCommand.Setup(c => c.CommandType).Returns(CommandType.Text);

        // Create a concrete collection that can be enumerated
        var parameterList = new TestDbParameterCollection();

        foreach (var (name, value) in parameters)
        {
            var mockParam = new Mock<DbParameter>();
            mockParam.Setup(p => p.ParameterName).Returns(name);
            mockParam.Setup(p => p.Value).Returns(value ?? DBNull.Value);
            parameterList.Add(mockParam.Object);
        }

        mockCommand.Protected()
            .Setup<DbParameterCollection>("DbParameterCollection")
            .Returns(parameterList);

        return mockCommand.Object;
    }

    // Simple test implementation of DbParameterCollection for testing
    private class TestDbParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> _parameters = new();

        public override int Count => _parameters.Count;
        public override object SyncRoot => ((ICollection)_parameters).SyncRoot;

        public override int Add(object value)
        {
            _parameters.Add((DbParameter)value);
            return _parameters.Count - 1;
        }

        public override void AddRange(Array values)
        {
            foreach (DbParameter value in values)
            {
                _parameters.Add(value);
            }
        }

        public override void Clear() => _parameters.Clear();
        public override bool Contains(object value) => _parameters.Contains((DbParameter)value);
        public override bool Contains(string value) => _parameters.Any(p => p.ParameterName == value);
        public override void CopyTo(Array array, int index) => ((ICollection)_parameters).CopyTo(array, index);
        public override IEnumerator GetEnumerator() => _parameters.GetEnumerator();
        public override int IndexOf(object value) => _parameters.IndexOf((DbParameter)value);
        public override int IndexOf(string parameterName) => _parameters.FindIndex(p => p.ParameterName == parameterName);
        public override void Insert(int index, object value) => _parameters.Insert(index, (DbParameter)value);
        public override void Remove(object value) => _parameters.Remove((DbParameter)value);
        public override void RemoveAt(int index) => _parameters.RemoveAt(index);
        public override void RemoveAt(string parameterName)
        {
            var index = IndexOf(parameterName);
            if (index >= 0) _parameters.RemoveAt(index);
        }

        protected override DbParameter GetParameter(int index) => _parameters[index];
        protected override DbParameter GetParameter(string parameterName) => _parameters.First(p => p.ParameterName == parameterName);
        protected override void SetParameter(int index, DbParameter value) => _parameters[index] = value;
        protected override void SetParameter(string parameterName, DbParameter value)
        {
            var index = IndexOf(parameterName);
            if (index >= 0) _parameters[index] = value;
        }
    }

    private static CommandExecutedEventData CreateCommandExecutedEventData(DbCommand command, double elapsedMs)
    {
        // Use reflection to create the event data since the constructor signature is complex
        var duration = TimeSpan.FromMilliseconds(elapsedMs);

        // Create minimal event data using the most basic constructor parameters available
        var eventData = (CommandExecutedEventData)Activator.CreateInstance(
            typeof(CommandExecutedEventData),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new object?[]
            {
                null,  // eventDefinition
                (Func<EventDefinitionBase, EventData, string>)((_, __) => "Test"),  // messageGenerator
                null,  // connection
                command,  // command
                null,  // context
                DbCommandMethod.ExecuteReader,  // executeMethod
                Guid.NewGuid(),  // commandId
                Guid.NewGuid(),  // connectionId
                null,  // result
                false,  // async
                false,  // logParameterValues
                DateTimeOffset.UtcNow,  // startTime
                duration,  // duration
                CommandSource.Unknown  // commandSource
            },
            null)!;

        return eventData;
    }

    private static CommandErrorEventData CreateCommandErrorEventData(DbCommand command, Exception exception, double elapsedMs)
    {
        // Use reflection to create the event data
        var duration = TimeSpan.FromMilliseconds(elapsedMs);

        var eventData = (CommandErrorEventData)Activator.CreateInstance(
            typeof(CommandErrorEventData),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new object?[]
            {
                null,  // eventDefinition
                (Func<EventDefinitionBase, EventData, string>)((_, __) => "Test"),  // messageGenerator
                null,  // connection
                command,  // command
                null,  // context
                DbCommandMethod.ExecuteReader,  // executeMethod
                Guid.NewGuid(),  // commandId
                Guid.NewGuid(),  // connectionId
                exception,  // exception
                false,  // async
                false,  // logParameterValues
                DateTimeOffset.UtcNow,  // startTime
                duration,  // duration
                CommandSource.Unknown  // commandSource
            },
            null)!;

        return eventData;
    }

    #endregion
}
