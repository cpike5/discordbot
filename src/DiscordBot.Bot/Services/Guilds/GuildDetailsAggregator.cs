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
        _assistantOptions = assistantOptions.Value;
        _logger = logger;
    }

    public async Task<GuildDetailsAggregateDto?> BuildAsync(ulong guildId, int recentCommandsLimit, CancellationToken cancellationToken)
    {
        var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", guildId);
            return null;
        }

        var commandQuery = new CommandLogQueryDto
        {
            GuildId = guildId,
            Page = 1,
            PageSize = recentCommandsLimit
        };
        var recentCommandsResponse = await _commandLogService.GetLogsAsync(commandQuery, cancellationToken);

        var welcomeConfig = await _welcomeService.GetConfigurationAsync(guildId, cancellationToken);
        var welcomeEnabled = welcomeConfig?.IsEnabled ?? false;

        var (scheduledMessages, scheduledTotalCount) = await _scheduledMessageService.GetByGuildIdAsync(guildId, 1, 100, cancellationToken);
        var messagesList = scheduledMessages.ToList();
        var scheduledActive = messagesList.Count(m => m.IsEnabled);
        var scheduledPaused = messagesList.Count(m => !m.IsEnabled);

        var nextMessage = messagesList
            .Where(m => m.IsEnabled && m.NextExecutionAt.HasValue && m.NextExecutionAt.Value > DateTime.UtcNow)
            .OrderBy(m => m.NextExecutionAt)
            .FirstOrDefault();

        var ratWatchSettings = await _ratWatchService.GetGuildSettingsAsync(guildId, cancellationToken);
        var (ratWatches, ratWatchTotalCount) = await _ratWatchService.GetByGuildAsync(guildId, 1, 100, cancellationToken);
        var ratWatchList = ratWatches.ToList();
        var ratWatchPending = ratWatchList.Count(w => w.Status == RatWatchStatus.Pending || w.Status == RatWatchStatus.Voting);
        var ratWatchCompleted = ratWatchList.Count(w => w.Status == RatWatchStatus.Guilty || w.Status == RatWatchStatus.NotGuilty);
        var leaderboard = await _ratWatchService.GetLeaderboardAsync(guildId, 5, cancellationToken);

        var (remindersTotal, remindersPending, remindersDeliveredToday, remindersFailed) =
            await _reminderRepository.GetGuildStatsAsync(guildId, cancellationToken);
        var upcomingReminders = (await _reminderRepository.GetUpcomingAsync(guildId, 5, cancellationToken)).ToList();

        var memberCountQuery = new GuildMemberQueryDto { IsActive = true };
        var membersTotalCount = await _guildMemberService.GetMemberCountAsync(guildId, memberCountQuery, cancellationToken);

        var activeTodayQuery = new GuildMemberQueryDto
        {
            IsActive = true,
            LastActiveAtStart = DateTime.UtcNow.Date
        };
        var membersActiveToday = await _guildMemberService.GetMemberCountAsync(guildId, activeTodayQuery, cancellationToken);

        var newestMembersQuery = new GuildMemberQueryDto
        {
            IsActive = true,
            SortBy = "JoinedAt",
            SortDescending = true,
            Page = 1,
            PageSize = 5
        };
        var newestMembersResponse = await _guildMemberService.GetMembersAsync(guildId, newestMembersQuery, cancellationToken);

        var audioSettings = await _guildAudioSettingsService.GetSettingsAsync(guildId, cancellationToken);
        var audioEnabled = audioSettings?.AudioEnabled ?? false;
        var totalSoundCount = await _soundRepository.GetSoundCountAsync(guildId, cancellationToken);

        var oneWeekAgo = DateTime.UtcNow.AddDays(-7);
        var topSounds = (await _soundRepository.GetTopSoundsByPlayCountAsync(guildId, 3, oneWeekAgo, cancellationToken)).ToList();
        var mostUsedTtsVoice = await _ttsMessageRepository.GetMostUsedVoiceAsync(guildId, oneWeekAgo, cancellationToken);

        var assistantGloballyEnabled = _assistantOptions.GloballyEnabled;
        var assistantSettings = await _assistantGuildSettingsService.GetOrCreateSettingsAsync(guildId, cancellationToken);

        _logger.LogDebug(
            "Aggregated guild {GuildId}: {CommandCount} recent commands, WelcomeEnabled={WelcomeEnabled}, ScheduledMessages={ScheduledCount}, RatWatches={RatWatchCount}, Reminders={ReminderCount}, Members={MemberCount}, AudioEnabled={AudioEnabled}, Sounds={SoundCount}, AssistantEnabled={AssistantEnabled}",
            guildId, recentCommandsResponse.Items.Count, welcomeEnabled, scheduledTotalCount, ratWatchTotalCount, remindersTotal, membersTotalCount, audioEnabled, totalSoundCount, assistantSettings.IsEnabled);

        return new GuildDetailsAggregateDto
        {
            Guild = guild,
            RecentCommandLogs = recentCommandsResponse.Items,
            WelcomeEnabled = welcomeEnabled,
            ScheduledMessagesTotal = scheduledTotalCount,
            ScheduledMessagesActive = scheduledActive,
            ScheduledMessagesPaused = scheduledPaused,
            NextScheduledExecution = nextMessage?.NextExecutionAt,
            NextScheduledMessageTitle = nextMessage?.Title,
            RatWatchEnabled = ratWatchSettings.IsEnabled,
            RatWatchTotal = ratWatchTotalCount,
            RatWatchPending = ratWatchPending,
            RatWatchCompleted = ratWatchCompleted,
            TopRatLeaderboard = leaderboard.ToList(),
            RemindersTotal = remindersTotal,
            RemindersPending = remindersPending,
            RemindersDeliveredToday = remindersDeliveredToday,
            RemindersFailed = remindersFailed,
            UpcomingReminders = upcomingReminders,
            MembersTotalCount = membersTotalCount,
            MembersActiveToday = membersActiveToday,
            NewestMembers = newestMembersResponse.Items.ToList(),
            AudioEnabled = audioEnabled,
            TotalSoundCount = totalSoundCount,
            TopSounds = topSounds,
            MostUsedTtsVoice = mostUsedTtsVoice,
            AssistantGloballyEnabled = assistantGloballyEnabled,
            AssistantLocallyEnabled = assistantSettings.IsEnabled,
            AssistantChannelCount = assistantSettings.GetAllowedChannelIdsList().Count,
            AssistantIsRateLimitOverride = assistantSettings.RateLimitOverride.HasValue,
            AssistantRateLimit = assistantSettings.RateLimitOverride ?? _assistantOptions.DefaultRateLimit,
            AssistantRateLimitWindowMinutes = _assistantOptions.RateLimitWindowMinutes
        };
    }
}
