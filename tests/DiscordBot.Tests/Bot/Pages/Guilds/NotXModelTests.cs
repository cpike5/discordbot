using DiscordBot.Bot.Pages.Guilds;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Bot.Pages.Guilds;

/// <summary>Tests for the not-X settings page model.</summary>
public class NotXModelTests
{
    private const ulong GuildId = 123456789UL;
    private const ulong ChannelA = 1001UL;
    private const ulong ChannelB = 1002UL;
    private const ulong ForeignChannel = 9999UL;

    private readonly Mock<INotXService> _notX = new();
    private readonly Mock<IGuildService> _guilds = new();
    private readonly Mock<IDiscordChannelResolver> _channels = new();
    private readonly Mock<IAuditLogService> _audit = new();
    private readonly Mock<IAuditLogBuilder> _auditBuilder = new();
    private readonly Mock<ISettingsService> _settings = new();
    private readonly NotXGuildSettings _entity = new() { GuildId = GuildId, IsEnabled = false, SensitiveOnly = true };

    public NotXModelTests()
    {
        _guilds
            .Setup(s => s.GetGuildByIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildDto { Id = GuildId, Name = "Test Server", IsActive = true });
        _notX.Setup(n => n.GetOrCreateSettingsAsync(GuildId)).ReturnsAsync(_entity);
        _channels.Setup(c => c.GetTextChannels(GuildId)).Returns(new List<ChannelInfo>
        {
            new(ChannelA, "general", 0, ChannelDisplayType.Text),
            new(ChannelB, "links", 1, ChannelDisplayType.Text)
        });

        _auditBuilder.Setup(b => b.ForCategory(It.IsAny<AuditLogCategory>())).Returns(_auditBuilder.Object);
        _auditBuilder.Setup(b => b.WithAction(It.IsAny<AuditLogAction>())).Returns(_auditBuilder.Object);
        _auditBuilder.Setup(b => b.ByUser(It.IsAny<string>())).Returns(_auditBuilder.Object);
        _auditBuilder.Setup(b => b.InGuild(It.IsAny<ulong>())).Returns(_auditBuilder.Object);
        _auditBuilder.Setup(b => b.OnTarget(It.IsAny<string>(), It.IsAny<string>())).Returns(_auditBuilder.Object);
        _auditBuilder.Setup(b => b.WithDetails(It.IsAny<object>())).Returns(_auditBuilder.Object);
        _auditBuilder.Setup(b => b.LogAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _audit.Setup(a => a.CreateBuilder()).Returns(_auditBuilder.Object);
    }

    private NotXModel CreateModel(bool configEnabled = true)
    {
        var model = new NotXModel(
            _notX.Object, _guilds.Object, _channels.Object, _audit.Object, _settings.Object,
            Options.Create(new NotXOptions { Enabled = configEnabled }),
            Mock.Of<ILogger<NotXModel>>());
        var httpContext = new DefaultHttpContext();
        model.PageContext = new PageContext(
            new ActionContext(httpContext, new RouteData(), new PageActionDescriptor(), new ModelStateDictionary()));
        model.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        return model;
    }

    [Fact]
    public async Task OnGet_MapsTheStoredSettingsOntoTheForm()
    {
        _entity.IsEnabled = true;
        _entity.SensitiveOnly = false;
        _entity.HideSensitiveLabel = true;
        _entity.OutputChannelId = ChannelB;
        _entity.SetMonitoredChannelIds(new[] { ChannelA });
        var model = CreateModel();

        var result = await model.OnGetAsync(GuildId, CancellationToken.None);

        result.Should().BeOfType<PageResult>();
        model.Input.IsEnabled.Should().BeTrue();
        model.Input.SensitiveOnly.Should().BeFalse();
        model.Input.HideSensitiveLabel.Should().BeTrue();
        model.Input.OutputChannelId.Should().Be(ChannelB);
        model.Input.MonitoredChannelIds.Should().Equal(ChannelA);
        model.AvailableChannels.Select(c => c.Id).Should().Equal(ChannelA, ChannelB);
    }

    [Fact]
    public async Task OnGet_ForAGuildWithDefaults_ShowsDisabledAndSensitiveOnly_AndDoesNotAudit()
    {
        var model = CreateModel();

        await model.OnGetAsync(GuildId, CancellationToken.None);

        model.Input.IsEnabled.Should().BeFalse();
        model.Input.SensitiveOnly.Should().BeTrue();
        model.Input.OutputChannelId.Should().BeNull();
        model.Input.MonitoredChannelIds.Should().BeEmpty();
        _audit.Verify(a => a.CreateBuilder(), Times.Never);
        _notX.Verify(n => n.UpdateSettingsAsync(It.IsAny<NotXGuildSettings>()), Times.Never);
    }

    [Fact]
    public async Task OnGet_UnknownGuild_ReturnsNotFound()
    {
        _guilds.Setup(s => s.GetGuildByIdAsync(GuildId, It.IsAny<CancellationToken>())).ReturnsAsync((GuildDto?)null);

        var result = await CreateModel().OnGetAsync(GuildId, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Theory]
    [InlineData(true, null, false)]
    [InlineData(true, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    public async Task OnGet_FlagsGlobalDisable(bool configEnabled, bool? featureSetting, bool expectedDisabled)
    {
        _settings
            .Setup(s => s.GetSettingValueAsync<bool?>("Features:NotXEnabled", It.IsAny<CancellationToken>()))
            .ReturnsAsync(featureSetting);
        var model = CreateModel(configEnabled);

        await model.OnGetAsync(GuildId, CancellationToken.None);

        model.GloballyDisabled.Should().Be(expectedDisabled);
    }

    [Fact]
    public async Task OnPost_PersistsEveryFieldAndWritesAnAuditEntry()
    {
        var model = CreateModel();
        model.Input = new NotXModel.InputModel
        {
            IsEnabled = true,
            SensitiveOnly = false,
            HideSensitiveLabel = true,
            OutputChannelId = ChannelB,
            MonitoredChannelIds = new List<ulong> { ChannelA, ChannelB }
        };

        var result = await model.OnPostAsync(GuildId, CancellationToken.None);

        result.Should().BeOfType<RedirectToPageResult>();
        _notX.Verify(n => n.UpdateSettingsAsync(_entity), Times.Once);
        _entity.IsEnabled.Should().BeTrue();
        _entity.SensitiveOnly.Should().BeFalse();
        _entity.HideSensitiveLabel.Should().BeTrue();
        _entity.OutputChannelId.Should().Be(ChannelB);
        _entity.GetMonitoredChannelIds().Should().Equal(ChannelA, ChannelB);
        _auditBuilder.Verify(b => b.WithAction(AuditLogAction.SettingChanged), Times.Once);
        _auditBuilder.Verify(b => b.InGuild(GuildId), Times.Once);
        _auditBuilder.Verify(b => b.LogAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnPost_DropsChannelsThatDoNotBelongToTheGuild()
    {
        var model = CreateModel();
        model.Input = new NotXModel.InputModel
        {
            IsEnabled = true,
            OutputChannelId = ForeignChannel,
            MonitoredChannelIds = new List<ulong> { ChannelA, ForeignChannel }
        };

        await model.OnPostAsync(GuildId, CancellationToken.None);

        _entity.OutputChannelId.Should().BeNull();
        _entity.GetMonitoredChannelIds().Should().Equal(ChannelA);
    }

    [Fact]
    public async Task OnPost_WithNoMonitoredChannels_StoresAnEmptyListMeaningAllChannels()
    {
        _entity.SetMonitoredChannelIds(new[] { ChannelA });
        var model = CreateModel();
        model.Input = new NotXModel.InputModel { IsEnabled = true, MonitoredChannelIds = new List<ulong>() };

        await model.OnPostAsync(GuildId, CancellationToken.None);

        _entity.GetMonitoredChannelIds().Should().BeEmpty();
        _entity.MonitoredChannelIdsJson.Should().Be("[]");
    }

    [Fact]
    public async Task OnPost_WhenTheBotCannotSeeAnyChannels_KeepsTheSavedChannels()
    {
        _channels.Setup(c => c.GetTextChannels(GuildId)).Returns(new List<ChannelInfo>());
        _entity.OutputChannelId = ChannelB;
        _entity.SetMonitoredChannelIds(new[] { ChannelA });
        var model = CreateModel();
        model.Input = new NotXModel.InputModel
        {
            IsEnabled = true,
            OutputChannelId = ChannelB,
            MonitoredChannelIds = new List<ulong> { ChannelA, ForeignChannel }
        };

        await model.OnPostAsync(GuildId, CancellationToken.None);

        _entity.OutputChannelId.Should().Be(ChannelB);
        _entity.GetMonitoredChannelIds().Should().Equal(ChannelA);
    }
}
