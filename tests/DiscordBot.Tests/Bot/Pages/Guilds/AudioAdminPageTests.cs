using System.Security.Claims;
using Discord.WebSocket;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Services.Tts;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.DTOs.Tts;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Models;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using AudioLogModel = DiscordBot.Bot.Pages.Guilds.AudioModerationLog.IndexModel;
using AudioSettingsModel = DiscordBot.Bot.Pages.Guilds.AudioSettings.IndexModel;
using SoundboardModel = DiscordBot.Bot.Pages.Guilds.Soundboard.IndexModel;
using TtsModel = DiscordBot.Bot.Pages.Guilds.TextToSpeech.IndexModel;

namespace DiscordBot.Tests.Bot.Pages.Guilds;

/// <summary>
/// The audio admin pages (Soundboard, Text-to-Speech, Audio Settings, Audio Log): what their
/// handlers save, refuse and answer. The soundboard handlers run over the real repositories on
/// PostgreSQL, because the category bug they guard against only shows in the database.
/// </summary>
public class AudioAdminPageTests : IDisposable
{
    private const ulong GuildId = 123456789012345678UL;

    private readonly BotDbContext _db;
    private readonly TestDatabase _database;

    public AudioAdminPageTests()
    {
        (_db, _database) = TestDbContextFactory.CreateContext();
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    private static void Wire(PageModel model, ClaimsPrincipal? user = null)
    {
        var httpContext = new DefaultHttpContext();
        if (user != null) httpContext.User = user;
        httpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
        var actionContext = new ActionContext(httpContext, new RouteData(), new PageActionDescriptor(), new ModelStateDictionary());
        model.PageContext = new PageContext(actionContext);
    }

    private static T Json<T>(IActionResult result, int expectedStatus = 200) where T : class
    {
        var json = result.Should().BeOfType<JsonResult>().Subject;
        (json.StatusCode ?? 200).Should().Be(expectedStatus);
        return (json.Value as T)!;
    }

    private static string Prop(object value, string name)
        => value.GetType().GetProperty(name)!.GetValue(value)?.ToString() ?? string.Empty;

    // ------------------------------------------------------------------ Soundboard

    private SoundboardModel CreateSoundboard()
    {
        var sounds = new SoundRepository(_db, NullLogger<SoundRepository>.Instance, NullLogger<Repository<Sound>>.Instance);
        var categories = new SoundCategoryRepository(_db, NullLogger<SoundCategoryRepository>.Instance, NullLogger<Repository<SoundCategory>>.Instance);

        var soundService = new Mock<ISoundService>();
        soundService
            .Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .Returns((Guid id, ulong guild, CancellationToken ct) => sounds.GetByIdAndGuildAsync(id, guild, ct));

        var model = new SoundboardModel(
            soundService.Object,
            sounds,
            categories,
            Mock.Of<ISoundFileService>(),
            Mock.Of<ISoundboardOrchestrationService>(),
            Mock.Of<IGuildAudioSettingsRepository>(),
            Mock.Of<ISoundPlayLogRepository>(),
            Mock.Of<IGuildService>(),
            new Mock<DiscordSocketClient>(new DiscordSocketConfig()).Object,
            Mock.Of<IAudioService>(),
            Mock.Of<ISettingsService>(),
            NullLogger<SoundboardModel>.Instance);
        Wire(model);
        return model;
    }

    private async Task<(Sound sound, SoundCategory first, SoundCategory second)> SeedSoundAsync()
    {
        _db.Guilds.Add(new Guild { Id = GuildId, Name = "Test", JoinedAt = DateTime.UtcNow, IsActive = true });
        var first = new SoundCategory { GuildId = GuildId, Name = "Memes", CreatedAt = DateTime.UtcNow };
        var second = new SoundCategory { GuildId = GuildId, Name = "Alerts", CreatedAt = DateTime.UtcNow };
        _db.SoundCategories.AddRange(first, second);
        await _db.SaveChangesAsync();

        var sound = new Sound
        {
            Id = Guid.NewGuid(), GuildId = GuildId, Name = "airhorn", FileName = "airhorn.mp3",
            FileSizeBytes = 100, DurationSeconds = 1, UploadedAt = DateTime.UtcNow, CategoryId = first.Id
        };
        _db.Sounds.Add(sound);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return (sound, first, second);
    }

    [Fact]
    public async Task AssignCategory_MovesTheSoundToTheNewCategory()
    {
        // The sound is read with its old Category loaded; Update used to copy that old key back
        var (sound, _, second) = await SeedSoundAsync();
        var model = CreateSoundboard();

        var result = await model.OnPostAssignCategoryAsync(GuildId, new SoundboardModel.AssignCategoryDto { SoundId = sound.Id, CategoryId = second.Id });

        Json<object>(result);
        _db.ChangeTracker.Clear();
        (await _db.Sounds.FindAsync(sound.Id))!.CategoryId.Should().Be(second.Id);
    }

    [Fact]
    public async Task AssignCategory_NullTakesTheSoundOutOfItsCategory()
    {
        var (sound, _, _) = await SeedSoundAsync();
        var model = CreateSoundboard();

        Json<object>(await model.OnPostAssignCategoryAsync(GuildId, new SoundboardModel.AssignCategoryDto { SoundId = sound.Id, CategoryId = null }));

        _db.ChangeTracker.Clear();
        (await _db.Sounds.FindAsync(sound.Id))!.CategoryId.Should().BeNull();
    }

    [Fact]
    public async Task AssignCategory_AnotherServersCategoryIsRefused()
    {
        var (sound, _, _) = await SeedSoundAsync();
        _db.Guilds.Add(new Guild { Id = GuildId + 1, Name = "Other", JoinedAt = DateTime.UtcNow, IsActive = true });
        var foreign = new SoundCategory { GuildId = GuildId + 1, Name = "Foreign", CreatedAt = DateTime.UtcNow };
        _db.SoundCategories.Add(foreign);
        await _db.SaveChangesAsync();
        var model = CreateSoundboard();

        var result = await model.OnPostAssignCategoryAsync(GuildId, new SoundboardModel.AssignCategoryDto { SoundId = sound.Id, CategoryId = foreign.Id });

        Json<object>(result, 400);
    }

    [Fact]
    public async Task CreateCategory_RefusesADuplicateNameAnyCase()
    {
        await SeedSoundAsync();
        var model = CreateSoundboard();

        var duplicate = await model.OnPostCreateCategoryAsync(GuildId, new SoundboardModel.CategoryNameDto { Name = "  memes " });
        Prop(Json<object>(duplicate, 400), "message").Should().Contain("already exists");

        var fresh = await model.OnPostCreateCategoryAsync(GuildId, new SoundboardModel.CategoryNameDto { Name = "Music" });
        Json<object>(fresh);
        _db.SoundCategories.Count(c => c.GuildId == GuildId).Should().Be(3);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateCategory_RefusesAnEmptyName(string name)
    {
        var model = CreateSoundboard();
        Json<object>(await model.OnPostCreateCategoryAsync(GuildId, new SoundboardModel.CategoryNameDto { Name = name }), 400);
    }

    [Fact]
    public async Task DeleteCategory_LeavesItsSoundsUncategorized()
    {
        var (sound, first, _) = await SeedSoundAsync();
        var model = CreateSoundboard();

        Json<object>(await model.OnPostDeleteCategoryAsync(GuildId, new SoundboardModel.CategoryNameDto { Id = first.Id }));

        _db.ChangeTracker.Clear();
        (await _db.Sounds.FindAsync(sound.Id))!.CategoryId.Should().BeNull();
    }

    [Fact]
    public async Task RenameCategory_AnotherServersCategoryIsNotFound()
    {
        var (_, first, _) = await SeedSoundAsync();
        var model = CreateSoundboard();

        var result = await model.OnPostRenameCategoryAsync(GuildId + 5, new SoundboardModel.CategoryNameDto { Id = first.Id, Name = "Hijack" });

        Json<object>(result, 404);
    }

    // ------------------------------------------------------------------ Audio Settings

    private (AudioSettingsModel model, Mock<IGuildAudioSettingsService> audio, Mock<ITtsSettingsService> tts, GuildAudioSettings settings, GuildTtsSettings ttsSettings) CreateAudioSettings()
    {
        var settings = new GuildAudioSettings { GuildId = GuildId };
        var ttsSettings = new GuildTtsSettings { GuildId = GuildId };

        var audio = new Mock<IGuildAudioSettingsService>();
        audio
            .Setup(a => a.UpdateSettingsAsync(GuildId, It.IsAny<Action<GuildAudioSettings>>(), It.IsAny<CancellationToken>()))
            .Callback((ulong _, Action<GuildAudioSettings> apply, CancellationToken _) => apply(settings))
            .ReturnsAsync(settings);

        var tts = new Mock<ITtsSettingsService>();
        tts.Setup(t => t.GetOrCreateSettingsAsync(GuildId, It.IsAny<CancellationToken>())).ReturnsAsync(ttsSettings);

        var model = new AudioSettingsModel(
            audio.Object,
            Mock.Of<IGuildService>(),
            Mock.Of<ISoundService>(),
            new Mock<DiscordSocketClient>(new DiscordSocketConfig()).Object,
            Options.Create(new SoundboardOptions()),
            Mock.Of<ISettingsService>(),
            tts.Object,
            NullLogger<AudioSettingsModel>.Instance)
        {
            GuildId = GuildId
        };
        Wire(model);
        return (model, audio, tts, settings, ttsSettings);
    }

    private static AudioSettingsModel.SaveAllDto ValidSave() => new()
    {
        AudioEnabled = true, AutoLeaveTimeoutMinutes = 7, QueueEnabled = false, EnableMemberPortal = true, SilentPlayback = true,
        MaxDurationSeconds = 45, MaxFileSizeMB = 8, MaxSoundsPerGuild = 60,
        SsmlEnabled = true, StrictSsmlValidation = true, MaxSsmlComplexity = 80, DefaultStyle = "cheerful",
        CommandRoles = new() { ["play"] = new() { 111, 222, 111 }, ["stop"] = new() }
    };

    [Fact]
    public void Validate_AnEmptyNumberIsAnErrorNotAZero()
    {
        var request = ValidSave();
        request.AutoLeaveTimeoutMinutes = null;
        request.MaxSoundsPerGuild = null;

        var errors = AudioSettingsModel.Validate(request);

        errors.Should().ContainKey("autoLeaveTimeout").WhoseValue.Should().Contain("whole number");
        errors.Should().ContainKey("maxSounds");
        errors.Should().NotContainKey("maxDuration");
    }

    [Theory]
    [InlineData(nameof(AudioSettingsModel.SaveAllDto.AutoLeaveTimeoutMinutes), 61, "autoLeaveTimeout")]
    [InlineData(nameof(AudioSettingsModel.SaveAllDto.AutoLeaveTimeoutMinutes), -1, "autoLeaveTimeout")]
    [InlineData(nameof(AudioSettingsModel.SaveAllDto.MaxDurationSeconds), 0, "maxDuration")]
    [InlineData(nameof(AudioSettingsModel.SaveAllDto.MaxDurationSeconds), 301, "maxDuration")]
    [InlineData(nameof(AudioSettingsModel.SaveAllDto.MaxFileSizeMB), 51, "maxFileSize")]
    [InlineData(nameof(AudioSettingsModel.SaveAllDto.MaxSoundsPerGuild), 501, "maxSounds")]
    [InlineData(nameof(AudioSettingsModel.SaveAllDto.MaxSsmlComplexity), 9, "maxSsmlComplexity")]
    public void Validate_OutOfRangeNamesTheField(string property, int value, string field)
    {
        var request = ValidSave();
        typeof(AudioSettingsModel.SaveAllDto).GetProperty(property)!.SetValue(request, value);

        AudioSettingsModel.Validate(request).Should().ContainKey(field);
    }

    [Fact]
    public void Validate_AcceptsTheBoundariesAndAKnownStyle()
    {
        var request = ValidSave();
        request.AutoLeaveTimeoutMinutes = 0;
        request.MaxDurationSeconds = 300;
        request.MaxFileSizeMB = 1;
        request.DefaultStyle = null;

        AudioSettingsModel.Validate(request).Should().BeEmpty();
    }

    [Fact]
    public void Validate_RefusesAnUnknownStyleAndAnUnknownCommand()
    {
        var request = ValidSave();
        request.DefaultStyle = "evil";
        request.CommandRoles!["nuke"] = new();

        var errors = AudioSettingsModel.Validate(request);

        errors.Should().ContainKey("defaultStyle");
        errors.Should().ContainKey("form");
    }

    [Fact]
    public async Task SaveAll_WithABadNumber_SavesNothingAndReportsTheField()
    {
        var (model, audio, tts, _, _) = CreateAudioSettings();
        var request = ValidSave();
        request.MaxDurationSeconds = 9999;

        var result = await model.OnPostSaveAllAsync(request, CancellationToken.None);

        var body = Json<object>(result, 400);
        ((Dictionary<string, string>)body.GetType().GetProperty("errors")!.GetValue(body)!).Should().ContainKey("maxDuration");
        audio.Verify(a => a.UpdateSettingsAsync(It.IsAny<ulong>(), It.IsAny<Action<GuildAudioSettings>>(), It.IsAny<CancellationToken>()), Times.Never);
        tts.Verify(t => t.UpdateSettingsAsync(It.IsAny<GuildTtsSettings>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SaveAll_SavesEverythingTogether()
    {
        var (model, _, tts, settings, ttsSettings) = CreateAudioSettings();

        var result = await model.OnPostSaveAllAsync(ValidSave(), CancellationToken.None);

        Json<object>(result);
        settings.AutoLeaveTimeoutMinutes.Should().Be(7);
        settings.QueueEnabled.Should().BeFalse();
        settings.EnableMemberPortal.Should().BeTrue();
        settings.SilentPlayback.Should().BeTrue();
        settings.MaxDurationSeconds.Should().Be(45);
        settings.MaxFileSizeBytes.Should().Be(8L * 1024 * 1024);
        settings.MaxSoundsPerGuild.Should().Be(60);
        settings.CommandRoleRestrictions.Should().ContainSingle(r => r.CommandName == "play")
            .Which.AllowedRoleIds.Should().BeEquivalentTo(new ulong[] { 111, 222 }, "duplicates collapse");
        settings.CommandRoleRestrictions.Should().NotContain(r => r.CommandName == "stop", "an empty list needs no row");
        ttsSettings.SsmlEnabled.Should().BeTrue();
        ttsSettings.MaxSsmlComplexity.Should().Be(80);
        ttsSettings.DefaultStyle.Should().Be("cheerful");
        tts.Verify(t => t.UpdateSettingsAsync(ttsSettings, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveAll_ClearingAllRolesEmptiesAnExistingRestriction()
    {
        var (model, _, _, settings, _) = CreateAudioSettings();
        settings.CommandRoleRestrictions.Add(new CommandRoleRestriction { GuildId = GuildId, CommandName = "play", AllowedRoleIds = new() { 5 } });
        var request = ValidSave();
        request.CommandRoles = new() { ["play"] = new() };

        Json<object>(await model.OnPostSaveAllAsync(request, CancellationToken.None));

        settings.CommandRoleRestrictions.Single().AllowedRoleIds.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAll_WhenTheTtsHalfFails_SaysWhichHalfWasSaved()
    {
        var (model, _, tts, _, _) = CreateAudioSettings();
        tts.Setup(t => t.UpdateSettingsAsync(It.IsAny<GuildTtsSettings>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("db down: secret detail"));

        var result = await model.OnPostSaveAllAsync(ValidSave(), CancellationToken.None);

        var message = Prop(Json<object>(result, 500), "message");
        message.Should().Contain("audio settings were saved");
        message.Should().NotContain("secret detail");
    }

    // ------------------------------------------------------------------ Audio Log

    [Theory]
    [InlineData("123456789012345678", true, 123456789012345678UL)]
    [InlineData("  42 ", true, 42UL)]
    [InlineData("<@123456789012345678>", true, 123456789012345678UL)]
    [InlineData("<@!987>", true, 987UL)]
    [InlineData("abc", false, 0UL)]
    [InlineData("12abc", false, 0UL)]
    [InlineData("-5", false, 0UL)]
    [InlineData("0", false, 0UL)]
    [InlineData("99999999999999999999999", false, 0UL)]
    [InlineData("", false, 0UL)]
    [InlineData(null, false, 0UL)]
    public void TryParseUserId_AcceptsDigitsAndMentionsOnly(string? text, bool ok, ulong expected)
    {
        AudioLogModel.TryParseUserId(text, out var id).Should().Be(ok);
        id.Should().Be(expected);
    }

    private (AudioLogModel model, Mock<IAudioPlaybackLogRepository> repo) CreateAudioLog()
    {
        var repo = new Mock<IAudioPlaybackLogRepository>();
        repo.Setup(r => r.GetPagedAsync(It.IsAny<ulong>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<AudioFeatureType?>(), It.IsAny<ulong?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<AudioPlaybackLog>)new List<AudioPlaybackLog>(), 0));
        var guilds = new Mock<IGuildService>();
        guilds.Setup(g => g.GetGuildByIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildDto { Id = GuildId, Name = "Test" });

        var model = new AudioLogModel(repo.Object, guilds.Object, new Mock<DiscordSocketClient>(new DiscordSocketConfig()).Object, NullLogger<AudioLogModel>.Instance)
        {
            GuildId = GuildId
        };
        Wire(model);
        return (model, repo);
    }

    [Fact]
    public async Task AuditLog_AnUnreadableUserFilterShowsNothingNotEverything()
    {
        var (model, repo) = CreateAudioLog();
        model.UserFilter = "not-an-id";

        var result = await model.OnGetAsync(CancellationToken.None);

        result.Should().BeOfType<PageResult>();
        model.UserFilterError.Should().NotBeNullOrEmpty();
        model.FiltersValid.Should().BeFalse();
        model.LogEntries.Should().BeEmpty();
        repo.Verify(r => r.GetPagedAsync(It.IsAny<ulong>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<AudioFeatureType?>(), It.IsAny<ulong?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AuditLog_AnEndDateBeforeTheStartIsRefused()
    {
        var (model, repo) = CreateAudioLog();
        model.DateFrom = new DateTime(2026, 10, 5);
        model.DateTo = new DateTime(2026, 10, 1);

        await model.OnGetAsync(CancellationToken.None);

        model.DateRangeError.Should().NotBeNullOrEmpty();
        repo.Verify(r => r.GetPagedAsync(It.IsAny<ulong>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<AudioFeatureType?>(), It.IsAny<ulong?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AuditLog_AMentionFiltersByThatUser()
    {
        var (model, repo) = CreateAudioLog();
        model.UserFilter = "<@111>";

        await model.OnGetAsync(CancellationToken.None);

        model.FiltersValid.Should().BeTrue();
        repo.Verify(r => r.GetPagedAsync(GuildId, It.IsAny<int>(), It.IsAny<int>(), null, 111UL, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ------------------------------------------------------------------ TTS

    [Fact]
    public void ResolveOptions_UsesWhatIsOnScreenAndFallsBackToTheSavedDefaults()
    {
        var saved = new GuildTtsSettings { DefaultVoice = "en-US-JennyNeural", DefaultSpeed = 1.2, DefaultPitch = 0.8, DefaultVolume = 0.7 };

        var onScreen = TtsModel.ResolveOptions(new TtsModel.TtsSendDto { Message = "x", Voice = "en-GB-RyanNeural", Speed = 1.5, Pitch = 1.1, Volume = 0.3 }, saved);
        onScreen.Voice.Should().Be("en-GB-RyanNeural");
        onScreen.Speed.Should().Be(1.5);
        onScreen.Pitch.Should().Be(1.1);
        onScreen.Volume.Should().Be(0.3);

        var defaults = TtsModel.ResolveOptions(new TtsModel.TtsSendDto { Message = "x" }, saved);
        defaults.Voice.Should().Be("en-US-JennyNeural");
        defaults.Speed.Should().Be(1.2);
        defaults.Pitch.Should().Be(0.8);
        defaults.Volume.Should().Be(0.7);
    }

    [Fact]
    public void ResolveOptions_ClampsOutOfRangeValuesAndNeverLeavesTheVoiceEmpty()
    {
        var saved = new GuildTtsSettings { DefaultVoice = "" };

        var options = TtsModel.ResolveOptions(new TtsModel.TtsSendDto { Message = "x", Speed = 9, Pitch = -3, Volume = 4 }, saved);

        options.Speed.Should().Be(2.0);
        options.Pitch.Should().Be(0.5);
        options.Volume.Should().Be(1.0);
        options.Voice.Should().Be("en-US-JennyNeural");
    }

    private (TtsModel model, Mock<ITtsService> tts, Mock<ITtsPlaybackService> playback) CreateTts(bool configured = true, bool connected = true)
    {
        var tts = new Mock<ITtsService>();
        tts.SetupGet(t => t.IsConfigured).Returns(configured);
        tts.Setup(t => t.SynthesizeSpeechAsync(It.IsAny<string>(), It.IsAny<TtsOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(new byte[16]));
        tts.Setup(t => t.SynthesizeSpeechAsync(It.IsAny<string>(), It.IsAny<TtsOptions?>(), It.IsAny<SynthesisMode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(new byte[16]));

        var audio = new Mock<IAudioService>();
        audio.Setup(a => a.IsConnected(GuildId)).Returns(connected);

        var settingsService = new Mock<ITtsSettingsService>();
        settingsService.Setup(s => s.GetOrCreateSettingsAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildTtsSettings { GuildId = GuildId, DefaultVoice = "en-US-JennyNeural", MaxMessageLength = 20 });

        var playback = new Mock<ITtsPlaybackService>();
        playback.Setup(p => p.PlayAsync(It.IsAny<ulong>(), It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TtsPlaybackResult { Success = false, ErrorMessage = "Playback failed." });

        var model = new TtsModel(
            Mock.Of<ITtsHistoryService>(),
            settingsService.Object,
            tts.Object,
            audio.Object,
            playback.Object,
            new Mock<DiscordSocketClient>(new DiscordSocketConfig()).Object,
            Mock.Of<IGuildService>(),
            Mock.Of<ISettingsService>(),
            Mock.Of<IGuildAudioSettingsRepository>(),
            Mock.Of<ISsmlBuilder>(),
            NullLogger<TtsModel>.Instance);
        Wire(model, new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("discord:username", "admin") }, "test")));
        return (model, tts, playback);
    }

    [Fact]
    public async Task SendMessage_SpeaksWithTheVoiceAndSpeedOnScreen()
    {
        var (model, tts, playback) = CreateTts();
        TtsOptions? captured = null;
        tts.Setup(t => t.SynthesizeSpeechAsync("hello", It.IsAny<TtsOptions?>(), It.IsAny<CancellationToken>()))
            .Callback((string _, TtsOptions? o, CancellationToken _) => captured = o)
            .ReturnsAsync(() => new MemoryStream(new byte[16]));

        await model.OnPostSendMessageAsync(GuildId, new TtsModel.TtsSendDto { Message = "hello", Voice = "en-GB-RyanNeural", Speed = 1.5, Pitch = 0.9, Volume = 0.4 });

        captured.Should().NotBeNull();
        captured!.Voice.Should().Be("en-GB-RyanNeural");
        captured.Speed.Should().Be(1.5);
        captured.Pitch.Should().Be(0.9);
        captured.Volume.Should().Be(0.4);
        playback.Verify(p => p.PlayAsync(GuildId, It.IsAny<ulong>(), "admin", "hello", "en-GB-RyanNeural", It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMessage_ProModeSsmlIsSentAsWritten()
    {
        var (model, tts, _) = CreateTts();

        await model.OnPostSendMessageAsync(GuildId, new TtsModel.TtsSendDto { Message = "hi", Ssml = "<speak>hi</speak>" });

        tts.Verify(t => t.SynthesizeSpeechAsync("<speak>hi</speak>", null, SynthesisMode.Ssml, It.IsAny<CancellationToken>()), Times.Once);
        tts.Verify(t => t.SynthesizeSpeechAsync(It.IsAny<string>(), It.IsAny<TtsOptions?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendMessage_RefusalsAreJsonWithAPlainMessage()
    {
        var empty = await CreateTts().model.OnPostSendMessageAsync(GuildId, new TtsModel.TtsSendDto { Message = "  " });
        Prop(Json<object>(empty, 400), "field").Should().Be("message");

        var unconfigured = await CreateTts(configured: false).model.OnPostSendMessageAsync(GuildId, new TtsModel.TtsSendDto { Message = "hi" });
        Prop(Json<object>(unconfigured, 400), "message").Should().Contain("not set up");

        var offline = await CreateTts(connected: false).model.OnPostSendMessageAsync(GuildId, new TtsModel.TtsSendDto { Message = "hi" });
        Prop(Json<object>(offline, 400), "code").Should().Be("not_connected");

        var tooLong = await CreateTts().model.OnPostSendMessageAsync(GuildId, new TtsModel.TtsSendDto { Message = new string('x', 21) });
        Prop(Json<object>(tooLong, 400), "message").Should().Contain("limit is 20");
    }

    [Fact]
    public async Task SendMessage_AnUnreachableSpeechServiceIsA503WithoutExceptionText()
    {
        var (model, tts, _) = CreateTts();
        tts.Setup(t => t.SynthesizeSpeechAsync(It.IsAny<string>(), It.IsAny<TtsOptions?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DiscordBot.Core.Exceptions.TtsUpstreamUnavailableException("WS_OPEN_ERROR_UNDERLYING_IO_OPEN_FAILED", 2));

        var result = await model.OnPostSendMessageAsync(GuildId, new TtsModel.TtsSendDto { Message = "hi" });

        var message = Prop(Json<object>(result, 503), "message");
        message.Should().Contain("could not be reached");
        message.Should().NotContain("WS_OPEN_ERROR");
    }

    [Fact]
    public async Task SendMessage_AnInvalidOperationIsPlainLanguageNotTheExceptionMessage()
    {
        var (model, tts, _) = CreateTts();
        tts.Setup(t => t.SynthesizeSpeechAsync(It.IsAny<string>(), It.IsAny<TtsOptions?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SpeechConfig key abc123 rejected"));

        var result = await model.OnPostSendMessageAsync(GuildId, new TtsModel.TtsSendDto { Message = "hi" });

        Prop(Json<object>(result, 400), "message").Should().NotContain("abc123");
    }

    [Fact]
    public async Task UpdateSettings_RefusesARateLimitOutsideOneToSixty()
    {
        var (model, _, _) = CreateTts();
        var result = await model.OnPostUpdateSettingsAsync(GuildId, new TtsModel.UpdateTtsSettingsDto { RateLimitPerMinute = 0 });
        Prop(Json<object>(result, 400), "field").Should().Be("rateLimitPerMinute");
    }

    // ------------------------------------------------------------------ WAV

    [Fact]
    public void WavAudio_WrapsPcmWithAWellFormedHeader()
    {
        var pcm = new MemoryStream(new byte[100]);

        var wav = WavAudio.WrapPcm(pcm);

        wav.Length.Should().Be(144);
        wav.Position.Should().Be(0);
        var bytes = wav.ToArray();
        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("RIFF");
        System.Text.Encoding.ASCII.GetString(bytes, 8, 4).Should().Be("WAVE");
        System.Text.Encoding.ASCII.GetString(bytes, 36, 4).Should().Be("data");
        BitConverter.ToInt32(bytes, 40).Should().Be(100);
        BitConverter.ToInt32(bytes, 24).Should().Be(48000);
    }
}
