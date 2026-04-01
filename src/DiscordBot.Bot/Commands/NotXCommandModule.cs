using Discord;
using Discord.Interactions;
using DiscordBot.Bot.Helpers;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// Slash command module for configuring the not-X X/Twitter link preview feature.
/// All commands require the ManageGuild permission and respond ephemerally.
/// </summary>
[Group("notx", "Configure not-X tweet previews")]
[RequireUserPermission(GuildPermission.ManageGuild)]
public class NotXCommandModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly INotXService _notXService;
    private readonly ILogger<NotXCommandModule> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotXCommandModule"/> class.
    /// </summary>
    public NotXCommandModule(
        INotXService notXService,
        ILogger<NotXCommandModule> logger)
    {
        _notXService = notXService;
        _logger = logger;
    }

    /// <summary>
    /// Enables the not-X feature for the current guild.
    /// </summary>
    [SlashCommand("enable", "Enable not-X tweet previews for this guild")]
    public async Task EnableAsync()
    {
        var guildId = Context.Guild.Id;

        _logger.LogInformation(
            "not-X enable command executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            Context.Guild.Name,
            guildId);

        try
        {
            var settings = await _notXService.GetOrCreateSettingsAsync(guildId);
            settings.IsEnabled = true;
            await _notXService.UpdateSettingsAsync(settings);

            _logger.LogInformation(
                "not-X enabled for guild {GuildId} by user {UserId}",
                guildId,
                Context.User.Id);

            await RespondAsync(
                embed: EmbedHelper.Success("not-X Enabled", "Tweet preview posting has been enabled for this guild."),
                ephemeral: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable not-X for guild {GuildId}", guildId);

            await RespondAsync(
                embed: EmbedHelper.Error("Error", "An error occurred while enabling not-X. Please try again later."),
                ephemeral: true);
        }
    }

    /// <summary>
    /// Disables the not-X feature for the current guild.
    /// </summary>
    [SlashCommand("disable", "Disable not-X tweet previews for this guild")]
    public async Task DisableAsync()
    {
        var guildId = Context.Guild.Id;

        _logger.LogInformation(
            "not-X disable command executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            Context.Guild.Name,
            guildId);

        try
        {
            var settings = await _notXService.GetOrCreateSettingsAsync(guildId);
            settings.IsEnabled = false;
            await _notXService.UpdateSettingsAsync(settings);

            _logger.LogInformation(
                "not-X disabled for guild {GuildId} by user {UserId}",
                guildId,
                Context.User.Id);

            await RespondAsync(
                embed: EmbedHelper.Success("not-X Disabled", "Tweet preview posting has been disabled for this guild."),
                ephemeral: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disable not-X for guild {GuildId}", guildId);

            await RespondAsync(
                embed: EmbedHelper.Error("Error", "An error occurred while disabling not-X. Please try again later."),
                ephemeral: true);
        }
    }

    /// <summary>
    /// Displays the current not-X settings for the guild.
    /// </summary>
    [SlashCommand("status", "Show current not-X settings for this guild")]
    public async Task StatusAsync()
    {
        var guildId = Context.Guild.Id;

        _logger.LogDebug(
            "not-X status command executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            Context.Guild.Name,
            guildId);

        try
        {
            var settings = await _notXService.GetOrCreateSettingsAsync(guildId);

            var statusLine = settings.IsEnabled ? "✅ Enabled" : "❌ Disabled";
            var sensitiveOnlyLine = settings.SensitiveOnly
                ? "✅ Yes (only posts when tweet is flagged)"
                : "❌ No (posts for all tweet links)";
            var outputChannelLine = settings.OutputChannelId.HasValue
                ? $"<#{settings.OutputChannelId.Value}>"
                : "Originating channel (default)";
            var monitoredChannelIds = settings.GetMonitoredChannelIds();
            var monitoredLine = monitoredChannelIds.Count == 0
                ? "All channels"
                : string.Join(", ", monitoredChannelIds.Select(id => $"<#{id}>"));

            var embed = new EmbedBuilder()
                .WithTitle("not-X Settings")
                .WithColor(Color.Blue)
                .AddField("Status", statusLine, inline: true)
                .AddField("Sensitive only", sensitiveOnlyLine, inline: true)
                .AddField("Output channel", outputChannelLine, inline: false)
                .AddField("Monitored channels", monitoredLine, inline: false)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: embed, ephemeral: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve not-X settings for guild {GuildId}", guildId);

            await RespondAsync(
                embed: EmbedHelper.Error("Error", "An error occurred while retrieving not-X settings. Please try again later."),
                ephemeral: true);
        }
    }

    /// <summary>
    /// Toggles whether not-X only posts previews for tweets flagged as sensitive.
    /// </summary>
    /// <param name="enabled">When true, only posts previews for sensitive tweets; when false, posts for all tweet links.</param>
    [SlashCommand("sensitive-only", "Toggle whether to only post previews for sensitive tweets")]
    public async Task SensitiveOnlyAsync(
        [Summary("enabled", "True to only post for sensitive tweets; false to post for all tweet links")]
        bool enabled)
    {
        var guildId = Context.Guild.Id;

        _logger.LogInformation(
            "not-X sensitive-only command executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId}), enabled={Enabled}",
            Context.User.Username,
            Context.User.Id,
            Context.Guild.Name,
            guildId,
            enabled);

        try
        {
            var settings = await _notXService.GetOrCreateSettingsAsync(guildId);
            settings.SensitiveOnly = enabled;
            await _notXService.UpdateSettingsAsync(settings);

            var description = enabled
                ? "not-X will only post previews when a tweet is flagged as sensitive content."
                : "not-X will post previews for all tweet links, regardless of sensitivity.";

            await RespondAsync(
                embed: EmbedHelper.Success("Sensitive-Only Updated", description),
                ephemeral: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update sensitive-only setting for guild {GuildId}", guildId);

            await RespondAsync(
                embed: EmbedHelper.Error("Error", "An error occurred while updating the setting. Please try again later."),
                ephemeral: true);
        }
    }

    /// <summary>
    /// Subgroup for configuring the output channel for tweet previews.
    /// </summary>
    [Group("channel", "Configure the output channel for tweet previews")]
    public class ChannelSubModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly INotXService _notXService;
        private readonly ILogger<ChannelSubModule> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChannelSubModule"/> class.
        /// </summary>
        public ChannelSubModule(
            INotXService notXService,
            ILogger<ChannelSubModule> logger)
        {
            _notXService = notXService;
            _logger = logger;
        }

        /// <summary>
        /// Sets the channel where tweet previews will be posted.
        /// </summary>
        /// <param name="channel">The channel to route tweet previews to.</param>
        [SlashCommand("set", "Route tweet previews to a specific channel")]
        public async Task SetAsync(
            [Summary("channel", "The channel to send tweet previews to")]
            ITextChannel channel)
        {
            var guildId = Context.Guild.Id;

            _logger.LogInformation(
                "not-X channel set command executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId}), channel={ChannelId}",
                Context.User.Username,
                Context.User.Id,
                Context.Guild.Name,
                guildId,
                channel.Id);

            try
            {
                var settings = await _notXService.GetOrCreateSettingsAsync(guildId);
                settings.OutputChannelId = channel.Id;
                await _notXService.UpdateSettingsAsync(settings);

                await RespondAsync(
                    embed: EmbedHelper.Success("Output Channel Set", $"Tweet previews will be posted to {channel.Mention}."),
                    ephemeral: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set output channel for guild {GuildId}", guildId);

                await RespondAsync(
                    embed: EmbedHelper.Error("Error", "An error occurred while setting the output channel. Please try again later."),
                    ephemeral: true);
            }
        }

        /// <summary>
        /// Clears the output channel override, reverting to posting in the originating channel.
        /// </summary>
        [SlashCommand("clear", "Reset tweet previews to post in the originating channel")]
        public async Task ClearAsync()
        {
            var guildId = Context.Guild.Id;

            _logger.LogInformation(
                "not-X channel clear command executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId})",
                Context.User.Username,
                Context.User.Id,
                Context.Guild.Name,
                guildId);

            try
            {
                var settings = await _notXService.GetOrCreateSettingsAsync(guildId);
                settings.OutputChannelId = null;
                await _notXService.UpdateSettingsAsync(settings);

                await RespondAsync(
                    embed: EmbedHelper.Success("Output Channel Cleared", "Tweet previews will now be posted in the originating channel."),
                    ephemeral: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear output channel for guild {GuildId}", guildId);

                await RespondAsync(
                    embed: EmbedHelper.Error("Error", "An error occurred while clearing the output channel. Please try again later."),
                    ephemeral: true);
            }
        }
    }

    /// <summary>
    /// Subgroup for managing the list of channels monitored for tweet links.
    /// </summary>
    [Group("monitor", "Manage which channels are monitored for tweet links")]
    public class MonitorSubModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly INotXService _notXService;
        private readonly ILogger<MonitorSubModule> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="MonitorSubModule"/> class.
        /// </summary>
        public MonitorSubModule(
            INotXService notXService,
            ILogger<MonitorSubModule> logger)
        {
            _notXService = notXService;
            _logger = logger;
        }

        /// <summary>
        /// Adds a channel to the monitored channels list.
        /// </summary>
        /// <param name="channel">The channel to start monitoring for tweet links.</param>
        [SlashCommand("add", "Add a channel to the monitored channels list")]
        public async Task AddAsync(
            [Summary("channel", "The channel to monitor for tweet links")]
            ITextChannel channel)
        {
            var guildId = Context.Guild.Id;

            _logger.LogInformation(
                "not-X monitor add command executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId}), channel={ChannelId}",
                Context.User.Username,
                Context.User.Id,
                Context.Guild.Name,
                guildId,
                channel.Id);

            try
            {
                var settings = await _notXService.GetOrCreateSettingsAsync(guildId);
                var ids = settings.GetMonitoredChannelIds();

                if (!ids.Contains(channel.Id))
                {
                    ids.Add(channel.Id);
                    settings.SetMonitoredChannelIds(ids);
                    await _notXService.UpdateSettingsAsync(settings);
                }

                await RespondAsync(
                    embed: EmbedHelper.Success("Monitor Channel Added", $"{channel.Mention} will now be monitored for tweet links."),
                    ephemeral: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add monitor channel {ChannelId} for guild {GuildId}", channel.Id, guildId);

                await RespondAsync(
                    embed: EmbedHelper.Error("Error", "An error occurred while adding the monitor channel. Please try again later."),
                    ephemeral: true);
            }
        }

        /// <summary>
        /// Removes a channel from the monitored channels list.
        /// </summary>
        /// <param name="channel">The channel to stop monitoring for tweet links.</param>
        [SlashCommand("remove", "Remove a channel from the monitored channels list")]
        public async Task RemoveAsync(
            [Summary("channel", "The channel to stop monitoring for tweet links")]
            ITextChannel channel)
        {
            var guildId = Context.Guild.Id;

            _logger.LogInformation(
                "not-X monitor remove command executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId}), channel={ChannelId}",
                Context.User.Username,
                Context.User.Id,
                Context.Guild.Name,
                guildId,
                channel.Id);

            try
            {
                var settings = await _notXService.GetOrCreateSettingsAsync(guildId);
                var ids = settings.GetMonitoredChannelIds();
                ids.Remove(channel.Id);
                settings.SetMonitoredChannelIds(ids);
                await _notXService.UpdateSettingsAsync(settings);

                await RespondAsync(
                    embed: EmbedHelper.Success("Monitor Channel Removed", $"{channel.Mention} will no longer be monitored for tweet links."),
                    ephemeral: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove monitor channel {ChannelId} for guild {GuildId}", channel.Id, guildId);

                await RespondAsync(
                    embed: EmbedHelper.Error("Error", "An error occurred while removing the monitor channel. Please try again later."),
                    ephemeral: true);
            }
        }

        /// <summary>
        /// Clears the monitored channels list, causing not-X to monitor all channels.
        /// </summary>
        [SlashCommand("clear", "Monitor all channels (clears the monitored channels list)")]
        public async Task ClearAsync()
        {
            var guildId = Context.Guild.Id;

            _logger.LogInformation(
                "not-X monitor clear command executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId})",
                Context.User.Username,
                Context.User.Id,
                Context.Guild.Name,
                guildId);

            try
            {
                var settings = await _notXService.GetOrCreateSettingsAsync(guildId);
                settings.SetMonitoredChannelIds(Array.Empty<ulong>());
                await _notXService.UpdateSettingsAsync(settings);

                await RespondAsync(
                    embed: EmbedHelper.Success("Monitor Channels Cleared", "not-X will now monitor all channels for tweet links."),
                    ephemeral: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear monitor channels for guild {GuildId}", guildId);

                await RespondAsync(
                    embed: EmbedHelper.Error("Error", "An error occurred while clearing the monitor channels. Please try again later."),
                    ephemeral: true);
            }
        }
    }
}
