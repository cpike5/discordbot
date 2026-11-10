using System.Text.Json;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using cpike.Agent;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Services.LLM.Tools;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Services.LLM;

/// <summary>
/// Unit tests for the documentation tools and <see cref="DocumentationLibrary"/>.
/// Tests cover tool definitions, command search, and feature documentation retrieval.
/// </summary>
public class DocumentationToolsTests
{
    private readonly Mock<ICommandMetadataService> _mockCommandMetadataService;
    private readonly Mock<IOptions<AssistantOptions>> _mockAssistantOptions;
    private readonly Mock<IOptions<ApplicationOptions>> _mockApplicationOptions;
    private readonly GetFeatureDocumentationTool _getFeatureDocumentation;
    private readonly SearchCommandsTool _searchCommands;
    private readonly GetCommandDetailsTool _getCommandDetails;
    private readonly ListFeaturesTool _listFeatures;

    public DocumentationToolsTests()
    {
        _mockCommandMetadataService = new Mock<ICommandMetadataService>();
        _mockAssistantOptions = new Mock<IOptions<AssistantOptions>>();
        _mockApplicationOptions = new Mock<IOptions<ApplicationOptions>>();

        _mockAssistantOptions.Setup(o => o.Value).Returns(new AssistantOptions
        {
            Tools = new()
            {
                DocumentationBasePath = "docs/articles"
            },
            BaseUrl = "https://test.example.com"
        });

        _mockApplicationOptions.Setup(o => o.Value).Returns(new ApplicationOptions
        {
            BaseUrl = "https://test.example.com"
        });

        var library = new DocumentationLibrary(
            Mock.Of<ILogger<DocumentationLibrary>>(),
            _mockCommandMetadataService.Object,
            _mockAssistantOptions.Object,
            _mockApplicationOptions.Object);
        var run = TestRunContext.Create(123456789, 987654321);

        _getFeatureDocumentation = new GetFeatureDocumentationTool(library, run);
        _searchCommands = new SearchCommandsTool(library);
        _getCommandDetails = new GetCommandDetailsTool(library);
        _listFeatures = new ListFeaturesTool(library);
    }

    #region Tool Definition Tests

    [Fact]
    public void ThereAreFourTools_WithTheirCataloguedNames()
    {
        AllTools().Select(t => t.Definition.Name).Should().BeEquivalentTo(
            "get_feature_documentation", "search_commands", "get_command_details", "list_features");
    }

    [Fact]
    public void NoDocumentationToolRequiresEdit()
    {
        AllTools().Should().OnlyContain(t => !t.Definition.RequiresEdit);
    }

    [Fact]
    public void AllToolsHaveValidSchema()
    {
        foreach (var tool in AllTools())
        {
            JsonSerializer.SerializeToElement(tool.Definition.InputSchema).ValueKind.Should().Be(JsonValueKind.Object);
            tool.Definition.Description.Should().NotBeEmpty();
        }
    }

    private IEnumerable<IAgentTool> AllTools() =>
        [_getFeatureDocumentation, _searchCommands, _getCommandDetails, _listFeatures];

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

        // Act
        var result = await Invoke(_searchCommands, input);

        // Assert
        Parse(result).GetProperty("results").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SearchCommands_ReturnsMissingParameterError()
    {
        // Arrange - Empty input (no query parameter)
        var input = CreateJsonElement(new { });

        // Act
        var result = await Invoke(_searchCommands, input);

        // Assert
        Parse(result).GetProperty("error").GetString().Should().Contain("query");
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

        // Act
        var result = await Invoke(_searchCommands, input);

        // Assert
        Parse(result).GetProperty("results").GetArrayLength().Should().Be(5);
        Parse(result).GetProperty("limited_to").GetInt32().Should().Be(5);
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

        // Act
        var result = await Invoke(_getCommandDetails, input);

        // Assert
        Parse(result).GetProperty("name").GetString().Should().Be("remind");
        Parse(result).GetProperty("module").GetString().Should().Be("ReminderModule");
    }

    [Fact]
    public async Task GetCommandDetails_ReturnsErrorForUnknownCommand()
    {
        // Arrange
        _mockCommandMetadataService
            .Setup(s => s.GetAllModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CommandModuleDto>());

        var input = CreateJsonElement(new { command_name = "nonexistent" });

        // Act
        var result = await Invoke(_getCommandDetails, input);

        // Assert
        Parse(result).TryGetProperty("error", out var error).Should().BeTrue();
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

        // Act
        var result = await Invoke(_getCommandDetails, input);

        // Assert
        Parse(result).GetProperty("name").GetString().Should().Be("ping");
    }

    #endregion

    #region ListFeatures Tests

    [Fact]
    public async Task ListFeatures_ReturnsAllFeatures()
    {
        // Arrange
        var input = CreateJsonElement(new { });

        // Act
        var result = await Invoke(_listFeatures, input);

        // Assert
        Parse(result).GetProperty("features").GetArrayLength().Should().BeGreaterThan(0);
        Parse(result).GetProperty("total_count").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ListFeatures_IncludesDocumentationUrls()
    {
        // Arrange
        var input = CreateJsonElement(new { });

        // Act
        var result = await Invoke(_listFeatures, input);

        // Assert
        var features = Parse(result).GetProperty("features");
        features[0].TryGetProperty("documentation_url", out var url).Should().BeTrue();
        url.GetString().Should().Contain("https://test.example.com");
    }

    #endregion

    #region Helper Methods

    private static Task<string> Invoke(IAgentTool tool, JsonElement input) =>
        tool.InvokeAsync(
            input.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone()),
            CancellationToken.None);

    private static JsonElement Parse(string result) => JsonDocument.Parse(result).RootElement.Clone();

    private static JsonElement CreateJsonElement(object obj)
    {
        var json = JsonSerializer.Serialize(obj);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    #endregion
}
