using System.Text.Json;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Infrastructure.Services.LLM;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services.LLM;

/// <summary>
/// Unit tests for <see cref="ToolRegistry"/>.
/// Tests cover provider registration, enable/disable, tool retrieval, and tool execution.
/// </summary>
public class ToolRegistryTests
{
    private readonly Mock<ILogger<ToolRegistry>> _mockLogger;
    private readonly ToolRegistry _registry;

    public ToolRegistryTests()
    {
        _mockLogger = new Mock<ILogger<ToolRegistry>>();
        _registry = new ToolRegistry(_mockLogger.Object);
    }

    #region Registration Tests

    [Fact]
    public void RegisterProvider_RegistersProviderSuccessfully()
    {
        // Arrange
        var provider = CreateMockProvider("TestProvider", "Test Description", "test_tool");

        // Act
        _registry.RegisterProvider(provider.Object);

        // Assert
        _registry.IsProviderRegistered("TestProvider").Should().BeTrue();
        _registry.IsProviderEnabled("TestProvider").Should().BeTrue();
    }

    [Fact]
    public void RegisterProvider_RegistersDisabledProvider()
    {
        // Arrange
        var provider = CreateMockProvider("TestProvider", "Test Description", "test_tool");

        // Act
        _registry.RegisterProvider(provider.Object, enabled: false);

        // Assert
        _registry.IsProviderRegistered("TestProvider").Should().BeTrue();
        _registry.IsProviderEnabled("TestProvider").Should().BeFalse();
    }

    [Fact]
    public void RegisterProvider_IgnoresDuplicateRegistration()
    {
        // Arrange
        var provider = CreateMockProvider("TestProvider", "Test Description", "test_tool");

        // Act
        _registry.RegisterProvider(provider.Object);
        _registry.RegisterProvider(provider.Object); // Duplicate

        // Assert - Should not throw and only register once
        _registry.GetProviderNames().Count(n => n == "TestProvider").Should().Be(1);
    }

    [Fact]
    public void RegisterProvider_ThrowsOnNullProvider()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _registry.RegisterProvider(null!));
    }

    #endregion

    #region Enable/Disable Tests

    [Fact]
    public void EnableProvider_EnablesDisabledProvider()
    {
        // Arrange
        var provider = CreateMockProvider("TestProvider", "Test Description", "test_tool");
        _registry.RegisterProvider(provider.Object, enabled: false);

        // Act
        _registry.EnableProvider("TestProvider");

        // Assert
        _registry.IsProviderEnabled("TestProvider").Should().BeTrue();
    }

    [Fact]
    public void DisableProvider_DisablesEnabledProvider()
    {
        // Arrange
        var provider = CreateMockProvider("TestProvider", "Test Description", "test_tool");
        _registry.RegisterProvider(provider.Object, enabled: true);

        // Act
        _registry.DisableProvider("TestProvider");

        // Assert
        _registry.IsProviderEnabled("TestProvider").Should().BeFalse();
    }

    [Fact]
    public void EnableProvider_ThrowsOnUnknownProvider()
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _registry.EnableProvider("UnknownProvider"));
    }

    [Fact]
    public void DisableProvider_ThrowsOnUnknownProvider()
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _registry.DisableProvider("UnknownProvider"));
    }

    [Fact]
    public void EnableProvider_IsCaseInsensitive()
    {
        // Arrange
        var provider = CreateMockProvider("TestProvider", "Test Description", "test_tool");
        _registry.RegisterProvider(provider.Object, enabled: false);

        // Act
        _registry.EnableProvider("TESTPROVIDER");

        // Assert
        _registry.IsProviderEnabled("TestProvider").Should().BeTrue();
    }

    #endregion

    #region GetEnabledTools Tests

    [Fact]
    public void GetEnabledTools_ReturnsToolsFromEnabledProviders()
    {
        // Arrange
        var provider1 = CreateMockProvider("Provider1", "Description 1", "tool1");
        var provider2 = CreateMockProvider("Provider2", "Description 2", "tool2");

        _registry.RegisterProvider(provider1.Object, enabled: true);
        _registry.RegisterProvider(provider2.Object, enabled: true);

        // Act
        var tools = _registry.GetEnabledTools().ToList();

        // Assert
        tools.Should().HaveCount(2);
        tools.Select(t => t.Name).Should().Contain("tool1");
        tools.Select(t => t.Name).Should().Contain("tool2");
    }

    [Fact]
    public void GetEnabledTools_ExcludesDisabledProviders()
    {
        // Arrange
        var provider1 = CreateMockProvider("Provider1", "Description 1", "tool1");
        var provider2 = CreateMockProvider("Provider2", "Description 2", "tool2");

        _registry.RegisterProvider(provider1.Object, enabled: true);
        _registry.RegisterProvider(provider2.Object, enabled: false);

        // Act
        var tools = _registry.GetEnabledTools().ToList();

        // Assert
        tools.Should().HaveCount(1);
        tools.Single().Name.Should().Be("tool1");
    }

    [Fact]
    public void GetEnabledTools_ReturnsEmptyWhenNoProviders()
    {
        // Act
        var tools = _registry.GetEnabledTools().ToList();

        // Assert
        tools.Should().BeEmpty();
    }

    #endregion

    #region ExecuteToolAsync Tests

    [Fact]
    public async Task ExecuteToolAsync_ExecutesToolSuccessfully()
    {
        // Arrange
        var expectedResult = ToolExecutionResult.CreateSuccess(CreateJsonElement(new { success = true }));
        var provider = CreateMockProvider("TestProvider", "Description", "test_tool", expectedResult);
        _registry.RegisterProvider(provider.Object);

        var context = CreateToolContext();
        var input = CreateJsonElement(new { });

        // Act
        var result = await _registry.ExecuteToolAsync("test_tool", input, context);

        // Assert
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteToolAsync_ThrowsWhenToolNotFound()
    {
        // Arrange
        var provider = CreateMockProvider("TestProvider", "Description", "tool1");
        _registry.RegisterProvider(provider.Object);

        var context = CreateToolContext();
        var input = CreateJsonElement(new { });

        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            _registry.ExecuteToolAsync("nonexistent_tool", input, context));
    }

    [Fact]
    public async Task ExecuteToolAsync_DoesNotExecuteFromDisabledProvider()
    {
        // Arrange
        var provider = CreateMockProvider("TestProvider", "Description", "test_tool");
        _registry.RegisterProvider(provider.Object, enabled: false);

        var context = CreateToolContext();
        var input = CreateJsonElement(new { });

        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            _registry.ExecuteToolAsync("test_tool", input, context));
    }

    [Fact]
    public async Task ExecuteToolAsync_ReturnsErrorOnException()
    {
        // Arrange
        var provider = new Mock<IToolProvider>();
        provider.Setup(p => p.Name).Returns("TestProvider");
        provider.Setup(p => p.Description).Returns("Test");
        provider.Setup(p => p.GetTools()).Returns(new[]
        {
            new LlmToolDefinition { Name = "test_tool", Description = "Test" }
        });
        provider.Setup(p => p.ExecuteToolAsync(It.IsAny<string>(), It.IsAny<JsonElement>(), It.IsAny<ToolContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test error"));

        _registry.RegisterProvider(provider.Object);

        var context = CreateToolContext();
        var input = CreateJsonElement(new { });

        // Act
        var result = await _registry.ExecuteToolAsync("test_tool", input, context);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Test error");
    }

    [Fact]
    public async Task ExecuteToolAsync_IsCaseInsensitiveForToolName()
    {
        // Arrange
        var expectedResult = ToolExecutionResult.CreateSuccess(CreateJsonElement(new { success = true }));
        var provider = CreateMockProvider("TestProvider", "Description", "test_tool", expectedResult);
        _registry.RegisterProvider(provider.Object);

        var context = CreateToolContext();
        var input = CreateJsonElement(new { });

        // Act
        var result = await _registry.ExecuteToolAsync("TEST_TOOL", input, context);

        // Assert
        result.Success.Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private static Mock<IToolProvider> CreateMockProvider(
        string name,
        string description,
        string toolName,
        ToolExecutionResult? executionResult = null)
    {
        var provider = new Mock<IToolProvider>();
        provider.Setup(p => p.Name).Returns(name);
        provider.Setup(p => p.Description).Returns(description);
        provider.Setup(p => p.GetTools()).Returns(new[]
        {
            new LlmToolDefinition
            {
                Name = toolName,
                Description = $"Tool: {toolName}",
                InputSchema = CreateJsonElement(new { type = "object" })
            }
        });

        if (executionResult != null)
        {
            provider.Setup(p => p.ExecuteToolAsync(
                    It.IsAny<string>(),
                    It.IsAny<JsonElement>(),
                    It.IsAny<ToolContext>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(executionResult);
        }

        return provider;
    }

    private static ToolContext CreateToolContext()
    {
        return new ToolContext
        {
            UserId = 123456789,
            GuildId = 987654321,
            ChannelId = 111222333,
            MessageId = 444555666,
            UserRoles = new List<string> { "Member" }
        };
    }

    private static JsonElement CreateJsonElement(object obj)
    {
        var json = JsonSerializer.Serialize(obj);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    #endregion
}
