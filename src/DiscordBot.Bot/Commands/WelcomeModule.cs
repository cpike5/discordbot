using Discord;
using Discord.Interactions;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// Welcome message management commands for configuring automated welcome messages for new guild members.
/// Allows administrators to enable/disable welcome messages, set the welcome channel, customize the message template, and test the configuration.
/// </summary>
[Group("welcome", "Manage welcome message settings")]
[RequireAdmin]
[RateLimit(5, 60)]
public class WelcomeModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IWelcomeService _welcomeService;
    private readonly ILogger<WelcomeModule> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WelcomeModule"/> class.
    /// </summary>
    public WelcomeModule(
        IWelcomeService welcomeService,
        ILogger<WelcomeModule> logger)
    {
        _welcomeService = welcomeService;
        _logger = logger;
    }

    /// <summary>
    /// Displays the current welcome message configuration for the guild.
    /// </summary>
    [SlashCommand("show", "Display current welcome message configuration")]
    public async Task ShowAsync()
    {
        var guildId = Context.Guild.Id;

        _logger.LogDebug(
            "Show command executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            Context.Guild.Name,
            guildId);

        try
        {
            var config = await _welcomeService.GetConfigurationAsync(guildId);

            if (config == null)
            {
                _logger.LogDebug("No welcome configuration found for guild {GuildId}", guildId);

                var noConfigEmbed = new EmbedBuilder()
                    .WithTitle("Welcome Message Configuration")
                    .WithDescription("Welcome messages have not been configured for this server yet.\n\n" +
                                   "Use `/welcome enable` to get started.")
                    .WithColor(Color.Blue)
                    .WithCurrentTimestamp()
                    .WithFooter("Admin Command")
                    .Build();

                await RespondAsync(embed: noConfigEmbed, ephemeral: true);
                return;
            }

            _logger.LogDebug(
                "Retrieved welcome configuration for guild {GuildId}: Enabled={IsEnabled}, Channel={ChannelId}",
                guildId,
                config.IsEnabled,
                config.WelcomeChannelId);

            var embedBuilder = new EmbedBuilder()
                .WithTitle("Welcome Message Configuration")
                .WithColor(config.IsEnabled ? Color.Green : Color.Orange)
                .AddField("Status", config.IsEnabled ? "Enabled" : "Disabled", inline: true)
                .AddField("Channel", config.WelcomeChannelId.HasValue ? $"<#{config.WelcomeChannelId.Value}>" : "Not set", inline: true)
                .AddField("Use Embed", config.UseEmbed ? "Yes" : "No", inline: true)
                .AddField("Include Avatar", config.IncludeAvatar ? "Yes" : "No", inline: true)
                .AddField("Embed Color", !string.IsNullOrEmpty(config.EmbedColor) ? config.EmbedColor : "Default", inline: true)
                .AddField("Message Template", !string.IsNullOrEmpty(config.WelcomeMessage) ? $"```{config.WelcomeMessage}```" : "Not set", inline: false)
                .AddField("Template Variables", "{user} - mentions the user\n{username} - user's display name\n{server} - guild name\n{membercount} - current member count", inline: false)
                .WithCurrentTimestamp()
                .WithFooter("Admin Command")
                .Build();

            await RespondAsync(embed: embedBuilder, ephemeral: true);

            _logger.LogDebug("Show command completed successfully for guild {GuildId}", guildId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve welcome configuration for guild {GuildId}",
                guildId);

            var errorEmbed = new EmbedBuilder()
                .WithTitle("Error")
                .WithDescription("An error occurred while retrieving the welcome configuration. Please try again later.")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .WithFooter("Admin Command")
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
        }
    }

    /// <summary>
    /// Enables welcome messages for the guild.
    /// </summary>
    [SlashCommand("enable", "Enable welcome messages")]
    public async Task EnableAsync()
    {
        var guildId = Context.Guild.Id;

        _logger.LogDebug(
            "Enable command executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            Context.Guild.Name,
            guildId);

        try
        {
            var updateDto = new WelcomeConfigurationUpdateDto { IsEnabled = true };
            var config = await _welcomeService.UpdateConfigurationAsync(guildId, updateDto);

            if (config == null)
            {
                _logger.LogWarning("Failed to enable welcome messages for guild {GuildId} - guild not found", guildId);

                var errorEmbed = new EmbedBuilder()
                    .WithTitle("Error")
                    .WithDescription("Failed to enable welcome messages. Guild not found.")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .WithFooter("Admin Command")
                    .Build();

                await RespondAsync(embed: errorEmbed, ephemeral: true);
                return;
            }

            _logger.LogInformation(
                "Welcome messages enabled for guild {GuildId} by user {UserId}",
                guildId,
                Context.User.Id);

            var embedBuilder = new EmbedBuilder()
                .WithTitle("Welcome Messages Enabled")
                .WithDescription("Welcome messages have been enabled for this server.")
                .WithColor(Color.Green)
                .AddField("Channel", config.WelcomeChannelId.HasValue ? $"<#{config.WelcomeChannelId.Value}>" : "Not set - configure with `/welcome channel`", inline: false)
                .AddField("Message", !string.IsNullOrEmpty(config.WelcomeMessage) ? $"```{config.WelcomeMessage}```" : "Not set - configure with `/welcome message`", inline: false)
                .WithCurrentTimestamp()
                .WithFooter("Admin Command")
                .Build();

            await RespondAsync(embed: embedBuilder, ephemeral: true);

            _logger.LogDebug("Enable command completed successfully for guild {GuildId}", guildId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to enable welcome messages for guild {GuildId}",
                guildId);

            var errorEmbed = new EmbedBuilder()
                .WithTitle("Error")
                .WithDescription("An error occurred while enabling welcome messages. Please try again later.")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .WithFooter("Admin Command")
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
        }
    }

    /// <summary>
    /// Disables welcome messages for the guild.
    /// </summary>
    [SlashCommand("disable", "Disable welcome messages")]
    public async Task DisableAsync()
    {
        var guildId = Context.Guild.Id;

        _logger.LogDebug(
            "Disable command executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            Context.Guild.Name,
            guildId);

        try
        {
            var updateDto = new WelcomeConfigurationUpdateDto { IsEnabled = false };
            var config = await _welcomeService.UpdateConfigurationAsync(guildId, updateDto);

            if (config == null)
            {
                _logger.LogWarning("Failed to disable welcome messages for guild {GuildId} - guild not found", guildId);

                var errorEmbed = new EmbedBuilder()
                    .WithTitle("Error")
                    .WithDescription("Failed to disable welcome messages. Guild not found.")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .WithFooter("Admin Command")
                    .Build();

                await RespondAsync(embed: errorEmbed, ephemeral: true);
                return;
            }

            _logger.LogInformation(
                "Welcome messages disabled for guild {GuildId} by user {UserId}",
                guildId,
                Context.User.Id);

            var successEmbed = new EmbedBuilder()
                .WithTitle("Welcome Messages Disabled")
                .WithDescription("Welcome messages have been disabled for this server.\n\n" +
                               "Your configuration has been saved and can be re-enabled with `/welcome enable`.")
                .WithColor(Color.Green)
                .WithCurrentTimestamp()
                .WithFooter("Admin Command")
                .Build();

            await RespondAsync(embed: successEmbed, ephemeral: true);

            _logger.LogDebug("Disable command completed successfully for guild {GuildId}", guildId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to disable welcome messages for guild {GuildId}",
                guildId);

            var errorEmbed = new EmbedBuilder()
                .WithTitle("Error")
                .WithDescription("An error occurred while disabling welcome messages. Please try again later.")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .WithFooter("Admin Command")
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
        }
    }

    /// <summary>
    /// Sets the channel where welcome messages will be sent.
    /// </summary>
    /// <param name="channel">The text channel to send welcome messages to.</param>
    [SlashCommand("channel", "Set the channel for welcome messages")]
    public async Task ChannelAsync(
        [Summary("channel", "The channel to send welcome messages to")]
        ITextChannel channel)
    {
        var guildId = Context.Guild.Id;

        _logger.LogDebug(
            "Channel command executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId}) with channel {ChannelName} (ID: {ChannelId})",
            Context.User.Username,
            Context.User.Id,
            Context.Guild.Name,
            guildId,
            channel.Name,
            channel.Id);

        try
        {
            var updateDto = new WelcomeConfigurationUpdateDto { WelcomeChannelId = channel.Id };
            var config = await _welcomeService.UpdateConfigurationAsync(guildId, updateDto);

            if (config == null)
            {
                _logger.LogWarning("Failed to set welcome channel for guild {GuildId} - guild not found", guildId);

                var errorEmbed = new EmbedBuilder()
                    .WithTitle("Error")
                    .WithDescription("Failed to set welcome channel. Guild not found.")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .WithFooter("Admin Command")
                    .Build();

                await RespondAsync(embed: errorEmbed, ephemeral: true);
                return;
            }

            _logger.LogInformation(
                "Welcome channel set to {ChannelId} for guild {GuildId} by user {UserId}",
                channel.Id,
                guildId,
                Context.User.Id);

            var embedBuilder = new EmbedBuilder()
                .WithTitle("Welcome Channel Updated")
                .WithDescription($"Welcome messages will now be sent to {channel.Mention}.")
                .WithColor(Color.Green)
                .AddField("Status", config.IsEnabled ? "Enabled" : "Disabled - use `/welcome enable` to activate", inline: false)
                .WithCurrentTimestamp()
                .WithFooter("Admin Command")
                .Build();

            await RespondAsync(embed: embedBuilder, ephemeral: true);

            _logger.LogDebug("Channel command completed successfully for guild {GuildId}", guildId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to set welcome channel for guild {GuildId} to channel {ChannelId}",
                guildId,
                channel.Id);

            var errorEmbed = new EmbedBuilder()
                .WithTitle("Error")
                .WithDescription("An error occurred while setting the welcome channel. Please try again later.")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .WithFooter("Admin Command")
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
        }
    }

    /// <summary>
    /// Sets the welcome message template.
    /// </summary>
    /// <param name="message">The welcome message template (supports {user}, {username}, {server}, {membercount}).</param>
    [SlashCommand("message", "Set the welcome message template")]
    public async Task MessageAsync(
        [Summary("message", "The welcome message template (supports {user}, {username}, {server}, {membercount})")]
        [MaxLength(2000)]
        string message)
    {
        var guildId = Context.Guild.Id;

        _logger.LogDebug(
            "Message command executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId}) with message length {MessageLength}",
            Context.User.Username,
            Context.User.Id,
            Context.Guild.Name,
            guildId,
            message.Length);

        try
        {
            var updateDto = new WelcomeConfigurationUpdateDto { WelcomeMessage = message };
            var config = await _welcomeService.UpdateConfigurationAsync(guildId, updateDto);

            if (config == null)
            {
                _logger.LogWarning("Failed to set welcome message for guild {GuildId} - guild not found", guildId);

                var errorEmbed = new EmbedBuilder()
                    .WithTitle("Error")
                    .WithDescription("Failed to set welcome message. Guild not found.")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .WithFooter("Admin Command")
                    .Build();

                await RespondAsync(embed: errorEmbed, ephemeral: true);
                return;
            }

            _logger.LogInformation(
                "Welcome message updated for guild {GuildId} by user {UserId}",
                guildId,
                Context.User.Id);

            var embedBuilder = new EmbedBuilder()
                .WithTitle("Welcome Message Updated")
                .WithDescription("The welcome message template has been updated.")
                .WithColor(Color.Green)
                .AddField("New Message", $"```{config.WelcomeMessage}```", inline: false)
                .AddField("Template Variables", "{user} - mentions the user\n{username} - user's display name\n{server} - guild name\n{membercount} - current member count", inline: false)
                .AddField("Status", config.IsEnabled ? "Enabled" : "Disabled - use `/welcome enable` to activate", inline: false)
                .WithCurrentTimestamp()
                .WithFooter("Admin Command")
                .Build();

            await RespondAsync(embed: embedBuilder, ephemeral: true);

            _logger.LogDebug("Message command completed successfully for guild {GuildId}", guildId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to set welcome message for guild {GuildId}",
                guildId);

            var errorEmbed = new EmbedBuilder()
                .WithTitle("Error")
                .WithDescription("An error occurred while setting the welcome message. Please try again later.")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .WithFooter("Admin Command")
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
        }
    }

    /// <summary>
    /// Sends a test welcome message to the current channel using the current user as the preview target.
    /// </summary>
    [SlashCommand("test", "Send a test welcome message")]
    public async Task TestAsync()
    {
        var guildId = Context.Guild.Id;
        var userId = Context.User.Id;

        _logger.LogDebug(
            "Test command executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            userId,
            Context.Guild.Name,
            guildId);

        try
        {
            var config = await _welcomeService.GetConfigurationAsync(guildId);

            if (config == null || !config.WelcomeChannelId.HasValue || string.IsNullOrEmpty(config.WelcomeMessage))
            {
                _logger.LogDebug(
                    "Cannot send test welcome message for guild {GuildId} - configuration incomplete (Config exists: {ConfigExists}, Channel set: {ChannelSet}, Message set: {MessageSet})",
                    guildId,
                    config != null,
                    config?.WelcomeChannelId.HasValue ?? false,
                    !string.IsNullOrEmpty(config?.WelcomeMessage));

                var incompleteEmbed = new EmbedBuilder()
                    .WithTitle("Configuration Incomplete")
                    .WithDescription("Cannot send a test message because the welcome configuration is incomplete.\n\n" +
                                   "Please ensure you have set:\n" +
                                   "- Welcome channel with `/welcome channel`\n" +
                                   "- Welcome message with `/welcome message`")
                    .WithColor(Color.Orange)
                    .WithCurrentTimestamp()
                    .WithFooter("Admin Command")
                    .Build();

                await RespondAsync(embed: incompleteEmbed, ephemeral: true);
                return;
            }

            // Send the test message
            var success = await _welcomeService.SendWelcomeMessageAsync(guildId, userId);

            if (success)
            {
                _logger.LogInformation(
                    "Test welcome message sent for guild {GuildId} to channel {ChannelId} by user {UserId}",
                    guildId,
                    config.WelcomeChannelId.Value,
                    userId);

                var successEmbed = new EmbedBuilder()
                    .WithTitle("Test Message Sent")
                    .WithDescription($"A test welcome message has been sent to <#{config.WelcomeChannelId.Value}>.\n\n" +
                                   "Check the channel to see how your welcome message will appear to new members.")
                    .WithColor(Color.Green)
                    .WithCurrentTimestamp()
                    .WithFooter("Admin Command")
                    .Build();

                await RespondAsync(embed: successEmbed, ephemeral: true);

                _logger.LogDebug("Test command completed successfully for guild {GuildId}", guildId);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to send test welcome message for guild {GuildId} - send operation returned false",
                    guildId);

                var failedEmbed = new EmbedBuilder()
                    .WithTitle("Test Message Failed")
                    .WithDescription("Failed to send the test welcome message. Please check:\n" +
                                   "- The bot has permission to send messages in the welcome channel\n" +
                                   "- The welcome channel still exists\n" +
                                   "- The message template is valid")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .WithFooter("Admin Command")
                    .Build();

                await RespondAsync(embed: failedEmbed, ephemeral: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send test welcome message for guild {GuildId}",
                guildId);

            var errorEmbed = new EmbedBuilder()
                .WithTitle("Error")
                .WithDescription("An error occurred while sending the test welcome message. Please try again later.")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .WithFooter("Admin Command")
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
        }
    }
}
