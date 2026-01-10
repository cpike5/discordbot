using DiscordBot.Bot.Services;
using DiscordBot.Core.Enums;
using FluentAssertions;

namespace DiscordBot.Tests.Bot.Services;

/// <summary>
/// Unit tests for PlaybackService internal methods.
/// </summary>
public class PlaybackServiceTests
{
    private const string TestFilePath = "/sounds/test.mp3";

    [Fact]
    public void BuildFfmpegArguments_WithNoFilter_ReturnsStandardArguments()
    {
        // Act
        var result = PlaybackService.BuildFfmpegArguments(TestFilePath, AudioFilter.None);

        // Assert
        result.Should().Be($"-hide_banner -loglevel warning -i \"{TestFilePath}\" -ac 2 -f s16le -ar 48000 pipe:1");
    }

    [Fact]
    public void BuildFfmpegArguments_WithFilter_IncludesAfArgument()
    {
        // Act
        var result = PlaybackService.BuildFfmpegArguments(TestFilePath, AudioFilter.BassBoost);

        // Assert
        result.Should().Contain("-af \"");
        result.Should().Contain("equalizer=f=60:width_type=h:width=50:g=10");
    }

    [Theory]
    [InlineData(AudioFilter.Reverb, "aecho=0.8:0.9:40:0.4")]
    [InlineData(AudioFilter.BassBoost, "equalizer=f=60:width_type=h:width=50:g=10")]
    [InlineData(AudioFilter.TrebleBoost, "equalizer=f=3000:width_type=h:width=200:g=5")]
    [InlineData(AudioFilter.PitchUp, "asetrate=48000*1.25,aresample=48000")]
    [InlineData(AudioFilter.PitchDown, "asetrate=48000*0.75,aresample=48000")]
    [InlineData(AudioFilter.Nightcore, "asetrate=48000*1.25,aresample=48000,atempo=1.25")]
    [InlineData(AudioFilter.SlowMo, "atempo=0.8")]
    public void BuildFfmpegArguments_WithFilter_ContainsCorrectFilterString(AudioFilter filter, string expectedFilterString)
    {
        // Act
        var result = PlaybackService.BuildFfmpegArguments(TestFilePath, filter);

        // Assert
        result.Should().Contain($"-af \"{expectedFilterString}\"");
    }

    [Fact]
    public void BuildFfmpegArguments_AlwaysOutputs48kHzStereo()
    {
        // Arrange
        var filters = Enum.GetValues<AudioFilter>();

        // Act & Assert
        foreach (var filter in filters)
        {
            var result = PlaybackService.BuildFfmpegArguments(TestFilePath, filter);

            result.Should().Contain("-ac 2", $"should output stereo for filter {filter}");
            result.Should().Contain("-ar 48000", $"should output 48kHz for filter {filter}");
            result.Should().Contain("-f s16le", $"should output s16le format for filter {filter}");
        }
    }

    [Fact]
    public void BuildFfmpegArguments_FilterIsInsertedBetweenInputAndOutput()
    {
        // Act
        var result = PlaybackService.BuildFfmpegArguments(TestFilePath, AudioFilter.Reverb);

        // Assert - verify ordering: input file comes before filter, filter comes before output format
        var inputIndex = result.IndexOf($"-i \"{TestFilePath}\"");
        var filterIndex = result.IndexOf("-af \"");
        var outputIndex = result.IndexOf("-ac 2");

        inputIndex.Should().BeLessThan(filterIndex, "input should come before filter");
        filterIndex.Should().BeLessThan(outputIndex, "filter should come before output format");
    }

    [Fact]
    public void BuildFfmpegArguments_WithSpacesInFilePath_QuotesPath()
    {
        // Arrange
        var pathWithSpaces = "/sounds/my folder/test sound.mp3";

        // Act
        var result = PlaybackService.BuildFfmpegArguments(pathWithSpaces, AudioFilter.None);

        // Assert
        result.Should().Contain($"-i \"{pathWithSpaces}\"");
    }

    [Fact]
    public void BuildFfmpegArguments_OutputsToStdout()
    {
        // Act
        var result = PlaybackService.BuildFfmpegArguments(TestFilePath, AudioFilter.None);

        // Assert
        result.Should().EndWith("pipe:1");
    }

    [Fact]
    public void BuildFfmpegArguments_SuppressesBannerAndLimitsLogging()
    {
        // Act
        var result = PlaybackService.BuildFfmpegArguments(TestFilePath, AudioFilter.None);

        // Assert
        result.Should().Contain("-hide_banner");
        result.Should().Contain("-loglevel warning");
    }
}
