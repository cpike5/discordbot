using DiscordBot.Bot.Services;
using DiscordBot.Core.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for SoundFileService.
/// Tests cover file I/O, path resolution, and audio format validation.
/// </summary>
public class SoundFileServiceTests
{
    private readonly Mock<ILogger<SoundFileService>> _mockLogger;
    private readonly IOptions<SoundboardOptions> _options;
    private readonly SoundFileService _service;

    public SoundFileServiceTests()
    {
        _mockLogger = new Mock<ILogger<SoundFileService>>();
        _options = Options.Create(new SoundboardOptions
        {
            BasePath = "sounds",
            SupportedFormats = new() { ".mp3", ".wav", ".ogg", ".m4a" }
        });

        _service = new SoundFileService(
            _mockLogger.Object,
            _options);
    }

    #region GetSoundFilePath Tests

    [Fact]
    public void GetSoundFilePath_ReturnsCorrectPath()
    {
        // Arrange
        ulong guildId = 123456789UL;
        string fileName = "test-sound.mp3";

        // Act
        var result = _service.GetSoundFilePath(guildId, fileName);

        // Assert
        result.Should().Contain("sounds");
        result.Should().Contain("123456789");
        result.Should().Contain("test-sound.mp3");
        result.Should().Be(Path.Combine("sounds", "123456789", "test-sound.mp3"));
    }

    [Fact]
    public void GetSoundFilePath_WithDifferentGuildIds_ReturnsDifferentPaths()
    {
        // Arrange
        string fileName = "sound.wav";

        // Act
        var path1 = _service.GetSoundFilePath(111111UL, fileName);
        var path2 = _service.GetSoundFilePath(222222UL, fileName);

        // Assert
        path1.Should().NotBe(path2);
        path1.Should().Contain("111111");
        path2.Should().Contain("222222");
    }

    [Fact]
    public void GetSoundFilePath_WithDifferentFileNames_ReturnsDifferentPaths()
    {
        // Arrange
        ulong guildId = 123456789UL;

        // Act
        var path1 = _service.GetSoundFilePath(guildId, "sound1.mp3");
        var path2 = _service.GetSoundFilePath(guildId, "sound2.wav");

        // Assert
        path1.Should().NotBe(path2);
        path1.Should().EndWith("sound1.mp3");
        path2.Should().EndWith("sound2.wav");
    }

    #endregion

    #region IsValidAudioFormat Tests

    [Theory]
    [InlineData("sound.mp3")]
    [InlineData("sound.wav")]
    [InlineData("sound.ogg")]
    [InlineData("sound.m4a")]
    [InlineData("sound.MP3")]
    [InlineData("sound.WAV")]
    [InlineData("sound.OGG")]
    [InlineData("sound.M4A")]
    public void IsValidAudioFormat_WithSupportedFormats_ReturnsTrue(string fileName)
    {
        // Act
        var result = _service.IsValidAudioFormat(fileName);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("sound.txt")]
    [InlineData("sound.pdf")]
    [InlineData("sound.jpg")]
    [InlineData("sound.exe")]
    [InlineData("sound.flac")]
    [InlineData("sound")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void IsValidAudioFormat_WithUnsupportedFormatsOrEmpty_ReturnsFalse(string? fileName)
    {
        // Act
        var result = _service.IsValidAudioFormat(fileName ?? "");

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(".mp3")]
    [InlineData(".wav")]
    [InlineData(".ogg")]
    [InlineData(".m4a")]
    public void IsValidAudioFormat_WithExtensionOnly_ReturnsTrue(string fileName)
    {
        // Act
        var result = _service.IsValidAudioFormat(fileName);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("soundmp3")]
    [InlineData("sound.mp")]
    [InlineData("sound.mp4")]
    [InlineData("soundwav")]
    public void IsValidAudioFormat_WithInvalidExtensions_ReturnsFalse(string fileName)
    {
        // Act
        var result = _service.IsValidAudioFormat(fileName);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region SoundFileExists Tests

    [Fact]
    public void SoundFileExists_WhenFileExists_ReturnsTrue()
    {
        // Arrange
        ulong guildId = 123456789UL;
        string fileName = "existing-sound.mp3";
        var filePath = Path.Combine("sounds", "123456789", fileName);

        // Create the directory and file for this test
        var directory = Path.GetDirectoryName(filePath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory!);
        }

        File.WriteAllText(filePath, "test content");

        try
        {
            // Act
            var result = _service.SoundFileExists(guildId, fileName);

            // Assert
            result.Should().BeTrue();
        }
        finally
        {
            // Cleanup
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory, true);
            }
        }
    }

    [Fact]
    public void SoundFileExists_WhenFileDoesNotExist_ReturnsFalse()
    {
        // Arrange
        ulong guildId = 123456789UL;
        string fileName = "nonexistent-sound.mp3";

        // Act
        var result = _service.SoundFileExists(guildId, fileName);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void SoundFileExists_WithDifferentGuilds_ReturnsFalseForNonexistent()
    {
        // Arrange
        ulong guildId1 = 111111UL;
        ulong guildId2 = 222222UL;
        string fileName = "sound.mp3";

        // Act & Assert
        var result1 = _service.SoundFileExists(guildId1, fileName);
        var result2 = _service.SoundFileExists(guildId2, fileName);

        result1.Should().BeFalse();
        result2.Should().BeFalse();
    }

    #endregion

    #region IsValidAudioFormat Edge Cases Tests

    [Fact]
    public void IsValidAudioFormat_WithMixedCaseExtension_ReturnsTrueForValidFormat()
    {
        // Arrange
        var testCases = new[] { "sound.Mp3", "sound.wAv", "sound.OgG", "sound.m4A" };

        // Act & Assert
        foreach (var fileName in testCases)
        {
            var result = _service.IsValidAudioFormat(fileName);
            result.Should().BeTrue($"Format {fileName} should be valid (case-insensitive)");
        }
    }

    [Fact]
    public void IsValidAudioFormat_WithWhitespaceOnly_ReturnsFalse()
    {
        // Arrange
        var whitespaceTests = new[] { "   ", "\t", "\n" };

        // Act & Assert
        foreach (var input in whitespaceTests)
        {
            var result = _service.IsValidAudioFormat(input);
            result.Should().BeFalse($"Whitespace-only input '{input}' should be invalid");
        }
    }

    [Fact]
    public void IsValidAudioFormat_WithSpecialCharactersInName_ReturnsTrueForValidFormat()
    {
        // Arrange
        var fileName = "sound-with_special.chars.mp3";

        // Act
        var result = _service.IsValidAudioFormat(fileName);

        // Assert
        result.Should().BeTrue();
    }

    #endregion
}
