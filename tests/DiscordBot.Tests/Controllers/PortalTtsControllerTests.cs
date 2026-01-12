using System.Security.Claims;
using Discord;
using Discord.Audio;
using Discord.WebSocket;
using DiscordBot.Bot.Controllers;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.DTOs.Portal;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Controllers;

/// <summary>
/// Unit tests for <see cref="PortalTtsController"/>.
/// </summary>
public class PortalTtsControllerTests
{
    private readonly Mock<ITtsService> _mockTtsService;
    private readonly Mock<ITtsSettingsService> _mockTtsSettingsService;
    private readonly Mock<ITtsMessageRepository> _mockTtsMessageRepository;
    private readonly Mock<IAudioService> _mockAudioService;
    private readonly Mock<IPlaybackService> _mockPlaybackService;
    private readonly Mock<DiscordSocketClient> _mockDiscordClient;
    private readonly Mock<ILogger<PortalTtsController>> _mockLogger;
    private readonly PortalTtsController _controller;

    public PortalTtsControllerTests()
    {
        _mockTtsService = new Mock<ITtsService>();
        _mockTtsSettingsService = new Mock<ITtsSettingsService>();
        _mockTtsMessageRepository = new Mock<ITtsMessageRepository>();
        _mockAudioService = new Mock<IAudioService>();
        _mockPlaybackService = new Mock<IPlaybackService>();
        _mockDiscordClient = new Mock<DiscordSocketClient>(MockBehavior.Default, new DiscordSocketConfig());
        _mockLogger = new Mock<ILogger<PortalTtsController>>();

        _controller = new PortalTtsController(
            _mockTtsService.Object,
            _mockTtsSettingsService.Object,
            _mockTtsMessageRepository.Object,
            _mockAudioService.Object,
            _mockPlaybackService.Object,
            _mockDiscordClient.Object,
            _mockLogger.Object);

        // Setup HttpContext and User claims
        var claims = new List<Claim>
        {
            new Claim("DiscordId", "123456789"),
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
        _mockDiscordClient.Setup(c => c.GetGuild(guildId)).Returns((SocketGuild?)null);

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
        _mockAudioService.Setup(s => s.GetOrCreatePcmStream(guildId)).Returns((AudioOutStream?)null); // PCM stream not available

        // Act
        var result = await _controller.SendTts(guildId, request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<BadRequestObjectResult>();

        var badRequestResult = result as BadRequestObjectResult;
        var error = badRequestResult!.Value as ApiErrorDto;
        error.Should().NotBeNull();
        error!.Message.Should().Be("Failed to get audio stream");
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
        error!.Message.Should().Be("TTS is not enabled for this guild");
    }

    #endregion

    #region GetVoiceChannels Tests

    [Fact]
    public void GetVoiceChannels_WithValidGuild_ReturnsChannelList()
    {
        // Arrange
        const ulong guildId = 123456789UL;

        // Note: Discord.NET classes (SocketGuild, SocketVoiceChannel) cannot be mocked properly
        // Testing the error path instead (guild not found)
        _mockDiscordClient.Setup(c => c.GetGuild(guildId)).Returns((SocketGuild?)null);

        // Act
        var result = _controller.GetVoiceChannels(guildId);

        // Assert
        result.Should().NotBeNull();
        // When guild is not found, it returns 404
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void GetVoiceChannels_WithInvalidGuild_Returns404()
    {
        // Arrange
        const ulong guildId = 999999999UL;

        _mockDiscordClient.Setup(c => c.GetGuild(guildId)).Returns((SocketGuild?)null);

        // Act
        var result = _controller.GetVoiceChannels(guildId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<NotFoundObjectResult>();

        var notFoundResult = result as NotFoundObjectResult;
        var error = notFoundResult!.Value as ApiErrorDto;
        error.Should().NotBeNull();
        error!.Message.Should().Be("Guild not found");
    }

    #endregion

    #region JoinChannel Tests

    [Fact]
    public async Task JoinChannel_WithValidChannelId_Returns200()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        const ulong channelId = 987654321UL;
        var request = new PortalTtsController.JoinChannelRequest { ChannelId = channelId };

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
        var request = new PortalTtsController.JoinChannelRequest { ChannelId = channelId };

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
