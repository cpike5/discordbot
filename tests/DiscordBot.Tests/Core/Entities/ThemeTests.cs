using System.Text.Json;
using DiscordBot.Core.Entities;
using FluentAssertions;

namespace DiscordBot.Tests.Core.Entities;

/// <summary>
/// Unit tests for the Theme entity.
/// Tests cover default values, property behavior, and color definition handling.
/// </summary>
public class ThemeTests
{
    #region Default Values Tests

    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var theme = new Theme();

        // Assert
        theme.ThemeKey.Should().Be(string.Empty, "ThemeKey should default to empty string");
        theme.DisplayName.Should().Be(string.Empty, "DisplayName should default to empty string");
        theme.Description.Should().BeNull("Description should default to null");
        theme.ColorDefinition.Should().Be("{}", "ColorDefinition should default to empty JSON object");
        theme.IsActive.Should().BeTrue("newly created themes should be active by default");
        theme.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1),
            "CreatedAt should be set to current UTC time");
    }

    [Fact]
    public void PreferringUsers_DefaultsToEmptyCollection()
    {
        // Arrange & Act
        var theme = new Theme();

        // Assert
        theme.PreferringUsers.Should().NotBeNull("PreferringUsers collection should be initialized");
        theme.PreferringUsers.Should().BeEmpty("PreferringUsers should start empty");
    }

    #endregion

    #region Property Tests

    [Fact]
    public void Id_CanBeSet()
    {
        // Arrange
        var theme = new Theme();
        const int expectedId = 42;

        // Act
        theme.Id = expectedId;

        // Assert
        theme.Id.Should().Be(expectedId);
    }

    [Fact]
    public void ThemeKey_CanBeSet()
    {
        // Arrange
        var theme = new Theme();
        const string expectedKey = "discord-dark";

        // Act
        theme.ThemeKey = expectedKey;

        // Assert
        theme.ThemeKey.Should().Be(expectedKey);
    }

    [Fact]
    public void DisplayName_CanBeSet()
    {
        // Arrange
        var theme = new Theme();
        const string expectedName = "Discord Dark";

        // Act
        theme.DisplayName = expectedName;

        // Assert
        theme.DisplayName.Should().Be(expectedName);
    }

    [Fact]
    public void Description_CanBeSet()
    {
        // Arrange
        var theme = new Theme();
        const string expectedDescription = "A dark theme inspired by Discord's default appearance";

        // Act
        theme.Description = expectedDescription;

        // Assert
        theme.Description.Should().Be(expectedDescription);
    }

    [Fact]
    public void Description_CanBeSetToNull()
    {
        // Arrange
        var theme = new Theme { Description = "Some description" };

        // Act
        theme.Description = null;

        // Assert
        theme.Description.Should().BeNull("Description is optional and can be null");
    }

    [Fact]
    public void IsActive_CanBeSetToFalse()
    {
        // Arrange
        var theme = new Theme();

        // Act
        theme.IsActive = false;

        // Assert
        theme.IsActive.Should().BeFalse("themes can be deactivated");
    }

    [Fact]
    public void CreatedAt_CanBeSetToSpecificDate()
    {
        // Arrange
        var theme = new Theme();
        var expectedDate = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        theme.CreatedAt = expectedDate;

        // Assert
        theme.CreatedAt.Should().Be(expectedDate);
    }

    #endregion

    #region ColorDefinition Tests

    [Fact]
    public void ColorDefinition_CanBeSetToValidJson()
    {
        // Arrange
        var theme = new Theme();
        const string expectedJson = """{"bgPrimary":"#1d2022","bgSecondary":"#17181a","textPrimary":"#ffffff"}""";

        // Act
        theme.ColorDefinition = expectedJson;

        // Assert
        theme.ColorDefinition.Should().Be(expectedJson);
    }

    [Fact]
    public void ColorDefinition_ValidJsonCanBeParsed()
    {
        // Arrange
        var theme = new Theme
        {
            ColorDefinition = """{"bgPrimary":"#1d2022","bgSecondary":"#17181a","textPrimary":"#ffffff"}"""
        };

        // Act
        var parsed = JsonDocument.Parse(theme.ColorDefinition);

        // Assert
        parsed.RootElement.GetProperty("bgPrimary").GetString().Should().Be("#1d2022");
        parsed.RootElement.GetProperty("bgSecondary").GetString().Should().Be("#17181a");
        parsed.RootElement.GetProperty("textPrimary").GetString().Should().Be("#ffffff");
    }

    [Fact]
    public void ColorDefinition_EmptyObjectIsValidJson()
    {
        // Arrange
        var theme = new Theme();

        // Act
        var parsed = JsonDocument.Parse(theme.ColorDefinition);

        // Assert
        parsed.RootElement.ValueKind.Should().Be(JsonValueKind.Object);
        parsed.RootElement.EnumerateObject().Should().BeEmpty();
    }

    [Fact]
    public void ColorDefinition_ComplexThemeDefinition()
    {
        // Arrange
        var colorDefinition = new Dictionary<string, string>
        {
            { "bgPrimary", "#1d2022" },
            { "bgSecondary", "#17181a" },
            { "bgTertiary", "#1f2124" },
            { "bgAccent", "#2f3136" },
            { "textPrimary", "#ffffff" },
            { "textSecondary", "#b9bbbe" },
            { "textMuted", "#72767d" },
            { "borderDefault", "#3f4147" },
            { "accentPrimary", "#5865f2" },
            { "accentSuccess", "#3ba55c" },
            { "accentWarning", "#faa61a" },
            { "accentDanger", "#ed4245" }
        };

        var theme = new Theme
        {
            ColorDefinition = JsonSerializer.Serialize(colorDefinition)
        };

        // Act
        var parsed = JsonDocument.Parse(theme.ColorDefinition);

        // Assert
        foreach (var kvp in colorDefinition)
        {
            parsed.RootElement.GetProperty(kvp.Key).GetString().Should().Be(kvp.Value,
                $"Color '{kvp.Key}' should have value '{kvp.Value}'");
        }
    }

    [Fact]
    public void ColorDefinition_CanContainNestedStructure()
    {
        // Arrange - some theme systems use nested structures for organization
        const string nestedJson = """
        {
            "background": {
                "primary": "#1d2022",
                "secondary": "#17181a"
            },
            "text": {
                "primary": "#ffffff",
                "secondary": "#b9bbbe"
            }
        }
        """;

        var theme = new Theme { ColorDefinition = nestedJson };

        // Act
        var parsed = JsonDocument.Parse(theme.ColorDefinition);

        // Assert
        parsed.RootElement.GetProperty("background").GetProperty("primary").GetString()
            .Should().Be("#1d2022");
        parsed.RootElement.GetProperty("text").GetProperty("primary").GetString()
            .Should().Be("#ffffff");
    }

    #endregion

    #region Full Entity Tests

    [Fact]
    public void AllProperties_CanBeSetTogether()
    {
        // Arrange
        const int id = 1;
        const string themeKey = "discord-dark";
        const string displayName = "Discord Dark";
        const string description = "A dark theme inspired by Discord";
        const string colorDefinition = """{"bgPrimary":"#1d2022"}""";
        const bool isActive = true;
        var createdAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var theme = new Theme
        {
            Id = id,
            ThemeKey = themeKey,
            DisplayName = displayName,
            Description = description,
            ColorDefinition = colorDefinition,
            IsActive = isActive,
            CreatedAt = createdAt
        };

        // Assert
        theme.Id.Should().Be(id);
        theme.ThemeKey.Should().Be(themeKey);
        theme.DisplayName.Should().Be(displayName);
        theme.Description.Should().Be(description);
        theme.ColorDefinition.Should().Be(colorDefinition);
        theme.IsActive.Should().Be(isActive);
        theme.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void DiscordDarkTheme_TypicalConfiguration()
    {
        // Arrange & Act - simulate the seeded Discord Dark theme
        var theme = new Theme
        {
            Id = 1,
            ThemeKey = "discord-dark",
            DisplayName = "Discord Dark",
            Description = "Default dark theme matching Discord's appearance",
            ColorDefinition = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                { "bgPrimary", "#1d2022" },
                { "bgSecondary", "#17181a" },
                { "bgTertiary", "#1f2124" },
                { "textPrimary", "#ffffff" },
                { "textSecondary", "#b9bbbe" },
                { "accentPrimary", "#5865f2" }
            }),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        // Assert
        theme.ThemeKey.Should().Be("discord-dark");
        theme.IsActive.Should().BeTrue();

        var colors = JsonDocument.Parse(theme.ColorDefinition);
        colors.RootElement.GetProperty("bgPrimary").GetString().Should().StartWith("#",
            "color values should be hex codes");
        colors.RootElement.GetProperty("accentPrimary").GetString().Should().Be("#5865f2",
            "Discord blurple should be the accent color");
    }

    [Fact]
    public void PurpleDuskTheme_TypicalConfiguration()
    {
        // Arrange & Act - simulate the seeded Purple Dusk theme
        var theme = new Theme
        {
            Id = 2,
            ThemeKey = "purple-dusk",
            DisplayName = "Purple Dusk",
            Description = "A light theme with warm purple tones",
            ColorDefinition = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                { "bgPrimary", "#E8E3DF" },
                { "bgSecondary", "#F4F0ED" },
                { "bgTertiary", "#DCD6D1" },
                { "textPrimary", "#2F2B3A" },
                { "textSecondary", "#5C5668" },
                { "accentPrimary", "#7C3AED" }
            }),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        // Assert
        theme.ThemeKey.Should().Be("purple-dusk");
        theme.IsActive.Should().BeTrue();

        var colors = JsonDocument.Parse(theme.ColorDefinition);
        colors.RootElement.GetProperty("bgPrimary").GetString().Should().Be("#E8E3DF",
            "Purple Dusk should have warm light background");
        colors.RootElement.GetProperty("accentPrimary").GetString().Should().Be("#7C3AED",
            "Purple Dusk should have violet accent color");
    }

    #endregion
}
