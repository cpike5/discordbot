using System.Text.Json;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Services.LLM.Implementations;
using DiscordBot.Infrastructure.Services.LLM.Providers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Services.LLM;

/// <summary>
/// Unit tests for <see cref="DocumentationToolProvider"/>.
/// Tests cover tool definitions, command search, and feature documentation retrieval.
/// </summary>
public class DocumentationToolProviderTests
{
    private readonly Mock<ILogger<DocumentationToolProvider>> _mockLogger;
    private readonly Mock<ICommandMetadataService> _mockCommandMetadataService;
    private readonly Mock<IOptions<AssistantOptions>> _mockAssistantOptions;
    private readonly Mock<IOptions<ApplicationOptions>> _mockApplicationOptions;
    private readonly DocumentationToolProvider _provider;

    public DocumentationToolProviderTests()
    {
        _mockLogger = new Mock<ILogger<DocumentationToolProvider>>();
        _mockCommandMetadataService = new Mock<ICommandMetadataService>();
        _mockAssistantOptions = new Mock<IOptions<AssistantOptions>>();
        _mockApplicationOptions = new Mock<IOptions<ApplicationOptions>>();

        _mockAssistantOptions.Setup(o => o.Value).Returns(new AssistantOptions
        {
            DocumentationBasePath = "docs/articles",
            BaseUrl = "https://test.example.com"
        });

        _mockApplicationOptions.Setup(o => o.Value).Returns(new ApplicationOptions
        {
            BaseUrl = "https://test.example.com"
        });

        _provider = new DocumentationToolProvider(
            _mockLogger.Object,
            _mockCommandMetadataService.Object,
            _mockAssistantOptions.Object,
            _mockApplicationOptions.Object);
    }

    #region Provider Properties Tests

    [Fact]
    public void Name_ReturnsDocumentation()
    {
        _provider.Name.Should().Be("Documentation");
    }

    [Fact]
    public void Description_IsNotEmpty()
    {
        _provider.Description.Should().NotBeEmpty();
    }

    #endregion

    #region GetTools Tests

    [Fact]
    public void GetTools_ReturnsFourTools()
    {
        // Act
        var tools = _provider.GetTools().ToList();

        // Assert
        tools.Should().HaveCount(4);
    }

    [Fact]
    public void GetTools_ContainsGetFeatureDocumentation()
    {
        // Act
        var tools = _provider.GetTools().ToList();

        // Assert
        tools.Should().Contain(t => t.Name == DocumentationTools.GetFeatureDocumentation);
    }

    [Fact]
    public void GetTools_ContainsSearchCommands()
    {
        // Act
        var tools = _provider.GetTools().ToList();

        // Assert
        tools.Should().Contain(t => t.Name == DocumentationTools.SearchCommands);
    }

    [Fact]
    public void GetTools_ContainsGetCommandDetails()
    {
        // Act
        var tools = _provider.GetTools().ToList();

        // Assert
        tools.Should().Contain(t => t.Name == DocumentationTools.GetCommandDetails);
    }

    [Fact]
    public void GetTools_ContainsListFeatures()
    {
        // Act
        var tools = _provider.GetTools().ToList();

        // Assert
        tools.Should().Contain(t => t.Name == DocumentationTools.ListFeatures);
    }

    [Fact]
    public void GetTools_AllToolsHaveValidSchema()
    {
        // Act
        var tools = _provider.GetTools().ToList();

        // Assert
        foreach (var tool in tools)
        {
            tool.InputSchema.ValueKind.Should().Be(JsonValueKind.Object);
            tool.Description.Should().NotBeEmpty();
        }
    }

    #endregion

    #region SearchCommands Tests

    [Fact]
    public async Task SearchCommands_ReturnsMatchingCommands()
    {
        // Arrange
        var modules = new List<CommandModuleDto>
        {
            new()
            {
                Name = "TestModule",
                DisplayName = "Test",
                Commands = new List<CommandInfoDto>
                {
                    new()
                    {
                        Name = "ping",
                        FullName = "ping",
                        Description = "Test ping command",
                        ModuleName = "TestModule",
                        Parameters = new List<CommandParameterDto>(),
                        Preconditions = new List<PreconditionDto>()
                    },
                    new()
                    {
                        Name = "ban",
                        FullName = "ban",
                        Description = "Ban a user",
                        ModuleName = "TestModule",
                        Parameters = new List<CommandParameterDto>(),
                        Preconditions = new List<PreconditionDto>
                        {
                            new() { Name = "RequireAdmin", Configuration = "Requires admin" }
                        }
                    }
                }
            }
        };

        _mockCommandMetadataService
            .Setup(s => s.GetAllModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(modules);

        var input = CreateJsonElement(new { query = "ping", limit = 10 });
        var context = CreateToolContext();

        // Act
        var result = await _provider.ExecuteToolAsync(
            DocumentationTools.SearchCommands, input, context);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Value.GetProperty("results").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SearchCommands_ReturnsMissingParameterError()
    {
        // Arrange - Empty input (no query parameter)
        var input = CreateJsonElement(new { });
        var context = CreateToolContext();

        // Act
        var result = await _provider.ExecuteToolAsync(
            DocumentationTools.SearchCommands, input, context);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("query");
    }

    [Fact]
    public async Task SearchCommands_RespectsLimit()
    {
        // Arrange
        var commands = Enumerable.Range(1, 20).Select(i => new CommandInfoDto
        {
            Name = $"command{i}",
            FullName = $"command{i}",
            Description = "Test command",
            ModuleName = "TestModule",
            Parameters = new List<CommandParameterDto>(),
            Preconditions = new List<PreconditionDto>()
        }).ToList();

        var modules = new List<CommandModuleDto>
        {
            new()
            {
                Name = "TestModule",
                Commands = commands
            }
        };

        _mockCommandMetadataService
            .Setup(s => s.GetAllModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(modules);

        var input = CreateJsonElement(new { query = "command", limit = 5 });
        var context = CreateToolContext();

        // Act
        var result = await _provider.ExecuteToolAsync(
            DocumentationTools.SearchCommands, input, context);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.Value.GetProperty("results").GetArrayLength().Should().Be(5);
        result.Data!.Value.GetProperty("limited_to").GetInt32().Should().Be(5);
    }

    #endregion

    #region GetCommandDetails Tests

    [Fact]
    public async Task GetCommandDetails_ReturnsCommandDetails()
    {
        // Arrange
        var modules = new List<CommandModuleDto>
        {
            new()
            {
                Name = "ReminderModule",
                Commands = new List<CommandInfoDto>
                {
                    new()
                    {
                        Name = "remind",
                        FullName = "remind",
                        Description = "Set a reminder",
                        ModuleName = "ReminderModule",
                        Parameters = new List<CommandParameterDto>
                        {
                            new() { Name = "time", Type = "string", IsRequired = true, Description = "When" },
                            new() { Name = "message", Type = "string", IsRequired = true, Description = "What" }
                        },
                        Preconditions = new List<PreconditionDto>()
                    }
                }
            }
        };

        _mockCommandMetadataService
            .Setup(s => s.GetAllModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(modules);

        var input = CreateJsonElement(new { command_name = "remind" });
        var context = CreateToolContext();

        // Act
        var result = await _provider.ExecuteToolAsync(
            DocumentationTools.GetCommandDetails, input, context);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Value.GetProperty("name").GetString().Should().Be("remind");
        result.Data!.Value.GetProperty("module").GetString().Should().Be("ReminderModule");
    }

    [Fact]
    public async Task GetCommandDetails_ReturnsErrorForUnknownCommand()
    {
        // Arrange
        _mockCommandMetadataService
            .Setup(s => s.GetAllModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CommandModuleDto>());

        var input = CreateJsonElement(new { command_name = "nonexistent" });
        var context = CreateToolContext();

        // Act
        var result = await _provider.ExecuteToolAsync(
            DocumentationTools.GetCommandDetails, input, context);

        // Assert
        result.Success.Should().BeTrue(); // Tool succeeds but returns error in data
        result.Data!.Value.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task GetCommandDetails_StripsLeadingSlash()
    {
        // Arrange
        var modules = new List<CommandModuleDto>
        {
            new()
            {
                Name = "GeneralModule",
                Commands = new List<CommandInfoDto>
                {
                    new()
                    {
                        Name = "ping",
                        FullName = "ping",
                        Description = "Ping the bot",
                        ModuleName = "GeneralModule",
                        Parameters = new List<CommandParameterDto>(),
                        Preconditions = new List<PreconditionDto>()
                    }
                }
            }
        };

        _mockCommandMetadataService
            .Setup(s => s.GetAllModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(modules);

        var input = CreateJsonElement(new { command_name = "/ping" });
        var context = CreateToolContext();

        // Act
        var result = await _provider.ExecuteToolAsync(
            DocumentationTools.GetCommandDetails, input, context);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.Value.GetProperty("name").GetString().Should().Be("ping");
    }

    #endregion

    #region ListFeatures Tests

    [Fact]
    public async Task ListFeatures_ReturnsAllFeatures()
    {
        // Arrange
        var input = CreateJsonElement(new { });
        var context = CreateToolContext();

        // Act
        var result = await _provider.ExecuteToolAsync(
            DocumentationTools.ListFeatures, input, context);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Value.GetProperty("features").GetArrayLength().Should().BeGreaterThan(0);
        result.Data!.Value.GetProperty("total_count").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ListFeatures_IncludesDocumentationUrls()
    {
        // Arrange
        var input = CreateJsonElement(new { });
        var context = CreateToolContext();

        // Act
        var result = await _provider.ExecuteToolAsync(
            DocumentationTools.ListFeatures, input, context);

        // Assert
        result.Success.Should().BeTrue();
        var features = result.Data!.Value.GetProperty("features");
        features[0].TryGetProperty("documentation_url", out var url).Should().BeTrue();
        url.GetString().Should().Contain("https://test.example.com");
    }

    #endregion

    #region UnsupportedTool Tests

    [Fact]
    public async Task ExecuteToolAsync_ThrowsOnUnsupportedTool()
    {
        // Arrange
        var input = CreateJsonElement(new { });
        var context = CreateToolContext();

        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            _provider.ExecuteToolAsync("unsupported_tool", input, context));
    }

    #endregion

    #region Helper Methods

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
