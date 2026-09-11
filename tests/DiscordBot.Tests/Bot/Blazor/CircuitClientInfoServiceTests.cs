using System.Net;
using DiscordBot.Bot.Blazor.Services;
using FluentAssertions;

namespace DiscordBot.Tests.Bot.Blazor;

/// <summary>
/// Unit tests for <see cref="CircuitClientInfoService"/>.
/// </summary>
public class CircuitClientInfoServiceTests
{
    [Fact]
    public void DefaultState_HasNoIpOrUserAgent_AndEmptyIds()
    {
        var service = new CircuitClientInfoService();

        service.RemoteIp.Should().BeNull();
        service.UserAgent.Should().BeNull();
        service.CircuitId.Should().BeEmpty();
        service.CorrelationId.Should().BeEmpty();
    }

    [Fact]
    public void Populate_SetsAllProperties()
    {
        var service = new CircuitClientInfoService();
        var ip = IPAddress.Parse("203.0.113.7");

        service.Populate(ip, "TestAgent/1.0", "circuit-abc", "corr-123");

        service.RemoteIp.Should().Be(ip);
        service.UserAgent.Should().Be("TestAgent/1.0");
        service.CircuitId.Should().Be("circuit-abc");
        service.CorrelationId.Should().Be("corr-123");
    }

    [Fact]
    public void Populate_WithNullIpAndUserAgent_LeavesThemNull()
    {
        var service = new CircuitClientInfoService();

        service.Populate(null, null, "circuit-abc", "corr-123");

        service.RemoteIp.Should().BeNull();
        service.UserAgent.Should().BeNull();
        service.CircuitId.Should().Be("circuit-abc");
        service.CorrelationId.Should().Be("corr-123");
    }
}
