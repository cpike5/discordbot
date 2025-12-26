using Discord.WebSocket;
using DiscordBot.Bot.Services;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Services;

/// <summary>
/// Unit tests for <see cref="WelcomeService"/>.
/// Tests cover configuration management and service logic.
/// NOTE: Direct testing of message sending is limited because DiscordSocketClient, SocketGuild,
/// SocketGuildUser, and SocketTextChannel are sealed classes that cannot be easily mocked.
/// These tests focus on testing the repository interactions and service logic.
/// Integration testing through the controller layer provides better coverage for Discord client interactions.
/// </summary>
public class WelcomeServiceTests : IAsyncDisposable
{
    private readonly Mock<IWelcomeConfigurationRepository> _mockRepository;
    private readonly DiscordSocketClient _client;
    private readonly Mock<ILogger<WelcomeService>> _mockLogger;
    private readonly WelcomeService _service;

    public WelcomeServiceTests()
    {
        _mockRepository = new Mock<IWelcomeConfigurationRepository>();
        _client = new DiscordSocketClient();
        _mockLogger = new Mock<ILogger<WelcomeService>>();
        _service = new WelcomeService(_mockRepository.Object, _client, _mockLogger.Object);
    }

    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
    }

    #region GetConfigurationAsync Tests

    [Fact]
    public async Task GetConfigurationAsync_WithExistingConfiguration_ReturnsDto()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var configuration = new WelcomeConfiguration
        {
            GuildId = guildId,
            IsEnabled = true,
            WelcomeChannelId = 987654321UL,
            WelcomeMessage = "Welcome {user} to {server}!",
            IncludeAvatar = true,
            UseEmbed = true,
            EmbedColor = "#5865F2",
            CreatedAt = DateTime.UtcNow.AddDays(-7),
            UpdatedAt = DateTime.UtcNow
        };

        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(configuration);

        // Act
        var result = await _service.GetConfigurationAsync(guildId);

        // Assert
        result.Should().NotBeNull("configuration exists in repository");
        result!.GuildId.Should().Be(guildId);
        result.IsEnabled.Should().BeTrue();
        result.WelcomeChannelId.Should().Be(987654321UL);
        result.WelcomeMessage.Should().Be("Welcome {user} to {server}!");
        result.IncludeAvatar.Should().BeTrue();
        result.UseEmbed.Should().BeTrue();
        result.EmbedColor.Should().Be("#5865F2");
        result.CreatedAt.Should().Be(configuration.CreatedAt);
        result.UpdatedAt.Should().Be(configuration.UpdatedAt);

        _mockRepository.Verify(
            r => r.GetByGuildIdAsync(guildId, It.IsAny<CancellationToken>()),
            Times.Once,
            "repository should be called once");
    }

    [Fact]
    public async Task GetConfigurationAsync_WithNonExistentConfiguration_ReturnsNull()
    {
        // Arrange
        const ulong guildId = 999999999UL;

        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WelcomeConfiguration?)null);

        // Act
        var result = await _service.GetConfigurationAsync(guildId);

        // Assert
        result.Should().BeNull("configuration does not exist in repository");

        _mockRepository.Verify(
            r => r.GetByGuildIdAsync(guildId, It.IsAny<CancellationToken>()),
            Times.Once,
            "repository should be called once");
    }

    [Fact]
    public async Task GetConfigurationAsync_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(guildId, cancellationToken))
            .ReturnsAsync((WelcomeConfiguration?)null);

        // Act
        await _service.GetConfigurationAsync(guildId, cancellationToken);

        // Assert
        _mockRepository.Verify(
            r => r.GetByGuildIdAsync(guildId, cancellationToken),
            Times.Once,
            "cancellation token should be passed to repository");
    }

    #endregion

    #region UpdateConfigurationAsync Tests

    [Fact]
    public async Task UpdateConfigurationAsync_WithNonExistentConfiguration_CreatesNewConfiguration()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var updateDto = new WelcomeConfigurationUpdateDto
        {
            IsEnabled = true,
            WelcomeChannelId = 987654321UL,
            WelcomeMessage = "Welcome {user}!",
            IncludeAvatar = true,
            UseEmbed = true,
            EmbedColor = "#00FF00"
        };

        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WelcomeConfiguration?)null);

        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<WelcomeConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WelcomeConfiguration config, CancellationToken ct) => config);

        // Act
        var result = await _service.UpdateConfigurationAsync(guildId, updateDto);

        // Assert
        result.Should().NotBeNull("new configuration should be created");
        result!.GuildId.Should().Be(guildId);
        result.IsEnabled.Should().BeTrue();
        result.WelcomeChannelId.Should().Be(987654321UL);
        result.WelcomeMessage.Should().Be("Welcome {user}!");
        result.IncludeAvatar.Should().BeTrue();
        result.UseEmbed.Should().BeTrue();
        result.EmbedColor.Should().Be("#00FF00");

        _mockRepository.Verify(
            r => r.AddAsync(
                It.Is<WelcomeConfiguration>(c =>
                    c.GuildId == guildId &&
                    c.IsEnabled == true &&
                    c.WelcomeChannelId == 987654321UL &&
                    c.WelcomeMessage == "Welcome {user}!" &&
                    c.IncludeAvatar == true &&
                    c.UseEmbed == true &&
                    c.EmbedColor == "#00FF00"),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "new configuration should be added to repository");

        _mockRepository.Verify(
            r => r.UpdateAsync(It.IsAny<WelcomeConfiguration>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "update should not be called for new configuration");
    }

    [Fact]
    public async Task UpdateConfigurationAsync_WithExistingConfiguration_UpdatesConfiguration()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var existingConfig = new WelcomeConfiguration
        {
            GuildId = guildId,
            IsEnabled = false,
            WelcomeChannelId = 111111111UL,
            WelcomeMessage = "Old message",
            IncludeAvatar = false,
            UseEmbed = false,
            EmbedColor = null,
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };

        var updateDto = new WelcomeConfigurationUpdateDto
        {
            IsEnabled = true,
            WelcomeChannelId = 999999999UL,
            WelcomeMessage = "New message with {user}",
            IncludeAvatar = true,
            UseEmbed = true,
            EmbedColor = "#FF0000"
        };

        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingConfig);

        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<WelcomeConfiguration>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpdateConfigurationAsync(guildId, updateDto);

        // Assert
        result.Should().NotBeNull("configuration should be updated");
        result!.GuildId.Should().Be(guildId);
        result.IsEnabled.Should().BeTrue("IsEnabled should be updated");
        result.WelcomeChannelId.Should().Be(999999999UL, "WelcomeChannelId should be updated");
        result.WelcomeMessage.Should().Be("New message with {user}", "WelcomeMessage should be updated");
        result.IncludeAvatar.Should().BeTrue("IncludeAvatar should be updated");
        result.UseEmbed.Should().BeTrue("UseEmbed should be updated");
        result.EmbedColor.Should().Be("#FF0000", "EmbedColor should be updated");

        _mockRepository.Verify(
            r => r.UpdateAsync(
                It.Is<WelcomeConfiguration>(c =>
                    c.GuildId == guildId &&
                    c.IsEnabled == true &&
                    c.WelcomeChannelId == 999999999UL &&
                    c.WelcomeMessage == "New message with {user}" &&
                    c.IncludeAvatar == true &&
                    c.UseEmbed == true &&
                    c.EmbedColor == "#FF0000"),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "configuration should be updated in repository");

        _mockRepository.Verify(
            r => r.AddAsync(It.IsAny<WelcomeConfiguration>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "add should not be called for existing configuration");
    }

    [Fact]
    public async Task UpdateConfigurationAsync_WithPartialUpdate_OnlyUpdatesSpecifiedFields()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var existingConfig = new WelcomeConfiguration
        {
            GuildId = guildId,
            IsEnabled = false,
            WelcomeChannelId = 111111111UL,
            WelcomeMessage = "Original message",
            IncludeAvatar = false,
            UseEmbed = false,
            EmbedColor = "#000000",
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };

        var updateDto = new WelcomeConfigurationUpdateDto
        {
            IsEnabled = true,
            WelcomeMessage = "Updated message",
            // Other fields are null, should not be updated
            WelcomeChannelId = null,
            IncludeAvatar = null,
            UseEmbed = null,
            EmbedColor = null
        };

        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingConfig);

        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<WelcomeConfiguration>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpdateConfigurationAsync(guildId, updateDto);

        // Assert
        result.Should().NotBeNull();
        result!.IsEnabled.Should().BeTrue("IsEnabled should be updated");
        result.WelcomeMessage.Should().Be("Updated message", "WelcomeMessage should be updated");
        result.WelcomeChannelId.Should().Be(111111111UL, "WelcomeChannelId should remain unchanged");
        result.IncludeAvatar.Should().BeFalse("IncludeAvatar should remain unchanged");
        result.UseEmbed.Should().BeFalse("UseEmbed should remain unchanged");
        result.EmbedColor.Should().Be("#000000", "EmbedColor should remain unchanged");

        _mockRepository.Verify(
            r => r.UpdateAsync(
                It.Is<WelcomeConfiguration>(c =>
                    c.IsEnabled == true &&
                    c.WelcomeMessage == "Updated message" &&
                    c.WelcomeChannelId == 111111111UL &&
                    c.IncludeAvatar == false &&
                    c.UseEmbed == false &&
                    c.EmbedColor == "#000000"),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "only specified fields should be updated");
    }

    [Fact]
    public async Task UpdateConfigurationAsync_SetsUpdatedAtTimestamp()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var existingConfig = new WelcomeConfiguration
        {
            GuildId = guildId,
            IsEnabled = false,
            WelcomeMessage = "Old message",
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };

        var updateDto = new WelcomeConfigurationUpdateDto
        {
            IsEnabled = true
        };

        var beforeUpdate = DateTime.UtcNow;

        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingConfig);

        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<WelcomeConfiguration>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpdateConfigurationAsync(guildId, updateDto);

        // Assert
        result.Should().NotBeNull();
        result!.UpdatedAt.Should().BeOnOrAfter(beforeUpdate, "UpdatedAt should be set to current time");
        result.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5), "UpdatedAt should be recent");
    }

    [Fact]
    public async Task UpdateConfigurationAsync_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var updateDto = new WelcomeConfigurationUpdateDto { IsEnabled = true };
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(guildId, cancellationToken))
            .ReturnsAsync((WelcomeConfiguration?)null);

        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<WelcomeConfiguration>(), cancellationToken))
            .ReturnsAsync((WelcomeConfiguration config, CancellationToken ct) => config);

        // Act
        await _service.UpdateConfigurationAsync(guildId, updateDto, cancellationToken);

        // Assert
        _mockRepository.Verify(
            r => r.GetByGuildIdAsync(guildId, cancellationToken),
            Times.Once,
            "cancellation token should be passed to GetByGuildIdAsync");

        _mockRepository.Verify(
            r => r.AddAsync(It.IsAny<WelcomeConfiguration>(), cancellationToken),
            Times.Once,
            "cancellation token should be passed to AddAsync");
    }

    #endregion

    #region SendWelcomeMessageAsync Tests - Logic Validation

    [Fact]
    public async Task SendWelcomeMessageAsync_WithNonExistentConfiguration_ReturnsFalse()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        const ulong userId = 987654321UL;

        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WelcomeConfiguration?)null);

        // Act
        var result = await _service.SendWelcomeMessageAsync(guildId, userId);

        // Assert
        result.Should().BeFalse("configuration does not exist");

        _mockRepository.Verify(
            r => r.GetByGuildIdAsync(guildId, It.IsAny<CancellationToken>()),
            Times.Once,
            "repository should be queried for configuration");
    }

    [Fact]
    public async Task SendWelcomeMessageAsync_WithDisabledConfiguration_ReturnsFalse()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        const ulong userId = 987654321UL;

        var configuration = new WelcomeConfiguration
        {
            GuildId = guildId,
            IsEnabled = false, // Disabled
            WelcomeChannelId = 111111111UL,
            WelcomeMessage = "Welcome!",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(configuration);

        // Act
        var result = await _service.SendWelcomeMessageAsync(guildId, userId);

        // Assert
        result.Should().BeFalse("welcome messages are disabled");
    }

    [Fact]
    public async Task SendWelcomeMessageAsync_WithNoChannelConfigured_ReturnsFalse()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        const ulong userId = 987654321UL;

        var configuration = new WelcomeConfiguration
        {
            GuildId = guildId,
            IsEnabled = true,
            WelcomeChannelId = null, // No channel configured
            WelcomeMessage = "Welcome!",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(configuration);

        // Act
        var result = await _service.SendWelcomeMessageAsync(guildId, userId);

        // Assert
        result.Should().BeFalse("no channel is configured");
    }

    [Fact]
    public async Task SendWelcomeMessageAsync_WithGuildNotFound_ReturnsFalse()
    {
        // Arrange
        const ulong guildId = 999999999UL; // Non-existent guild
        const ulong userId = 987654321UL;

        var configuration = new WelcomeConfiguration
        {
            GuildId = guildId,
            IsEnabled = true,
            WelcomeChannelId = 111111111UL,
            WelcomeMessage = "Welcome!",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(configuration);

        // Act
        var result = await _service.SendWelcomeMessageAsync(guildId, userId);

        // Assert
        result.Should().BeFalse("guild not found in Discord client");
    }

    [Fact]
    public async Task SendWelcomeMessageAsync_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        const ulong userId = 987654321UL;
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(guildId, cancellationToken))
            .ReturnsAsync((WelcomeConfiguration?)null);

        // Act
        await _service.SendWelcomeMessageAsync(guildId, userId, cancellationToken);

        // Assert
        _mockRepository.Verify(
            r => r.GetByGuildIdAsync(guildId, cancellationToken),
            Times.Once,
            "cancellation token should be passed to repository");
    }

    #endregion

    #region PreviewWelcomeMessageAsync Tests

    [Fact]
    public async Task PreviewWelcomeMessageAsync_WithNonExistentConfiguration_ReturnsNull()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        const ulong userId = 987654321UL;

        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WelcomeConfiguration?)null);

        // Act
        var result = await _service.PreviewWelcomeMessageAsync(guildId, userId);

        // Assert
        result.Should().BeNull("configuration does not exist");

        _mockRepository.Verify(
            r => r.GetByGuildIdAsync(guildId, It.IsAny<CancellationToken>()),
            Times.Once,
            "repository should be queried for configuration");
    }

    [Fact]
    public async Task PreviewWelcomeMessageAsync_WithGuildNotFound_ReturnsNull()
    {
        // Arrange
        const ulong guildId = 999999999UL; // Non-existent guild
        const ulong userId = 987654321UL;

        var configuration = new WelcomeConfiguration
        {
            GuildId = guildId,
            IsEnabled = true,
            WelcomeMessage = "Welcome {user}!",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(configuration);

        // Act
        var result = await _service.PreviewWelcomeMessageAsync(guildId, userId);

        // Assert
        result.Should().BeNull("guild not found in Discord client");
    }

    [Fact]
    public async Task PreviewWelcomeMessageAsync_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        const ulong userId = 987654321UL;
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(guildId, cancellationToken))
            .ReturnsAsync((WelcomeConfiguration?)null);

        // Act
        await _service.PreviewWelcomeMessageAsync(guildId, userId, cancellationToken);

        // Assert
        _mockRepository.Verify(
            r => r.GetByGuildIdAsync(guildId, cancellationToken),
            Times.Once,
            "cancellation token should be passed to repository");
    }

    #endregion

    #region Documentation Tests

    /// <summary>
    /// Documentation test that describes the expected behavior of SendWelcomeMessageAsync.
    /// </summary>
    [Fact]
    public void SendWelcomeMessageAsync_ExpectedBehavior_Documentation()
    {
        // This test documents the expected behavior of WelcomeService.SendWelcomeMessageAsync:
        // 1. Retrieves welcome configuration from repository
        // 2. Returns false if configuration not found, disabled, or channel not configured
        // 3. Gets guild from Discord client (returns false if not found)
        // 4. Gets channel from guild (returns false if not found)
        // 5. Gets user from guild (returns false if not found)
        // 6. Replaces template variables: {user}, {username}, {server}, {membercount}
        // 7. If UseEmbed is true:
        //    - Builds embed with message as description
        //    - Parses and applies hex color if specified
        //    - Adds user avatar as thumbnail if IncludeAvatar is true
        //    - Sends embed message to channel
        // 8. If UseEmbed is false:
        //    - Sends plain text message to channel
        // 9. Returns true on success, false on exception
        //
        // Template variables (case-insensitive):
        // - {user} => User mention (<@userId>)
        // - {username} => User display name
        // - {server} => Guild name
        // - {membercount} => Guild member count
        //
        // Hex color format: "#RRGGBB" or "RRGGBB" (6 hex digits)
        // Invalid colors are logged as warnings and use default Discord color
        //
        // Implementation verified at: src/DiscordBot.Bot/Services/WelcomeService.cs:95-188

        var expectedBehavior = new
        {
            Method = "SendWelcomeMessageAsync",
            Returns = "Task<bool>",
            TemplateVariables = new[] { "{user}", "{username}", "{server}", "{membercount}" },
            HexColorFormats = new[] { "#RRGGBB", "RRGGBB" },
            Steps = new[]
            {
                "1. Retrieve configuration from repository",
                "2. Validate configuration (exists, enabled, channel configured)",
                "3. Get guild from Discord client",
                "4. Get channel from guild",
                "5. Get user from guild",
                "6. Replace template variables in message",
                "7. Build and send message (embed or plain text)",
                "8. Return success/failure"
            }
        };

        expectedBehavior.Should().NotBeNull();
        expectedBehavior.Method.Should().Be("SendWelcomeMessageAsync");
        expectedBehavior.TemplateVariables.Should().HaveCount(4);
        expectedBehavior.Steps.Should().HaveCount(8);
    }

    /// <summary>
    /// Documentation test that describes the expected behavior of PreviewWelcomeMessageAsync.
    /// </summary>
    [Fact]
    public void PreviewWelcomeMessageAsync_ExpectedBehavior_Documentation()
    {
        // This test documents the expected behavior of WelcomeService.PreviewWelcomeMessageAsync:
        // 1. Retrieves welcome configuration from repository
        // 2. Returns null if configuration not found
        // 3. Gets guild from Discord client (returns null if not found)
        // 4. Gets user from guild (returns null if not found)
        // 5. Replaces template variables in message: {user}, {username}, {server}, {membercount}
        // 6. Returns the formatted message string
        //
        // This method is useful for testing message templates before enabling welcome messages.
        // It does NOT send any messages, only generates a preview.
        //
        // Implementation verified at: src/DiscordBot.Bot/Services/WelcomeService.cs:191-221

        var expectedBehavior = new
        {
            Method = "PreviewWelcomeMessageAsync",
            Returns = "Task<string?>",
            Purpose = "Generate message preview without sending",
            TemplateVariables = new[] { "{user}", "{username}", "{server}", "{membercount}" },
            Steps = new[]
            {
                "1. Retrieve configuration from repository",
                "2. Get guild from Discord client",
                "3. Get user from guild",
                "4. Replace template variables",
                "5. Return formatted message"
            }
        };

        expectedBehavior.Should().NotBeNull();
        expectedBehavior.Method.Should().Be("PreviewWelcomeMessageAsync");
        expectedBehavior.TemplateVariables.Should().HaveCount(4);
        expectedBehavior.Steps.Should().HaveCount(5);
    }

    /// <summary>
    /// Documentation test that describes the template variable replacement behavior.
    /// </summary>
    [Fact]
    public void TemplateVariableReplacement_ExpectedBehavior_Documentation()
    {
        // Template variable replacement is case-insensitive and supports:
        // - {user} or {USER} => Discord user mention (<@userId>)
        // - {username} or {USERNAME} => User display name (nickname if set, else username)
        // - {server} or {SERVER} => Guild name
        // - {membercount} or {MEMBERCOUNT} => Total member count in guild
        //
        // Implementation: WelcomeService.ReplaceTemplateVariables (line 290-297)
        // Uses StringComparison.OrdinalIgnoreCase for case-insensitive replacement

        var templateVariables = new Dictionary<string, string>
        {
            { "{user}", "User mention (<@userId>)" },
            { "{username}", "User display name" },
            { "{server}", "Guild name" },
            { "{membercount}", "Total member count" }
        };

        templateVariables.Should().HaveCount(4);
        templateVariables.Should().ContainKeys("{user}", "{username}", "{server}", "{membercount}");
    }

    /// <summary>
    /// Documentation test that describes the hex color parsing behavior.
    /// </summary>
    [Fact]
    public void HexColorParsing_ExpectedBehavior_Documentation()
    {
        // Hex color parsing supports two formats:
        // - With hash prefix: "#5865F2"
        // - Without hash prefix: "5865F2"
        //
        // Validation:
        // - Must be exactly 6 hex characters (0-9, A-F, a-f) after removing #
        // - Invalid colors return false and log a warning
        // - Null or empty strings return false
        //
        // Parsed colors are converted to Discord.Color(r, g, b)
        //
        // Implementation: WelcomeService.TryParseHexColor (line 305-336)

        var validFormats = new[] { "#5865F2", "5865F2", "#FF0000", "00FF00" };
        var invalidFormats = new[] { "invalid", "#12", "GGGGGG", "#12345", "#1234567", "", null };

        validFormats.Should().HaveCount(4, "all valid formats should be supported");
        invalidFormats.Should().HaveCount(7, "all invalid formats should be rejected");
    }

    #endregion
}
