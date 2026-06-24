using DiscordBot.Bot.Extensions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Tests for <see cref="HttpResilienceExtensions"/>. The standard resilience handler validates
/// cross-option timing invariants (total timeout >= attempt timeout, circuit-breaker sampling
/// duration >= 2x attempt timeout) when the pipeline is first built. These tests force pipeline
/// construction so a bad derivation would surface here rather than crashing the app at startup.
/// </summary>
public class HttpResilienceExtensionsTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(30)]
    [InlineData(60)]
    public void AddBotResilienceHandler_BuildsValidPipeline_AcrossAttemptTimeouts(int attemptSeconds)
    {
        var services = new ServiceCollection();
        services.AddHttpClient("test")
            .AddBotResilienceHandler(TimeSpan.FromSeconds(attemptSeconds));

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        // Creating the client materializes the resilience pipeline and triggers option validation.
        var act = () => factory.CreateClient("test");

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void AddBotResilienceHandler_BuildsValidPipeline_AcrossRetryCounts(int maxRetries)
    {
        var services = new ServiceCollection();
        services.AddHttpClient("test")
            .AddBotResilienceHandler(TimeSpan.FromSeconds(5), maxRetries);

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        var act = () => factory.CreateClient("test");

        act.Should().NotThrow();
    }
}
