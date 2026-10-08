using DiscordBot.Core.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace DiscordBot.Tests.Tracing;

/// <summary>
/// The shipped appsettings files must put the trace sampling rate where
/// <c>OpenTelemetryExtensions</c> binds <see cref="SamplingOptions"/>
/// (<c>OpenTelemetry:Tracing:Sampling</c>). A key anywhere else is silently ignored.
/// </summary>
public class SamplingConfigurationTests
{
    private const string SamplingSectionPath = "OpenTelemetry:Tracing:" + SamplingOptions.SectionName;

    [Theory]
    [InlineData("appsettings.Production.json", 0.1)]
    [InlineData("appsettings.Development.json", 1.0)]
    public void EnvironmentFile_SamplingRate_BindsToSamplingOptionsDefaultRate(string fileName, double expectedRate)
    {
        // Arrange
        var configuration = LoadBotSettings(fileName);

        // Act
        var section = configuration.GetSection(SamplingSectionPath);
        var options = section.Get<SamplingOptions>();

        // Assert
        section.Exists().Should().BeTrue(
            $"{fileName} should configure sampling under {SamplingSectionPath}, the section SamplingOptions binds");
        options!.DefaultRate.Should().Be(expectedRate);
    }

    [Theory]
    [InlineData("appsettings.json")]
    [InlineData("appsettings.Production.json")]
    [InlineData("appsettings.Development.json")]
    public void SettingsFile_HasNoUnboundSamplingRatioKey(string fileName)
    {
        // Arrange
        var configuration = LoadBotSettings(fileName);

        // Act
        var stray = configuration.GetSection("OpenTelemetry:Tracing:SamplingRatio");

        // Assert
        stray.Exists().Should().BeFalse(
            "nothing binds OpenTelemetry:Tracing:SamplingRatio; the rate belongs in Sampling:DefaultRate");
    }

    private static IConfiguration LoadBotSettings(string fileName)
    {
        var path = Path.Combine(FindRepositoryRoot(), "src", "DiscordBot.Bot", fileName);
        File.Exists(path).Should().BeTrue($"{fileName} should exist in the Bot project");

        return new ConfigurationBuilder()
            .AddJsonFile(path, optional: false, reloadOnChange: false)
            .Build();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DiscordBot.sln")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the tests must be able to find the repository they are testing");
        return directory!.FullName;
    }
}
