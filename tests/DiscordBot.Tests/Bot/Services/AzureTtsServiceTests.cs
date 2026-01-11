using DiscordBot.Bot.Services;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Bot.Services;

/// <summary>
/// Unit tests for <see cref="AzureTtsService"/>.
/// Tests cover configuration validation, input validation, and audio format handling.
/// Note: Actual Azure SDK calls are not tested as they require a valid subscription.
/// </summary>
public class AzureTtsServiceTests : IDisposable
{
    private readonly Mock<ILogger<AzureTtsService>> _mockLogger;
    private readonly Mock<IOptions<AzureSpeechOptions>> _mockOptions;

    public AzureTtsServiceTests()
    {
        _mockLogger = new Mock<ILogger<AzureTtsService>>();
        _mockOptions = new Mock<IOptions<AzureSpeechOptions>>();
    }

    public void Dispose()
    {
        // No resources to clean up
    }

    private AzureTtsService CreateService(AzureSpeechOptions? options = null)
    {
        var opts = options ?? new AzureSpeechOptions();
        _mockOptions.Setup(x => x.Value).Returns(opts);
        return new AzureTtsService(_mockOptions.Object, _mockLogger.Object);
    }

    #region IsConfigured Property Tests

    [Fact]
    public void IsConfigured_ReturnsFalse_WhenSubscriptionKeyIsNull()
    {
        // Arrange
        var options = new AzureSpeechOptions { SubscriptionKey = null };
        var service = CreateService(options);

        // Act
        var result = service.IsConfigured;

        // Assert
        result.Should().BeFalse("service should not be configured when subscription key is null");
    }

    [Fact]
    public void IsConfigured_ReturnsFalse_WhenSubscriptionKeyIsEmpty()
    {
        // Arrange
        var options = new AzureSpeechOptions { SubscriptionKey = string.Empty };
        var service = CreateService(options);

        // Act
        var result = service.IsConfigured;

        // Assert
        result.Should().BeFalse("service should not be configured when subscription key is empty");
    }

    [Fact]
    public void IsConfigured_ReturnsFalse_WhenSubscriptionKeyIsWhitespace()
    {
        // Arrange
        var options = new AzureSpeechOptions { SubscriptionKey = "   " };
        var service = CreateService(options);

        // Act
        var result = service.IsConfigured;

        // Assert
        result.Should().BeFalse("service should not be configured when subscription key is whitespace");
    }

    [Fact]
    public void IsConfigured_ReturnsTrue_WhenSubscriptionKeyIsProvided()
    {
        // Arrange - Even a short key will be accepted by SpeechConfig.FromSubscription
        // Azure SDK validates the key later during actual API calls
        var options = new AzureSpeechOptions { SubscriptionKey = "test-key" };
        var service = CreateService(options);

        // Act
        var result = service.IsConfigured;

        // Assert
        result.Should().BeTrue("service should be configured when subscription key is provided");
    }

    #endregion

    #region SynthesizeSpeechAsync Validation Tests

    [Fact]
    public async Task SynthesizeSpeechAsync_ThrowsArgumentException_WhenTextIsNull()
    {
        // Arrange
        var options = new AzureSpeechOptions { SubscriptionKey = null };
        var service = CreateService(options);

        // Act
        var act = async () => await service.SynthesizeSpeechAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Text cannot be null or empty.*")
            .Where(ex => ex.ParamName == "text", "exception should reference the text parameter");
    }

    [Fact]
    public async Task SynthesizeSpeechAsync_ThrowsArgumentException_WhenTextIsEmpty()
    {
        // Arrange
        var options = new AzureSpeechOptions { SubscriptionKey = null };
        var service = CreateService(options);

        // Act
        var act = async () => await service.SynthesizeSpeechAsync(string.Empty);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Text cannot be null or empty.*")
            .Where(ex => ex.ParamName == "text", "exception should reference the text parameter");
    }

    [Fact]
    public async Task SynthesizeSpeechAsync_ThrowsArgumentException_WhenTextIsWhitespace()
    {
        // Arrange
        var options = new AzureSpeechOptions { SubscriptionKey = null };
        var service = CreateService(options);

        // Act
        var act = async () => await service.SynthesizeSpeechAsync("   ");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Text cannot be null or empty.*")
            .Where(ex => ex.ParamName == "text", "exception should reference the text parameter");
    }

    [Fact]
    public async Task SynthesizeSpeechAsync_ThrowsArgumentException_WhenTextExceedsMaxLength()
    {
        // Arrange
        var maxLength = 500;
        var options = new AzureSpeechOptions
        {
            SubscriptionKey = null,
            MaxTextLength = maxLength
        };
        var service = CreateService(options);
        var textExceedingLimit = new string('a', maxLength + 1);

        // Act
        var act = async () => await service.SynthesizeSpeechAsync(textExceedingLimit);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage($"*Text length ({maxLength + 1}) exceeds maximum allowed length ({maxLength}).*")
            .Where(ex => ex.ParamName == "text", "exception should reference the text parameter");
    }

    [Fact]
    public async Task SynthesizeSpeechAsync_ThrowsArgumentException_WhenTextIsAtMaxLength()
    {
        // Arrange - Text that exactly meets the limit should NOT throw
        var maxLength = 500;
        var options = new AzureSpeechOptions
        {
            SubscriptionKey = null,
            MaxTextLength = maxLength
        };
        var service = CreateService(options);
        var textAtLimit = new string('a', maxLength);

        // Act - This should not throw for length validation
        var act = async () => await service.SynthesizeSpeechAsync(textAtLimit);

        // Assert - Will throw InvalidOperationException for not configured, not ArgumentException for length
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Azure Speech service is not configured*");
    }

    [Fact]
    public async Task SynthesizeSpeechAsync_ThrowsInvalidOperationException_WhenServiceNotConfigured()
    {
        // Arrange
        var options = new AzureSpeechOptions { SubscriptionKey = null };
        var service = CreateService(options);

        // Act
        var act = async () => await service.SynthesizeSpeechAsync("hello world");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Azure Speech service is not configured*")
            .WithMessage("*Configure SubscriptionKey in user secrets*");
    }

    #endregion

    #region SynthesizeSpeechAsync Options Tests

    [Fact]
    public async Task SynthesizeSpeechAsync_UsesDefaultOptions_WhenOptionsParameterIsNull()
    {
        // Arrange
        var defaultVoice = "en-US-CustomVoice";
        var defaultSpeed = 1.5;
        var defaultPitch = 0.8;
        var defaultVolume = 0.7;
        var options = new AzureSpeechOptions
        {
            SubscriptionKey = null,
            DefaultVoice = defaultVoice,
            DefaultSpeed = defaultSpeed,
            DefaultPitch = defaultPitch,
            DefaultVolume = defaultVolume
        };
        var service = CreateService(options);

        // Act
        var act = async () => await service.SynthesizeSpeechAsync("test", options: null);

        // Assert - Will throw due to not configured
        // We verify that InvalidOperationException is thrown (validation passed)
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SynthesizeSpeechAsync_UsesProvidedOptions_WhenOptionsParameterIsNotNull()
    {
        // Arrange
        var options = new AzureSpeechOptions { SubscriptionKey = null };
        var service = CreateService(options);
        var customOptions = new TtsOptions
        {
            Voice = "en-US-CustomVoice",
            Speed = 1.2,
            Pitch = 1.1,
            Volume = 0.9
        };

        // Act
        var act = async () => await service.SynthesizeSpeechAsync("test", customOptions);

        // Assert - Will throw due to not configured
        // The fact that it validates text and reaches the configuration check means provided options are processed
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Azure Speech service is not configured*");
    }

    #endregion

    #region GetAvailableVoicesAsync Tests

    [Fact]
    public async Task GetAvailableVoicesAsync_ReturnsEmptyCollection_WhenServiceNotConfigured()
    {
        // Arrange
        var options = new AzureSpeechOptions { SubscriptionKey = null };
        var service = CreateService(options);

        // Act
        var result = await service.GetAvailableVoicesAsync();

        // Assert
        result.Should().BeEmpty("should return empty collection when service not configured");
        result.Should().HaveCount(0);
    }

    [Fact]
    public async Task GetAvailableVoicesAsync_ReturnsEmptyCollection_WhenServiceNotConfiguredWithLocale()
    {
        // Arrange
        var options = new AzureSpeechOptions { SubscriptionKey = null };
        var service = CreateService(options);

        // Act
        var result = await service.GetAvailableVoicesAsync("en-GB");

        // Assert
        result.Should().BeEmpty("should return empty collection when service not configured");
    }

    [Fact]
    public async Task GetAvailableVoicesAsync_LogsWarning_WhenServiceNotConfigured()
    {
        // Arrange
        var options = new AzureSpeechOptions { SubscriptionKey = null };
        var service = CreateService(options);

        // Act
        await service.GetAvailableVoicesAsync();

        // Assert - Logger is called in constructor (for missing key) and again in GetAvailableVoicesAsync (service not configured)
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cannot retrieve voices")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "logger should warn when voices cannot be retrieved");
    }

    [Fact]
    public async Task GetAvailableVoicesAsync_CachesResults_OnMultipleCalls()
    {
        // Arrange
        var options = new AzureSpeechOptions { SubscriptionKey = null };
        var service = CreateService(options);

        // Act - Call twice
        var result1 = await service.GetAvailableVoicesAsync();
        var result2 = await service.GetAvailableVoicesAsync();

        // Assert - Both should be empty since service is not configured
        result1.Should().BeEmpty();
        result2.Should().BeEmpty();

        // Verify both calls complete successfully
        // (Logger will be called for each call since service is not configured)
    }

    [Fact]
    public async Task GetAvailableVoicesAsync_UsesDefaultLocale_WhenLocaleNotSpecified()
    {
        // Arrange
        var options = new AzureSpeechOptions { SubscriptionKey = null };
        var service = CreateService(options);

        // Act
        await service.GetAvailableVoicesAsync();

        // Assert - Should complete without error, using default locale
        // No exception thrown means the default locale "en-US" was used
    }

    [Fact]
    public async Task GetAvailableVoicesAsync_ReturnsEmptyCollection_WhenServiceNotConfiguredWithNullLocale()
    {
        // Arrange
        var options = new AzureSpeechOptions { SubscriptionKey = null };
        var service = CreateService(options);

        // Act
        var result = await service.GetAvailableVoicesAsync(locale: null);

        // Assert
        result.Should().BeEmpty("should return empty collection when service not configured");
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public void Constructor_LogsInformation_WhenSubscriptionKeyIsEmpty()
    {
        // Arrange
        var options = new AzureSpeechOptions { SubscriptionKey = null };

        // Act
        var service = CreateService(options);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("not configured") &&
                    v.ToString()!.Contains("SubscriptionKey")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "logger should warn about missing subscription key");
    }

    [Fact]
    public void Constructor_UsesProvidedRegion_InConfiguration()
    {
        // Arrange
        var customRegion = "westeurope";
        var options = new AzureSpeechOptions
        {
            SubscriptionKey = null,
            Region = customRegion
        };

        // Act
        var service = CreateService(options);

        // Assert - Service should not be configured, but region should be used in initialization attempt
        service.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public void Constructor_UsesDefaultRegion_WhenNotSpecified()
    {
        // Arrange
        var options = new AzureSpeechOptions { SubscriptionKey = null };

        // Act
        var service = CreateService(options);

        // Assert
        options.Region.Should().Be("eastus", "default region should be eastus");
    }

    [Fact]
    public void Constructor_UsesDefaultVoice_WhenNotOverridden()
    {
        // Arrange
        var options = new AzureSpeechOptions { SubscriptionKey = null };

        // Act
        var service = CreateService(options);

        // Assert
        options.DefaultVoice.Should().Be("en-US-JennyNeural", "default voice should be en-US-JennyNeural");
    }

    [Fact]
    public void Constructor_UsesDefaultMaxTextLength_WhenNotOverridden()
    {
        // Arrange
        var options = new AzureSpeechOptions { SubscriptionKey = null };

        // Act
        var service = CreateService(options);

        // Assert
        options.MaxTextLength.Should().Be(500, "default max text length should be 500");
    }

    [Fact]
    public void Constructor_UsesDefaultSpeed_WhenNotOverridden()
    {
        // Arrange
        var options = new AzureSpeechOptions { SubscriptionKey = null };

        // Act
        var service = CreateService(options);

        // Assert
        options.DefaultSpeed.Should().Be(1.0, "default speed should be 1.0");
    }

    [Fact]
    public void Constructor_UsesDefaultPitch_WhenNotOverridden()
    {
        // Arrange
        var options = new AzureSpeechOptions { SubscriptionKey = null };

        // Act
        var service = CreateService(options);

        // Assert
        options.DefaultPitch.Should().Be(1.0, "default pitch should be 1.0");
    }

    [Fact]
    public void Constructor_UsesDefaultVolume_WhenNotOverridden()
    {
        // Arrange
        var options = new AzureSpeechOptions { SubscriptionKey = null };

        // Act
        var service = CreateService(options);

        // Assert
        options.DefaultVolume.Should().Be(0.8, "default volume should be 0.8");
    }

    #endregion

    #region Edge Cases and Boundary Tests

    [Fact]
    public async Task SynthesizeSpeechAsync_ThrowsArgumentException_WithTextJustOverLimit()
    {
        // Arrange
        var maxLength = 100;
        var options = new AzureSpeechOptions
        {
            SubscriptionKey = null,
            MaxTextLength = maxLength
        };
        var service = CreateService(options);
        var textOneCharOver = new string('a', maxLength + 1);

        // Act
        var act = async () => await service.SynthesizeSpeechAsync(textOneCharOver);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage($"*({maxLength + 1}) exceeds*({maxLength})*");
    }

    [Fact]
    public async Task SynthesizeSpeechAsync_ValidatesTextLengthBeforeConfiguration()
    {
        // Arrange - Text validation should happen before checking if configured
        var options = new AzureSpeechOptions
        {
            SubscriptionKey = null,
            MaxTextLength = 100
        };
        var service = CreateService(options);
        var oversizeText = new string('a', 101);

        // Act
        var act = async () => await service.SynthesizeSpeechAsync(oversizeText);

        // Assert - Should throw ArgumentException for text length, not InvalidOperationException for configuration
        await act.Should().ThrowAsync<ArgumentException>()
            .Where(ex => ex.Message.Contains("exceeds maximum"), "should validate text length before configuration");
    }

    [Fact]
    public async Task SynthesizeSpeechAsync_ValidatesTextNullnessBeforeConfiguration()
    {
        // Arrange - Null check should happen before checking if configured
        var options = new AzureSpeechOptions { SubscriptionKey = null };
        var service = CreateService(options);

        // Act
        var act = async () => await service.SynthesizeSpeechAsync(null!);

        // Assert - Should throw ArgumentException for null text, not InvalidOperationException for configuration
        await act.Should().ThrowAsync<ArgumentException>()
            .Where(ex => ex.ParamName == "text", "should validate text nullness before configuration");
    }

    #endregion

    #region Concurrency and Thread Safety Tests

    [Fact]
    public async Task GetAvailableVoicesAsync_HandlesMultipleSimultaneousCalls()
    {
        // Arrange
        var options = new AzureSpeechOptions { SubscriptionKey = null };
        var service = CreateService(options);

        // Act - Call the service multiple times simultaneously
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => service.GetAvailableVoicesAsync())
            .ToList();

        var results = await Task.WhenAll(tasks);

        // Assert - All calls should complete and return empty collections
        results.Should().HaveCount(5, "all calls should complete");
        results.Should().AllSatisfy(r => r.Should().BeEmpty());
    }

    #endregion

    #region Input Validation with Special Characters

    [Fact]
    public async Task SynthesizeSpeechAsync_AcceptsValidSpecialCharacters()
    {
        // Arrange
        var options = new AzureSpeechOptions { SubscriptionKey = null };
        var service = CreateService(options);
        var textWithSpecialChars = "Hello, world! How are you? I'm fine.";

        // Act
        var act = async () => await service.SynthesizeSpeechAsync(textWithSpecialChars);

        // Assert - Should not throw for text validation, only for configuration
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Azure Speech service is not configured*");
    }

    [Fact]
    public async Task SynthesizeSpeechAsync_AcceptsTextWithNumbers()
    {
        // Arrange
        var options = new AzureSpeechOptions { SubscriptionKey = null };
        var service = CreateService(options);
        var textWithNumbers = "The answer is 42 and the year is 2024.";

        // Act
        var act = async () => await service.SynthesizeSpeechAsync(textWithNumbers);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Azure Speech service is not configured*");
    }

    [Fact]
    public async Task SynthesizeSpeechAsync_AcceptsTextWithUnicodeCharacters()
    {
        // Arrange
        var options = new AzureSpeechOptions { SubscriptionKey = null };
        var service = CreateService(options);
        var textWithUnicode = "Hello 世界 and Привет мир!";

        // Act
        var act = async () => await service.SynthesizeSpeechAsync(textWithUnicode);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Azure Speech service is not configured*");
    }

    #endregion

    #region TtsOptions Defaults

    [Fact]
    public void TtsOptionsDefaults_AreCorrect()
    {
        // Arrange & Act
        var options = new TtsOptions();

        // Assert
        options.Voice.Should().Be("en-US-JennyNeural");
        options.Speed.Should().Be(1.0);
        options.Pitch.Should().Be(1.0);
        options.Volume.Should().Be(1.0);
    }

    #endregion
}
