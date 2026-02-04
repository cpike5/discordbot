using System.Security.Claims;
using Discord;
using Discord.Audio;
using Discord.WebSocket;
using DiscordBot.Bot.Controllers;
using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.DTOs.Portal;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Models;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Integration;

/// <summary>
/// Integration tests for the TTS Portal feature.
/// Tests the full OAuth to TTS flow, authorization, rate limiting, and database persistence.
/// </summary>
public class PortalTtsIntegrationTests : IDisposable
{
    private readonly BotDbContext _dbContext;
    private readonly SqliteConnection _connection;
    private readonly PortalTtsController _controller;
    private readonly Mock<ITtsService> _mockTtsService;
    private readonly Mock<ITtsSettingsService> _mockTtsSettingsService;
    private readonly Mock<ITtsMessageRepository> _mockTtsMessageRepository;
    private readonly Mock<IAudioService> _mockAudioService;
    private readonly Mock<IPlaybackService> _mockPlaybackService;
    private readonly Mock<ITtsPlaybackService> _mockTtsPlaybackService;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<DiscordSocketClient> _mockDiscordClient;
    private readonly Mock<IVoiceCapabilityProvider> _mockVoiceCapabilityProvider;
    private readonly Mock<IStylePresetProvider> _mockStylePresetProvider;
    private readonly Mock<ISsmlValidator> _mockSsmlValidator;
    private readonly Mock<ISsmlBuilder> _mockSsmlBuilder;
    private readonly Mock<ILogger<PortalTtsController>> _mockLogger;
    private readonly AzureSpeechOptions _azureSpeechOptions;

    public PortalTtsIntegrationTests()
    {
        // Setup in-memory database
        (_dbContext, _connection) = TestDbContextFactory.CreateContext();

        // Setup mocks
        _mockTtsService = new Mock<ITtsService>();
        _mockTtsSettingsService = new Mock<ITtsSettingsService>();
        _mockTtsMessageRepository = new Mock<ITtsMessageRepository>();
        _mockAudioService = new Mock<IAudioService>();
        _mockPlaybackService = new Mock<IPlaybackService>();
        _mockTtsPlaybackService = new Mock<ITtsPlaybackService>();
        _mockSettingsService = new Mock<ISettingsService>();
        _mockDiscordClient = new Mock<DiscordSocketClient>(MockBehavior.Default, new DiscordSocketConfig());
        _mockVoiceCapabilityProvider = new Mock<IVoiceCapabilityProvider>();
        _mockStylePresetProvider = new Mock<IStylePresetProvider>();
        _mockSsmlValidator = new Mock<ISsmlValidator>();
        _mockSsmlBuilder = new Mock<ISsmlBuilder>();
        _mockLogger = new Mock<ILogger<PortalTtsController>>();

        // Setup bot-level audio enabled by default
        _mockSettingsService.Setup(s => s.GetSettingValueAsync<bool?>("Features:AudioEnabled", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Setup Azure Speech options
        _azureSpeechOptions = new AzureSpeechOptions
        {
            SubscriptionKey = "test-key",
            Region = "eastus",
            MaxTextLength = 500
        };

        // Create controller instance
        _controller = new PortalTtsController(
            _mockTtsService.Object,
            _mockTtsSettingsService.Object,
            _mockTtsMessageRepository.Object,
            _mockAudioService.Object,
            _mockPlaybackService.Object,
            _mockTtsPlaybackService.Object,
            _mockSettingsService.Object,
            _mockDiscordClient.Object,
            Options.Create(_azureSpeechOptions),
            _mockVoiceCapabilityProvider.Object,
            _mockStylePresetProvider.Object,
            _mockSsmlValidator.Object,
            _mockSsmlBuilder.Object,
            _mockLogger.Object);

        // Setup HttpContext with Discord claims (simulating OAuth authentication)
        SetupAuthenticatedUser();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    /// <summary>
    /// Sets up authenticated user context with Discord OAuth claims.
    /// </summary>
    private void SetupAuthenticatedUser(ulong? userId = 123456789UL, string? username = "TestUser")
    {
        var claims = new List<Claim>
        {
            new Claim("DiscordId", userId?.ToString() ?? "123456789"),
            new Claim(ClaimTypes.Name, username ?? "TestUser"),
            new Claim(ClaimTypes.NameIdentifier, userId?.ToString() ?? "123456789")
        };

        var identity = new ClaimsIdentity(claims, "Discord");
        var principal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };
    }

    /// <summary>
    /// Creates a mock AudioOutStream for testing.
    /// </summary>
    private AudioOutStream CreateMockAudioStream()
    {
        var mockStream = new Mock<AudioOutStream>();
        mockStream.Setup(s => s.WriteAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockStream.Setup(s => s.FlushAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mockStream.Object;
    }

    #region Full OAuth to TTS Flow Tests

    [Fact]
    public async Task SendTts_FullFlow_WithValidRequest_SuccessfullyPlaysAndPersistsToDatabase()
    {
        // Arrange
        const ulong guildId = 111111111UL;
        const ulong userId = 123456789UL;
        const string message = "Hello, this is a test message";
        const string voiceName = "en-US-AvaNeural";
        const float speed = 1.0f;
        const float pitch = 1.0f;

        var request = new SendTtsRequest
        {
            Message = message,
            Voice = voiceName,
            Speed = speed,
            Pitch = pitch
        };

        // Setup TTS settings for guild
        var ttsSettings = new GuildTtsSettings
        {
            GuildId = guildId,
            TtsEnabled = true,
            MaxMessageLength = 500,
            RateLimitPerMinute = 10,
            DefaultVoice = voiceName
        };

        _mockTtsSettingsService
            .Setup(s => s.GetOrCreateSettingsAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ttsSettings);

        // Setup audio service - bot is connected
        _mockAudioService.Setup(s => s.IsConnected(guildId)).Returns(true);
        _mockAudioService.Setup(s => s.GetConnectedChannelId(guildId)).Returns(123123123UL);

        // Setup rate limit check - user is not rate limited
        _mockTtsSettingsService
            .Setup(s => s.IsUserRateLimitedAsync(guildId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Setup TTS synthesis to return audio stream
        var audioStream = new MemoryStream(new byte[192000]); // 1 second of PCM audio at 48kHz stereo
        _mockTtsService
            .Setup(s => s.SynthesizeSpeechAsync(message, It.IsAny<TtsOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(audioStream);

        // Setup PCM stream for playback
        var mockPcmStream = CreateMockAudioStream();
        _mockAudioService.Setup(s => s.GetOrCreatePcmStream(guildId)).Returns(mockPcmStream);

        // Setup repository to capture the saved message
        var savedMessage = new TtsMessage();
        _mockTtsMessageRepository
            .Setup(r => r.AddAsync(It.IsAny<TtsMessage>(), It.IsAny<CancellationToken>()))
            .Callback<TtsMessage, CancellationToken>((msg, ct) =>
            {
                savedMessage = msg;
            })
            .ReturnsAsync(new TtsMessage());

        // Act
        var result = await _controller.SendTts(guildId, request, CancellationToken.None);

        // Assert - verify HTTP response
        result.Should().NotBeNull();
        result.Should().BeOfType<OkObjectResult>();

        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);

        // Assert - verify database persistence
        _mockTtsMessageRepository.Verify(
            r => r.AddAsync(It.IsAny<TtsMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);

        savedMessage.GuildId.Should().Be(guildId);
        savedMessage.UserId.Should().Be(userId);
        savedMessage.Message.Should().Be(message);
        savedMessage.Voice.Should().Be(voiceName);
        savedMessage.DurationSeconds.Should().BeGreaterThan(0);
        savedMessage.CreatedAt.Should().BeOnOrBefore(DateTime.UtcNow.AddSeconds(1));

        // Assert - verify audio service interactions
        _mockAudioService.Verify(s => s.IsConnected(guildId), Times.Once);
        _mockAudioService.Verify(s => s.UpdateLastActivity(guildId), Times.Once);
        _mockTtsSettingsService.Verify(
            s => s.IsUserRateLimitedAsync(guildId, userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendTts_FullFlow_WithMaxLengthMessage_SuccessfullyProcesses()
    {
        // Arrange
        const ulong guildId = 111111111UL;
        const ulong userId = 123456789UL;
        const int maxLength = 500;
        var message = new string('a', maxLength);

        var request = new SendTtsRequest
        {
            Message = message,
            Voice = "en-US-AvaNeural",
            Speed = 1.0f,
            Pitch = 1.0f
        };

        var ttsSettings = new GuildTtsSettings
        {
            GuildId = guildId,
            TtsEnabled = true,
            MaxMessageLength = maxLength,
            RateLimitPerMinute = 10,
            DefaultVoice = "en-US-AvaNeural"
        };

        _mockTtsSettingsService
            .Setup(s => s.GetOrCreateSettingsAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ttsSettings);

        _mockAudioService.Setup(s => s.IsConnected(guildId)).Returns(true);
        _mockAudioService.Setup(s => s.GetConnectedChannelId(guildId)).Returns(123123123UL);

        _mockTtsSettingsService
            .Setup(s => s.IsUserRateLimitedAsync(guildId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var audioStream = new MemoryStream(new byte[192000]);
        _mockTtsService
            .Setup(s => s.SynthesizeSpeechAsync(message, It.IsAny<TtsOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(audioStream);

        var mockPcmStream = CreateMockAudioStream();
        _mockAudioService.Setup(s => s.GetOrCreatePcmStream(guildId)).Returns(mockPcmStream);

        _mockTtsMessageRepository
            .Setup(r => r.AddAsync(It.IsAny<TtsMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TtsMessage());

        // Act
        var result = await _controller.SendTts(guildId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task SendTts_WithTtsDisabled_ReturnsBadRequest()
    {
        // Arrange
        const ulong guildId = 111111111UL;
        var request = new SendTtsRequest
        {
            Message = "Test message",
            Voice = "en-US-AvaNeural",
            Speed = 1.0f,
            Pitch = 1.0f
        };

        var ttsSettings = new GuildTtsSettings
        {
            GuildId = guildId,
            TtsEnabled = false,
            MaxMessageLength = 500,
            RateLimitPerMinute = 10,
            DefaultVoice = "en-US-AvaNeural"
        };

        _mockTtsSettingsService
            .Setup(s => s.GetOrCreateSettingsAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ttsSettings);

        // Act
        var result = await _controller.SendTts(guildId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result as BadRequestObjectResult;
        badRequest.Should().NotBeNull();
        badRequest!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var errorDto = badRequest.Value as ApiErrorDto;
        errorDto.Should().NotBeNull();
        errorDto!.Message.Should().Contain("not enabled");
    }

    [Fact]
    public async Task SendTts_WhenBotNotConnectedToVoice_ReturnsBadRequest()
    {
        // Arrange
        const ulong guildId = 111111111UL;
        var request = new SendTtsRequest
        {
            Message = "Test message",
            Voice = "en-US-AvaNeural",
            Speed = 1.0f,
            Pitch = 1.0f
        };

        var ttsSettings = new GuildTtsSettings
        {
            GuildId = guildId,
            TtsEnabled = true,
            MaxMessageLength = 500,
            RateLimitPerMinute = 10,
            DefaultVoice = "en-US-AvaNeural"
        };

        _mockTtsSettingsService
            .Setup(s => s.GetOrCreateSettingsAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ttsSettings);

        // Bot is not connected to voice
        _mockAudioService.Setup(s => s.IsConnected(guildId)).Returns(false);

        // Act
        var result = await _controller.SendTts(guildId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result as BadRequestObjectResult;
        badRequest.Should().NotBeNull();
        badRequest!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var errorDto = badRequest.Value as ApiErrorDto;
        errorDto.Should().NotBeNull();
        errorDto!.Message.Should().Contain("voice channel");
    }

    #endregion

    #region Message Validation Tests

    [Fact]
    public async Task SendTts_WithEmptyMessage_ReturnsBadRequest()
    {
        // Arrange
        const ulong guildId = 111111111UL;
        var request = new SendTtsRequest
        {
            Message = "",
            Voice = "en-US-AvaNeural",
            Speed = 1.0f,
            Pitch = 1.0f
        };

        var ttsSettings = new GuildTtsSettings
        {
            GuildId = guildId,
            TtsEnabled = true,
            MaxMessageLength = 500,
            RateLimitPerMinute = 10,
            DefaultVoice = "en-US-AvaNeural"
        };

        _mockTtsSettingsService
            .Setup(s => s.GetOrCreateSettingsAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ttsSettings);

        _mockAudioService.Setup(s => s.IsConnected(guildId)).Returns(true);

        // Act
        var result = await _controller.SendTts(guildId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result as BadRequestObjectResult;
        badRequest!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var errorDto = badRequest.Value as ApiErrorDto;
        errorDto!.Message.Should().Contain("empty");
    }

    [Fact]
    public async Task SendTts_WithWhitespaceOnlyMessage_ReturnsBadRequest()
    {
        // Arrange
        const ulong guildId = 111111111UL;
        var request = new SendTtsRequest
        {
            Message = "   \t\n  ",
            Voice = "en-US-AvaNeural",
            Speed = 1.0f,
            Pitch = 1.0f
        };

        var ttsSettings = new GuildTtsSettings
        {
            GuildId = guildId,
            TtsEnabled = true,
            MaxMessageLength = 500,
            RateLimitPerMinute = 10,
            DefaultVoice = "en-US-AvaNeural"
        };

        _mockTtsSettingsService
            .Setup(s => s.GetOrCreateSettingsAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ttsSettings);

        _mockAudioService.Setup(s => s.IsConnected(guildId)).Returns(true);

        // Act
        var result = await _controller.SendTts(guildId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SendTts_WithMessageExceedingMaxLength_ReturnsBadRequest()
    {
        // Arrange
        const ulong guildId = 111111111UL;
        const int maxLength = 100;
        var tooLongMessage = new string('a', maxLength + 1);

        var request = new SendTtsRequest
        {
            Message = tooLongMessage,
            Voice = "en-US-AvaNeural",
            Speed = 1.0f,
            Pitch = 1.0f
        };

        var ttsSettings = new GuildTtsSettings
        {
            GuildId = guildId,
            TtsEnabled = true,
            MaxMessageLength = maxLength,
            RateLimitPerMinute = 10,
            DefaultVoice = "en-US-AvaNeural"
        };

        _mockTtsSettingsService
            .Setup(s => s.GetOrCreateSettingsAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ttsSettings);

        _mockAudioService.Setup(s => s.IsConnected(guildId)).Returns(true);

        // Act
        var result = await _controller.SendTts(guildId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result as BadRequestObjectResult;
        badRequest!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var errorDto = badRequest.Value as ApiErrorDto;
        errorDto!.Message.Should().Contain("too long");
    }

    #endregion

    #region Rate Limiting Tests

    [Fact]
    public async Task SendTts_WhenUserRateLimited_Returns429TooManyRequests()
    {
        // Arrange
        const ulong guildId = 111111111UL;
        const ulong userId = 123456789UL;
        const int rateLimitPerMinute = 5;

        var request = new SendTtsRequest
        {
            Message = "Test message",
            Voice = "en-US-AvaNeural",
            Speed = 1.0f,
            Pitch = 1.0f
        };

        var ttsSettings = new GuildTtsSettings
        {
            GuildId = guildId,
            TtsEnabled = true,
            MaxMessageLength = 500,
            RateLimitPerMinute = rateLimitPerMinute,
            DefaultVoice = "en-US-AvaNeural"
        };

        _mockTtsSettingsService
            .Setup(s => s.GetOrCreateSettingsAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ttsSettings);

        _mockAudioService.Setup(s => s.IsConnected(guildId)).Returns(true);

        // User is rate limited
        _mockTtsSettingsService
            .Setup(s => s.IsUserRateLimitedAsync(guildId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.SendTts(guildId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = result as ObjectResult;
        objectResult!.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);

        var errorDto = objectResult.Value as ApiErrorDto;
        errorDto.Should().NotBeNull();
        errorDto!.Message.Should().Contain("Rate limit");
        errorDto.Detail.Should().Contain(rateLimitPerMinute.ToString());
    }

    [Fact]
    public async Task SendTts_RateLimitingPerUser_WithMultipleUsers_EnforcesIndividually()
    {
        // Arrange
        const ulong guildId = 111111111UL;
        const ulong user1Id = 111111111UL;
        const ulong user2Id = 222222222UL;

        var request = new SendTtsRequest
        {
            Message = "Test message",
            Voice = "en-US-AvaNeural",
            Speed = 1.0f,
            Pitch = 1.0f
        };

        var ttsSettings = new GuildTtsSettings
        {
            GuildId = guildId,
            TtsEnabled = true,
            MaxMessageLength = 500,
            RateLimitPerMinute = 10,
            DefaultVoice = "en-US-AvaNeural"
        };

        _mockTtsSettingsService
            .Setup(s => s.GetOrCreateSettingsAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ttsSettings);

        _mockAudioService.Setup(s => s.IsConnected(guildId)).Returns(true);

        var audioStream = new MemoryStream(new byte[192000]);
        _mockTtsService
            .Setup(s => s.SynthesizeSpeechAsync(It.IsAny<string>(), It.IsAny<TtsOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(audioStream);

        var mockPcmStream = CreateMockAudioStream();
        _mockAudioService.Setup(s => s.GetOrCreatePcmStream(guildId)).Returns(mockPcmStream);

        _mockTtsMessageRepository
            .Setup(r => r.AddAsync(It.IsAny<TtsMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TtsMessage());

        // User 1 is NOT rate limited
        _mockTtsSettingsService
            .Setup(s => s.IsUserRateLimitedAsync(guildId, user1Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // User 2 IS rate limited
        _mockTtsSettingsService
            .Setup(s => s.IsUserRateLimitedAsync(guildId, user2Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act - User 1 sends message (should succeed)
        SetupAuthenticatedUser(user1Id, "User1");
        var result1 = await _controller.SendTts(guildId, request, CancellationToken.None);

        // Act - User 2 sends message (should be rate limited)
        SetupAuthenticatedUser(user2Id, "User2");
        var result2 = await _controller.SendTts(guildId, request, CancellationToken.None);

        // Assert
        result1.Should().BeOfType<OkObjectResult>();
        result2.Should().BeOfType<ObjectResult>();
        (result2 as ObjectResult)!.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
    }

    #endregion

    #region TTS Service Failure Tests

    [Fact]
    public async Task SendTts_WhenTtsServiceNotConfigured_ReturnsBadRequest()
    {
        // Arrange
        const ulong guildId = 111111111UL;
        var request = new SendTtsRequest
        {
            Message = "Test message",
            Voice = "en-US-AvaNeural",
            Speed = 1.0f,
            Pitch = 1.0f
        };

        var ttsSettings = new GuildTtsSettings
        {
            GuildId = guildId,
            TtsEnabled = true,
            MaxMessageLength = 500,
            RateLimitPerMinute = 10,
            DefaultVoice = "en-US-AvaNeural"
        };

        _mockTtsSettingsService
            .Setup(s => s.GetOrCreateSettingsAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ttsSettings);

        _mockAudioService.Setup(s => s.IsConnected(guildId)).Returns(true);

        _mockTtsSettingsService
            .Setup(s => s.IsUserRateLimitedAsync(guildId, It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // TTS service throws InvalidOperationException (not configured)
        _mockTtsService
            .Setup(s => s.SynthesizeSpeechAsync(It.IsAny<string>(), It.IsAny<TtsOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("TTS service not configured"));

        // Act
        var result = await _controller.SendTts(guildId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result as BadRequestObjectResult;
        badRequest!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var errorDto = badRequest.Value as ApiErrorDto;
        errorDto!.Message.Should().Contain("not available");
    }

    [Fact]
    public async Task SendTts_WhenTtsRequestInvalid_ReturnsBadRequest()
    {
        // Arrange
        const ulong guildId = 111111111UL;
        var request = new SendTtsRequest
        {
            Message = "Test message",
            Voice = "invalid-voice-name",
            Speed = 1.0f,
            Pitch = 1.0f
        };

        var ttsSettings = new GuildTtsSettings
        {
            GuildId = guildId,
            TtsEnabled = true,
            MaxMessageLength = 500,
            RateLimitPerMinute = 10,
            DefaultVoice = "en-US-AvaNeural"
        };

        _mockTtsSettingsService
            .Setup(s => s.GetOrCreateSettingsAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ttsSettings);

        _mockAudioService.Setup(s => s.IsConnected(guildId)).Returns(true);

        _mockTtsSettingsService
            .Setup(s => s.IsUserRateLimitedAsync(guildId, It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // TTS service throws ArgumentException (invalid voice)
        _mockTtsService
            .Setup(s => s.SynthesizeSpeechAsync(It.IsAny<string>(), It.IsAny<TtsOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid voice name"));

        // Act
        var result = await _controller.SendTts(guildId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result as BadRequestObjectResult;
        badRequest!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var errorDto = badRequest.Value as ApiErrorDto;
        errorDto!.Message.Should().Contain("Invalid");
    }

    [Fact]
    public async Task SendTts_WhenAudioStreamingFails_ReturnsBadRequest()
    {
        // Arrange
        const ulong guildId = 111111111UL;
        var request = new SendTtsRequest
        {
            Message = "Test message",
            Voice = "en-US-AvaNeural",
            Speed = 1.0f,
            Pitch = 1.0f
        };

        var ttsSettings = new GuildTtsSettings
        {
            GuildId = guildId,
            TtsEnabled = true,
            MaxMessageLength = 500,
            RateLimitPerMinute = 10,
            DefaultVoice = "en-US-AvaNeural"
        };

        _mockTtsSettingsService
            .Setup(s => s.GetOrCreateSettingsAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ttsSettings);

        _mockAudioService.Setup(s => s.IsConnected(guildId)).Returns(true);

        _mockTtsSettingsService
            .Setup(s => s.IsUserRateLimitedAsync(guildId, It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var audioStream = new MemoryStream(new byte[192000]);
        _mockTtsService
            .Setup(s => s.SynthesizeSpeechAsync(It.IsAny<string>(), It.IsAny<TtsOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(audioStream);

        // PCM stream is null - connection failed
        _mockAudioService.Setup(s => s.GetOrCreatePcmStream(guildId)).Returns((AudioOutStream?)null);

        // Act
        var result = await _controller.SendTts(guildId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result as BadRequestObjectResult;
        badRequest!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var errorDto = badRequest.Value as ApiErrorDto;
        errorDto!.Message.Should().Contain("audio stream");
    }

    #endregion

    #region Voice Channel Tests

    [Fact]
    public void GetStatus_WhenConnected_ReturnsStatusWithChannelInfo()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        const ulong channelId = 987654321UL;

        _mockAudioService.Setup(s => s.IsConnected(guildId)).Returns(true);
        _mockAudioService.Setup(s => s.GetConnectedChannelId(guildId)).Returns(channelId);
        _mockPlaybackService.Setup(s => s.IsPlaying(guildId)).Returns(false);

        var mockGuild = new Mock<SocketGuild>();
        var mockChannel = new Mock<SocketVoiceChannel>();
        mockChannel.Setup(c => c.Name).Returns("General Voice");

        mockGuild.Setup(g => g.GetVoiceChannel(channelId)).Returns(mockChannel.Object);
        _mockDiscordClient.Setup(c => c.GetGuild(guildId)).Returns(mockGuild.Object);

        // Act
        var result = _controller.GetStatus(guildId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeOfType<TtsStatusResponse>();

        var status = okResult.Value as TtsStatusResponse;
        status.Should().NotBeNull();
        status!.IsConnected.Should().BeTrue();
        status.ChannelId.Should().Be(channelId);
        status.ChannelName.Should().Be("General Voice");
        status.IsPlaying.Should().BeFalse();
    }

    [Fact]
    public void GetStatus_WhenNotConnected_ReturnsDisconnectedStatus()
    {
        // Arrange
        const ulong guildId = 123456789UL;

        _mockAudioService.Setup(s => s.IsConnected(guildId)).Returns(false);
        _mockAudioService.Setup(s => s.GetConnectedChannelId(guildId)).Returns((ulong?)null);
        _mockPlaybackService.Setup(s => s.IsPlaying(guildId)).Returns(false);

        // Act
        var result = _controller.GetStatus(guildId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeOfType<TtsStatusResponse>();

        var status = okResult.Value as TtsStatusResponse;
        status.Should().NotBeNull();
        status!.IsConnected.Should().BeFalse();
        status.ChannelId.Should().BeNull();
        status.IsPlaying.Should().BeFalse();
    }

    [Fact]
    public void GetVoiceChannels_WithValidGuild_ReturnsChannels()
    {
        // Arrange
        const ulong guildId = 123456789UL;

        var mockGuild = new Mock<SocketGuild>();
        var mockChannel1 = new Mock<SocketVoiceChannel>();
        mockChannel1.Setup(c => c.Id).Returns(111111111UL);
        mockChannel1.Setup(c => c.Name).Returns("General");
        mockChannel1.Setup(c => c.Position).Returns(0);

        var mockChannel2 = new Mock<SocketVoiceChannel>();
        mockChannel2.Setup(c => c.Id).Returns(222222222UL);
        mockChannel2.Setup(c => c.Name).Returns("Gaming");
        mockChannel2.Setup(c => c.Position).Returns(1);

        mockGuild.Setup(g => g.VoiceChannels).Returns(new[] { mockChannel1.Object, mockChannel2.Object });
        _mockDiscordClient.Setup(c => c.GetGuild(guildId)).Returns(mockGuild.Object);

        // Act
        var result = _controller.GetVoiceChannels(guildId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeOfType<List<object>>();

        var channels = okResult.Value as List<object>;
        channels.Should().HaveCount(2);
    }

    [Fact]
    public void GetVoiceChannels_WithNonExistentGuild_ReturnsNotFound()
    {
        // Arrange
        const ulong guildId = 999999999UL;

        _mockDiscordClient.Setup(c => c.GetGuild(guildId)).Returns((SocketGuild?)null);

        // Act
        var result = _controller.GetVoiceChannels(guildId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
        var notFoundResult = result as NotFoundObjectResult;
        notFoundResult!.StatusCode.Should().Be(StatusCodes.Status404NotFound);

        var errorDto = notFoundResult.Value as ApiErrorDto;
        errorDto!.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task JoinChannel_WithValidRequest_SuccessfullyJoins()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        const ulong channelId = 987654321UL;

        var request = new PortalTtsController.JoinChannelRequest { ChannelId = channelId };

        var ttsSettings = new GuildTtsSettings
        {
            GuildId = guildId,
            TtsEnabled = true,
            MaxMessageLength = 500,
            RateLimitPerMinute = 10,
            DefaultVoice = "en-US-AvaNeural"
        };

        _mockTtsSettingsService
            .Setup(s => s.GetOrCreateSettingsAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ttsSettings);

        var mockAudioClient = new Mock<IAudioClient>();
        _mockAudioService
            .Setup(s => s.JoinChannelAsync(guildId, channelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockAudioClient.Object);

        // Act
        var result = await _controller.JoinChannel(guildId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task JoinChannel_WithTtsDisabled_ReturnsBadRequest()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        const ulong channelId = 987654321UL;

        var request = new PortalTtsController.JoinChannelRequest { ChannelId = channelId };

        var ttsSettings = new GuildTtsSettings
        {
            GuildId = guildId,
            TtsEnabled = false,
            MaxMessageLength = 500,
            RateLimitPerMinute = 10,
            DefaultVoice = "en-US-AvaNeural"
        };

        _mockTtsSettingsService
            .Setup(s => s.GetOrCreateSettingsAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ttsSettings);

        // Act
        var result = await _controller.JoinChannel(guildId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result as BadRequestObjectResult;
        badRequest!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task JoinChannel_WithInvalidChannel_ReturnsNotFound()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        const ulong channelId = 999999999UL;

        var request = new PortalTtsController.JoinChannelRequest { ChannelId = channelId };

        var ttsSettings = new GuildTtsSettings
        {
            GuildId = guildId,
            TtsEnabled = true,
            MaxMessageLength = 500,
            RateLimitPerMinute = 10,
            DefaultVoice = "en-US-AvaNeural"
        };

        _mockTtsSettingsService
            .Setup(s => s.GetOrCreateSettingsAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ttsSettings);

        _mockAudioService
            .Setup(s => s.JoinChannelAsync(guildId, channelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IAudioClient?)null);

        // Act
        var result = await _controller.JoinChannel(guildId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task LeaveChannel_WhenConnected_SuccessfullyDisconnects()
    {
        // Arrange
        const ulong guildId = 123456789UL;

        _mockAudioService.Setup(s => s.IsConnected(guildId)).Returns(true);
        _mockPlaybackService.Setup(s => s.StopAsync(guildId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mockAudioService.Setup(s => s.LeaveChannelAsync(guildId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var result = await _controller.LeaveChannel(guildId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);

        _mockPlaybackService.Verify(s => s.StopAsync(guildId, It.IsAny<CancellationToken>()), Times.Once);
        _mockAudioService.Verify(s => s.LeaveChannelAsync(guildId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LeaveChannel_WhenNotConnected_ReturnsBadRequest()
    {
        // Arrange
        const ulong guildId = 123456789UL;

        _mockAudioService.Setup(s => s.IsConnected(guildId)).Returns(false);

        // Act
        var result = await _controller.LeaveChannel(guildId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result as BadRequestObjectResult;
        badRequest!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    #endregion

    #region Playback Control Tests

    [Fact]
    public async Task StopPlayback_WhenPlaying_StopsSuccessfully()
    {
        // Arrange
        const ulong guildId = 123456789UL;

        _mockAudioService.Setup(s => s.IsConnected(guildId)).Returns(true);
        _mockPlaybackService.Setup(s => s.IsPlaying(guildId)).Returns(true);
        _mockPlaybackService.Setup(s => s.StopAsync(guildId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.StopPlayback(guildId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);

        _mockPlaybackService.Verify(s => s.StopAsync(guildId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StopPlayback_WhenNotPlaying_ReturnsOkWithNothingPlayingMessage()
    {
        // Arrange
        const ulong guildId = 123456789UL;

        _mockAudioService.Setup(s => s.IsConnected(guildId)).Returns(true);
        _mockPlaybackService.Setup(s => s.IsPlaying(guildId)).Returns(false);

        // Act
        var result = await _controller.StopPlayback(guildId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);

        _mockPlaybackService.Verify(s => s.StopAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StopPlayback_WhenNotConnected_ReturnsBadRequest()
    {
        // Arrange
        const ulong guildId = 123456789UL;

        _mockAudioService.Setup(s => s.IsConnected(guildId)).Returns(false);

        // Act
        var result = await _controller.StopPlayback(guildId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result as BadRequestObjectResult;
        badRequest!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    #endregion
}
