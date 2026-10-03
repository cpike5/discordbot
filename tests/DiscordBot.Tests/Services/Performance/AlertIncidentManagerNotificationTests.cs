using DiscordBot.Bot.Services.Performance;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Services.Performance;

/// <summary>
/// The notification an admin sees for an alert is titled with the metric's display name
/// ("Memory Usage Alert"), not its key ("memory_usage Alert").
/// </summary>
public class AlertIncidentManagerNotificationTests
{
    [Fact]
    public async Task HandleBreachAsync_TitlesTheNotificationWithTheDisplayName()
    {
        var notificationService = new Mock<INotificationService>();
        var titleSeen = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        notificationService
            .Setup(n => n.CreateForAllAdminsAsync(
                It.IsAny<NotificationType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<AlertSeverity?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Callback((NotificationType _, string title, string _, string? _, AlertSeverity? _, string? _, string? _, TimeSpan? _, CancellationToken _) =>
                titleSeen.TrySetResult(title))
            .ReturnsAsync(true);

        var services = new ServiceCollection();
        services.AddSingleton(notificationService.Object);
        var manager = new AlertIncidentManager(
            services.BuildServiceProvider(),
            Mock.Of<IPerformanceNotifier>(),
            NullLogger<AlertIncidentManager>.Instance,
            Options.Create(new NotificationOptions { EnablePerformanceAlerts = true }));

        var repository = new Mock<IPerformanceAlertRepository>();
        repository
            .Setup(r => r.GetActiveIncidentByMetricAsync("memory_usage", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PerformanceIncident?)null);
        repository
            .Setup(r => r.CreateIncidentAsync(It.IsAny<PerformanceIncident>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PerformanceIncident i, CancellationToken _) => i);

        var config = new PerformanceAlertConfig
        {
            MetricName = "memory_usage",
            DisplayName = "Memory Usage",
            ThresholdUnit = "MB"
        };

        await manager.HandleBreachAsync(config, 1700, AlertSeverity.Critical, 1536, repository.Object, CancellationToken.None);

        var title = await titleSeen.Task.WaitAsync(TimeSpan.FromSeconds(10));
        title.Should().Be("Memory Usage Alert");
    }
}
