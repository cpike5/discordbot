using DiscordBot.Bot.ViewModels.Components;
using FluentAssertions;

namespace DiscordBot.Tests.ViewModels.Components;

/// <summary>
/// Unit tests for <see cref="VoiceChannelPanelViewModel"/>, <see cref="NowPlayingInfo"/>, and <see cref="QueueItemInfo"/>.
/// Tests verify default values, computed properties, and duration formatting.
/// </summary>
public class VoiceChannelPanelViewModelTests
{
    #region VoiceChannelPanelViewModel Tests

    [Fact]
    public void VoiceChannelPanelViewModel_ShowNowPlaying_DefaultsToTrue()
    {
        // Arrange & Act
        var viewModel = new VoiceChannelPanelViewModel
        {
            GuildId = 123456789
        };

        // Assert
        viewModel.ShowNowPlaying.Should().BeTrue("ShowNowPlaying should default to true");
    }

    [Fact]
    public void VoiceChannelPanelViewModel_ShowProgress_DefaultsToTrue()
    {
        // Arrange & Act
        var viewModel = new VoiceChannelPanelViewModel
        {
            GuildId = 123456789
        };

        // Assert
        viewModel.ShowProgress.Should().BeTrue("ShowProgress should default to true");
    }

    [Fact]
    public void VoiceChannelPanelViewModel_IsCompact_DefaultsToFalse()
    {
        // Arrange & Act
        var viewModel = new VoiceChannelPanelViewModel
        {
            GuildId = 123456789
        };

        // Assert
        viewModel.IsCompact.Should().BeFalse("IsCompact should default to false");
    }

    [Fact]
    public void VoiceChannelPanelViewModel_QueueCount_ReturnsQueueCount()
    {
        // Arrange
        var queue = new List<QueueItemInfo>
        {
            new() { Position = 1, Name = "Sound 1", DurationSeconds = 30 },
            new() { Position = 2, Name = "Sound 2", DurationSeconds = 45 },
            new() { Position = 3, Name = "Sound 3", DurationSeconds = 60 }
        };

        var viewModel = new VoiceChannelPanelViewModel
        {
            GuildId = 123456789,
            Queue = queue
        };

        // Act
        var count = viewModel.QueueCount;

        // Assert
        count.Should().Be(3, "QueueCount should return the number of items in the Queue");
    }

    [Fact]
    public void VoiceChannelPanelViewModel_QueueCount_ReturnsZeroWhenQueueIsEmpty()
    {
        // Arrange
        var viewModel = new VoiceChannelPanelViewModel
        {
            GuildId = 123456789,
            Queue = []
        };

        // Act
        var count = viewModel.QueueCount;

        // Assert
        count.Should().Be(0, "QueueCount should return 0 when Queue is empty");
    }

    [Fact]
    public void VoiceChannelPanelViewModel_QueueCount_ReturnsZeroWhenQueueIsDefaultValue()
    {
        // Arrange
        var viewModel = new VoiceChannelPanelViewModel
        {
            GuildId = 123456789
        };

        // Act
        var count = viewModel.QueueCount;

        // Assert
        count.Should().Be(0, "QueueCount should return 0 when Queue uses default value (empty array)");
    }

    [Fact]
    public void VoiceChannelPanelViewModel_AvailableChannels_DefaultsToEmptyList()
    {
        // Arrange & Act
        var viewModel = new VoiceChannelPanelViewModel
        {
            GuildId = 123456789
        };

        // Assert
        viewModel.AvailableChannels.Should().NotBeNull("AvailableChannels should not be null");
        viewModel.AvailableChannels.Should().BeEmpty("AvailableChannels should default to empty list");
    }

    [Fact]
    public void VoiceChannelPanelViewModel_Queue_DefaultsToEmptyList()
    {
        // Arrange & Act
        var viewModel = new VoiceChannelPanelViewModel
        {
            GuildId = 123456789
        };

        // Assert
        viewModel.Queue.Should().NotBeNull("Queue should not be null");
        viewModel.Queue.Should().BeEmpty("Queue should default to empty list");
    }

    [Fact]
    public void VoiceChannelPanelViewModel_CanSetAllProperties()
    {
        // Arrange
        var nowPlaying = new NowPlayingInfo
        {
            Name = "Test Sound",
            DurationSeconds = 120,
            PositionSeconds = 60
        };

        var queue = new List<QueueItemInfo>
        {
            new() { Position = 1, Name = "Queued Sound", DurationSeconds = 90 }
        };

        var channels = new List<VoiceChannelInfo>
        {
            new() { Id = 111, Name = "General Voice", MemberCount = 5 },
            new() { Id = 222, Name = "Music Voice", MemberCount = 2 }
        };

        // Act
        var viewModel = new VoiceChannelPanelViewModel
        {
            GuildId = 987654321,
            IsCompact = true,
            ShowNowPlaying = false,
            ShowProgress = false,
            IsConnected = true,
            ConnectedChannelName = "General Voice",
            ConnectedChannelId = 111,
            ChannelMemberCount = 5,
            AvailableChannels = channels,
            NowPlaying = nowPlaying,
            Queue = queue
        };

        // Assert
        viewModel.GuildId.Should().Be(987654321);
        viewModel.IsCompact.Should().BeTrue();
        viewModel.ShowNowPlaying.Should().BeFalse();
        viewModel.ShowProgress.Should().BeFalse();
        viewModel.IsConnected.Should().BeTrue();
        viewModel.ConnectedChannelName.Should().Be("General Voice");
        viewModel.ConnectedChannelId.Should().Be(111);
        viewModel.ChannelMemberCount.Should().Be(5);
        viewModel.AvailableChannels.Should().HaveCount(2);
        viewModel.NowPlaying.Should().NotBeNull();
        viewModel.Queue.Should().HaveCount(1);
    }

    #endregion

    #region NowPlayingInfo Tests

    [Fact]
    public void NowPlayingInfo_ProgressPercent_ReturnsZeroWhenDurationIsZero()
    {
        // Arrange
        var nowPlaying = new NowPlayingInfo
        {
            Name = "Test Sound",
            DurationSeconds = 0,
            PositionSeconds = 0
        };

        // Act
        var progress = nowPlaying.ProgressPercent;

        // Assert
        progress.Should().Be(0, "ProgressPercent should return 0 when DurationSeconds is 0");
    }

    [Fact]
    public void NowPlayingInfo_ProgressPercent_CalculatesCorrectly()
    {
        // Arrange
        var nowPlaying = new NowPlayingInfo
        {
            Name = "Test Sound",
            DurationSeconds = 60,
            PositionSeconds = 30
        };

        // Act
        var progress = nowPlaying.ProgressPercent;

        // Assert
        progress.Should().Be(50, "ProgressPercent should be 50 when position is 30s and duration is 60s");
    }

    [Fact]
    public void NowPlayingInfo_ProgressPercent_RoundsCorrectly()
    {
        // Arrange - 33.33 seconds out of 100 seconds = 33.33%, should round to 33
        var nowPlaying = new NowPlayingInfo
        {
            Name = "Test Sound",
            DurationSeconds = 100,
            PositionSeconds = 33.33
        };

        // Act
        var progress = nowPlaying.ProgressPercent;

        // Assert
        progress.Should().Be(33, "ProgressPercent should round 33.33% to 33");
    }

    [Fact]
    public void NowPlayingInfo_ProgressPercent_RoundsUpCorrectly()
    {
        // Arrange - 66.66 seconds out of 100 seconds = 66.66%, should round to 67
        var nowPlaying = new NowPlayingInfo
        {
            Name = "Test Sound",
            DurationSeconds = 100,
            PositionSeconds = 66.66
        };

        // Act
        var progress = nowPlaying.ProgressPercent;

        // Assert
        progress.Should().Be(67, "ProgressPercent should round 66.66% to 67");
    }

    [Fact]
    public void NowPlayingInfo_ProgressPercent_Returns100AtEndOfTrack()
    {
        // Arrange
        var nowPlaying = new NowPlayingInfo
        {
            Name = "Test Sound",
            DurationSeconds = 120,
            PositionSeconds = 120
        };

        // Act
        var progress = nowPlaying.ProgressPercent;

        // Assert
        progress.Should().Be(100, "ProgressPercent should be 100 when position equals duration");
    }

    [Fact]
    public void NowPlayingInfo_ProgressPercent_HandlesZeroPosition()
    {
        // Arrange
        var nowPlaying = new NowPlayingInfo
        {
            Name = "Test Sound",
            DurationSeconds = 180,
            PositionSeconds = 0
        };

        // Act
        var progress = nowPlaying.ProgressPercent;

        // Assert
        progress.Should().Be(0, "ProgressPercent should be 0 at the start of playback");
    }

    [Fact]
    public void NowPlayingInfo_ProgressPercent_HandlesVerySmallPercentage()
    {
        // Arrange - 0.5 seconds out of 1000 seconds = 0.05%, should round to 0
        var nowPlaying = new NowPlayingInfo
        {
            Name = "Test Sound",
            DurationSeconds = 1000,
            PositionSeconds = 0.5
        };

        // Act
        var progress = nowPlaying.ProgressPercent;

        // Assert
        progress.Should().Be(0, "ProgressPercent should round very small percentages to 0");
    }

    #endregion

    #region QueueItemInfo Tests

    [Fact]
    public void QueueItemInfo_DurationFormatted_FormatsSecondsCorrectly()
    {
        // Arrange
        var queueItem = new QueueItemInfo
        {
            Position = 1,
            Name = "Test Sound",
            DurationSeconds = 30
        };

        // Act
        var formatted = queueItem.DurationFormatted;

        // Assert
        formatted.Should().Be("0:30", "30 seconds should format as '0:30'");
    }

    [Fact]
    public void QueueItemInfo_DurationFormatted_ZeroPadsSeconds()
    {
        // Arrange
        var queueItem = new QueueItemInfo
        {
            Position = 1,
            Name = "Test Sound",
            DurationSeconds = 5
        };

        // Act
        var formatted = queueItem.DurationFormatted;

        // Assert
        formatted.Should().Be("0:05", "5 seconds should format as '0:05' with zero-padded seconds");
    }

    [Fact]
    public void QueueItemInfo_DurationFormatted_FormatsMinutesAndSecondsCorrectly()
    {
        // Arrange
        var queueItem = new QueueItemInfo
        {
            Position = 1,
            Name = "Test Sound",
            DurationSeconds = 125 // 2 minutes, 5 seconds
        };

        // Act
        var formatted = queueItem.DurationFormatted;

        // Assert
        formatted.Should().Be("2:05", "125 seconds should format as '2:05'");
    }

    [Fact]
    public void QueueItemInfo_DurationFormatted_FormatsExactMinutesCorrectly()
    {
        // Arrange
        var queueItem = new QueueItemInfo
        {
            Position = 1,
            Name = "Test Sound",
            DurationSeconds = 180 // 3 minutes, 0 seconds
        };

        // Act
        var formatted = queueItem.DurationFormatted;

        // Assert
        formatted.Should().Be("3:00", "180 seconds should format as '3:00'");
    }

    [Fact]
    public void QueueItemInfo_DurationFormatted_FormatsHoursCorrectly()
    {
        // Arrange
        var queueItem = new QueueItemInfo
        {
            Position = 1,
            Name = "Test Sound",
            DurationSeconds = 3661 // 1 hour, 1 minute, 1 second
        };

        // Act
        var formatted = queueItem.DurationFormatted;

        // Assert
        formatted.Should().Be("1:01:01", "3661 seconds should format as '1:01:01' with hours");
    }

    [Fact]
    public void QueueItemInfo_DurationFormatted_ZeroPadsMinutesWhenHoursPresent()
    {
        // Arrange
        var queueItem = new QueueItemInfo
        {
            Position = 1,
            Name = "Test Sound",
            DurationSeconds = 3605 // 1 hour, 0 minutes, 5 seconds
        };

        // Act
        var formatted = queueItem.DurationFormatted;

        // Assert
        formatted.Should().Be("1:00:05", "3605 seconds should format as '1:00:05' with zero-padded minutes");
    }

    [Fact]
    public void QueueItemInfo_DurationFormatted_HandlesMultipleHours()
    {
        // Arrange
        var queueItem = new QueueItemInfo
        {
            Position = 1,
            Name = "Test Sound",
            DurationSeconds = 7384 // 2 hours, 3 minutes, 4 seconds
        };

        // Act
        var formatted = queueItem.DurationFormatted;

        // Assert
        formatted.Should().Be("2:03:04", "7384 seconds should format as '2:03:04'");
    }

    [Fact]
    public void QueueItemInfo_DurationFormatted_HandlesExactHour()
    {
        // Arrange
        var queueItem = new QueueItemInfo
        {
            Position = 1,
            Name = "Test Sound",
            DurationSeconds = 3600 // Exactly 1 hour
        };

        // Act
        var formatted = queueItem.DurationFormatted;

        // Assert
        formatted.Should().Be("1:00:00", "3600 seconds should format as '1:00:00'");
    }

    [Fact]
    public void QueueItemInfo_DurationFormatted_HandlesZeroSeconds()
    {
        // Arrange
        var queueItem = new QueueItemInfo
        {
            Position = 1,
            Name = "Test Sound",
            DurationSeconds = 0
        };

        // Act
        var formatted = queueItem.DurationFormatted;

        // Assert
        formatted.Should().Be("0:00", "0 seconds should format as '0:00'");
    }

    [Fact]
    public void QueueItemInfo_DurationFormatted_HandlesJustUnderOneHour()
    {
        // Arrange
        var queueItem = new QueueItemInfo
        {
            Position = 1,
            Name = "Test Sound",
            DurationSeconds = 3599 // 59 minutes, 59 seconds
        };

        // Act
        var formatted = queueItem.DurationFormatted;

        // Assert
        formatted.Should().Be("59:59", "3599 seconds should format as '59:59' without hours");
    }

    #endregion

    #region VoiceChannelInfo Tests

    [Fact]
    public void VoiceChannelInfo_CanSetAllProperties()
    {
        // Arrange & Act
        var channelInfo = new VoiceChannelInfo
        {
            Id = 123456789,
            Name = "General Voice",
            MemberCount = 10
        };

        // Assert
        channelInfo.Id.Should().Be(123456789);
        channelInfo.Name.Should().Be("General Voice");
        channelInfo.MemberCount.Should().Be(10);
    }

    [Fact]
    public void VoiceChannelInfo_MemberCount_DefaultsToZero()
    {
        // Arrange & Act
        var channelInfo = new VoiceChannelInfo
        {
            Id = 123456789,
            Name = "Empty Voice Channel"
        };

        // Assert
        channelInfo.MemberCount.Should().Be(0, "MemberCount should default to 0 when not specified");
    }

    #endregion

    #region Record Value Equality Tests

    [Fact]
    public void VoiceChannelPanelViewModel_IsRecord_SupportsValueEquality()
    {
        // Arrange
        var nowPlaying = new NowPlayingInfo
        {
            Name = "Test Sound",
            DurationSeconds = 120,
            PositionSeconds = 60
        };

        var viewModel1 = new VoiceChannelPanelViewModel
        {
            GuildId = 123456789,
            IsConnected = true,
            ConnectedChannelName = "General Voice",
            NowPlaying = nowPlaying
        };

        var viewModel2 = new VoiceChannelPanelViewModel
        {
            GuildId = 123456789,
            IsConnected = true,
            ConnectedChannelName = "General Voice",
            NowPlaying = nowPlaying
        };

        // Act & Assert
        viewModel1.Should().Be(viewModel2, "records with same property values should be equal");
    }

    [Fact]
    public void NowPlayingInfo_IsRecord_SupportsValueEquality()
    {
        // Arrange
        var nowPlaying1 = new NowPlayingInfo
        {
            Id = "sound-123",
            Name = "Test Sound",
            DurationSeconds = 120,
            PositionSeconds = 60
        };

        var nowPlaying2 = new NowPlayingInfo
        {
            Id = "sound-123",
            Name = "Test Sound",
            DurationSeconds = 120,
            PositionSeconds = 60
        };

        // Act & Assert
        nowPlaying1.Should().Be(nowPlaying2, "records with same property values should be equal");
    }

    [Fact]
    public void QueueItemInfo_IsRecord_SupportsValueEquality()
    {
        // Arrange
        var queueItem1 = new QueueItemInfo
        {
            Position = 1,
            Id = "sound-456",
            Name = "Queued Sound",
            DurationSeconds = 90
        };

        var queueItem2 = new QueueItemInfo
        {
            Position = 1,
            Id = "sound-456",
            Name = "Queued Sound",
            DurationSeconds = 90
        };

        // Act & Assert
        queueItem1.Should().Be(queueItem2, "records with same property values should be equal");
    }

    [Fact]
    public void VoiceChannelInfo_IsRecord_SupportsValueEquality()
    {
        // Arrange
        var channelInfo1 = new VoiceChannelInfo
        {
            Id = 123456789,
            Name = "General Voice",
            MemberCount = 5
        };

        var channelInfo2 = new VoiceChannelInfo
        {
            Id = 123456789,
            Name = "General Voice",
            MemberCount = 5
        };

        // Act & Assert
        channelInfo1.Should().Be(channelInfo2, "records with same property values should be equal");
    }

    #endregion
}
