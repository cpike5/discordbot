using DiscordBot.Bot.Extensions;
using DiscordBot.Core.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace DiscordBot.Tests.Configuration;

/// <summary>
/// Unit tests for <see cref="OtlpOptions"/> and the OTLP resolution in <see cref="OpenTelemetryExtensions"/>.
/// </summary>
public class OtlpOptionsTests
{
    [Fact]
    public void GetSignalEndpoint_Grpc_UsesEndpointAsIs()
    {
        var options = new OtlpOptions { Protocol = "grpc" };

        options.GetSignalEndpoint("http://otel-collector:4317", "logs")
            .Should().Be(new Uri("http://otel-collector:4317"));
    }

    [Theory]
    [InlineData("http://otel-collector:4318", "traces", "http://otel-collector:4318/v1/traces")]
    [InlineData("http://otel-collector:4318/", "metrics", "http://otel-collector:4318/v1/metrics")]
    [InlineData("http://otel-collector:4318/v1/logs", "logs", "http://otel-collector:4318/v1/logs")]
    [InlineData("https://otlp.example.com/otlp", "logs", "https://otlp.example.com/otlp/v1/logs")]
    public void GetSignalEndpoint_Http_AppendsSignalPath(string endpoint, string signal, string expected)
    {
        var options = new OtlpOptions { Protocol = "http/protobuf" };

        options.GetSignalEndpoint(endpoint, signal).Should().Be(new Uri(expected));
    }

    [Fact]
    public void ParseHeaders_SkipsMalformedPairs()
    {
        var options = new OtlpOptions { Headers = "Authorization=Bearer abc=, x-scope-orgid = tenant1 ,broken," };

        options.ParseHeaders().Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer abc=",
            ["x-scope-orgid"] = "tenant1"
        });
    }

    [Fact]
    public void ResolveOtlp_PrefersConfiguredEndpoint()
    {
        var configuration = Build(new()
        {
            ["OpenTelemetry:Otlp:Endpoint"] = "http://collector:4317",
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://other:4317"
        });

        OpenTelemetryExtensions.ResolveOtlp(configuration).Endpoint.Should().Be("http://collector:4317");
    }

    [Fact]
    public void ResolveOtlp_FallsBackToStandardVariable()
    {
        var configuration = Build(new() { ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://other:4317" });

        OpenTelemetryExtensions.ResolveOtlp(configuration).Endpoint.Should().Be("http://other:4317");
    }

    [Fact]
    public void ResolveOtlp_WithNothingConfigured_ReturnsNullEndpoint()
    {
        OpenTelemetryExtensions.ResolveOtlp(Build(new())).Endpoint.Should().BeNull();
    }

    [Fact]
    public void BuildResourceAttributes_IncludesEnvironmentLowercase()
    {
        var attributes = OpenTelemetryExtensions.BuildResourceAttributes(Build(new()
        {
            ["environment"] = "Staging",
            ["OpenTelemetry:ServiceName"] = "discordbot-test"
        }));

        attributes["deployment.environment"].Should().Be("staging");
        attributes["service.name"].Should().Be("discordbot-test");
        attributes.Should().ContainKeys("service.version", "service.instance.id");
    }

    private static IConfiguration Build(Dictionary<string, string?> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
