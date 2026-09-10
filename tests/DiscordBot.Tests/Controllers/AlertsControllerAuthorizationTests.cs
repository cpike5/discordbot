using System.Reflection;
using DiscordBot.Bot.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;

namespace DiscordBot.Tests.Controllers;

/// <summary>
/// Authorization attribute tests for <see cref="AlertsController"/>.
/// Asserts the class-level and per-action <see cref="AuthorizeAttribute"/> policies via
/// reflection, mirroring the request-role matrix documented for the alerts API.
/// </summary>
public class AlertsControllerAuthorizationTests
{
    [Fact]
    public void Controller_ShouldHaveClassLevelRequireViewerPolicy()
    {
        // Assert - defense in depth: every action requires at least Viewer.
        var attribute = typeof(AlertsController).GetCustomAttribute<AuthorizeAttribute>();

        attribute.Should().NotBeNull("AlertsController must declare a class-level authorization policy");
        attribute!.Policy.Should().Be("RequireViewer");
    }

    [Theory]
    [InlineData(nameof(AlertsController.GetAllConfigs), "RequireViewer")]
    [InlineData(nameof(AlertsController.GetConfigByMetricName), "RequireViewer")]
    [InlineData(nameof(AlertsController.UpdateConfig), "RequireAdmin")]
    [InlineData(nameof(AlertsController.GetActiveIncidents), "RequireViewer")]
    [InlineData(nameof(AlertsController.GetIncidentHistory), "RequireViewer")]
    [InlineData(nameof(AlertsController.GetIncidentById), "RequireViewer")]
    [InlineData(nameof(AlertsController.AcknowledgeIncident), "RequireAdmin")]
    [InlineData(nameof(AlertsController.AcknowledgeAllIncidents), "RequireAdmin")]
    [InlineData(nameof(AlertsController.GetActiveAlertSummary), "RequireViewer")]
    [InlineData(nameof(AlertsController.GetAlertFrequencyStats), "RequireViewer")]
    public void Action_ShouldRequireExpectedPolicy(string methodName, string expectedPolicy)
    {
        // Arrange
        var method = typeof(AlertsController).GetMethod(methodName);
        method.Should().NotBeNull($"{methodName} should exist on AlertsController");

        // Act
        var attribute = method!.GetCustomAttribute<AuthorizeAttribute>();

        // Assert
        attribute.Should().NotBeNull($"{methodName} should declare an [Authorize] attribute");
        attribute!.Policy.Should().Be(expectedPolicy);
    }

    [Theory]
    [InlineData(nameof(AlertsController.UpdateConfig))]
    [InlineData(nameof(AlertsController.AcknowledgeIncident))]
    [InlineData(nameof(AlertsController.AcknowledgeAllIncidents))]
    public void MutationAction_ShouldRequireAdmin_NotJustViewer(string methodName)
    {
        // Config mutation and incident acknowledgment change state and must not be
        // reachable by a plain Viewer, even though the class-level policy allows Viewer.
        var method = typeof(AlertsController).GetMethod(methodName);
        var attribute = method!.GetCustomAttribute<AuthorizeAttribute>();

        attribute.Should().NotBeNull();
        attribute!.Policy.Should().Be("RequireAdmin");
    }
}
