using System.Net;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Bot.Metrics;
using DiscordBot.Bot.Middleware;
using DiscordBot.Tests.Metrics;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Blazor;

/// <summary>
/// Unit tests for <see cref="BlazorCircuitHandler"/>. <see cref="Microsoft.AspNetCore.Components.Server.Circuits.Circuit"/>
/// has no public constructor, so these exercise the testable core
/// (<see cref="BlazorCircuitHandler.HandleCircuitOpened"/> / <see cref="BlazorCircuitHandler.HandleCircuitClosed"/>)
/// directly with a synthetic circuit ID rather than a real <c>OnCircuitOpenedAsync</c> call.
/// </summary>
public class BlazorCircuitHandlerTests : IDisposable
{
    private const string CircuitId = "circuit-abc123";

    private readonly SimpleMeterFactory _meterFactory = new();
    private readonly BlazorMetrics _metrics;
    private readonly MetricCollector<long> _openedCollector;
    private readonly MetricCollector<long> _activeCollector;
    private readonly CircuitClientInfoService _clientInfo = new();
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor = new();
    private readonly Mock<ILogger<BlazorCircuitHandler>> _mockLogger = new();

    public BlazorCircuitHandlerTests()
    {
        _metrics = new BlazorMetrics(_meterFactory);
        var meter = _meterFactory.GetMeter(BlazorMetrics.MeterName)!;
        _openedCollector = new MetricCollector<long>(meter, "blazor.circuits.opened_total");
        _activeCollector = new MetricCollector<long>(meter, "blazor.circuits.active");
    }

    private BlazorCircuitHandler CreateHandler()
        => new(_clientInfo, _mockHttpContextAccessor.Object, _mockLogger.Object, _metrics);

    [Fact]
    public void HandleCircuitOpened_WithHttpContext_PopulatesClientInfoFromRequest()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.5");
        httpContext.Request.Headers.UserAgent = "TestBrowser/1.0";
        httpContext.Items[CorrelationIdMiddleware.ItemKey] = "existing-correlation-id";

        var handler = CreateHandler();

        handler.HandleCircuitOpened(CircuitId, httpContext);

        _clientInfo.RemoteIp.Should().Be(IPAddress.Parse("198.51.100.5"));
        _clientInfo.UserAgent.Should().Be("TestBrowser/1.0");
        _clientInfo.CircuitId.Should().Be(CircuitId);
        _clientInfo.CorrelationId.Should().Be("existing-correlation-id",
            "an existing request correlation ID should be reused rather than generating a new one");
    }

    [Fact]
    public void HandleCircuitOpened_WithoutHttpContext_GeneratesCorrelationId_AndLeavesIpUserAgentNull()
    {
        var handler = CreateHandler();

        handler.HandleCircuitOpened(CircuitId, httpContext: null);

        _clientInfo.RemoteIp.Should().BeNull();
        _clientInfo.UserAgent.Should().BeNull();
        _clientInfo.CircuitId.Should().Be(CircuitId);
        _clientInfo.CorrelationId.Should().NotBeNullOrEmpty();
        _clientInfo.CorrelationId.Should().HaveLength(16, "correlation IDs follow CorrelationIdMiddleware's 16-char hex convention");
    }

    [Fact]
    public void HandleCircuitOpened_WithoutUserAgentHeader_LeavesUserAgentNull()
    {
        var httpContext = new DefaultHttpContext();

        var handler = CreateHandler();
        handler.HandleCircuitOpened(CircuitId, httpContext);

        _clientInfo.UserAgent.Should().BeNull();
    }

    [Fact]
    public void HandleCircuitOpened_RecordsOpenedAndActiveMetrics()
    {
        var handler = CreateHandler();

        handler.HandleCircuitOpened(CircuitId, httpContext: null);

        var opened = _openedCollector.GetMeasurementSnapshot();
        opened.Should().ContainSingle();
        opened.Single().Value.Should().Be(1);

        var active = _activeCollector.GetMeasurementSnapshot();
        active.Should().ContainSingle();
        active.Single().Value.Should().Be(1);
    }

    [Fact]
    public void HandleCircuitOpened_ThenClosed_ReturnsActiveGaugeToZero()
    {
        var handler = CreateHandler();

        handler.HandleCircuitOpened(CircuitId, httpContext: null);
        handler.HandleCircuitClosed(CircuitId);

        var active = _activeCollector.GetMeasurementSnapshot();
        active.Select(m => m.Value).Sum().Should().Be(0, "one open (+1) and one close (-1) should net to zero");
    }

    [Fact]
    public void HandleCircuitOpened_LogsInformationWithCircuitId()
    {
        var handler = CreateHandler();

        handler.HandleCircuitOpened(CircuitId, httpContext: null);

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Blazor circuit opened") && v.ToString()!.Contains(CircuitId)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void HandleCircuitClosed_LogsInformationWithCircuitId()
    {
        var handler = CreateHandler();
        handler.HandleCircuitOpened(CircuitId, httpContext: null);

        handler.HandleCircuitClosed(CircuitId);

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Blazor circuit closed") && v.ToString()!.Contains(CircuitId)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    public void Dispose() => _metrics.Dispose();
}
