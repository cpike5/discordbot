using System.ComponentModel.DataAnnotations;
using System.Reflection;
using DiscordBot.Core.Entities;
using FluentAssertions;
using AudioSettingsModel = DiscordBot.Bot.Pages.Guilds.AudioSettings.IndexModel;
using GuildEditModel = DiscordBot.Bot.Pages.Guilds.EditModel;

namespace DiscordBot.Tests.Bot.Pages.Guilds;

/// <summary>
/// The two screens that set the voice auto-leave timeout (Guild edit and Audio settings) used to
/// accept different ranges, so a value saved on one could be rejected on the other. Both now use
/// <see cref="GuildAudioSettings.MaxAutoLeaveTimeoutMinutes"/>.
/// </summary>
public class AutoLeaveLimitTests
{
    [Fact]
    public void GuildEdit_AcceptsUpToTheSharedLimit_AndNoMore()
    {
        var range = typeof(GuildEditModel.InputModel)
            .GetProperty(nameof(GuildEditModel.InputModel.AutoLeaveTimeoutMinutes))!
            .GetCustomAttribute<RangeAttribute>()!;

        range.Minimum.Should().Be(0);
        range.Maximum.Should().Be(GuildAudioSettings.MaxAutoLeaveTimeoutMinutes);
        range.ErrorMessage.Should().Contain($"0 to {GuildAudioSettings.MaxAutoLeaveTimeoutMinutes}");
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(GuildAudioSettings.MaxAutoLeaveTimeoutMinutes, true)]
    [InlineData(GuildAudioSettings.MaxAutoLeaveTimeoutMinutes + 1, false)]
    [InlineData(1440, false)]
    public void AudioSettings_AcceptsTheSameRange(int minutes, bool accepted)
    {
        var request = new AudioSettingsModel.SaveAllDto
        {
            AutoLeaveTimeoutMinutes = minutes,
            MaxDurationSeconds = 30,
            MaxFileSizeMB = 8,
            MaxSoundsPerGuild = 60,
            MaxSsmlComplexity = 50
        };

        var errors = AudioSettingsModel.Validate(request);

        errors.ContainsKey("autoLeaveTimeout").Should().Be(!accepted);
    }

    [Fact]
    public void BothScreensAgree_ForEveryWholeNumberOfMinutes()
    {
        var edit = typeof(GuildEditModel.InputModel)
            .GetProperty(nameof(GuildEditModel.InputModel.AutoLeaveTimeoutMinutes))!
            .GetCustomAttribute<RangeAttribute>()!;

        foreach (var minutes in new[] { -1, 0, 5, 60, 61, 120, 1440, 1441 })
        {
            var editAccepts = edit.IsValid(minutes);
            var audioAccepts = !AudioSettingsModel.Validate(new AudioSettingsModel.SaveAllDto
            {
                AutoLeaveTimeoutMinutes = minutes,
                MaxDurationSeconds = 30,
                MaxFileSizeMB = 8,
                MaxSoundsPerGuild = 60,
                MaxSsmlComplexity = 50
            }).ContainsKey("autoLeaveTimeout");

            editAccepts.Should().Be(audioAccepts, $"{minutes} minutes must be valid on both screens or neither");
        }
    }
}
