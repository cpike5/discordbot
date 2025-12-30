using Discord.WebSocket;
using DiscordBot.Bot.Services;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for RatWatchService.
/// Tests cover CRUD operations, voting, time parsing, and statistics.
/// </summary>
public class RatWatchServiceTests
{
    private readonly Mock<IRatWatchRepository> _mockWatchRepository;
    private readonly Mock<IRatVoteRepository> _mockVoteRepository;
    private readonly Mock<IRatRecordRepository> _mockRecordRepository;
    private readonly Mock<IGuildRatWatchSettingsRepository> _mockSettingsRepository;
    private readonly Mock<DiscordSocketClient> _mockDiscordClient;
    private readonly Mock<IRatWatchStatusService> _mockRatWatchStatusService;
    private readonly Mock<ILogger<RatWatchService>> _mockLogger;
    private readonly IOptions<RatWatchOptions> _options;
    private readonly RatWatchService _service;

    public RatWatchServiceTests()
    {
        _mockWatchRepository = new Mock<IRatWatchRepository>();
        _mockVoteRepository = new Mock<IRatVoteRepository>();
        _mockRecordRepository = new Mock<IRatRecordRepository>();
        _mockSettingsRepository = new Mock<IGuildRatWatchSettingsRepository>();
        _mockDiscordClient = new Mock<DiscordSocketClient>();
        _mockRatWatchStatusService = new Mock<IRatWatchStatusService>();
        _mockLogger = new Mock<ILogger<RatWatchService>>();
        _options = Options.Create(new RatWatchOptions
        {
            DefaultVotingDurationMinutes = 5,
            DefaultMaxAdvanceHours = 24
        });

        _service = new RatWatchService(
            _mockWatchRepository.Object,
            _mockVoteRepository.Object,
            _mockRecordRepository.Object,
            _mockSettingsRepository.Object,
            _mockDiscordClient.Object,
            _mockRatWatchStatusService.Object,
            _mockLogger.Object,
            _options);
    }

    #region Helper Methods

    private static RatWatch CreateTestWatch(
        Guid? id = null,
        ulong guildId = 123456789UL,
        ulong accusedUserId = 111111UL,
        ulong initiatorUserId = 222222UL,
        RatWatchStatus status = RatWatchStatus.Pending,
        DateTime? scheduledAt = null,
        DateTime? votingStartedAt = null)
    {
        return new RatWatch
        {
            Id = id ?? Guid.NewGuid(),
            GuildId = guildId,
            ChannelId = 333333UL,
            AccusedUserId = accusedUserId,
            InitiatorUserId = initiatorUserId,
            OriginalMessageId = 444444UL,
            CustomMessage = "Test commitment",
            ScheduledAt = scheduledAt ?? DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow,
            Status = status,
            VotingStartedAt = votingStartedAt,
            Votes = new List<RatVote>()
        };
    }

    private static GuildRatWatchSettings CreateTestSettings(
        ulong guildId = 123456789UL,
        bool isEnabled = true,
        string timezone = "Eastern Standard Time",
        int votingDurationMinutes = 5,
        int maxAdvanceHours = 24)
    {
        return new GuildRatWatchSettings
        {
            GuildId = guildId,
            IsEnabled = isEnabled,
            Timezone = timezone,
            VotingDurationMinutes = votingDurationMinutes,
            MaxAdvanceHours = maxAdvanceHours,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    #endregion

    #region CreateWatchAsync Tests

    [Fact]
    public async Task CreateWatchAsync_WithValidData_CreatesWatch()
    {
        // Arrange
        var createDto = new RatWatchCreateDto
        {
            GuildId = 123456789UL,
            ChannelId = 333333UL,
            AccusedUserId = 111111UL,
            InitiatorUserId = 222222UL,
            OriginalMessageId = 444444UL,
            CustomMessage = "Test commitment",
            ScheduledAt = DateTime.UtcNow.AddHours(1)
        };

        _mockWatchRepository
            .Setup(r => r.FindDuplicateAsync(
                It.IsAny<ulong>(), It.IsAny<ulong>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RatWatch?)null);

        _mockWatchRepository
            .Setup(r => r.AddAsync(It.IsAny<RatWatch>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RatWatch w, CancellationToken _) => w);

        _mockVoteRepository
            .Setup(r => r.GetVoteTallyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((0, 0));

        // Act
        var result = await _service.CreateWatchAsync(createDto);

        // Assert
        result.Should().NotBeNull();
        result.GuildId.Should().Be(createDto.GuildId);
        result.AccusedUserId.Should().Be(createDto.AccusedUserId);
        result.InitiatorUserId.Should().Be(createDto.InitiatorUserId);
        result.Status.Should().Be(RatWatchStatus.Pending);

        _mockWatchRepository.Verify(
            r => r.AddAsync(It.Is<RatWatch>(w =>
                w.GuildId == createDto.GuildId &&
                w.AccusedUserId == createDto.AccusedUserId &&
                w.Status == RatWatchStatus.Pending),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateWatchAsync_WithDuplicateWatch_ThrowsException()
    {
        // Arrange
        var createDto = new RatWatchCreateDto
        {
            GuildId = 123456789UL,
            AccusedUserId = 111111UL,
            ScheduledAt = DateTime.UtcNow.AddHours(1)
        };

        var existingWatch = CreateTestWatch();

        _mockWatchRepository
            .Setup(r => r.FindDuplicateAsync(
                It.IsAny<ulong>(), It.IsAny<ulong>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingWatch);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateWatchAsync(createDto));

        _mockWatchRepository.Verify(
            r => r.AddAsync(It.IsAny<RatWatch>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region ClearWatchAsync Tests

    [Fact]
    public async Task ClearWatchAsync_WhenAccusedUser_ClearsWatch()
    {
        // Arrange
        var watchId = Guid.NewGuid();
        ulong accusedUserId = 111111UL;
        var watch = CreateTestWatch(id: watchId, accusedUserId: accusedUserId, status: RatWatchStatus.Pending);

        _mockWatchRepository
            .Setup(r => r.GetByIdAsync(watchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(watch);

        _mockWatchRepository
            .Setup(r => r.UpdateAsync(It.IsAny<RatWatch>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.ClearWatchAsync(watchId, accusedUserId);

        // Assert
        result.Should().BeTrue();

        _mockWatchRepository.Verify(
            r => r.UpdateAsync(It.Is<RatWatch>(w =>
                w.Id == watchId &&
                w.Status == RatWatchStatus.ClearedEarly &&
                w.ClearedAt.HasValue),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ClearWatchAsync_WhenNotAccusedUser_ReturnsFalse()
    {
        // Arrange
        var watchId = Guid.NewGuid();
        ulong accusedUserId = 111111UL;
        ulong wrongUserId = 999999UL;
        var watch = CreateTestWatch(id: watchId, accusedUserId: accusedUserId, status: RatWatchStatus.Pending);

        _mockWatchRepository
            .Setup(r => r.GetByIdAsync(watchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(watch);

        // Act
        var result = await _service.ClearWatchAsync(watchId, wrongUserId);

        // Assert
        result.Should().BeFalse();

        _mockWatchRepository.Verify(
            r => r.UpdateAsync(It.IsAny<RatWatch>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ClearWatchAsync_WhenNotPending_ReturnsFalse()
    {
        // Arrange
        var watchId = Guid.NewGuid();
        ulong accusedUserId = 111111UL;
        var watch = CreateTestWatch(id: watchId, accusedUserId: accusedUserId, status: RatWatchStatus.Voting);

        _mockWatchRepository
            .Setup(r => r.GetByIdAsync(watchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(watch);

        // Act
        var result = await _service.ClearWatchAsync(watchId, accusedUserId);

        // Assert
        result.Should().BeFalse();

        _mockWatchRepository.Verify(
            r => r.UpdateAsync(It.IsAny<RatWatch>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region CancelWatchAsync Tests

    [Fact]
    public async Task CancelWatchAsync_WhenPending_CancelsWatch()
    {
        // Arrange
        var watchId = Guid.NewGuid();
        var watch = CreateTestWatch(id: watchId, status: RatWatchStatus.Pending);

        _mockWatchRepository
            .Setup(r => r.GetByIdAsync(watchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(watch);

        _mockWatchRepository
            .Setup(r => r.UpdateAsync(It.IsAny<RatWatch>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.CancelWatchAsync(watchId, "Admin cancelled");

        // Assert
        result.Should().BeTrue();

        _mockWatchRepository.Verify(
            r => r.UpdateAsync(It.Is<RatWatch>(w =>
                w.Id == watchId &&
                w.Status == RatWatchStatus.Cancelled),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _mockRatWatchStatusService.Verify(
            s => s.RequestStatusUpdate(),
            Times.Once,
            "Should request status update to refresh bot status after cancellation");
    }

    [Fact]
    public async Task CancelWatchAsync_WhenNotPending_ReturnsFalse()
    {
        // Arrange
        var watchId = Guid.NewGuid();
        var watch = CreateTestWatch(id: watchId, status: RatWatchStatus.Guilty);

        _mockWatchRepository
            .Setup(r => r.GetByIdAsync(watchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(watch);

        // Act
        var result = await _service.CancelWatchAsync(watchId, "Admin cancelled");

        // Assert
        result.Should().BeFalse();

        _mockRatWatchStatusService.Verify(
            s => s.RequestStatusUpdate(),
            Times.Never,
            "Should not request status update when cancellation fails");
    }

    [Fact]
    public async Task CancelWatchAsync_WhenNotFound_ReturnsFalse()
    {
        // Arrange
        var watchId = Guid.NewGuid();

        _mockWatchRepository
            .Setup(r => r.GetByIdAsync(watchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RatWatch?)null);

        // Act
        var result = await _service.CancelWatchAsync(watchId, "Admin cancelled");

        // Assert
        result.Should().BeFalse();

        _mockRatWatchStatusService.Verify(
            s => s.RequestStatusUpdate(),
            Times.Never,
            "Should not request status update when watch not found");
    }

    #endregion

    #region CastVoteAsync Tests

    [Fact]
    public async Task CastVoteAsync_NewVote_CreatesVote()
    {
        // Arrange
        var watchId = Guid.NewGuid();
        ulong voterId = 555555UL;
        var watch = CreateTestWatch(id: watchId, status: RatWatchStatus.Voting);

        _mockWatchRepository
            .Setup(r => r.GetByIdAsync(watchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(watch);

        _mockVoteRepository
            .Setup(r => r.GetUserVoteAsync(watchId, voterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RatVote?)null);

        _mockVoteRepository
            .Setup(r => r.AddAsync(It.IsAny<RatVote>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RatVote v, CancellationToken _) => v);

        // Act
        var result = await _service.CastVoteAsync(watchId, voterId, isGuilty: true);

        // Assert
        result.Should().BeTrue();

        _mockVoteRepository.Verify(
            r => r.AddAsync(It.Is<RatVote>(v =>
                v.RatWatchId == watchId &&
                v.VoterUserId == voterId &&
                v.IsGuiltyVote == true),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CastVoteAsync_ExistingVote_UpdatesVote()
    {
        // Arrange
        var watchId = Guid.NewGuid();
        ulong voterId = 555555UL;
        var watch = CreateTestWatch(id: watchId, status: RatWatchStatus.Voting);
        var existingVote = new RatVote
        {
            Id = Guid.NewGuid(),
            RatWatchId = watchId,
            VoterUserId = voterId,
            IsGuiltyVote = true,
            VotedAt = DateTime.UtcNow.AddMinutes(-1)
        };

        _mockWatchRepository
            .Setup(r => r.GetByIdAsync(watchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(watch);

        _mockVoteRepository
            .Setup(r => r.GetUserVoteAsync(watchId, voterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingVote);

        _mockVoteRepository
            .Setup(r => r.UpdateAsync(It.IsAny<RatVote>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.CastVoteAsync(watchId, voterId, isGuilty: false);

        // Assert
        result.Should().BeTrue();

        _mockVoteRepository.Verify(
            r => r.UpdateAsync(It.Is<RatVote>(v =>
                v.Id == existingVote.Id &&
                v.IsGuiltyVote == false),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _mockVoteRepository.Verify(
            r => r.AddAsync(It.IsAny<RatVote>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CastVoteAsync_WhenNotVoting_ReturnsFalse()
    {
        // Arrange
        var watchId = Guid.NewGuid();
        ulong voterId = 555555UL;
        var watch = CreateTestWatch(id: watchId, status: RatWatchStatus.Pending);

        _mockWatchRepository
            .Setup(r => r.GetByIdAsync(watchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(watch);

        // Act
        var result = await _service.CastVoteAsync(watchId, voterId, isGuilty: true);

        // Assert
        result.Should().BeFalse();

        _mockVoteRepository.Verify(
            r => r.AddAsync(It.IsAny<RatVote>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region StartVotingAsync Tests

    [Fact]
    public async Task StartVotingAsync_WhenPending_StartsVoting()
    {
        // Arrange
        var watchId = Guid.NewGuid();
        var watch = CreateTestWatch(id: watchId, status: RatWatchStatus.Pending);

        _mockWatchRepository
            .Setup(r => r.GetByIdAsync(watchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(watch);

        _mockWatchRepository
            .Setup(r => r.UpdateAsync(It.IsAny<RatWatch>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.StartVotingAsync(watchId);

        // Assert
        result.Should().BeTrue();

        _mockWatchRepository.Verify(
            r => r.UpdateAsync(It.Is<RatWatch>(w =>
                w.Id == watchId &&
                w.Status == RatWatchStatus.Voting &&
                w.VotingStartedAt.HasValue),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StartVotingAsync_WhenNotPending_ReturnsFalse()
    {
        // Arrange
        var watchId = Guid.NewGuid();
        var watch = CreateTestWatch(id: watchId, status: RatWatchStatus.ClearedEarly);

        _mockWatchRepository
            .Setup(r => r.GetByIdAsync(watchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(watch);

        // Act
        var result = await _service.StartVotingAsync(watchId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region FinalizeVotingAsync Tests

    [Fact]
    public async Task FinalizeVotingAsync_WithGuiltyMajority_CreatesRecord()
    {
        // Arrange
        var watchId = Guid.NewGuid();
        var watch = CreateTestWatch(
            id: watchId,
            status: RatWatchStatus.Voting,
            votingStartedAt: DateTime.UtcNow.AddMinutes(-10));

        _mockWatchRepository
            .Setup(r => r.GetByIdWithVotesAsync(watchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(watch);

        _mockVoteRepository
            .Setup(r => r.GetVoteTallyAsync(watchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((5, 2)); // 5 guilty, 2 not guilty

        _mockWatchRepository
            .Setup(r => r.UpdateAsync(It.IsAny<RatWatch>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockRecordRepository
            .Setup(r => r.AddAsync(It.IsAny<RatRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RatRecord r, CancellationToken _) => r);

        // Act
        var result = await _service.FinalizeVotingAsync(watchId);

        // Assert
        result.Should().BeTrue();

        _mockWatchRepository.Verify(
            r => r.UpdateAsync(It.Is<RatWatch>(w =>
                w.Status == RatWatchStatus.Guilty &&
                w.VotingEndedAt.HasValue),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _mockRecordRepository.Verify(
            r => r.AddAsync(It.Is<RatRecord>(rec =>
                rec.RatWatchId == watchId &&
                rec.UserId == watch.AccusedUserId &&
                rec.GuiltyVotes == 5 &&
                rec.NotGuiltyVotes == 2),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task FinalizeVotingAsync_WithNotGuiltyMajority_DoesNotCreateRecord()
    {
        // Arrange
        var watchId = Guid.NewGuid();
        var watch = CreateTestWatch(
            id: watchId,
            status: RatWatchStatus.Voting,
            votingStartedAt: DateTime.UtcNow.AddMinutes(-10));

        _mockWatchRepository
            .Setup(r => r.GetByIdWithVotesAsync(watchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(watch);

        _mockVoteRepository
            .Setup(r => r.GetVoteTallyAsync(watchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((2, 5)); // 2 guilty, 5 not guilty

        _mockWatchRepository
            .Setup(r => r.UpdateAsync(It.IsAny<RatWatch>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.FinalizeVotingAsync(watchId);

        // Assert
        result.Should().BeTrue();

        _mockWatchRepository.Verify(
            r => r.UpdateAsync(It.Is<RatWatch>(w =>
                w.Status == RatWatchStatus.NotGuilty),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _mockRecordRepository.Verify(
            r => r.AddAsync(It.IsAny<RatRecord>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task FinalizeVotingAsync_WithTiedVotes_ResultsInNotGuilty()
    {
        // Arrange
        var watchId = Guid.NewGuid();
        var watch = CreateTestWatch(
            id: watchId,
            status: RatWatchStatus.Voting,
            votingStartedAt: DateTime.UtcNow.AddMinutes(-10));

        _mockWatchRepository
            .Setup(r => r.GetByIdWithVotesAsync(watchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(watch);

        _mockVoteRepository
            .Setup(r => r.GetVoteTallyAsync(watchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((3, 3)); // Tied vote

        _mockWatchRepository
            .Setup(r => r.UpdateAsync(It.IsAny<RatWatch>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.FinalizeVotingAsync(watchId);

        // Assert
        result.Should().BeTrue();

        _mockWatchRepository.Verify(
            r => r.UpdateAsync(It.Is<RatWatch>(w =>
                w.Status == RatWatchStatus.NotGuilty),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _mockRecordRepository.Verify(
            r => r.AddAsync(It.IsAny<RatRecord>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region ParseScheduleTime Tests

    [Theory]
    [InlineData("10m")]
    [InlineData("30m")]
    [InlineData("2h")]
    [InlineData("1h30m")]
    [InlineData("2h 30m")]
    [InlineData("in 10m")]
    [InlineData("1hour")]
    [InlineData("30min")]
    public void ParseScheduleTime_RelativeFormats_ReturnsValidTime(string input)
    {
        // Arrange & Act
        var result = _service.ParseScheduleTime(input, "Eastern Standard Time");

        // Assert
        result.Should().NotBeNull();
        result!.Value.Should().BeAfter(DateTime.UtcNow);
    }

    [Theory]
    [InlineData("10pm")]
    [InlineData("10am")]
    [InlineData("10:30pm")]
    [InlineData("9:00am")]
    [InlineData("22:00")]
    [InlineData("14:30")]
    public void ParseScheduleTime_AbsoluteFormats_ReturnsValidTime(string input)
    {
        // Arrange & Act
        var result = _service.ParseScheduleTime(input, "Eastern Standard Time");

        // Assert
        result.Should().NotBeNull();
        result!.Value.Should().BeAfter(DateTime.UtcNow.AddDays(-1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid")]
    [InlineData("abc123")]
    [InlineData("25:00")]
    [InlineData("13pm")]
    public void ParseScheduleTime_InvalidFormats_ReturnsNull(string? input)
    {
        // Arrange & Act
        var result = _service.ParseScheduleTime(input!, "Eastern Standard Time");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ParseScheduleTime_RelativeMinutes_ReturnsCorrectOffset()
    {
        // Arrange
        var beforeCall = DateTime.UtcNow;

        // Act
        var result = _service.ParseScheduleTime("10m", "UTC");

        // Assert
        result.Should().NotBeNull();

        var expectedMin = beforeCall.AddMinutes(9);
        var expectedMax = DateTime.UtcNow.AddMinutes(11);

        result!.Value.Should().BeOnOrAfter(expectedMin);
        result.Value.Should().BeOnOrBefore(expectedMax);
    }

    [Fact]
    public void ParseScheduleTime_RelativeHours_ReturnsCorrectOffset()
    {
        // Arrange
        var beforeCall = DateTime.UtcNow;

        // Act
        var result = _service.ParseScheduleTime("2h", "UTC");

        // Assert
        result.Should().NotBeNull();

        var expectedMin = beforeCall.AddHours(1).AddMinutes(59);
        var expectedMax = DateTime.UtcNow.AddHours(2).AddMinutes(1);

        result!.Value.Should().BeOnOrAfter(expectedMin);
        result.Value.Should().BeOnOrBefore(expectedMax);
    }

    #endregion

    #region GetGuildSettingsAsync Tests

    [Fact]
    public async Task GetGuildSettingsAsync_ReturnsSettings()
    {
        // Arrange
        ulong guildId = 123456789UL;
        var settings = CreateTestSettings(guildId);

        _mockSettingsRepository
            .Setup(r => r.GetOrCreateAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        // Act
        var result = await _service.GetGuildSettingsAsync(guildId);

        // Assert
        result.Should().NotBeNull();
        result.GuildId.Should().Be(guildId);
        result.IsEnabled.Should().BeTrue();
        result.Timezone.Should().Be("Eastern Standard Time");
    }

    #endregion

    #region UpdateGuildSettingsAsync Tests

    [Fact]
    public async Task UpdateGuildSettingsAsync_UpdatesSettings()
    {
        // Arrange
        ulong guildId = 123456789UL;
        var settings = CreateTestSettings(guildId);

        _mockSettingsRepository
            .Setup(r => r.GetOrCreateAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        _mockSettingsRepository
            .Setup(r => r.UpdateAsync(It.IsAny<GuildRatWatchSettings>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpdateGuildSettingsAsync(guildId, s =>
        {
            s.Timezone = "Pacific Standard Time";
            s.VotingDurationMinutes = 10;
        });

        // Assert
        result.Timezone.Should().Be("Pacific Standard Time");
        result.VotingDurationMinutes.Should().Be(10);

        _mockSettingsRepository.Verify(
            r => r.UpdateAsync(It.Is<GuildRatWatchSettings>(s =>
                s.Timezone == "Pacific Standard Time" &&
                s.VotingDurationMinutes == 10),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region GetDueWatchesAsync Tests

    [Fact]
    public async Task GetDueWatchesAsync_ReturnsWatchesPastScheduledTime()
    {
        // Arrange
        var dueWatches = new List<RatWatch>
        {
            CreateTestWatch(scheduledAt: DateTime.UtcNow.AddMinutes(-5)),
            CreateTestWatch(scheduledAt: DateTime.UtcNow.AddMinutes(-10))
        };

        _mockWatchRepository
            .Setup(r => r.GetPendingWatchesAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dueWatches);

        // Act
        var result = await _service.GetDueWatchesAsync();

        // Assert
        result.Should().HaveCount(2);
    }

    #endregion
}
