using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// Admin commands module for bot management and monitoring.
/// </summary>
[RequireAdmin]
public class AdminModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DiscordSocketClient _client;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<AdminModule> _logger;
    private static readonly DateTime _processStartTime = DateTime.UtcNow;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminModule"/> class.
    /// </summary>
    public AdminModule(
        DiscordSocketClient client,
        IHostApplicationLifetime lifetime,
        ILogger<AdminModule> logger)
    {
        _client = client;
        _lifetime = lifetime;
        _logger = logger;
    }

    /// <summary>
    /// Displays bot status information including uptime, guild count, and latency.
    /// </summary>
    [SlashCommand("status", "Display bot status and health information")]
    public async Task StatusAsync()
    {
        _logger.LogInformation(
            "Status command executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            Context.Guild?.Name ?? "DM",
            Context.Guild?.Id ?? 0);

        var uptime = DateTime.UtcNow - _processStartTime;
        var guildCount = _client.Guilds.Count;
        var latency = _client.Latency;
        var connectionState = _client.ConnectionState.ToString();
        var botUsername = _client.CurrentUser?.Username ?? "Unknown";

        var statusDto = new BotStatusDto
        {
            Uptime = uptime,
            GuildCount = guildCount,
            LatencyMs = latency,
            StartTime = _processStartTime,
            BotUsername = botUsername,
            ConnectionState = connectionState
        };

        _logger.LogDebug(
            "Bot status: Uptime={Uptime}, Guilds={GuildCount}, Latency={Latency}ms, State={ConnectionState}",
            uptime,
            guildCount,
            latency,
            connectionState);

        var statusColor = connectionState == "Connected" ? Color.Green : Color.Orange;

        var embed = new EmbedBuilder()
            .WithTitle("🤖 Bot Status")
            .WithColor(statusColor)
            .AddField("Bot Username", statusDto.BotUsername, inline: true)
            .AddField("Connection State", statusDto.ConnectionState, inline: true)
            .AddField("Uptime", FormatUptime(statusDto.Uptime), inline: true)
            .AddField("Guild Count", statusDto.GuildCount.ToString(), inline: true)
            .AddField("Latency", $"{statusDto.LatencyMs}ms", inline: true)
            .AddField("Started At", $"<t:{new DateTimeOffset(statusDto.StartTime).ToUnixTimeSeconds()}:F>", inline: true)
            .WithCurrentTimestamp()
            .WithFooter("Admin Command")
            .Build();

        await RespondAsync(embed: embed);

        _logger.LogDebug("Status command response sent successfully");
    }

    /// <summary>
    /// Lists all guilds the bot is connected to.
    /// </summary>
    [SlashCommand("guilds", "List all guilds the bot is connected to")]
    public async Task GuildsAsync()
    {
        _logger.LogInformation(
            "Guilds command executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            Context.Guild?.Name ?? "DM",
            Context.Guild?.Id ?? 0);

        var guilds = _client.Guilds
            .OrderByDescending(g => g.MemberCount)
            .Select(g => new GuildInfoDto
            {
                Id = g.Id,
                Name = g.Name,
                MemberCount = g.MemberCount,
                IconUrl = g.IconUrl,
                JoinedAt = g.CurrentUser?.JoinedAt?.UtcDateTime
            })
            .ToList();

        _logger.LogDebug("Retrieved {GuildCount} guilds", guilds.Count);

        var embed = new EmbedBuilder()
            .WithTitle($"📋 Connected Guilds ({guilds.Count})")
            .WithColor(Color.Blue)
            .WithCurrentTimestamp()
            .WithFooter("Admin Command");

        if (guilds.Count == 0)
        {
            embed.WithDescription("The bot is not connected to any guilds.");
        }
        else
        {
            // Discord embeds have a limit of 25 fields, so we'll show the top 25 guilds
            var topGuilds = guilds.Take(25);
            foreach (var guild in topGuilds)
            {
                var joinedText = guild.JoinedAt.HasValue
                    ? $"<t:{new DateTimeOffset(guild.JoinedAt.Value).ToUnixTimeSeconds()}:R>"
                    : "Unknown";

                embed.AddField(
                    guild.Name,
                    $"ID: `{guild.Id}`\nMembers: {guild.MemberCount}\nJoined: {joinedText}",
                    inline: false);
            }

            if (guilds.Count > 25)
            {
                embed.WithDescription($"Showing top 25 of {guilds.Count} guilds (sorted by member count).");
            }
        }

        await RespondAsync(embed: embed.Build());

        _logger.LogDebug("Guilds command response sent successfully");
    }

    /// <summary>
    /// Gracefully shuts down the bot. Owner only.
    /// </summary>
    [SlashCommand("shutdown", "Gracefully shut down the bot")]
    [Preconditions.RequireOwner]
    public async Task ShutdownAsync()
    {
        _logger.LogWarning(
            "Shutdown command executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            Context.Guild?.Name ?? "DM",
            Context.Guild?.Id ?? 0);

        var embed = new EmbedBuilder()
            .WithTitle("⚠️ Bot Shutdown")
            .WithDescription("The bot is shutting down gracefully...")
            .WithColor(Color.Orange)
            .WithCurrentTimestamp()
            .WithFooter("Owner Command")
            .Build();

        await RespondAsync(embed: embed);

        _logger.LogWarning("Initiating bot shutdown as requested by owner");

        // Trigger graceful shutdown
        _lifetime.StopApplication();
    }

    /// <summary>
    /// Formats a TimeSpan into a human-readable uptime string.
    /// </summary>
    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1)
        {
            return $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";
        }
        else if (uptime.TotalHours >= 1)
        {
            return $"{(int)uptime.TotalHours}h {uptime.Minutes}m {uptime.Seconds}s";
        }
        else if (uptime.TotalMinutes >= 1)
        {
            return $"{(int)uptime.TotalMinutes}m {uptime.Seconds}s";
        }
        else
        {
            return $"{(int)uptime.TotalSeconds}s";
        }
    }
}
