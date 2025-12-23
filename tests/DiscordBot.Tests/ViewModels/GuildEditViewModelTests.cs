using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using FluentAssertions;
using System.Text.Json;

namespace DiscordBot.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="GuildEditViewModel"/>.
/// </summary>
public class GuildEditViewModelTests
{
    #region FromDto Tests

    [Fact]
    public void FromDto_WithValidSettings_PopulatesAllProperties()
    {
        // Arrange
        var sampleSettings = @"{
            ""welcomeChannel"": ""general"",
            ""logChannel"": ""bot-logs"",
            ""autoModEnabled"": true,
            ""welcomeMessagesEnabled"": true,
            ""leaveMessagesEnabled"": false,
            ""moderationAlertsEnabled"": true,
            ""commandLoggingEnabled"": true
        }";

        var sampleDto = new GuildDto
        {
            Id = 123456789012345678,
            Name = "Test Guild",
            IconUrl = "https://cdn.discordapp.com/icons/123/abc.png",
            IsActive = true,
            Prefix = "!",
            Settings = sampleSettings
        };

        // Act
        var viewModel = GuildEditViewModel.FromDto(sampleDto);

        // Assert
        viewModel.Should().NotBeNull();

        // Guild identity properties
        viewModel.Id.Should().Be(123456789012345678, "Id should be copied from DTO");
        viewModel.Name.Should().Be("Test Guild", "Name should be copied from DTO");
        viewModel.IconUrl.Should().Be("https://cdn.discordapp.com/icons/123/abc.png", "IconUrl should be copied from DTO");
        viewModel.IsActive.Should().BeTrue("IsActive should be copied from DTO");
        viewModel.Prefix.Should().Be("!", "Prefix should be copied from DTO");

        // Notification settings
        viewModel.WelcomeMessagesEnabled.Should().BeTrue("WelcomeMessagesEnabled should be parsed from settings JSON");
        viewModel.LeaveMessagesEnabled.Should().BeFalse("LeaveMessagesEnabled should be parsed from settings JSON");
        viewModel.ModerationAlertsEnabled.Should().BeTrue("ModerationAlertsEnabled should be parsed from settings JSON");
        viewModel.CommandLoggingEnabled.Should().BeTrue("CommandLoggingEnabled should be parsed from settings JSON");

        // Advanced settings
        viewModel.WelcomeChannel.Should().Be("general", "WelcomeChannel should be parsed from settings JSON");
        viewModel.LogChannel.Should().Be("bot-logs", "LogChannel should be parsed from settings JSON");
        viewModel.AutoModEnabled.Should().BeTrue("AutoModEnabled should be parsed from settings JSON");
    }

    [Fact]
    public void FromDto_WithNullSettings_ReturnsDefaults()
    {
        // Arrange
        var dto = new GuildDto
        {
            Id = 987654321098765432,
            Name = "No Settings Guild",
            IconUrl = null,
            IsActive = true,
            Prefix = null,
            Settings = null
        };

        // Act
        var viewModel = GuildEditViewModel.FromDto(dto);

        // Assert
        viewModel.Should().NotBeNull();

        // Guild identity properties
        viewModel.Id.Should().Be(987654321098765432, "Id should be copied from DTO");
        viewModel.Name.Should().Be("No Settings Guild", "Name should be copied from DTO");
        viewModel.IconUrl.Should().BeNull("IconUrl should be null when DTO has null");
        viewModel.IsActive.Should().BeTrue("IsActive should be copied from DTO");
        viewModel.Prefix.Should().BeNull("Prefix should be null when DTO has null");

        // All settings should have default values
        viewModel.WelcomeMessagesEnabled.Should().BeFalse("WelcomeMessagesEnabled should default to false");
        viewModel.LeaveMessagesEnabled.Should().BeFalse("LeaveMessagesEnabled should default to false");
        viewModel.ModerationAlertsEnabled.Should().BeFalse("ModerationAlertsEnabled should default to false");
        viewModel.CommandLoggingEnabled.Should().BeFalse("CommandLoggingEnabled should default to false");
        viewModel.WelcomeChannel.Should().BeNull("WelcomeChannel should default to null");
        viewModel.LogChannel.Should().BeNull("LogChannel should default to null");
        viewModel.AutoModEnabled.Should().BeFalse("AutoModEnabled should default to false");
    }

    [Fact]
    public void FromDto_WithMalformedJson_ReturnsDefaults()
    {
        // Arrange
        var dto = new GuildDto
        {
            Id = 111222333444555666,
            Name = "Broken Settings Guild",
            IconUrl = "https://cdn.discordapp.com/icons/111/xyz.png",
            IsActive = false,
            Prefix = "?",
            Settings = "{ this is not valid json }"
        };

        // Act
        var viewModel = GuildEditViewModel.FromDto(dto);

        // Assert
        viewModel.Should().NotBeNull();

        // Guild identity properties should still be copied
        viewModel.Id.Should().Be(111222333444555666, "Id should be copied from DTO");
        viewModel.Name.Should().Be("Broken Settings Guild", "Name should be copied from DTO");
        viewModel.IconUrl.Should().Be("https://cdn.discordapp.com/icons/111/xyz.png", "IconUrl should be copied from DTO");
        viewModel.IsActive.Should().BeFalse("IsActive should be copied from DTO");
        viewModel.Prefix.Should().Be("?", "Prefix should be copied from DTO");

        // Settings should gracefully default when JSON is malformed
        viewModel.WelcomeMessagesEnabled.Should().BeFalse("WelcomeMessagesEnabled should default to false on malformed JSON");
        viewModel.LeaveMessagesEnabled.Should().BeFalse("LeaveMessagesEnabled should default to false on malformed JSON");
        viewModel.ModerationAlertsEnabled.Should().BeFalse("ModerationAlertsEnabled should default to false on malformed JSON");
        viewModel.CommandLoggingEnabled.Should().BeFalse("CommandLoggingEnabled should default to false on malformed JSON");
        viewModel.WelcomeChannel.Should().BeNull("WelcomeChannel should default to null on malformed JSON");
        viewModel.LogChannel.Should().BeNull("LogChannel should default to null on malformed JSON");
        viewModel.AutoModEnabled.Should().BeFalse("AutoModEnabled should default to false on malformed JSON");
    }

    [Fact]
    public void FromDto_CopiesGuildIdentityProperties()
    {
        // Arrange
        var dto = new GuildDto
        {
            Id = 555666777888999000,
            Name = "Identity Test Guild",
            IconUrl = "https://cdn.discordapp.com/icons/555/def.png",
            IsActive = true,
            Prefix = ">>",
            Settings = "{}"
        };

        // Act
        var viewModel = GuildEditViewModel.FromDto(dto);

        // Assert
        viewModel.Id.Should().Be(555666777888999000, "Id should be copied correctly");
        viewModel.Name.Should().Be("Identity Test Guild", "Name should be copied correctly");
        viewModel.IconUrl.Should().Be("https://cdn.discordapp.com/icons/555/def.png", "IconUrl should be copied correctly");
        viewModel.IsActive.Should().BeTrue("IsActive should be copied correctly");
        viewModel.Prefix.Should().Be(">>", "Prefix should be copied correctly");
    }

    [Fact]
    public void FromDto_WithEmptySettings_ReturnsDefaults()
    {
        // Arrange
        var dto = new GuildDto
        {
            Id = 777888999000111222,
            Name = "Empty Settings Guild",
            IconUrl = null,
            IsActive = true,
            Prefix = "!",
            Settings = string.Empty
        };

        // Act
        var viewModel = GuildEditViewModel.FromDto(dto);

        // Assert
        viewModel.Should().NotBeNull();
        viewModel.WelcomeMessagesEnabled.Should().BeFalse("empty settings should default to false");
        viewModel.LeaveMessagesEnabled.Should().BeFalse("empty settings should default to false");
        viewModel.ModerationAlertsEnabled.Should().BeFalse("empty settings should default to false");
        viewModel.CommandLoggingEnabled.Should().BeFalse("empty settings should default to false");
        viewModel.WelcomeChannel.Should().BeNull("empty settings should default to null");
        viewModel.LogChannel.Should().BeNull("empty settings should default to null");
        viewModel.AutoModEnabled.Should().BeFalse("empty settings should default to false");
    }

    [Fact]
    public void FromDto_WithPartialSettings_ParsesAvailableFields()
    {
        // Arrange - JSON with only some fields
        var partialSettings = @"{
            ""welcomeChannel"": ""welcome-here"",
            ""autoModEnabled"": true
        }";

        var dto = new GuildDto
        {
            Id = 999000111222333444,
            Name = "Partial Settings Guild",
            IconUrl = null,
            IsActive = true,
            Prefix = "!",
            Settings = partialSettings
        };

        // Act
        var viewModel = GuildEditViewModel.FromDto(dto);

        // Assert
        viewModel.WelcomeChannel.Should().Be("welcome-here", "WelcomeChannel should be parsed");
        viewModel.AutoModEnabled.Should().BeTrue("AutoModEnabled should be parsed");

        // Missing fields should have defaults
        viewModel.LogChannel.Should().BeNull("LogChannel should be null when not in JSON");
        viewModel.WelcomeMessagesEnabled.Should().BeFalse("WelcomeMessagesEnabled should default to false when not in JSON");
        viewModel.LeaveMessagesEnabled.Should().BeFalse("LeaveMessagesEnabled should default to false when not in JSON");
        viewModel.ModerationAlertsEnabled.Should().BeFalse("ModerationAlertsEnabled should default to false when not in JSON");
        viewModel.CommandLoggingEnabled.Should().BeFalse("CommandLoggingEnabled should default to false when not in JSON");
    }

    #endregion

    #region ToSettingsJson Tests

    [Fact]
    public void ToSettingsJson_SerializesAllProperties()
    {
        // Arrange
        var viewModel = new GuildEditViewModel
        {
            WelcomeChannel = "general",
            LogChannel = "bot-logs",
            AutoModEnabled = true,
            WelcomeMessagesEnabled = true,
            LeaveMessagesEnabled = false,
            ModerationAlertsEnabled = true,
            CommandLoggingEnabled = true
        };

        // Act
        var json = viewModel.ToSettingsJson();

        // Assert
        json.Should().NotBeNullOrEmpty("ToSettingsJson should return valid JSON");
        json.Should().Contain("\"welcomeChannel\":\"general\"", "WelcomeChannel should be serialized");
        json.Should().Contain("\"logChannel\":\"bot-logs\"", "LogChannel should be serialized");
        json.Should().Contain("\"autoModEnabled\":true", "AutoModEnabled should be serialized");
        json.Should().Contain("\"welcomeMessagesEnabled\":true", "WelcomeMessagesEnabled should be serialized");
        json.Should().Contain("\"leaveMessagesEnabled\":false", "LeaveMessagesEnabled should be serialized");
        json.Should().Contain("\"moderationAlertsEnabled\":true", "ModerationAlertsEnabled should be serialized");
        json.Should().Contain("\"commandLoggingEnabled\":true", "CommandLoggingEnabled should be serialized");
    }

    [Fact]
    public void ToSettingsJson_UsesCamelCase()
    {
        // Arrange
        var viewModel = new GuildEditViewModel
        {
            WelcomeChannel = "test",
            LogChannel = "logs",
            AutoModEnabled = true,
            WelcomeMessagesEnabled = true,
            LeaveMessagesEnabled = true,
            ModerationAlertsEnabled = true,
            CommandLoggingEnabled = true
        };

        // Act
        var json = viewModel.ToSettingsJson();

        // Assert
        json.Should().Contain("welcomeChannel", "should use camelCase for WelcomeChannel");
        json.Should().Contain("logChannel", "should use camelCase for LogChannel");
        json.Should().Contain("autoModEnabled", "should use camelCase for AutoModEnabled");
        json.Should().Contain("welcomeMessagesEnabled", "should use camelCase for WelcomeMessagesEnabled");
        json.Should().Contain("leaveMessagesEnabled", "should use camelCase for LeaveMessagesEnabled");
        json.Should().Contain("moderationAlertsEnabled", "should use camelCase for ModerationAlertsEnabled");
        json.Should().Contain("commandLoggingEnabled", "should use camelCase for CommandLoggingEnabled");

        // Should NOT contain PascalCase
        json.Should().NotContain("WelcomeChannel", "should not use PascalCase");
        json.Should().NotContain("LogChannel", "should not use PascalCase");
        json.Should().NotContain("AutoModEnabled", "should not use PascalCase");
    }

    [Fact]
    public void ToSettingsJson_WithDefaultValues_SerializesCorrectly()
    {
        // Arrange - All defaults (false/null)
        var viewModel = new GuildEditViewModel
        {
            WelcomeChannel = null,
            LogChannel = null,
            AutoModEnabled = false,
            WelcomeMessagesEnabled = false,
            LeaveMessagesEnabled = false,
            ModerationAlertsEnabled = false,
            CommandLoggingEnabled = false
        };

        // Act
        var json = viewModel.ToSettingsJson();

        // Assert
        json.Should().NotBeNullOrEmpty("ToSettingsJson should return valid JSON even with defaults");
        json.Should().Contain("\"welcomeChannel\":null", "null WelcomeChannel should serialize to null");
        json.Should().Contain("\"logChannel\":null", "null LogChannel should serialize to null");
        json.Should().Contain("\"autoModEnabled\":false", "false AutoModEnabled should serialize to false");
        json.Should().Contain("\"welcomeMessagesEnabled\":false", "false WelcomeMessagesEnabled should serialize to false");
        json.Should().Contain("\"leaveMessagesEnabled\":false", "false LeaveMessagesEnabled should serialize to false");
        json.Should().Contain("\"moderationAlertsEnabled\":false", "false ModerationAlertsEnabled should serialize to false");
        json.Should().Contain("\"commandLoggingEnabled\":false", "false CommandLoggingEnabled should serialize to false");
    }

    [Fact]
    public void ToSettingsJson_ProducesValidJson()
    {
        // Arrange
        var viewModel = new GuildEditViewModel
        {
            WelcomeChannel = "welcome",
            LogChannel = "logs",
            AutoModEnabled = true,
            WelcomeMessagesEnabled = true,
            LeaveMessagesEnabled = false,
            ModerationAlertsEnabled = true,
            CommandLoggingEnabled = false
        };

        // Act
        var json = viewModel.ToSettingsJson();

        // Assert - Should be deserializable without error
        var deserializeAction = () => JsonDocument.Parse(json);
        deserializeAction.Should().NotThrow("ToSettingsJson should produce valid JSON");

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("welcomeChannel").GetString().Should().Be("welcome");
        doc.RootElement.GetProperty("logChannel").GetString().Should().Be("logs");
        doc.RootElement.GetProperty("autoModEnabled").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("welcomeMessagesEnabled").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("leaveMessagesEnabled").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("moderationAlertsEnabled").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("commandLoggingEnabled").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public void ToSettingsJson_WithEmptyStrings_SerializesEmptyStrings()
    {
        // Arrange
        var viewModel = new GuildEditViewModel
        {
            WelcomeChannel = "",
            LogChannel = "",
            AutoModEnabled = false,
            WelcomeMessagesEnabled = false,
            LeaveMessagesEnabled = false,
            ModerationAlertsEnabled = false,
            CommandLoggingEnabled = false
        };

        // Act
        var json = viewModel.ToSettingsJson();

        // Assert
        json.Should().Contain("\"welcomeChannel\":\"\"", "empty string should serialize as empty string");
        json.Should().Contain("\"logChannel\":\"\"", "empty string should serialize as empty string");
    }

    #endregion

    #region Roundtrip Tests

    [Fact]
    public void Roundtrip_FromDtoAndToJson_PreservesValues()
    {
        // Arrange
        var originalSettings = @"{
            ""welcomeChannel"": ""general"",
            ""logChannel"": ""bot-logs"",
            ""autoModEnabled"": true,
            ""welcomeMessagesEnabled"": true,
            ""leaveMessagesEnabled"": false,
            ""moderationAlertsEnabled"": true,
            ""commandLoggingEnabled"": true
        }";

        var originalDto = new GuildDto
        {
            Id = 123456789012345678,
            Name = "Test Guild",
            IconUrl = "https://cdn.discordapp.com/icons/123/abc.png",
            IsActive = true,
            Prefix = "!",
            Settings = originalSettings
        };

        // Act - Convert DTO to ViewModel, then back to JSON
        var viewModel = GuildEditViewModel.FromDto(originalDto);
        var resultJson = viewModel.ToSettingsJson();

        // Parse both JSON strings for comparison
        using var originalDoc = JsonDocument.Parse(originalSettings);
        using var resultDoc = JsonDocument.Parse(resultJson);

        // Assert - All values should be preserved
        resultDoc.RootElement.GetProperty("welcomeChannel").GetString()
            .Should().Be(originalDoc.RootElement.GetProperty("welcomeChannel").GetString(),
                "WelcomeChannel should be preserved in roundtrip");

        resultDoc.RootElement.GetProperty("logChannel").GetString()
            .Should().Be(originalDoc.RootElement.GetProperty("logChannel").GetString(),
                "LogChannel should be preserved in roundtrip");

        resultDoc.RootElement.GetProperty("autoModEnabled").GetBoolean()
            .Should().Be(originalDoc.RootElement.GetProperty("autoModEnabled").GetBoolean(),
                "AutoModEnabled should be preserved in roundtrip");

        resultDoc.RootElement.GetProperty("welcomeMessagesEnabled").GetBoolean()
            .Should().Be(originalDoc.RootElement.GetProperty("welcomeMessagesEnabled").GetBoolean(),
                "WelcomeMessagesEnabled should be preserved in roundtrip");

        resultDoc.RootElement.GetProperty("leaveMessagesEnabled").GetBoolean()
            .Should().Be(originalDoc.RootElement.GetProperty("leaveMessagesEnabled").GetBoolean(),
                "LeaveMessagesEnabled should be preserved in roundtrip");

        resultDoc.RootElement.GetProperty("moderationAlertsEnabled").GetBoolean()
            .Should().Be(originalDoc.RootElement.GetProperty("moderationAlertsEnabled").GetBoolean(),
                "ModerationAlertsEnabled should be preserved in roundtrip");

        resultDoc.RootElement.GetProperty("commandLoggingEnabled").GetBoolean()
            .Should().Be(originalDoc.RootElement.GetProperty("commandLoggingEnabled").GetBoolean(),
                "CommandLoggingEnabled should be preserved in roundtrip");
    }

    [Fact]
    public void Roundtrip_WithDefaultValues_PreservesDefaults()
    {
        // Arrange - DTO with null settings
        var dto = new GuildDto
        {
            Id = 987654321098765432,
            Name = "Default Guild",
            IconUrl = null,
            IsActive = true,
            Prefix = null,
            Settings = null
        };

        // Act - Convert DTO to ViewModel, then back to JSON
        var viewModel = GuildEditViewModel.FromDto(dto);
        var resultJson = viewModel.ToSettingsJson();

        // Parse result JSON
        using var resultDoc = JsonDocument.Parse(resultJson);

        // Assert - All values should be defaults
        resultDoc.RootElement.GetProperty("welcomeChannel").ValueKind.Should().Be(JsonValueKind.Null,
            "WelcomeChannel should be null");
        resultDoc.RootElement.GetProperty("logChannel").ValueKind.Should().Be(JsonValueKind.Null,
            "LogChannel should be null");
        resultDoc.RootElement.GetProperty("autoModEnabled").GetBoolean().Should().BeFalse(
            "AutoModEnabled should be false");
        resultDoc.RootElement.GetProperty("welcomeMessagesEnabled").GetBoolean().Should().BeFalse(
            "WelcomeMessagesEnabled should be false");
        resultDoc.RootElement.GetProperty("leaveMessagesEnabled").GetBoolean().Should().BeFalse(
            "LeaveMessagesEnabled should be false");
        resultDoc.RootElement.GetProperty("moderationAlertsEnabled").GetBoolean().Should().BeFalse(
            "ModerationAlertsEnabled should be false");
        resultDoc.RootElement.GetProperty("commandLoggingEnabled").GetBoolean().Should().BeFalse(
            "CommandLoggingEnabled should be false");
    }

    [Fact]
    public void Roundtrip_WithMixedValues_PreservesAllSettings()
    {
        // Arrange - Mix of true/false, null/value
        var mixedSettings = @"{
            ""welcomeChannel"": null,
            ""logChannel"": ""moderation"",
            ""autoModEnabled"": false,
            ""welcomeMessagesEnabled"": true,
            ""leaveMessagesEnabled"": false,
            ""moderationAlertsEnabled"": true,
            ""commandLoggingEnabled"": false
        }";

        var dto = new GuildDto
        {
            Id = 555666777888999000,
            Name = "Mixed Settings Guild",
            IconUrl = "https://cdn.discordapp.com/icons/555/xyz.png",
            IsActive = false,
            Prefix = ">>",
            Settings = mixedSettings
        };

        // Act
        var viewModel = GuildEditViewModel.FromDto(dto);
        var resultJson = viewModel.ToSettingsJson();

        // Assert - Verify specific values
        viewModel.WelcomeChannel.Should().BeNull();
        viewModel.LogChannel.Should().Be("moderation");
        viewModel.AutoModEnabled.Should().BeFalse();
        viewModel.WelcomeMessagesEnabled.Should().BeTrue();
        viewModel.LeaveMessagesEnabled.Should().BeFalse();
        viewModel.ModerationAlertsEnabled.Should().BeTrue();
        viewModel.CommandLoggingEnabled.Should().BeFalse();

        // Verify JSON output matches
        using var resultDoc = JsonDocument.Parse(resultJson);
        resultDoc.RootElement.GetProperty("welcomeChannel").ValueKind.Should().Be(JsonValueKind.Null);
        resultDoc.RootElement.GetProperty("logChannel").GetString().Should().Be("moderation");
        resultDoc.RootElement.GetProperty("autoModEnabled").GetBoolean().Should().BeFalse();
        resultDoc.RootElement.GetProperty("welcomeMessagesEnabled").GetBoolean().Should().BeTrue();
        resultDoc.RootElement.GetProperty("leaveMessagesEnabled").GetBoolean().Should().BeFalse();
        resultDoc.RootElement.GetProperty("moderationAlertsEnabled").GetBoolean().Should().BeTrue();
        resultDoc.RootElement.GetProperty("commandLoggingEnabled").GetBoolean().Should().BeFalse();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void FromDto_WithWhitespaceSettings_ReturnsDefaults()
    {
        // Arrange
        var dto = new GuildDto
        {
            Id = 111222333444555666,
            Name = "Whitespace Guild",
            IconUrl = null,
            IsActive = true,
            Prefix = "!",
            Settings = "   "
        };

        // Act
        var viewModel = GuildEditViewModel.FromDto(dto);

        // Assert
        viewModel.Should().NotBeNull();
        viewModel.WelcomeChannel.Should().BeNull("whitespace settings should default to null");
        viewModel.LogChannel.Should().BeNull("whitespace settings should default to null");
        viewModel.AutoModEnabled.Should().BeFalse("whitespace settings should default to false");
    }

    [Fact]
    public void FromDto_WithEmptyJsonObject_ReturnsDefaults()
    {
        // Arrange
        var dto = new GuildDto
        {
            Id = 222333444555666777,
            Name = "Empty Object Guild",
            IconUrl = null,
            IsActive = true,
            Prefix = "!",
            Settings = "{}"
        };

        // Act
        var viewModel = GuildEditViewModel.FromDto(dto);

        // Assert
        viewModel.WelcomeChannel.Should().BeNull("empty JSON object should have null channels");
        viewModel.LogChannel.Should().BeNull("empty JSON object should have null channels");
        viewModel.AutoModEnabled.Should().BeFalse("empty JSON object should have false booleans");
        viewModel.WelcomeMessagesEnabled.Should().BeFalse("empty JSON object should have false booleans");
        viewModel.LeaveMessagesEnabled.Should().BeFalse("empty JSON object should have false booleans");
        viewModel.ModerationAlertsEnabled.Should().BeFalse("empty JSON object should have false booleans");
        viewModel.CommandLoggingEnabled.Should().BeFalse("empty JSON object should have false booleans");
    }

    [Fact]
    public void FromDto_WithExtraJsonFields_IgnoresExtraFields()
    {
        // Arrange - JSON with unknown fields
        var settingsWithExtra = @"{
            ""welcomeChannel"": ""welcome"",
            ""unknownField"": ""ignored"",
            ""anotherUnknown"": 12345,
            ""autoModEnabled"": true
        }";

        var dto = new GuildDto
        {
            Id = 333444555666777888,
            Name = "Extra Fields Guild",
            IconUrl = null,
            IsActive = true,
            Prefix = "!",
            Settings = settingsWithExtra
        };

        // Act
        var viewModel = GuildEditViewModel.FromDto(dto);

        // Assert - Known fields should be parsed, unknown ignored
        viewModel.WelcomeChannel.Should().Be("welcome", "known fields should be parsed");
        viewModel.AutoModEnabled.Should().BeTrue("known fields should be parsed");
        viewModel.LogChannel.Should().BeNull("missing fields should be null");
    }

    [Fact]
    public void ToSettingsJson_DoesNotIncludeGuildIdentityProperties()
    {
        // Arrange
        var viewModel = new GuildEditViewModel
        {
            Id = 444555666777888999,
            Name = "Test Guild",
            IconUrl = "https://example.com/icon.png",
            IsActive = true,
            Prefix = "!",
            WelcomeChannel = "general",
            LogChannel = "logs",
            AutoModEnabled = true,
            WelcomeMessagesEnabled = true,
            LeaveMessagesEnabled = false,
            ModerationAlertsEnabled = true,
            CommandLoggingEnabled = true
        };

        // Act
        var json = viewModel.ToSettingsJson();

        // Assert - Guild identity properties should NOT be in settings JSON
        json.Should().NotContain("id", "Id should not be in settings JSON");
        json.Should().NotContain("name", "Name should not be in settings JSON");
        json.Should().NotContain("iconUrl", "IconUrl should not be in settings JSON");
        json.Should().NotContain("isActive", "IsActive should not be in settings JSON");
        json.Should().NotContain("prefix", "Prefix should not be in settings JSON");

        // Only settings properties should be present
        json.Should().Contain("welcomeChannel", "WelcomeChannel should be in settings JSON");
        json.Should().Contain("logChannel", "LogChannel should be in settings JSON");
        json.Should().Contain("autoModEnabled", "AutoModEnabled should be in settings JSON");
    }

    #endregion
}
