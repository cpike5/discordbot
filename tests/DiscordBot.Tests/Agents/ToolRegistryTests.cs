using System.Text.Json;
using DiscordBot.Agents.Contracts;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Agents.Abstractions;
using DiscordBot.Infrastructure.Services.LLM;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using DiscordBot.Agents;

namespace DiscordBot.Tests.Agents;

/// <summary>
/// Unit tests for <see cref="ToolRegistry"/>.
/// Tests cover provider registration, tool retrieval, tool execution, and composition with
/// <see cref="FilteredToolRegistry"/>.
/// </summary>
public class ToolRegistryTests
{
    private readonly Mock<ILogger<ToolRegistry>> _mockLogger;
    private readonly ToolRegistry _registry;

    public ToolRegistryTests()
    {
        _mockLogger = new Mock<ILogger<ToolRegistry>>();
        _registry = new ToolRegistry(_mockLogger.Object, Enumerable.Empty<IToolProvider>());
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
        _registry.GetEnabledTools().Should().ContainSingle(t => t.Name == "test_tool");
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

    [Fact]
    public void Constructor_AutoRegistersInjectedProviders()
    {
        // Arrange
        var provider1 = CreateMockProvider("Provider1", "Description 1", "tool1");
        var provider2 = CreateMockProvider("Provider2", "Description 2", "tool2");
        var providers = new[] { provider1.Object, provider2.Object };

        // Act
        var registry = new ToolRegistry(_mockLogger.Object, providers);

        // Assert
        registry.IsProviderRegistered("Provider1").Should().BeTrue();
        registry.IsProviderRegistered("Provider2").Should().BeTrue();
        registry.GetEnabledTools().Should().HaveCount(2);
    }

    #endregion

    #region GetEnabledTools Tests

    [Fact]
    public void GetEnabledTools_ReturnsToolsFromEnabledProviders()
    {
        // Arrange
        var provider1 = CreateMockProvider("Provider1", "Description 1", "tool1");
        var provider2 = CreateMockProvider("Provider2", "Description 2", "tool2");

        _registry.RegisterProvider(provider1.Object);
        _registry.RegisterProvider(provider2.Object);

        // Act
        var tools = _registry.GetEnabledTools().ToList();

        // Assert
        tools.Should().HaveCount(2);
        tools.Select(t => t.Name).Should().Contain("tool1");
        tools.Select(t => t.Name).Should().Contain("tool2");
    }


    [Fact]
    public void GetEnabledTools_ReturnsToolsSortedByNameRegardlessOfRegistrationOrder()
    {
        // Tool schemas serialize at position 0 of the request, ahead of the system message, so
        // their order is part of the cached prefix. Registration order is DI order, which a future
        // reshuffle changes silently - the symptom is a tenfold price rise with correct answers.
        _registry.RegisterProvider(CreateMockProvider("Zulu", "Last registered", "zeta_tool").Object);
        _registry.RegisterProvider(CreateMockProvider("Alpha", "First registered", "alpha_tool").Object);
        _registry.RegisterProvider(CreateMockProvider("Mike", "Middle", "Beta_tool").Object);

        var names = _registry.GetEnabledTools().Select(t => t.Name).ToList();

        // Ordinal, so an uppercase name sorts before the lowercase ones rather than by culture.
        names.Should().Equal("Beta_tool", "alpha_tool", "zeta_tool");
    }

    [Fact]
    public void GetEnabledTools_ReturnsTheSameOrderOnEveryCall()
    {
        _registry.RegisterProvider(CreateMockProvider("Zulu", "Z", "zeta_tool").Object);
        _registry.RegisterProvider(CreateMockProvider("Alpha", "A", "alpha_tool").Object);

        var first = _registry.GetEnabledTools().Select(t => t.Name).ToList();
        var second = _registry.GetEnabledTools().Select(t => t.Name).ToList();

        second.Should().Equal(first);
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

    #region Composition With FilteredToolRegistry

    [Fact]
    public void FilteredToolRegistry_AdvertisesOnlyAllowedToolsOfTheInnerRegistry()
    {
        _registry.RegisterProvider(CreateMockProvider("Provider1", "D1", "tool1").Object);
        _registry.RegisterProvider(CreateMockProvider("Provider2", "D2", "tool2").Object);

        var filtered = new FilteredToolRegistry(
            _registry, new HashSet<string> { "tool2" });

        filtered.GetEnabledTools().Select(t => t.Name).Should().Equal("tool2");
    }

    [Fact]
    public async Task FilteredToolRegistry_RefusesADisallowedToolEvenThoughTheInnerRegistryHasIt()
    {
        // Defence in depth: a model that saw the tool in an earlier cached prefix will sometimes
        // call it after the host stopped advertising it.
        var expected = ToolExecutionResult.CreateSuccess(CreateJsonElement(new { success = true }));
        _registry.RegisterProvider(CreateMockProvider("Provider1", "D1", "tool1", expected).Object);

        var filtered = new FilteredToolRegistry(_registry, new HashSet<string> { "tool2" });

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            filtered.ExecuteToolAsync("tool1", CreateJsonElement(new { }), CreateToolContext()));
    }

    [Fact]
    public async Task FilteredToolRegistry_ExecutesAnAllowedToolThroughTheInnerRegistry()
    {
        var expected = ToolExecutionResult.CreateSuccess(CreateJsonElement(new { success = true }));
        _registry.RegisterProvider(CreateMockProvider("Provider1", "D1", "tool1", expected).Object);

        var filtered = new FilteredToolRegistry(_registry, new HashSet<string> { "tool1" });

        var result = await filtered.ExecuteToolAsync(
            "tool1", CreateJsonElement(new { }), CreateToolContext());

        result.Success.Should().BeTrue();
    }

    [Fact]
    public void FilteredToolRegistry_MatchesToolNamesCaseInsensitivelyEvenWithACaseSensitiveSet()
    {
        _registry.RegisterProvider(CreateMockProvider("Provider1", "D1", "tool1").Object);

        // A caller handing in an ordinal set must not accidentally make the filter case-sensitive.
        var filtered = new FilteredToolRegistry(
            _registry, new HashSet<string>(StringComparer.Ordinal) { "TOOL1" });

        filtered.GetEnabledTools().Select(t => t.Name).Should().Equal("tool1");
    }

    [Fact]
    public void FilteredToolRegistry_ReSortsAfterFilteringSoThePrefixOrderIsStable()
    {
        _registry.RegisterProvider(CreateMockProvider("Zulu", "Z", "zeta_tool").Object);
        _registry.RegisterProvider(CreateMockProvider("Alpha", "A", "alpha_tool").Object);
        _registry.RegisterProvider(CreateMockProvider("Mike", "M", "Beta_tool").Object);

        var filtered = new FilteredToolRegistry(
            _registry, new HashSet<string> { "zeta_tool", "Beta_tool" });

        filtered.GetEnabledTools().Select(t => t.Name).Should().Equal("Beta_tool", "zeta_tool");
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
            MessageId = 444555666
        };
    }

    private static JsonElement CreateJsonElement(object obj)
    {
        var json = JsonSerializer.Serialize(obj);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    #endregion
}
