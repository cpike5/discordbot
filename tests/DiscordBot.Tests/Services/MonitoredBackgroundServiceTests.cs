using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DiscordBot.Tests.Services;

/// <summary>
/// How a background service failure is contained: the host ignores it (so one loop cannot stop
/// the bot) and the failed service stays visible in the health registry as "Error".
/// </summary>
public class MonitoredBackgroundServiceTests
{
    [Fact]
    public void AddApplicationServices_ConfiguresHostToIgnoreBackgroundServiceExceptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        services.AddApplicationServices(configuration);
        using var provider = services.BuildServiceProvider();
        var hostOptions = provider.GetRequiredService<IOptions<HostOptions>>().Value;

        // Assert
        hostOptions.BackgroundServiceExceptionBehavior.Should().Be(
            BackgroundServiceExceptionBehavior.Ignore,
            "an exception escaping one background service must not stop the whole bot");
    }

    [Fact]
    public async Task ExecuteAsync_WhenServiceFaults_StaysRegisteredWithErrorStatus()
    {
        // Arrange
        var registry = new BackgroundServiceHealthRegistry(NullLogger<BackgroundServiceHealthRegistry>.Instance);
        var service = CreateService(registry, _ => throw new InvalidOperationException("loop exploded"));

        // Act
        await service.StartAsync(CancellationToken.None);
        var act = async () => await service.ExecuteTask!.ConfigureAwait(false);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        var health = registry.GetHealth(TestService.Name);
        health.Should().NotBeNull("a faulted service must stay visible to health monitoring");
        health!.Status.Should().Be("Error");
        health.LastError.Should().Be("loop exploded");
        registry.GetOverallStatus().Should().Be("Unhealthy");
    }

    [Fact]
    public async Task ExecuteAsync_WhenStoppedCleanly_UnregistersFromHealthMonitoring()
    {
        // Arrange
        var registry = new BackgroundServiceHealthRegistry(NullLogger<BackgroundServiceHealthRegistry>.Instance);
        var service = CreateService(registry, ct => Task.Delay(Timeout.Infinite, ct));

        // Act
        await service.StartAsync(CancellationToken.None);
        var registered = await TestHelpers.LogTestHelper.WaitUntilAsync(
            () => registry.GetHealth(TestService.Name) is not null).ConfigureAwait(false);
        await service.StopAsync(CancellationToken.None);

        // Assert
        registered.Should().BeTrue("the service registers once it starts running");
        registry.GetHealth(TestService.Name).Should().BeNull();
        service.Status.Should().Be("Stopped");
    }

    private static TestService CreateService(
        BackgroundServiceHealthRegistry registry,
        Func<CancellationToken, Task> work)
    {
        var provider = new ServiceCollection()
            .AddSingleton<DiscordBot.Core.Interfaces.IBackgroundServiceHealthRegistry>(registry)
            .BuildServiceProvider();
        return new TestService(provider, work);
    }

    private sealed class TestService : MonitoredBackgroundService
    {
        public const string Name = "Test Monitored Service";
        private readonly Func<CancellationToken, Task> _work;

        public TestService(IServiceProvider serviceProvider, Func<CancellationToken, Task> work)
            : base(serviceProvider, NullLogger.Instance)
        {
            _work = work;
        }

        public override string ServiceName => Name;

        protected override Task ExecuteMonitoredAsync(CancellationToken stoppingToken) => _work(stoppingToken);
    }
}
