using DiscordBot.Bot.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.RegularExpressions;

namespace DiscordBot.Tests.Middleware;

/// <summary>
/// Unit tests for <see cref="CorrelationIdMiddleware"/>.
/// </summary>
public class CorrelationIdMiddlewareTests
{
    private readonly Mock<ILogger<CorrelationIdMiddleware>> _mockLogger;

    public CorrelationIdMiddlewareTests()
    {
        _mockLogger = new Mock<ILogger<CorrelationIdMiddleware>>();
    }

    [Fact]
    public async Task InvokeAsync_WithExistingHeader_UsesProvidedCorrelationId()
    {
        // Arrange
        const string expectedCorrelationId = "test-correlation-id-abc123";
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = expectedCorrelationId;

        var nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new CorrelationIdMiddleware(next, _mockLogger.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Items.Should().ContainKey(CorrelationIdMiddleware.ItemKey,
            "the correlation ID should be stored in HttpContext.Items");
        context.Items[CorrelationIdMiddleware.ItemKey].Should().Be(expectedCorrelationId,
            "the provided correlation ID should be used");
        context.Response.Headers.Should().ContainKey(CorrelationIdMiddleware.HeaderName,
            "the correlation ID should be added to response headers");
        context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString().Should().Be(expectedCorrelationId,
            "the response header should match the provided correlation ID");
        nextCalled.Should().BeTrue("the next middleware should be called");
    }

    [Fact]
    public async Task InvokeAsync_WithoutHeader_GeneratesNewCorrelationId()
    {
        // Arrange
        var context = new DefaultHttpContext();

        var nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new CorrelationIdMiddleware(next, _mockLogger.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Items.Should().ContainKey(CorrelationIdMiddleware.ItemKey,
            "a correlation ID should be stored in HttpContext.Items");
        context.Items[CorrelationIdMiddleware.ItemKey].Should().NotBeNull(
            "a correlation ID should be generated");

        var correlationId = context.Items[CorrelationIdMiddleware.ItemKey] as string;
        correlationId.Should().NotBeNullOrWhiteSpace("the correlation ID should not be empty");
        correlationId!.Length.Should().Be(16, "the generated correlation ID should be 16 characters");
        correlationId.Should().MatchRegex("^[0-9a-f]{16}$",
            "the correlation ID should be a 16-character hexadecimal string");

        context.Response.Headers.Should().ContainKey(CorrelationIdMiddleware.HeaderName,
            "the correlation ID should be added to response headers");
        context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString().Should().Be(correlationId,
            "the response header should match the generated correlation ID");
        nextCalled.Should().BeTrue("the next middleware should be called");
    }

    [Fact]
    public async Task InvokeAsync_AlwaysAddsResponseHeader()
    {
        // Arrange
        var context = new DefaultHttpContext();

        RequestDelegate next = (ctx) => Task.CompletedTask;
        var middleware = new CorrelationIdMiddleware(next, _mockLogger.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.Should().ContainKey(CorrelationIdMiddleware.HeaderName,
            "the X-Correlation-ID header should always be added to the response");
        context.Response.Headers[CorrelationIdMiddleware.HeaderName].Should().NotBeEmpty(
            "the response header should have a value");
    }

    [Fact]
    public async Task InvokeAsync_CallsNextDelegate()
    {
        // Arrange
        var context = new DefaultHttpContext();

        var nextCalled = false;
        var nextCalledWithCorrectContext = false;

        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            nextCalledWithCorrectContext = ctx == context;
            return Task.CompletedTask;
        };

        var middleware = new CorrelationIdMiddleware(next, _mockLogger.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue("the next middleware delegate should be invoked");
        nextCalledWithCorrectContext.Should().BeTrue(
            "the next middleware should be called with the same HttpContext");
    }

    [Fact]
    public async Task GeneratedCorrelationId_IsValid16CharHex()
    {
        // Arrange
        var generatedIds = new List<string>();
        var context1 = new DefaultHttpContext();
        var context2 = new DefaultHttpContext();
        var context3 = new DefaultHttpContext();

        RequestDelegate next = (ctx) => Task.CompletedTask;
        var middleware = new CorrelationIdMiddleware(next, _mockLogger.Object);

        // Act - Generate multiple correlation IDs
        await middleware.InvokeAsync(context1);
        await middleware.InvokeAsync(context2);
        await middleware.InvokeAsync(context3);

        generatedIds.Add(context1.Items[CorrelationIdMiddleware.ItemKey] as string ?? "");
        generatedIds.Add(context2.Items[CorrelationIdMiddleware.ItemKey] as string ?? "");
        generatedIds.Add(context3.Items[CorrelationIdMiddleware.ItemKey] as string ?? "");

        // Assert - All generated IDs should be valid
        generatedIds.Should().AllSatisfy(id =>
        {
            id.Should().NotBeNullOrWhiteSpace("each correlation ID should be generated");
            id.Length.Should().Be(16, "each correlation ID should be exactly 16 characters");
            id.Should().MatchRegex("^[0-9a-f]{16}$",
                "each correlation ID should contain only hexadecimal characters (0-9, a-f)");
        });

        // Assert - Generated IDs should be unique
        generatedIds.Distinct().Should().HaveCount(3,
            "each generated correlation ID should be unique");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public async Task InvokeAsync_WithWhitespaceHeader_GeneratesNewCorrelationId(string whitespaceValue)
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = whitespaceValue;

        RequestDelegate next = (ctx) => Task.CompletedTask;
        var middleware = new CorrelationIdMiddleware(next, _mockLogger.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var correlationId = context.Items[CorrelationIdMiddleware.ItemKey] as string;
        correlationId.Should().NotBeNullOrWhiteSpace(
            "a new correlation ID should be generated when header is whitespace");
        correlationId.Should().MatchRegex("^[0-9a-f]{16}$",
            "the generated correlation ID should be a valid 16-character hex string");
    }

    [Fact]
    public async Task InvokeAsync_WithMultipleHeaderValues_UsesFirstValue()
    {
        // Arrange
        const string firstValue = "first-correlation-id";
        const string secondValue = "second-correlation-id";
        var context = new DefaultHttpContext();
        context.Request.Headers.Append(CorrelationIdMiddleware.HeaderName, firstValue);
        context.Request.Headers.Append(CorrelationIdMiddleware.HeaderName, secondValue);

        RequestDelegate next = (ctx) => Task.CompletedTask;
        var middleware = new CorrelationIdMiddleware(next, _mockLogger.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var correlationId = context.Items[CorrelationIdMiddleware.ItemKey] as string;
        correlationId.Should().Be(firstValue,
            "the first header value should be used when multiple values are present");
    }

    [Fact]
    public async Task InvokeAsync_WithExistingHeader_LogsTraceMessage()
    {
        // Arrange
        const string correlationId = "existing-correlation-id";
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = correlationId;

        RequestDelegate next = (ctx) => Task.CompletedTask;
        var middleware = new CorrelationIdMiddleware(next, _mockLogger.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Trace,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Using existing correlation ID from request")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "a trace log should be written when using an existing correlation ID");
    }

    [Fact]
    public async Task InvokeAsync_WithoutHeader_LogsTraceMessage()
    {
        // Arrange
        var context = new DefaultHttpContext();

        RequestDelegate next = (ctx) => Task.CompletedTask;
        var middleware = new CorrelationIdMiddleware(next, _mockLogger.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Trace,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Generated new correlation ID")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "a trace log should be written when generating a new correlation ID");
    }

    [Fact]
    public async Task InvokeAsync_StoresCorrelationIdBeforeCallingNext()
    {
        // Arrange
        var context = new DefaultHttpContext();
        string? correlationIdInNext = null;

        RequestDelegate next = (ctx) =>
        {
            correlationIdInNext = ctx.Items[CorrelationIdMiddleware.ItemKey] as string;
            return Task.CompletedTask;
        };

        var middleware = new CorrelationIdMiddleware(next, _mockLogger.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        correlationIdInNext.Should().NotBeNullOrWhiteSpace(
            "the correlation ID should be available to downstream middleware");
        correlationIdInNext.Should().Be(context.Items[CorrelationIdMiddleware.ItemKey] as string,
            "the same correlation ID should be used throughout the request pipeline");
    }

    [Fact]
    public async Task InvokeAsync_PreservesCustomCorrelationIdFormat()
    {
        // Arrange
        const string customCorrelationId = "MY-CUSTOM-FORMAT-12345";
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = customCorrelationId;

        RequestDelegate next = (ctx) => Task.CompletedTask;
        var middleware = new CorrelationIdMiddleware(next, _mockLogger.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var correlationId = context.Items[CorrelationIdMiddleware.ItemKey] as string;
        correlationId.Should().Be(customCorrelationId,
            "the middleware should accept and preserve custom correlation ID formats");
        context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString().Should().Be(customCorrelationId,
            "the custom correlation ID should be echoed in the response");
    }

    [Fact]
    public async Task InvokeAsync_PropagatesExceptionsFromNext()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var expectedException = new InvalidOperationException("Test exception");

        RequestDelegate next = (ctx) => throw expectedException;
        var middleware = new CorrelationIdMiddleware(next, _mockLogger.Object);

        // Act & Assert
        var act = async () => await middleware.InvokeAsync(context);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Test exception",
                "exceptions from downstream middleware should propagate");

        // Verify correlation ID was still set before the exception
        context.Items.Should().ContainKey(CorrelationIdMiddleware.ItemKey,
            "the correlation ID should be set even if downstream middleware throws");
    }

    [Fact]
    public void HeaderName_IsCorrect()
    {
        // Assert
        CorrelationIdMiddleware.HeaderName.Should().Be("X-Correlation-ID",
            "the header name constant should match the expected value");
    }

    [Fact]
    public void ItemKey_IsCorrect()
    {
        // Assert
        CorrelationIdMiddleware.ItemKey.Should().Be("CorrelationId",
            "the items key constant should match the expected value");
    }
}
