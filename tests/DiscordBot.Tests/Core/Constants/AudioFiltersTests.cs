using DiscordBot.Core.Constants;
using DiscordBot.Core.Enums;
using FluentAssertions;

namespace DiscordBot.Tests.Core.Constants;

/// <summary>
/// Unit tests for the AudioFilters constants class.
/// </summary>
public class AudioFiltersTests
{
    [Fact]
    public void Definitions_ContainsAllEnumValues()
    {
        // Arrange
        var allEnumValues = Enum.GetValues<AudioFilter>();

        // Act & Assert
        AudioFilters.Definitions.Keys.Should().BeEquivalentTo(allEnumValues,
            "all AudioFilter enum values should have a definition");
    }

    [Theory]
    [InlineData(AudioFilter.None, "")]
    [InlineData(AudioFilter.Reverb, "aecho=0.8:0.9:40:0.4")]
    [InlineData(AudioFilter.BassBoost, "equalizer=f=60:width_type=h:width=50:g=10")]
    [InlineData(AudioFilter.TrebleBoost, "equalizer=f=3000:width_type=h:width=200:g=5")]
    [InlineData(AudioFilter.PitchUp, "asetrate=48000*1.25,aresample=48000")]
    [InlineData(AudioFilter.PitchDown, "asetrate=48000*0.75,aresample=48000")]
    [InlineData(AudioFilter.Nightcore, "asetrate=48000*1.25,aresample=48000,atempo=1.25")]
    [InlineData(AudioFilter.SlowMo, "atempo=0.8")]
    public void Definitions_HasCorrectFfmpegFilter(AudioFilter filter, string expectedFfmpegFilter)
    {
        // Act
        var definition = AudioFilters.Definitions[filter];

        // Assert
        definition.FfmpegFilter.Should().Be(expectedFfmpegFilter,
            $"FFmpeg filter for {filter} should match the expected value");
    }

    [Theory]
    [InlineData(AudioFilter.None, "None")]
    [InlineData(AudioFilter.Reverb, "Reverb")]
    [InlineData(AudioFilter.BassBoost, "Bass Boost")]
    [InlineData(AudioFilter.TrebleBoost, "Treble Boost")]
    [InlineData(AudioFilter.PitchUp, "Pitch Up")]
    [InlineData(AudioFilter.PitchDown, "Pitch Down")]
    [InlineData(AudioFilter.Nightcore, "Nightcore")]
    [InlineData(AudioFilter.SlowMo, "Slow Mo")]
    public void Definitions_HasCorrectName(AudioFilter filter, string expectedName)
    {
        // Act
        var definition = AudioFilters.Definitions[filter];

        // Assert
        definition.Name.Should().Be(expectedName,
            $"display name for {filter} should match the expected value");
    }

    [Fact]
    public void Definitions_AllHaveNonEmptyDescriptions()
    {
        // Act & Assert
        foreach (var (filter, definition) in AudioFilters.Definitions)
        {
            definition.Description.Should().NotBeNullOrWhiteSpace(
                $"description for {filter} should not be empty");
        }
    }

    [Theory]
    [InlineData(AudioFilter.None, "")]
    [InlineData(AudioFilter.Reverb, "aecho=0.8:0.9:40:0.4")]
    [InlineData(AudioFilter.BassBoost, "equalizer=f=60:width_type=h:width=50:g=10")]
    public void GetFfmpegFilter_ReturnsCorrectFilter(AudioFilter filter, string expectedFfmpegFilter)
    {
        // Act
        var result = AudioFilters.GetFfmpegFilter(filter);

        // Assert
        result.Should().Be(expectedFfmpegFilter);
    }

    [Fact]
    public void GetFfmpegFilter_ReturnsEmptyForUndefinedFilter()
    {
        // Arrange - use an invalid enum value
        var invalidFilter = (AudioFilter)999;

        // Act
        var result = AudioFilters.GetFfmpegFilter(invalidFilter);

        // Assert
        result.Should().BeEmpty("undefined filter should return empty string");
    }

    [Fact]
    public void Definitions_NoneHasEmptyFfmpegFilter()
    {
        // Arrange & Act
        var noneDefinition = AudioFilters.Definitions[AudioFilter.None];

        // Assert
        noneDefinition.FfmpegFilter.Should().BeEmpty(
            "None filter should have an empty FFmpeg filter string");
    }

    [Fact]
    public void Definitions_AllNonNoneHaveNonEmptyFfmpegFilter()
    {
        // Act & Assert
        foreach (var (filter, definition) in AudioFilters.Definitions)
        {
            if (filter != AudioFilter.None)
            {
                definition.FfmpegFilter.Should().NotBeNullOrWhiteSpace(
                    $"FFmpeg filter for {filter} should not be empty");
            }
        }
    }
}
