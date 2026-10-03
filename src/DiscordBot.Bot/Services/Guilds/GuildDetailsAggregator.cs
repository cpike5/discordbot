using DiscordBot.Bot.Configuration;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services.Guilds;

/// <summary>
/// Default implementation of <see cref="IGuildDetailsAggregator"/>. Fetches the guild record
/// plus every widget's summary data in parallel-friendly sequence and assembles a single
/// <see cref="GuildDetailsAggregateDto"/> for the Guild Details page.
/// </summary>
public class GuildDetailsAggregator : IGuildDetailsAggregator
{
    private readonly IGuildService _guildService;
    private readonly ICommandLogService _commandLogService;
    private readonly IWelcomeService _welcomeService;
    private readonly IScheduledMessageService _scheduledMessageService;
    private readonly IRatWatchService _ratWatchService;
    private readonly IReminderRepository _reminderRepository;
    private readonly IGuildMemberService _guildMemberService;
    private readonly IGuildAudioSettingsService _guildAudioSettingsService;
    private readonly ISoundRepository _soundRepository;
    private readonly ITtsMessageRepository _ttsMessageRepository;
    private readonly IAssistantGuildSettingsService _assistantGuildSettingsService;
    private readonly ISettingsService _settingsService;
    private readonly AssistantOptions _assistantOptions;
    private readonly ILogger<GuildDetailsAggregator> _logger;

    public GuildDetailsAggregator(
        IGuildService guildService,
        ICommandLogService commandLogService,
        IWelcomeService welcomeService,
        IScheduledMessageService scheduledMessageService,
        IRatWatchService ratWatchService,
        IReminderRepository reminderRepository,
        IGuildMemberService guildMemberService,
        IGuildAudioSettingsService guildAudioSettingsService,
        ISoundRepository soundRepository,
        ITtsMessageRepository ttsMessageRepository,
        IAssistantGuildSettingsService assistantGuildSettingsService,
        ISettingsService settingsService,
        IOptions<AssistantOptions> assistantOptions,
        ILogger<GuildDetailsAggregator> logger)
    {
        _guildService = guildService;
        _commandLogService = commandLogService;
        _welcomeService = welcomeService;
        _scheduledMessageService = scheduledMessageService;
        _ratWatchService = ratWatchService;
        _reminderRepository = reminderRepository;
        _guildMemberService = guildMemberService;
        _guildAudioSettingsService = guildAudioSettingsService;
        _soundRepository = soundRepository;
        _ttsMessageRepository = ttsMessageRepository;
        _assistantGuildSettingsService = assistantGuildSettingsService;
        _settingsService = settingsService;
        _assistantOptions = assistantOptions.Value;
        _logger = logger;
    }

    public async Task<GuildDetailsAggregateDto?> BuildAsync(ulong guildId, int recentCommandsLimit, CancellationToken cancellationToken)
    {
        // The guild record itself is the page: if it cannot be read there is nothing to show.
        var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", guildId);
            return null;
        }

        // Every widget is its own section. One that throws is recorded and left at its defaults,
        // and the page shows a retry state for that widget alone, not a 500 for the whole page.
        var failed = new List<string>();

        async Task<T> Section<T>(string name, Func<Task<T>> load, T fallback)
        {
            try
            {
                return await load();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Guild details section {Section} failed to load for guild {GuildId}", name, guildId);
                failed.Add(name);
                return fallback;
            }
        }

        var recentCommands = await Section(GuildDetailsSections.Activity, async () =>
        {
            var response = await _commandLogService.GetLogsAsync(new CommandLogQueryDto
            {
                GuildId = guildId,
                Page = 1,
                PageSize = recentCommandsLimit
            }, cancellationToken);
            return (IReadOnlyList<CommandLogDto>)response.Items;
        }, Array.Empty<CommandLogDto>());

        var welcomeEnabled = await Section(GuildDetailsSections.Welcome, async () =>
        {
            var welcomeConfig = await _welcomeService.GetConfigurationAsync(guildId, cancellationToken);
            return welcomeConfig?.IsEnabled ?? false;
        }, false);

        var scheduled = await Section(GuildDetailsSections.ScheduledMessages, async () =>
        {
            var (scheduledMessages, scheduledTotalCount) = await _scheduledMessageService.GetByGuildIdAsync(guildId, 1, 100, cancellationToken);
            var messagesList = scheduledMessages.ToList();

            var nextMessage = messagesList
                .Where(m => m.IsEnabled && m.NextExecutionAt.HasValue && m.NextExecutionAt.Value > DateTime.UtcNow)
                .OrderBy(m => m.NextExecutionAt)
                .FirstOrDefault();

            return new ScheduledSummary(
                scheduledTotalCount,
                messagesList.Count(m => m.IsEnabled),
                messagesList.Count(m => !m.IsEnabled),
                nextMessage?.NextExecutionAt,
                nextMessage?.Title);
        }, new ScheduledSummary(0, 0, 0, null, null));

        var ratWatch = await Section(GuildDetailsSections.RatWatch, async () =>
        {
            var ratWatchSettings = await _ratWatchService.GetGuildSettingsAsync(guildId, cancellationToken);
            var (ratWatches, ratWatchTotalCount) = await _ratWatchService.GetByGuildAsync(guildId, 1, 100, cancellationToken);
            var ratWatchList = ratWatches.ToList();
            var leaderboard = await _ratWatchService.GetLeaderboardAsync(guildId, 5, cancellationToken);

            return new RatWatchSummary(
                ratWatchSettings.IsEnabled,
                ratWatchTotalCount,
                ratWatchList.Count(w => w.Status == RatWatchStatus.Pending || w.Status == RatWatchStatus.Voting),
                ratWatchList.Count(w => w.Status == RatWatchStatus.Guilty || w.Status == RatWatchStatus.NotGuilty),
                leaderboard.ToList());
        }, new RatWatchSummary(false, 0, 0, 0, new List<RatLeaderboardEntryDto>()));

        var reminders = await Section(GuildDetailsSections.Reminders, async () =>
        {
            var (total, pending, deliveredToday, failedCount) =
                await _reminderRepository.GetGuildStatsAsync(guildId, cancellationToken);
            var upcoming = (await _reminderRepository.GetUpcomingAsync(guildId, 5, cancellationToken)).ToList();
            return new ReminderSummary(total, pending, deliveredToday, failedCount, upcoming);
        }, new ReminderSummary(0, 0, 0, 0, new List<UpcomingReminderDto>()));

        var members = await Section(GuildDetailsSections.Members, async () =>
        {
            var membersTotalCount = await _guildMemberService.GetMemberCountAsync(
                guildId, new GuildMemberQueryDto { IsActive = true }, cancellationToken);

            var membersActiveToday = await _guildMemberService.GetMemberCountAsync(
                guildId, new GuildMemberQueryDto { IsActive = true, LastActiveAtStart = DateTime.UtcNow.Date }, cancellationToken);

            var newestMembersResponse = await _guildMemberService.GetMembersAsync(guildId, new GuildMemberQueryDto
            {
                IsActive = true,
                SortBy = "JoinedAt",
                SortDescending = true,
                Page = 1,
                PageSize = 5
            }, cancellationToken);

            return new MemberSummary(membersTotalCount, membersActiveToday, newestMembersResponse.Items.ToList());
        }, new MemberSummary(0, 0, new List<GuildMemberDto>()));

        var audio = await Section(GuildDetailsSections.Audio, async () =>
        {
            var audioSettings = await _guildAudioSettingsService.GetSettingsAsync(guildId, cancellationToken);
            var totalSoundCount = await _soundRepository.GetSoundCountAsync(guildId, cancellationToken);

            var oneWeekAgo = DateTime.UtcNow.AddDays(-7);
            var topSounds = (await _soundRepository.GetTopSoundsByPlayCountAsync(guildId, 3, oneWeekAgo, cancellationToken)).ToList();
            var mostUsedTtsVoice = await _ttsMessageRepository.GetMostUsedVoiceAsync(guildId, oneWeekAgo, cancellationToken);

            return new AudioSummary(audioSettings?.AudioEnabled ?? false, totalSoundCount, topSounds, mostUsedTtsVoice);
        }, new AudioSummary(false, 0, new List<(string Name, int PlayCount)>(), null));

        var assistant = await Section(GuildDetailsSections.Assistant, async () =>
        {
            // Read from the settings service rather than the bound options so runtime changes made on the
            // admin Settings page are respected (same source as the Assistant Settings page).
            var globallyEnabled = await _settingsService.GetSettingValueAsync<bool>("Assistant:GloballyEnabled", cancellationToken);
            var assistantSettings = await _assistantGuildSettingsService.GetOrCreateSettingsAsync(guildId, cancellationToken);

            return new AssistantSummary(
                globallyEnabled,
                assistantSettings.IsEnabled,
                assistantSettings.GetAllowedChannelIdsList().Count,
                assistantSettings.RateLimitOverride.HasValue,
                assistantSettings.RateLimitOverride ?? _assistantOptions.RateLimits.DefaultRateLimit,
                _assistantOptions.RateLimits.RateLimitWindowMinutes);
        }, new AssistantSummary(false, false, 0, false, _assistantOptions.RateLimits.DefaultRateLimit, _assistantOptions.RateLimits.RateLimitWindowMinutes));

        _logger.LogDebug(
            "Aggregated guild {GuildId}: {CommandCount} recent commands, WelcomeEnabled={WelcomeEnabled}, ScheduledMessages={ScheduledCount}, RatWatches={RatWatchCount}, Reminders={ReminderCount}, Members={MemberCount}, AudioEnabled={AudioEnabled}, Sounds={SoundCount}, AssistantEnabled={AssistantEnabled}, FailedSections={FailedSections}",
            guildId, recentCommands.Count, welcomeEnabled, scheduled.Total, ratWatch.Total, reminders.Total, members.Total, audio.Enabled, audio.SoundCount, assistant.LocallyEnabled, string.Join(",", failed));

        return new GuildDetailsAggregateDto
        {
            Guild = guild,
            FailedSections = failed,
            RecentCommandLogs = recentCommands,
            WelcomeEnabled = welcomeEnabled,
            ScheduledMessagesTotal = scheduled.Total,
            ScheduledMessagesActive = scheduled.Active,
            ScheduledMessagesPaused = scheduled.Paused,
            NextScheduledExecution = scheduled.NextExecution,
            NextScheduledMessageTitle = scheduled.NextTitle,
            RatWatchEnabled = ratWatch.Enabled,
            RatWatchTotal = ratWatch.Total,
            RatWatchPending = ratWatch.Pending,
            RatWatchCompleted = ratWatch.Completed,
            TopRatLeaderboard = ratWatch.Leaderboard,
            RemindersTotal = reminders.Total,
            RemindersPending = reminders.Pending,
            RemindersDeliveredToday = reminders.DeliveredToday,
            RemindersFailed = reminders.Failed,
            UpcomingReminders = reminders.Upcoming,
            MembersTotalCount = members.Total,
            MembersActiveToday = members.ActiveToday,
            NewestMembers = members.Newest,
            AudioEnabled = audio.Enabled,
            TotalSoundCount = audio.SoundCount,
            TopSounds = audio.TopSounds,
            MostUsedTtsVoice = audio.MostUsedVoice,
            AssistantGloballyEnabled = assistant.GloballyEnabled,
            AssistantLocallyEnabled = assistant.LocallyEnabled,
            AssistantChannelCount = assistant.ChannelCount,
            AssistantIsRateLimitOverride = assistant.IsRateLimitOverride,
            AssistantRateLimit = assistant.RateLimit,
            AssistantRateLimitWindowMinutes = assistant.RateLimitWindowMinutes
        };
    }

    private sealed record ScheduledSummary(int Total, int Active, int Paused, DateTime? NextExecution, string? NextTitle);

    private sealed record RatWatchSummary(bool Enabled, int Total, int Pending, int Completed, IReadOnlyList<RatLeaderboardEntryDto> Leaderboard);

    private sealed record ReminderSummary(int Total, int Pending, int DeliveredToday, int Failed, IReadOnlyList<UpcomingReminderDto> Upcoming);

    private sealed record MemberSummary(int Total, int ActiveToday, IReadOnlyList<GuildMemberDto> Newest);

    private sealed record AudioSummary(bool Enabled, int SoundCount, IReadOnlyList<(string Name, int PlayCount)> TopSounds, string? MostUsedVoice);

    private sealed record AssistantSummary(bool GloballyEnabled, bool LocallyEnabled, int ChannelCount, bool IsRateLimitOverride, int RateLimit, int RateLimitWindowMinutes);
}
