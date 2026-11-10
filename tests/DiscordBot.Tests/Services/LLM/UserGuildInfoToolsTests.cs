using System.Text.Json;
using Discord;
using Discord.WebSocket;
using DiscordBot.Core.DTOs;
using cpike.Agent;
using DiscordBot.Core.Interfaces;
using DiscordBot.Bot.Services.LLM.Tools;
using DiscordBot.Infrastructure.Services.LLM.Tooling;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services.LLM;

/// <summary>
/// Unit tests for the user and guild information tools.
/// Tests cover tool definitions, user profile retrieval, guild info, and role lookup.
/// </summary>
public class UserGuildInfoToolsTests
{
    private readonly Mock<DiscordSocketClient> _mockDiscordClient;
    private readonly Mock<IGuildService> _mockGuildService;

    public UserGuildInfoToolsTests()
    {
        _mockDiscordClient = new Mock<DiscordSocketClient>();
        _mockGuildService = new Mock<IGuildService>();
    }

    private GetUserProfileTool Profile(DiscordRunContext run) =>
        new(_mockDiscordClient.Object, run, Mock.Of<ILogger<GetUserProfileTool>>());

    private GetGuildInfoTool GuildInfo(DiscordRunContext run) =>
        new(_mockDiscordClient.Object, _mockGuildService.Object, run, Mock.Of<ILogger<GetGuildInfoTool>>());

    private GetUserRolesTool Roles(DiscordRunContext run) =>
        new(_mockDiscordClient.Object, run, Mock.Of<ILogger<GetUserRolesTool>>());

    #region Tool Definition Tests

    [Fact]
    public void ThereAreThreeReadOnlyTools_WithTheirCataloguedNames()
    {
        var run = TestRunContext.Create();
        IAgentTool[] tools = [Profile(run), GuildInfo(run), Roles(run)];

        tools.Select(t => t.Definition.Name).Should().BeEquivalentTo(
            "get_user_profile", "get_guild_info", "get_user_roles");
        tools.Should().OnlyContain(t => !t.Definition.RequiresEdit);
    }

    [Fact]
    public void AllToolsHaveValidSchema()
    {
        var run = TestRunContext.Create();
        IAgentTool[] tools = [Profile(run), GuildInfo(run), Roles(run)];

        foreach (var tool in tools)
        {
            JsonSerializer.SerializeToElement(tool.Definition.InputSchema).ValueKind.Should().Be(JsonValueKind.Object);
            tool.Definition.Description.Should().NotBeEmpty();
        }
    }

    #endregion

    #region GetGuildInfo Tests (using database fallback)

    [Fact]
    public async Task GetGuildInfo_ReturnsGuildFromDatabase_WhenNotConnected()
    {
        // Arrange
        var guildId = 987654321UL;
        var guildDto = new GuildDto
        {
            Id = guildId,
            Name = "Test Guild",
            IconUrl = "https://cdn.example.com/icon.png",
            MemberCount = 100,
            IsActive = true
        };

        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guildDto);

        var input = CreateJsonElement(new { });
        var context = TestRunContext.Create(userId: 123456789, guildId: guildId);

        // Act
        var result = await Invoke(GuildInfo(context), input);

        // Assert
        Parse(result).GetProperty("name").GetString().Should().Be("Test Guild");
        Parse(result).GetProperty("bot_connected").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task GetGuildInfo_ReturnsErrorForNoGuildContext()
    {
        // Arrange
        var input = CreateJsonElement(new { });
        var context = TestRunContext.Create(userId: 123456789, guildId: 0); // No guild context

        // Act
        var result = await Invoke(GuildInfo(context), input);

        // Assert
        Parse(result).GetProperty("error").GetString().Should().Contain("guild context");
    }

    [Fact]
    public async Task GetGuildInfo_ReturnsErrorForUnknownGuild()
    {
        // Arrange
        var guildId = 987654321UL;

        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildDto?)null);

        var input = CreateJsonElement(new { });
        var context = TestRunContext.Create(userId: 123456789, guildId: guildId);

        // Act
        var result = await Invoke(GuildInfo(context), input);

        // Assert
        Parse(result).TryGetProperty("error", out var error).Should().BeTrue();
        error.GetBoolean().Should().BeTrue();
    }

    #endregion

    #region GetUserRoles Tests

    [Fact]
    public async Task GetUserRoles_ReturnsErrorForNoGuildContext()
    {
        // Arrange
        var input = CreateJsonElement(new { });
        var context = TestRunContext.Create(userId: 123456789, guildId: 0); // No guild context

        // Act
        var result = await Invoke(Roles(context), input);

        // Assert
        Parse(result).GetProperty("error").GetString().Should().Contain("guild context");
    }

    [Fact]
    public async Task GetUserRoles_ReturnsErrorForUnknownGuild()
    {
        // Arrange
        var input = CreateJsonElement(new { });
        var context = TestRunContext.Create(userId: 123456789, guildId: 987654321);

        // Act
        var result = await Invoke(Roles(context), input);

        // Assert
        Parse(result).TryGetProperty("error", out var error).Should().BeTrue();
    }

    #endregion

    #region Input Parsing Tests

    [Fact]
    public async Task GetUserProfile_UsesContextUserIdWhenNotProvided()
    {
        // Arrange - Note: The mock Discord client won't return a user, so we expect an error result
        // but the point is that it uses the context user ID
        var input = CreateJsonElement(new { }); // No user_id in input
        var context = TestRunContext.Create(userId: 123456789, guildId: 987654321);

        // Act
        var result = await Invoke(Profile(context), input);

        // Assert - Tool returns error for unknown user, which is expected
        // The important thing is it doesn't throw and attempted to look up context.UserId
        Parse(result).TryGetProperty("error", out _).Should().BeTrue();
        Parse(result).TryGetProperty("message", out var msg).Should().BeTrue();
        msg.GetString().Should().Contain("123456789");
    }

    [Fact]
    public async Task GetUserProfile_UsesProvidedUserId()
    {
        // Arrange
        var providedUserId = 999888777UL;
        var input = CreateJsonElement(new { user_id = providedUserId.ToString() });
        var context = TestRunContext.Create(userId: 123456789, guildId: 987654321); // Different from provided

        // Act
        var result = await Invoke(Profile(context), input);

        // Assert - Should try to look up the provided user ID, not the context one
        Parse(result).TryGetProperty("message", out var msg).Should().BeTrue();
        msg.GetString().Should().Contain("999888777");
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
