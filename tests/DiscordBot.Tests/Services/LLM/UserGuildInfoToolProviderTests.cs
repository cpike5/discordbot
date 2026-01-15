using System.Text.Json;
using Discord;
using Discord.WebSocket;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Services.LLM.Implementations;
using DiscordBot.Bot.Services.LLM.Providers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services.LLM;

/// <summary>
/// Unit tests for <see cref="UserGuildInfoToolProvider"/>.
/// Tests cover tool definitions, user profile retrieval, guild info, and role lookup.
/// </summary>
public class UserGuildInfoToolProviderTests
{
    private readonly Mock<ILogger<UserGuildInfoToolProvider>> _mockLogger;
    private readonly Mock<DiscordSocketClient> _mockDiscordClient;
    private readonly Mock<IGuildService> _mockGuildService;
    private readonly UserGuildInfoToolProvider _provider;

    public UserGuildInfoToolProviderTests()
    {
        _mockLogger = new Mock<ILogger<UserGuildInfoToolProvider>>();
        _mockDiscordClient = new Mock<DiscordSocketClient>();
        _mockGuildService = new Mock<IGuildService>();

        _provider = new UserGuildInfoToolProvider(
            _mockLogger.Object,
            _mockDiscordClient.Object,
            _mockGuildService.Object);
    }

    #region Provider Properties Tests

    [Fact]
    public void Name_ReturnsUserGuildInfo()
    {
        _provider.Name.Should().Be("UserGuildInfo");
    }

    [Fact]
    public void Description_IsNotEmpty()
    {
        _provider.Description.Should().NotBeEmpty();
    }

    #endregion

    #region GetTools Tests

    [Fact]
    public void GetTools_ReturnsThreeTools()
    {
        // Act
        var tools = _provider.GetTools().ToList();

        // Assert
        tools.Should().HaveCount(3);
    }

    [Fact]
    public void GetTools_ContainsGetUserProfile()
    {
        // Act
        var tools = _provider.GetTools().ToList();

        // Assert
        tools.Should().Contain(t => t.Name == UserGuildInfoTools.GetUserProfile);
    }

    [Fact]
    public void GetTools_ContainsGetGuildInfo()
    {
        // Act
        var tools = _provider.GetTools().ToList();

        // Assert
        tools.Should().Contain(t => t.Name == UserGuildInfoTools.GetGuildInfo);
    }

    [Fact]
    public void GetTools_ContainsGetUserRoles()
    {
        // Act
        var tools = _provider.GetTools().ToList();

        // Assert
        tools.Should().Contain(t => t.Name == UserGuildInfoTools.GetUserRoles);
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
        var context = CreateToolContext(guildId: guildId);

        // Act
        var result = await _provider.ExecuteToolAsync(
            UserGuildInfoTools.GetGuildInfo, input, context);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Value.GetProperty("name").GetString().Should().Be("Test Guild");
        result.Data!.Value.GetProperty("bot_connected").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task GetGuildInfo_ReturnsErrorForNoGuildContext()
    {
        // Arrange
        var input = CreateJsonElement(new { });
        var context = CreateToolContext(guildId: 0); // No guild context

        // Act
        var result = await _provider.ExecuteToolAsync(
            UserGuildInfoTools.GetGuildInfo, input, context);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("guild context");
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
        var context = CreateToolContext(guildId: guildId);

        // Act
        var result = await _provider.ExecuteToolAsync(
            UserGuildInfoTools.GetGuildInfo, input, context);

        // Assert
        result.Success.Should().BeTrue(); // Tool succeeds but returns error in data
        result.Data!.Value.TryGetProperty("error", out var error).Should().BeTrue();
        error.GetBoolean().Should().BeTrue();
    }

    #endregion

    #region GetUserRoles Tests

    [Fact]
    public async Task GetUserRoles_ReturnsErrorForNoGuildContext()
    {
        // Arrange
        var input = CreateJsonElement(new { });
        var context = CreateToolContext(guildId: 0); // No guild context

        // Act
        var result = await _provider.ExecuteToolAsync(
            UserGuildInfoTools.GetUserRoles, input, context);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("guild context");
    }

    [Fact]
    public async Task GetUserRoles_ReturnsErrorForUnknownGuild()
    {
        // Arrange
        var input = CreateJsonElement(new { });
        var context = CreateToolContext();

        // Act
        var result = await _provider.ExecuteToolAsync(
            UserGuildInfoTools.GetUserRoles, input, context);

        // Assert
        result.Success.Should().BeTrue(); // Tool succeeds but returns error in data
        result.Data!.Value.TryGetProperty("error", out var error).Should().BeTrue();
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

    #region Input Parsing Tests

    [Fact]
    public async Task GetUserProfile_UsesContextUserIdWhenNotProvided()
    {
        // Arrange - Note: The mock Discord client won't return a user, so we expect an error result
        // but the point is that it uses the context user ID
        var input = CreateJsonElement(new { }); // No user_id in input
        var context = CreateToolContext(userId: 123456789);

        // Act
        var result = await _provider.ExecuteToolAsync(
            UserGuildInfoTools.GetUserProfile, input, context);

        // Assert - Tool returns error for unknown user, which is expected
        // The important thing is it doesn't throw and attempted to look up context.UserId
        result.Data!.Value.TryGetProperty("error", out _).Should().BeTrue();
        result.Data!.Value.TryGetProperty("message", out var msg).Should().BeTrue();
        msg.GetString().Should().Contain("123456789");
    }

    [Fact]
    public async Task GetUserProfile_UsesProvidedUserId()
    {
        // Arrange
        var providedUserId = 999888777UL;
        var input = CreateJsonElement(new { user_id = providedUserId.ToString() });
        var context = CreateToolContext(userId: 123456789); // Different from provided

        // Act
        var result = await _provider.ExecuteToolAsync(
            UserGuildInfoTools.GetUserProfile, input, context);

        // Assert - Should try to look up the provided user ID, not the context one
        result.Data!.Value.TryGetProperty("message", out var msg).Should().BeTrue();
        msg.GetString().Should().Contain("999888777");
    }

    #endregion

    #region Helper Methods

    private static ToolContext CreateToolContext(
        ulong userId = 123456789,
        ulong guildId = 987654321,
        ulong channelId = 111222333)
    {
        return new ToolContext
        {
            UserId = userId,
            GuildId = guildId,
            ChannelId = channelId,
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
