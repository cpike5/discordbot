using System.Security.Claims;
using Discord;
using Discord.Audio;
using Discord.WebSocket;
using DiscordBot.Bot.Controllers;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Services.Tts;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Exceptions;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.DTOs.Portal;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Controllers;

/// <summary>
/// Unit tests for <see cref="PortalTtsPlaybackController"/> (the connection/playback-control
/// slice of the former monolithic PortalTtsController: status, send, channels, stop).
/// </summary>
public class PortalTtsControllerTests
{
    private readonly Mock<ITtsService> _mockTtsService;
    private readonly Mock<ITtsSettingsService> _mockTtsSettingsService;
    private readonly Mock<ITtsMessageRepository> _mockTtsMessageRepository;
    private readonly Mock<IAudioService> _mockAudioService;
    private readonly Mock<IPlaybackService> _mockPlaybackService;
    private readonly Mock<ITtsPlaybackService> _mockTtsPlaybackService;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<IPortalGuildDirectory> _mockGuildDirectory;
    private readonly Mock<IVoiceCapabilityProvider> _mockVoiceCapabilityProvider;
    private readonly Mock<IStylePresetProvider> _mockStylePresetProvider;
    private readonly Mock<ISsmlValidator> _mockSsmlValidator;
    private readonly Mock<ISsmlBuilder> _mockSsmlBuilder;
    private readonly Mock<IUserTtsPresetRepository> _mockUserTtsPresetRepository;
    private readonly Mock<ITtsMessageHistoryRepository> _mockTtsMessageHistoryRepository;
    private readonly Mock<IAudioModerationLogService> _mockAudioModerationLogService;
    private readonly Mock<ILogger<PortalTtsPlaybackController>> _mockLogger;
    private readonly ITtsSendPipeline _sendPipeline;
    private readonly PortalTtsPlaybackController _controller;

    public PortalTtsControllerTests()
    {
        _mockTtsService = new Mock<ITtsService>();
        _mockTtsSettingsService = new Mock<ITtsSettingsService>();
        _mockTtsMessageRepository = new Mock<ITtsMessageRepository>();
        _mockAudioService = new Mock<IAudioService>();
        _mockPlaybackService = new Mock<IPlaybackService>();
        _mockTtsPlaybackService = new Mock<ITtsPlaybackService>();
        _mockSettingsService = new Mock<ISettingsService>();
        _mockGuildDirectory = new Mock<IPortalGuildDirectory>();
        _mockVoiceCapabilityProvider = new Mock<IVoiceCapabilityProvider>();
        _mockStylePresetProvider = new Mock<IStylePresetProvider>();
        _mockSsmlValidator = new Mock<ISsmlValidator>();
        _mockSsmlBuilder = new Mock<ISsmlBuilder>();
        _mockUserTtsPresetRepository = new Mock<IUserTtsPresetRepository>();
        _mockTtsMessageHistoryRepository = new Mock<ITtsMessageHistoryRepository>();
        _mockAudioModerationLogService = new Mock<IAudioModerationLogService>();
        _mockLogger = new Mock<ILogger<PortalTtsPlaybackController>>();

        // Setup bot-level audio enabled by default
        _mockSettingsService.Setup(s => s.GetSettingValueAsync<bool?>("Features:AudioEnabled", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var azureSpeechOptions = Options.Create(new AzureSpeechOptions
        {
            SubscriptionKey = "test-key",
            Region = "eastus"
        });

        // TtsSendPipeline resolves its scoped/transient dependencies via IServiceScopeFactory,
        // so register the same mocks the tests configure directly.
        var services = new ServiceCollection();
        services.AddScoped(_ => _mockTtsSettingsService.Object);
        services.AddScoped(_ => _mockSsmlBuilder.Object);
        services.AddScoped(_ => _mockAudioModerationLogService.Object);
        services.AddScoped(_ => _mockTtsPlaybackService.Object);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        _sendPipeline = new TtsSendPipeline(
            _mockTtsService.Object,
            _mockSettingsService.Object,
            _mockAudioService.Object,
            scopeFactory,
            new Mock<ILogger<TtsSendPipeline>>().Object);

        _controller = new PortalTtsPlaybackController(
            _sendPipeline,
            _mockAudioService.Object,
            _mockPlaybackService.Object,
            _mockGuildDirectory.Object,
            azureSpeechOptions,
            _mockLogger.Object);

        // Setup HttpContext and User claims
        var claims = new List<Claim>
        {
            new Claim("discord:user_id", "123456789"),
            new Claim(ClaimTypes.Name, "TestUser")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };
    }

    #region GetStatus Tests

    [Fact]
    public void GetStatus_WhenConnected_ReturnsStatusWithChannelInfo()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        const ulong channelId = 987654321UL;

        _mockAudioService.Setup(s => s.IsConnected(guildId)).Returns(true);
        _mockAudioService.Setup(s => s.GetConnectedChannelId(guildId)).Returns(channelId);
        _mockPlaybackService.Setup(s => s.IsPlaying(guildId)).Returns(false);

        // Note: Discord.NET classes are not mockable, controller handles null guild/channel gracefully
        _mockGuildDirectory.Setup(d => d.IsGuildAvailableAsync(guildId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        var result = _controller.GetStatus(guildId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<OkObjectResult>();

        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeOfType<TtsStatusResponse>();

        var status = okResult.Value as TtsStatusResponse;
        status.Should().NotBeNull();
        status!.IsConnected.Should().BeTrue();
        status.ChannelId.Should().Be(channelId);
        status.ChannelName.Should().BeNull(); // No guild means no channel name
        status.IsPlaying.Should().BeFalse();
        status.CurrentMessage.Should().BeNull();
    }

    [Fact]
    public void GetStatus_WhenNotConnected_ReturnsStatusWithNoChannel()
    {
        // Arrange
        const ulong guildId = 123456789UL;

        _mockAudioService.Setup(s => s.IsConnected(guildId)).Returns(false);
        _mockAudioService.Setup(s => s.GetConnectedChannelId(guildId)).Returns((ulong?)null);
        _mockPlaybackService.Setup(s => s.IsPlaying(guildId)).Returns(false);

        // Act
        var result = _controller.GetStatus(guildId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<OkObjectResult>();

        var okResult = result as OkObjectResult;
        var status = okResult!.Value as TtsStatusResponse;
        status.Should().NotBeNull();
        status!.IsConnected.Should().BeFalse();
        status.ChannelId.Should().BeNull();
        status.ChannelName.Should().BeNull();
    }

    #endregion

    #region SendTts Tests

    [Fact]
    public async Task SendTts_WithPcmStreamFailure_Returns400()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var request = new SendTtsRequest
        {
            Message = "Hello world",
            Voice = "en-US-JennyNeural",
            Speed = 1.0,
            Pitch = 1.0
        };

        var settings = new GuildTtsSettings
        {
            GuildId = guildId,
            TtsEnabled = true,
            MaxMessageLength = 500,
            RateLimitPerMinute = 5
        };

        _mockTtsSettingsService
            .Setup(s => s.GetOrCreateSettingsAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);
        _mockAudioService.Setup(s => s.IsConnected(guildId)).Returns(true);
        _mockTtsSettingsService
            .Setup(s => s.IsUserRateLimitedAsync(guildId, It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockTtsService
            .Setup(s => s.SynthesizeSpeechAsync(request.Message, It.IsAny<TtsOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[192000]));

        // Playback service reports failure — simulates the case where the PCM stream is unavailable
        _mockTtsPlaybackService
            .Setup(s => s.PlayAsync(guildId, It.IsAny<ulong>(), It.IsAny<string>(), request.Message, request.Voice, It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiscordBot.Core.DTOs.Tts.TtsPlaybackResult { Success = false, ErrorMessage = "Failed to get audio stream" });

        // Act
        var result = await _controller.SendTts(guildId, request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<BadRequestObjectResult>();

        var badRequestResult = result as BadRequestObjectResult;
        var error = badRequestResult!.Value as ApiErrorDto;
        error.Should().NotBeNull();
        error!.Message.Should().Be("Failed to play TTS");
        error.Detail.Should().Be("Failed to get audio stream");
    }

    [Fact]
    public async Task SendTts_WhenSpeechServiceUnreachable_Returns503()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var request = new SendTtsRequest
        {
            Message = "Hello world",
            Voice = "en-US-JennyNeural",
            Speed = 1.0,
            Pitch = 1.0
        };

        var settings = new GuildTtsSettings
        {
            GuildId = guildId,
            TtsEnabled = true,
            MaxMessageLength = 500,
            RateLimitPerMinute = 5
        };

        _mockTtsSettingsService
            .Setup(s => s.GetOrCreateSettingsAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);
        _mockAudioService.Setup(s => s.IsConnected(guildId)).Returns(true);
        _mockTtsSettingsService
            .Setup(s => s.IsUserRateLimitedAsync(guildId, It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Azure Speech could not be reached (e.g. WS_OPEN_ERROR_UNDERLYING_IO_OPEN_FAILED) after all retries
        _mockTtsService
            .Setup(s => s.SynthesizeSpeechAsync(request.Message, It.IsAny<TtsOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TtsUpstreamUnavailableException("Azure Speech service in region 'eastus' is unreachable", attempts: 2));

        // Act
        var result = await _controller.SendTts(guildId, request, CancellationToken.None);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);

        var error = objectResult.Value.Should().BeOfType<ApiErrorDto>().Subject;
        error.ErrorCode.Should().Be("tts_upstream_unavailable");
        error.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        error.Message.Should().Be("Speech service unreachable");

        _mockTtsPlaybackService.Verify(
            s => s.PlayAsync(It.IsAny<ulong>(), It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendTts_WithEmptyMessage_Returns400()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var request = new SendTtsRequest
        {
            Message = "",
            Voice = "en-US-JennyNeural"
        };

        var settings = new GuildTtsSettings { GuildId = guildId, TtsEnabled = true };
        _mockTtsSettingsService
            .Setup(s => s.GetOrCreateSettingsAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);
        _mockAudioService.Setup(s => s.IsConnected(guildId)).Returns(true);

        // Act
        var result = await _controller.SendTts(guildId, request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<BadRequestObjectResult>();

        var badRequestResult = result as BadRequestObjectResult;
        var error = badRequestResult!.Value as ApiErrorDto;
        error.Should().NotBeNull();
        error!.Message.Should().Be("Message cannot be empty");
    }

    [Fact]
    public async Task SendTts_WithMessageTooLong_Returns400()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var request = new SendTtsRequest
        {
            Message = new string('a', 501),
            Voice = "en-US-JennyNeural"
        };

        var settings = new GuildTtsSettings
        {
            GuildId = guildId,
            TtsEnabled = true,
            MaxMessageLength = 500
        };

        _mockTtsSettingsService
            .Setup(s => s.GetOrCreateSettingsAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);
        _mockAudioService.Setup(s => s.IsConnected(guildId)).Returns(true);

        // Act
        var result = await _controller.SendTts(guildId, request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<BadRequestObjectResult>();

        var badRequestResult = result as BadRequestObjectResult;
        var error = badRequestResult!.Value as ApiErrorDto;
        error.Should().NotBeNull();
        error!.Message.Should().Be("Message too long");
        error.Detail.Should().Contain("501");
        error.Detail.Should().Contain("500");
    }

    [Fact]
    public async Task SendTts_WhenRateLimited_Returns429()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var request = new SendTtsRequest
        {
            Message = "Hello",
            Voice = "en-US-JennyNeural"
        };

        var settings = new GuildTtsSettings
        {
            GuildId = guildId,
            TtsEnabled = true,
            MaxMessageLength = 500,
            RateLimitPerMinute = 5
        };

        _mockTtsSettingsService
            .Setup(s => s.GetOrCreateSettingsAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);
        _mockAudioService.Setup(s => s.IsConnected(guildId)).Returns(true);
        _mockTtsSettingsService
            .Setup(s => s.IsUserRateLimitedAsync(guildId, It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.SendTts(guildId, request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<ObjectResult>();

        var objectResult = result as ObjectResult;
        objectResult!.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);

        var error = objectResult.Value as ApiErrorDto;
        error.Should().NotBeNull();
        error!.Message.Should().Be("Rate limit exceeded");
        error.Detail.Should().Contain("5 messages per minute");
    }

    [Fact]
    public async Task SendTts_WhenNotConnectedToChannel_Returns400()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var request = new SendTtsRequest
        {
            Message = "Hello",
            Voice = "en-US-JennyNeural"
        };

        var settings = new GuildTtsSettings { GuildId = guildId, TtsEnabled = true };
        _mockTtsSettingsService
            .Setup(s => s.GetOrCreateSettingsAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);
        _mockAudioService.Setup(s => s.IsConnected(guildId)).Returns(false);

        // Act
        var result = await _controller.SendTts(guildId, request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<BadRequestObjectResult>();

        var badRequestResult = result as BadRequestObjectResult;
        var error = badRequestResult!.Value as ApiErrorDto;
        error.Should().NotBeNull();
        error!.Message.Should().Be("Not connected to voice channel");
    }

    [Fact]
    public async Task SendTts_WhenTtsDisabled_Returns400()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var request = new SendTtsRequest
        {
            Message = "Hello",
            Voice = "en-US-JennyNeural"
        };

        var settings = new GuildTtsSettings
        {
            GuildId = guildId,
            TtsEnabled = false
        };

        _mockTtsSettingsService
            .Setup(s => s.GetOrCreateSettingsAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        // Act
        var result = await _controller.SendTts(guildId, request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<BadRequestObjectResult>();

        var badRequestResult = result as BadRequestObjectResult;
        var error = badRequestResult!.Value as ApiErrorDto;
        error.Should().NotBeNull();
        error!.Message.Should().Be("TTS is not enabled for this server");
    }

    #endregion

    #region GetVoiceChannels Tests

    [Fact]
    public async Task GetVoiceChannels_WithValidGuild_ReturnsChannelList()
    {
        // Arrange
        const ulong guildId = 123456789UL;

        _mockGuildDirectory.Setup(d => d.IsGuildAvailableAsync(guildId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _mockGuildDirectory.Setup(d => d.GetVoiceChannels(guildId)).Returns(new[]
        {
            new PortalVoiceChannel(11UL, "General", 2),
            new PortalVoiceChannel(12UL, "Gaming", 0)
        });

        // Act
        var result = await _controller.GetVoiceChannels(guildId);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var names = ((IEnumerable<object>)ok.Value!).Select(c => c.ToString()).ToList();
        names.Should().HaveCount(2);
        names[0].Should().Contain("General").And.Contain("11"); // ids stay strings for the browser
        names[1].Should().Contain("Gaming");
    }

    [Fact]
    public async Task GetVoiceChannels_WithInvalidGuild_Returns404()
    {
        // Arrange
        const ulong guildId = 999999999UL;

        _mockGuildDirectory.Setup(d => d.IsGuildAvailableAsync(guildId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        var result = await _controller.GetVoiceChannels(guildId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<NotFoundObjectResult>();

        var notFoundResult = result as NotFoundObjectResult;
        var error = notFoundResult!.Value as ApiErrorDto;
        error.Should().NotBeNull();
        error!.Message.Should().Be("Server not found");
    }

    #endregion

    #region JoinChannel Tests

    [Fact]
    public async Task JoinChannel_WithValidChannelId_Returns200()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        const ulong channelId = 987654321UL;
        var request = new PortalTtsPlaybackController.JoinChannelRequest { ChannelId = channelId };

        var settings = new GuildTtsSettings { GuildId = guildId, TtsEnabled = true };
        var mockAudioClient = new Mock<IAudioClient>();

        _mockTtsSettingsService
            .Setup(s => s.GetOrCreateSettingsAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);
        _mockAudioService
            .Setup(s => s.JoinChannelAsync(guildId, channelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockAudioClient.Object);

        // Act
        var result = await _controller.JoinChannel(guildId, request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<OkObjectResult>();

        var okResult = result as OkObjectResult;
        okResult!.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task JoinChannel_WithInvalidChannelId_Returns404()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        const ulong channelId = 999999999UL;
        var request = new PortalTtsPlaybackController.JoinChannelRequest { ChannelId = channelId };

        var settings = new GuildTtsSettings { GuildId = guildId, TtsEnabled = true };

        _mockTtsSettingsService
            .Setup(s => s.GetOrCreateSettingsAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);
        _mockAudioService
            .Setup(s => s.JoinChannelAsync(guildId, channelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IAudioClient?)null);

        // Act
        var result = await _controller.JoinChannel(guildId, request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<NotFoundObjectResult>();

        var notFoundResult = result as NotFoundObjectResult;
        var error = notFoundResult!.Value as ApiErrorDto;
        error.Should().NotBeNull();
        error!.Message.Should().Be("Failed to join voice channel");
    }

    #endregion

    #region LeaveChannel Tests

    [Fact]
    public async Task LeaveChannel_WhenConnected_Returns200()
    {
        // Arrange
        const ulong guildId = 123456789UL;

        _mockAudioService.Setup(s => s.IsConnected(guildId)).Returns(true);
        _mockAudioService
            .Setup(s => s.LeaveChannelAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockPlaybackService
            .Setup(s => s.StopAsync(guildId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.LeaveChannel(guildId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<OkObjectResult>();

        // Verify StopAsync was called before LeaveChannelAsync
        _mockPlaybackService.Verify(
            s => s.StopAsync(guildId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LeaveChannel_WhenNotConnected_Returns400()
    {
        // Arrange
        const ulong guildId = 123456789UL;

        _mockAudioService.Setup(s => s.IsConnected(guildId)).Returns(false);

        // Act
        var result = await _controller.LeaveChannel(guildId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<BadRequestObjectResult>();

        var badRequestResult = result as BadRequestObjectResult;
        var error = badRequestResult!.Value as ApiErrorDto;
        error.Should().NotBeNull();
        error!.Message.Should().Be("Not connected to voice");
    }

    #endregion

    #region StopPlayback Tests

    [Fact]
    public async Task StopPlayback_WhenPlaying_Returns200()
    {
        // Arrange
        const ulong guildId = 123456789UL;

        _mockAudioService.Setup(s => s.IsConnected(guildId)).Returns(true);
        _mockPlaybackService.Setup(s => s.IsPlaying(guildId)).Returns(true);
        _mockPlaybackService
            .Setup(s => s.StopAsync(guildId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.StopPlayback(guildId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<OkObjectResult>();

        _mockPlaybackService.Verify(
            s => s.StopAsync(guildId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StopPlayback_WhenNotPlaying_Returns200()
    {
        // Arrange
        const ulong guildId = 123456789UL;

        _mockAudioService.Setup(s => s.IsConnected(guildId)).Returns(true);
        _mockPlaybackService.Setup(s => s.IsPlaying(guildId)).Returns(false);

        // Act
        var result = await _controller.StopPlayback(guildId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<OkObjectResult>();

        var okResult = result as OkObjectResult;
        okResult!.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task StopPlayback_WhenNotConnected_Returns400()
    {
        // Arrange
        const ulong guildId = 123456789UL;

        _mockAudioService.Setup(s => s.IsConnected(guildId)).Returns(false);

        // Act
        var result = await _controller.StopPlayback(guildId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<BadRequestObjectResult>();

        var badRequestResult = result as BadRequestObjectResult;
        var error = badRequestResult!.Value as ApiErrorDto;
        error.Should().NotBeNull();
        error!.Message.Should().Be("Not connected to voice");
    }

    #endregion

    #region Playback token ownership

    private static readonly TimeSpan PlaybackWait = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Makes every TTS playback run until its token is cancelled, recording each token in order.
    /// </summary>
    private System.Collections.Concurrent.ConcurrentQueue<CancellationToken> PlayUntilCancelled()
    {
        var tokens = new System.Collections.Concurrent.ConcurrentQueue<CancellationToken>();
        _mockTtsPlaybackService
            .Setup(s => s.PlayAsync(It.IsAny<ulong>(), It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns<ulong, ulong, string, string, string, Stream, CancellationToken>(async (_, _, _, _, _, _, ct) =>
            {
                tokens.Enqueue(ct);
                await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                return new DiscordBot.Core.DTOs.Tts.TtsPlaybackResult { Success = true };
            });
        return tokens;
    }

    private static async Task WaitForCountAsync<T>(IReadOnlyCollection<T> items, int count)
    {
        var deadline = DateTime.UtcNow + PlaybackWait;
        while (items.Count < count)
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Expected {count} playback(s) to start; saw {items.Count}.");
            }

            await Task.Delay(10).ConfigureAwait(false);
        }
    }

    private static async Task<bool> WaitForCancellationAsync(CancellationToken token)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!token.IsCancellationRequested)
        {
            if (DateTime.UtcNow > deadline)
            {
                return false;
            }

            await Task.Delay(10).ConfigureAwait(false);
        }

        return true;
    }

    [Fact]
    public async Task SendTts_SupersededRequestFinishing_DoesNotBreakStopForTheNewerRequest()
    {
        // A second message cancels the first. The first request's finally used to remove and
        // dispose whatever token was registered for the guild, which by then was the second
        // request's, so Stop found nothing to cancel and the second message played on.
        const ulong guildId = 123456789UL;
        _mockTtsSettingsService
            .Setup(s => s.GetOrCreateSettingsAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildTtsSettings { GuildId = guildId, TtsEnabled = true, MaxMessageLength = 500, RateLimitPerMinute = 5 });
        _mockTtsSettingsService
            .Setup(s => s.IsUserRateLimitedAsync(guildId, It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockAudioService.Setup(s => s.IsConnected(guildId)).Returns(true);
        _mockTtsService
            .Setup(s => s.SynthesizeSpeechAsync(It.IsAny<string>(), It.IsAny<TtsOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(new byte[192000]));
        var tokens = PlayUntilCancelled();

        var first = _controller.SendTts(guildId, new SendTtsRequest { Message = "first", Voice = "en-US-JennyNeural" }, CancellationToken.None);
        await WaitForCountAsync(tokens, 1).ConfigureAwait(false);
        var second = _controller.SendTts(guildId, new SendTtsRequest { Message = "second", Voice = "en-US-JennyNeural" }, CancellationToken.None);
        await WaitForCountAsync(tokens, 2).ConfigureAwait(false);

        // The first request is cancelled by the second and has run its finally.
        var firstResult = await first.WaitAsync(PlaybackWait).ConfigureAwait(false);
        firstResult.Should().BeOfType<OkObjectResult>();
        var secondToken = tokens.Last();
        secondToken.IsCancellationRequested.Should().BeFalse();
        _sendPipeline.PlaybackState.ContainsKey(guildId).Should().BeTrue("the second message is still playing");

        await _controller.StopPlayback(guildId, CancellationToken.None).ConfigureAwait(false);

        (await WaitForCancellationAsync(secondToken).ConfigureAwait(false))
            .Should().BeTrue("Stop must cancel the message that is playing");
        (await second.WaitAsync(PlaybackWait).ConfigureAwait(false)).Should().BeOfType<OkObjectResult>();
        _sendPipeline.PlaybackCancellationTokens.Should().NotContainKey(guildId);
        _sendPipeline.PlaybackState.Should().NotContainKey(guildId);
    }

    [Fact]
    public async Task SynthesizeSsml_PlayInVoiceChannel_CanBeStopped()
    {
        // SSML "play live" set the playing state but registered no token, so Stop cleared the
        // state and the audio played on.
        const ulong guildId = 123456789UL;
        _mockTtsSettingsService
            .Setup(s => s.GetOrCreateSettingsAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildTtsSettings { GuildId = guildId, TtsEnabled = true, SsmlEnabled = true, MaxSsmlComplexity = 1000 });
        _mockAudioService.Setup(s => s.IsConnected(guildId)).Returns(true);
        _mockSsmlValidator
            .Setup(v => v.Validate(It.IsAny<string>()))
            .Returns(new SsmlValidationResult { IsValid = true, DetectedVoices = new[] { "en-US-JennyNeural" } });
        _mockSsmlValidator.Setup(v => v.ExtractPlainText(It.IsAny<string>())).Returns("hello");
        _mockTtsService
            .Setup(s => s.SynthesizeSpeechAsync(It.IsAny<string>(), It.IsAny<TtsOptions?>(), DiscordBot.Core.Enums.SynthesisMode.Ssml, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(new byte[192000]));
        var tokens = PlayUntilCancelled();

        var synthesisController = new PortalTtsSynthesisController(
            _sendPipeline,
            _mockSsmlBuilder.Object,
            _mockTtsSettingsService.Object,
            _mockAudioService.Object,
            _mockTtsPlaybackService.Object,
            _mockTtsService.Object,
            _mockSsmlValidator.Object,
            _mockVoiceCapabilityProvider.Object,
            new Mock<ILogger<PortalTtsSynthesisController>>().Object)
        {
            ControllerContext = _controller.ControllerContext
        };

        var play = synthesisController.SynthesizeSsml(
            guildId,
            new DiscordBot.Core.DTOs.Tts.SsmlSynthesisRequest { Ssml = "<speak>hello</speak>", PlayInVoiceChannel = true },
            CancellationToken.None);
        await WaitForCountAsync(tokens, 1).ConfigureAwait(false);

        await _controller.StopPlayback(guildId, CancellationToken.None).ConfigureAwait(false);

        (await WaitForCancellationAsync(tokens.Single()).ConfigureAwait(false))
            .Should().BeTrue("Stop must cancel SSML playback");
        (await play.WaitAsync(PlaybackWait).ConfigureAwait(false)).Should().BeOfType<OkObjectResult>();
        _sendPipeline.PlaybackCancellationTokens.Should().NotContainKey(guildId);
        _sendPipeline.PlaybackState.Should().NotContainKey(guildId);
    }

    #endregion

    /// <summary>
    /// Mock implementation of IReadOnlyCollection for testing.
    /// </summary>
    private class MockReadOnlyCollection<T> : IReadOnlyCollection<T>
    {
        private readonly IEnumerable<T> _items;

        public MockReadOnlyCollection(IEnumerable<T> items)
        {
            _items = items;
        }

        public int Count => _items.Count();

        public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
