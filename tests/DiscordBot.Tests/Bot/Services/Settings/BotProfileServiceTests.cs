using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Services;
using DiscordBot.Bot.Services.Settings;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Services.Settings;

/// <summary>
/// Unit tests for <see cref="BotProfileService"/>.
/// </summary>
public class BotProfileServiceTests
{
    private static readonly byte[] PngBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 1, 2, 3 };

    private static readonly BotProfile Current = new(
        "RatBot", "https://cdn.example/avatar.png", HasCustomAvatar: true, "https://cdn.example/banner.png", "Squeak.");

    private readonly Mock<IBotProfileGateway> _gateway = new();
    private readonly Mock<IAuditLogQueue> _auditLogQueue = new();
    private readonly BotProfileService _service;

    public BotProfileServiceTests()
    {
        _gateway.SetupGet(g => g.IsAvailable).Returns(true);
        _gateway.Setup(g => g.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Current);

        _service = new BotProfileService(_gateway.Object, _auditLogQueue.Object, Mock.Of<ILogger<BotProfileService>>());
    }

    private static BotProfileImage Image(byte[] bytes) => new(new MemoryStream(bytes), bytes.Length);

    [Fact]
    public async Task LoadAsync_WhenBotIsNotLoggedIn_ReportsUnavailableWithoutCallingDiscord()
    {
        _gateway.SetupGet(g => g.IsAvailable).Returns(false);

        var result = await _service.LoadAsync();

        result.Available.Should().BeFalse();
        result.Message.Should().Contain("not connected");
        _gateway.Verify(g => g.GetAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LoadAsync_WhenDiscordFails_ReportsUnavailableWithTheReason()
    {
        _gateway.Setup(g => g.GetAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BotProfileGatewayException("Discord did not answer in time."));

        var result = await _service.LoadAsync();

        result.Available.Should().BeFalse();
        result.Message.Should().Contain("Discord did not answer in time.");
    }

    [Fact]
    public async Task LoadAsync_ReturnsTheProfile()
    {
        var result = await _service.LoadAsync();

        result.Available.Should().BeTrue();
        result.Profile.Should().Be(Current);
    }

    [Fact]
    public async Task SaveAsync_WhenBotIsNotLoggedIn_Refuses()
    {
        _gateway.SetupGet(g => g.IsAvailable).Returns(false);

        var result = await _service.SaveAsync(new BotProfileUpdateRequest { Username = "NewName" }, "admin");

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        _gateway.Verify(g => g.ModifyUserAsync(It.IsAny<BotUserChanges>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SaveAsync_WithTheCurrentValues_ChangesNothingAndLogsNothing()
    {
        var result = await _service.SaveAsync(
            new BotProfileUpdateRequest { Username = " RatBot ", AboutMe = "Squeak.\r\n" }, "admin");

        result.Success.Should().BeTrue();
        result.ChangeCount.Should().Be(0);
        _gateway.Verify(g => g.ModifyUserAsync(It.IsAny<BotUserChanges>(), It.IsAny<CancellationToken>()), Times.Never);
        _gateway.Verify(g => g.SetAboutMeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _auditLogQueue.Verify(q => q.Enqueue(It.IsAny<AuditLogCreateDto>()), Times.Never);
    }

    [Fact]
    public async Task SaveAsync_SendsOnlyWhatChanged()
    {
        BotUserChanges? sent = null;
        _gateway.Setup(g => g.ModifyUserAsync(It.IsAny<BotUserChanges>(), It.IsAny<CancellationToken>()))
            .Callback<BotUserChanges, CancellationToken>((changes, _) => sent = changes)
            .Returns(Task.CompletedTask);

        var result = await _service.SaveAsync(
            new BotProfileUpdateRequest { Username = "NewName", AboutMe = "Squeak." }, "admin");

        result.Success.Should().BeTrue();
        result.ChangeCount.Should().Be(1);
        sent.Should().NotBeNull();
        sent!.Username.Should().Be("NewName");
        sent.Avatar.Should().BeNull();
        sent.Banner.Should().BeNull();
        _gateway.Verify(g => g.SetAboutMeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _auditLogQueue.Verify(q => q.Enqueue(It.Is<AuditLogCreateDto>(d => d.ActorId == "admin" && d.Details!.Contains("BotProfileUpdate"))), Times.Once);
    }

    [Fact]
    public async Task SaveAsync_AnEmptyAboutMe_ClearsIt()
    {
        var result = await _service.SaveAsync(new BotProfileUpdateRequest { AboutMe = "" }, "admin");

        result.Success.Should().BeTrue();
        _gateway.Verify(g => g.SetAboutMeAsync("", It.IsAny<CancellationToken>()), Times.Once);
        _gateway.Verify(g => g.ModifyUserAsync(It.IsAny<BotUserChanges>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SaveAsync_UploadsAnAvatar_AndAnUploadWinsOverRemove()
    {
        byte[]? uploaded = null;
        BotUserChanges? sent = null;
        _gateway.Setup(g => g.ModifyUserAsync(It.IsAny<BotUserChanges>(), It.IsAny<CancellationToken>()))
            .Callback<BotUserChanges, CancellationToken>((changes, _) =>
            {
                sent = changes;
                using var copy = new MemoryStream();
                changes.Avatar!.CopyTo(copy);
                uploaded = copy.ToArray();
            })
            .Returns(Task.CompletedTask);

        var result = await _service.SaveAsync(
            new BotProfileUpdateRequest { Avatar = Image(PngBytes), RemoveAvatar = true }, "admin");

        result.Success.Should().BeTrue();
        uploaded.Should().Equal(PngBytes);
        sent!.RemoveAvatar.Should().BeFalse();
    }

    [Fact]
    public async Task SaveAsync_RemovingWhatIsNotThere_IsNotAChange()
    {
        _gateway.Setup(g => g.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Current with { HasCustomAvatar = false, BannerUrl = null });

        var result = await _service.SaveAsync(
            new BotProfileUpdateRequest { RemoveAvatar = true, RemoveBanner = true }, "admin");

        result.ChangeCount.Should().Be(0);
        _gateway.Verify(g => g.ModifyUserAsync(It.IsAny<BotUserChanges>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SaveAsync_RemovesTheBanner()
    {
        BotUserChanges? sent = null;
        _gateway.Setup(g => g.ModifyUserAsync(It.IsAny<BotUserChanges>(), It.IsAny<CancellationToken>()))
            .Callback<BotUserChanges, CancellationToken>((changes, _) => sent = changes)
            .Returns(Task.CompletedTask);

        var result = await _service.SaveAsync(new BotProfileUpdateRequest { RemoveBanner = true }, "admin");

        result.Success.Should().BeTrue();
        sent!.RemoveBanner.Should().BeTrue();
        sent.RemoveAvatar.Should().BeFalse();
    }

    [Theory]
    [InlineData("a")]
    [InlineData("this-username-is-far-too-long-for-discord")]
    public async Task SaveAsync_RejectsABadUsername(string username)
    {
        var result = await _service.SaveAsync(new BotProfileUpdateRequest { Username = username }, "admin");

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().ContainSingle().Which.Should().Contain("username");
        _gateway.Verify(g => g.ModifyUserAsync(It.IsAny<BotUserChanges>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SaveAsync_RejectsAnAboutMeOverTheLimit()
    {
        var result = await _service.SaveAsync(
            new BotProfileUpdateRequest { AboutMe = new string('x', BotProfileService.MaxAboutMeLength + 1) }, "admin");

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Contain("About me");
    }

    [Fact]
    public async Task SaveAsync_RejectsAFileThatIsNotAnImage_WhateverItClaims()
    {
        var result = await _service.SaveAsync(
            new BotProfileUpdateRequest { Banner = Image("<script>alert(1)</script>"u8.ToArray()) }, "admin");

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Contain("banner must be a PNG");
        _gateway.Verify(g => g.ModifyUserAsync(It.IsAny<BotUserChanges>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SaveAsync_RejectsAnOversizedImage_WithoutReadingIt()
    {
        var content = new Mock<Stream>(MockBehavior.Strict);

        var result = await _service.SaveAsync(
            new BotProfileUpdateRequest { Avatar = new BotProfileImage(content.Object, BotProfileService.MaxImageBytes + 1) }, "admin");

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Contain("too large");
    }

    [Fact]
    public async Task SaveAsync_WhenDiscordRefuses_ReportsDiscordsReasonAndLogsNothing()
    {
        _gateway.Setup(g => g.ModifyUserAsync(It.IsAny<BotUserChanges>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BotProfileGatewayException("You are changing your username too fast."));

        var result = await _service.SaveAsync(new BotProfileUpdateRequest { Username = "NewName" }, "admin");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("was not saved").And.Contain("too fast");
        _auditLogQueue.Verify(q => q.Enqueue(It.IsAny<AuditLogCreateDto>()), Times.Never);
    }

    [Fact]
    public async Task SaveAsync_WhenOnlyTheSecondCallFails_SaysWhatWasSavedAndLogsIt()
    {
        _gateway.Setup(g => g.ModifyUserAsync(It.IsAny<BotUserChanges>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _gateway.Setup(g => g.SetAboutMeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BotProfileGatewayException("Discord did not answer in time."));

        var result = await _service.SaveAsync(
            new BotProfileUpdateRequest { Username = "NewName", AboutMe = "New bio" }, "admin");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Saved the username").And.Contain("did not answer");
        _auditLogQueue.Verify(q => q.Enqueue(It.Is<AuditLogCreateDto>(d => d.Details!.Contains("NewName"))), Times.Once);
    }

    [Theory]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, "png")]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "jpeg")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, "gif")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 1, 2, 3, 4, 0x57, 0x45, 0x42, 0x50 }, "webp")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 1, 2, 3, 4, 0x57, 0x41, 0x56, 0x45 }, null)]
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46 }, null)]
    [InlineData(new byte[0], null)]
    public void DetectImageFormat_GoesByTheFirstBytes(byte[] header, string? expected)
    {
        BotProfileService.DetectImageFormat(header).Should().Be(expected);
    }
}
