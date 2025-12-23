using DiscordBot.Bot.ViewModels.Pages;
using FluentAssertions;

namespace DiscordBot.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="GuildSettingsViewModel"/>.
/// </summary>
public class GuildSettingsViewModelTests
{
    [Fact]
    public void Parse_WithValidJson_ParsesAllSettings()
    {
        // Arrange
        var json = "{\"WelcomeChannel\":\"welcome\",\"LogChannel\":\"logs\",\"AutoModEnabled\":true}";

        // Act
        var result = GuildSettingsViewModel.Parse(json);

        // Assert
        result.Should().NotBeNull();
        result.WelcomeChannel.Should().Be("welcome", "WelcomeChannel should be parsed correctly");
        result.LogChannel.Should().Be("logs", "LogChannel should be parsed correctly");
        result.AutoModEnabled.Should().BeTrue("AutoModEnabled should be parsed correctly");
        result.HasSettings.Should().BeTrue("parsed settings should have HasSettings = true");
    }

    [Fact]
    public void Parse_WithNullJson_ReturnsEmptySettings()
    {
        // Act
        var result = GuildSettingsViewModel.Parse(null);

        // Assert
        result.Should().NotBeNull();
        result.WelcomeChannel.Should().BeNull("WelcomeChannel should be null");
        result.LogChannel.Should().BeNull("LogChannel should be null");
        result.AutoModEnabled.Should().BeFalse("AutoModEnabled should be false");
        result.HasSettings.Should().BeFalse("empty settings should have HasSettings = false");
    }

    [Fact]
    public void Parse_WithEmptyJson_ReturnsEmptySettings()
    {
        // Act
        var result = GuildSettingsViewModel.Parse(string.Empty);

        // Assert
        result.Should().NotBeNull();
        result.WelcomeChannel.Should().BeNull("WelcomeChannel should be null");
        result.LogChannel.Should().BeNull("LogChannel should be null");
        result.AutoModEnabled.Should().BeFalse("AutoModEnabled should be false");
        result.HasSettings.Should().BeFalse("empty settings should have HasSettings = false");
    }

    [Fact]
    public void Parse_WithInvalidJson_ReturnsEmptySettings()
    {
        // Arrange
        var invalidJson = "{ this is not valid json }";

        // Act
        var result = GuildSettingsViewModel.Parse(invalidJson);

        // Assert
        result.Should().NotBeNull("parser should handle invalid JSON gracefully");
        result.WelcomeChannel.Should().BeNull("WelcomeChannel should be null for invalid JSON");
        result.LogChannel.Should().BeNull("LogChannel should be null for invalid JSON");
        result.AutoModEnabled.Should().BeFalse("AutoModEnabled should be false for invalid JSON");
        result.HasSettings.Should().BeFalse("invalid JSON should result in HasSettings = false");
    }

    [Fact]
    public void HasSettings_WithNoSettings_ReturnsFalse()
    {
        // Arrange
        var settings = new GuildSettingsViewModel
        {
            WelcomeChannel = null,
            LogChannel = null,
            AutoModEnabled = false
        };

        // Assert
        settings.HasSettings.Should().BeFalse("settings with all null/false values should have HasSettings = false");
    }

    [Fact]
    public void HasSettings_WithSettings_ReturnsTrue()
    {
        // Test case 1: Only WelcomeChannel set
        var settings1 = new GuildSettingsViewModel
        {
            WelcomeChannel = "welcome",
            LogChannel = null,
            AutoModEnabled = false
        };

        settings1.HasSettings.Should().BeTrue("settings with WelcomeChannel should have HasSettings = true");

        // Test case 2: Only LogChannel set
        var settings2 = new GuildSettingsViewModel
        {
            WelcomeChannel = null,
            LogChannel = "logs",
            AutoModEnabled = false
        };

        settings2.HasSettings.Should().BeTrue("settings with LogChannel should have HasSettings = true");

        // Test case 3: Only AutoModEnabled set
        var settings3 = new GuildSettingsViewModel
        {
            WelcomeChannel = null,
            LogChannel = null,
            AutoModEnabled = true
        };

        settings3.HasSettings.Should().BeTrue("settings with AutoModEnabled should have HasSettings = true");

        // Test case 4: All settings set
        var settings4 = new GuildSettingsViewModel
        {
            WelcomeChannel = "welcome",
            LogChannel = "logs",
            AutoModEnabled = true
        };

        settings4.HasSettings.Should().BeTrue("settings with all values should have HasSettings = true");
    }

    [Fact]
    public void Parse_WithPartialJson_ParsesAvailableFields()
    {
        // Arrange - JSON with only WelcomeChannel
        var json = "{\"WelcomeChannel\":\"general\"}";

        // Act
        var result = GuildSettingsViewModel.Parse(json);

        // Assert
        result.Should().NotBeNull();
        result.WelcomeChannel.Should().Be("general", "WelcomeChannel should be parsed");
        result.LogChannel.Should().BeNull("LogChannel should be null when not in JSON");
        result.AutoModEnabled.Should().BeFalse("AutoModEnabled should be false when not in JSON");
        result.HasSettings.Should().BeTrue("partial settings should have HasSettings = true");
    }

    [Fact]
    public void Parse_WithEmptyJsonObject_ReturnsEmptySettings()
    {
        // Arrange
        var emptyJson = "{}";

        // Act
        var result = GuildSettingsViewModel.Parse(emptyJson);

        // Assert
        result.Should().NotBeNull();
        result.WelcomeChannel.Should().BeNull("WelcomeChannel should be null");
        result.LogChannel.Should().BeNull("LogChannel should be null");
        result.AutoModEnabled.Should().BeFalse("AutoModEnabled should be false");
        result.HasSettings.Should().BeFalse("empty JSON object should have HasSettings = false");
    }

    [Fact]
    public void Parse_IsCaseInsensitive()
    {
        // Arrange - JSON with different casing
        var json = "{\"welcomechannel\":\"welcome\",\"logchannel\":\"logs\",\"automodenabled\":true}";

        // Act
        var result = GuildSettingsViewModel.Parse(json);

        // Assert
        result.Should().NotBeNull();
        result.WelcomeChannel.Should().Be("welcome", "parser should be case insensitive");
        result.LogChannel.Should().Be("logs", "parser should be case insensitive");
        result.AutoModEnabled.Should().BeTrue("parser should be case insensitive");
    }

    [Fact]
    public void Parse_WithExtraJsonFields_IgnoresExtraFields()
    {
        // Arrange - JSON with extra unknown fields
        var json = "{\"WelcomeChannel\":\"welcome\",\"ExtraField\":\"ignored\",\"AnotherField\":123}";

        // Act
        var result = GuildSettingsViewModel.Parse(json);

        // Assert
        result.Should().NotBeNull("parser should handle extra fields gracefully");
        result.WelcomeChannel.Should().Be("welcome", "known fields should be parsed");
        result.LogChannel.Should().BeNull("missing fields should be null");
        result.AutoModEnabled.Should().BeFalse("missing fields should be false");
    }

    [Fact]
    public void Parse_WithWhitespaceJson_ReturnsEmptySettings()
    {
        // Arrange
        var whitespaceJson = "   ";

        // Act
        var result = GuildSettingsViewModel.Parse(whitespaceJson);

        // Assert
        result.Should().NotBeNull();
        result.WelcomeChannel.Should().BeNull();
        result.LogChannel.Should().BeNull();
        result.AutoModEnabled.Should().BeFalse();
        result.HasSettings.Should().BeFalse("whitespace-only string should result in empty settings");
    }

    [Fact]
    public void Parse_WithNullJsonField_HandlesGracefully()
    {
        // Arrange - JSON with explicit null values
        var json = "{\"WelcomeChannel\":null,\"LogChannel\":\"logs\",\"AutoModEnabled\":false}";

        // Act
        var result = GuildSettingsViewModel.Parse(json);

        // Assert
        result.Should().NotBeNull();
        result.WelcomeChannel.Should().BeNull("null JSON value should map to null");
        result.LogChannel.Should().Be("logs", "non-null value should be parsed");
        result.AutoModEnabled.Should().BeFalse("false value should be parsed");
    }

    [Fact]
    public void Parse_WithMalformedJson_ReturnsEmptySettings()
    {
        // Test various malformed JSON strings
        var malformedJsonSamples = new[]
        {
            "{ \"WelcomeChannel\": }",           // Missing value
            "{ WelcomeChannel: \"welcome\" }",   // Unquoted key
            "{ \"WelcomeChannel\": \"welcome\" ", // Missing closing brace
            "[\"WelcomeChannel\", \"welcome\"]",  // Array instead of object
            "null"                                 // Literal null
        };

        foreach (var malformedJson in malformedJsonSamples)
        {
            // Act
            var result = GuildSettingsViewModel.Parse(malformedJson);

            // Assert
            result.Should().NotBeNull($"parser should handle malformed JSON: {malformedJson}");
            result.HasSettings.Should().BeFalse($"malformed JSON should result in empty settings: {malformedJson}");
        }
    }

    [Fact]
    public void HasSettings_WithEmptyStringChannels_ReturnsFalse()
    {
        // Arrange - Empty strings should be treated as "no setting"
        var settings = new GuildSettingsViewModel
        {
            WelcomeChannel = "",
            LogChannel = "",
            AutoModEnabled = false
        };

        // Assert
        settings.HasSettings.Should().BeFalse("empty string channels should be treated as no settings");
    }

    [Fact]
    public void GuildSettingsViewModel_IsRecordType_SupportsValueEquality()
    {
        // Arrange
        var settings1 = new GuildSettingsViewModel
        {
            WelcomeChannel = "welcome",
            LogChannel = "logs",
            AutoModEnabled = true
        };

        var settings2 = new GuildSettingsViewModel
        {
            WelcomeChannel = "welcome",
            LogChannel = "logs",
            AutoModEnabled = true
        };

        var settings3 = new GuildSettingsViewModel
        {
            WelcomeChannel = "different",
            LogChannel = "logs",
            AutoModEnabled = true
        };

        // Assert
        settings1.Should().Be(settings2, "records with same values should be equal");
        settings1.Should().NotBe(settings3, "records with different values should not be equal");
    }

    [Fact]
    public void Parse_WithBooleanVariations_ParsesCorrectly()
    {
        // Test various boolean representations
        var testCases = new[]
        {
            ("{\"AutoModEnabled\":true}", true),
            ("{\"AutoModEnabled\":false}", false),
            ("{\"AutoModEnabled\":\"true\"}", false), // String "true" should not parse to bool true
        };

        foreach (var (json, expectedValue) in testCases)
        {
            // Act
            var result = GuildSettingsViewModel.Parse(json);

            // Assert
            result.AutoModEnabled.Should().Be(expectedValue, $"JSON {json} should parse AutoModEnabled to {expectedValue}");
        }
    }

    [Fact]
    public void Parse_WithNumericChannelIds_ParsesAsStrings()
    {
        // Arrange - Channel IDs might be numeric in JSON
        var json = "{\"WelcomeChannel\":\"123456789\",\"LogChannel\":\"987654321\"}";

        // Act
        var result = GuildSettingsViewModel.Parse(json);

        // Assert
        result.Should().NotBeNull();
        result.WelcomeChannel.Should().Be("123456789", "numeric channel ID should be parsed as string");
        result.LogChannel.Should().Be("987654321", "numeric channel ID should be parsed as string");
    }
}
