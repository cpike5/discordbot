using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Bot.Components;
using DiscordBot.Bot.Models;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// Admin commands module for bot management and monitoring.
/// </summary>
[RequireAdmin]
[RequireGuildActive]
public class AdminModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DiscordSocketClient _client;
    private readonly IBotService _botService;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IInteractionStateService _stateService;
    private readonly ILogger<AdminModule> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminModule"/> class.
    /// </summary>
    public AdminModule(
        DiscordSocketClient client,
        IBotService botService,
        IHostApplicationLifetime lifetime,
        IInteractionStateService stateService,
        ILogger<AdminModule> logger)
    {
        _client = client;
        _botService = botService;
        _lifetime = lifetime;
        _stateService = stateService;
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

        var statusDto = _botService.GetStatus();

        _logger.LogDebug(
            "Bot status: Uptime={Uptime}, Guilds={GuildCount}, Latency={Latency}ms, State={ConnectionState}",
            statusDto.Uptime,
            statusDto.GuildCount,
            statusDto.LatencyMs,
            statusDto.ConnectionState);

        var statusColor = statusDto.ConnectionState == "Connected" ? Color.Green : Color.Orange;

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

        await RespondAsync(embed: embed, ephemeral: true);

        _logger.LogDebug("Status command response sent successfully");
    }

    /// <summary>
    /// Lists all guilds the bot is connected to with pagination.
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
            .Select(g => new GuildDto
            {
                Id = g.Id,
                Name = g.Name,
                MemberCount = g.MemberCount,
                IconUrl = g.IconUrl,
                JoinedAt = g.CurrentUser?.JoinedAt?.UtcDateTime ?? DateTime.UtcNow
            })
            .ToList();

        _logger.LogDebug("Retrieved {GuildCount} guilds", guilds.Count);

        if (guilds.Count == 0)
        {
            var emptyEmbed = new EmbedBuilder()
                .WithTitle("📋 Connected Guilds")
                .WithDescription("The bot is not connected to any guilds.")
                .WithColor(Color.Blue)
                .WithCurrentTimestamp()
                .WithFooter("Admin Command")
                .Build();

            await RespondAsync(embed: emptyEmbed, ephemeral: true);
            _logger.LogDebug("Guilds command response sent (no guilds)");
            return;
        }

        // Set up pagination
        const int pageSize = 5;
        var totalPages = (int)Math.Ceiling(guilds.Count / (double)pageSize);
        var currentPage = 0;

        var state = new GuildsPaginationState
        {
            Guilds = guilds,
            CurrentPage = currentPage,
            PageSize = pageSize,
            TotalPages = totalPages
        };

        var correlationId = _stateService.CreateState(Context.User.Id, state);

        // Build first page
        var pageGuilds = guilds.Take(pageSize).ToList();
        var embed = new EmbedBuilder()
            .WithTitle($"Connected Guilds (Page 1/{totalPages})")
            .WithDescription(string.Join("\n", pageGuilds.Select(g => $"**{g.Name}** (ID: {g.Id})")))
            .WithColor(Color.Blue)
            .WithFooter($"Total: {guilds.Count} guilds")
            .WithCurrentTimestamp()
            .Build();

        // Build pagination buttons
        var components = new ComponentBuilder();
        if (totalPages > 1)
        {
            var nextId = ComponentIdBuilder.Build("guilds", "page", Context.User.Id, correlationId, "next");
            components.WithButton("Next", nextId, ButtonStyle.Primary);
        }

        await RespondAsync(embed: embed, components: components.Build(), ephemeral: true);

        _logger.LogDebug("Guilds command response sent with pagination (page 1/{TotalPages})", totalPages);
    }

    /// <summary>
    /// Gracefully shuts down the bot. Owner only.
    /// Shows a confirmation dialog before shutting down.
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

        // Create state for shutdown confirmation
        var state = new ShutdownState
        {
            RequestedAt = DateTime.UtcNow
        };

        var correlationId = _stateService.CreateState(Context.User.Id, state);

        // Build confirmation buttons
        var confirmId = ComponentIdBuilder.Build("shutdown", "confirm", Context.User.Id, correlationId);
        var cancelId = ComponentIdBuilder.Build("shutdown", "cancel", Context.User.Id, correlationId);

        var components = new ComponentBuilder()
            .WithButton("Confirm Shutdown", confirmId, ButtonStyle.Danger)
            .WithButton("Cancel", cancelId, ButtonStyle.Secondary)
            .Build();

        var embed = new EmbedBuilder()
            .WithTitle("⚠️ Shutdown Confirmation")
            .WithDescription("Are you sure you want to shut down the bot? This will stop all services and disconnect from Discord.")
            .WithColor(Color.Orange)
            .WithCurrentTimestamp()
            .WithFooter("Owner Command")
            .Build();

        await RespondAsync(embed: embed, components: components, ephemeral: true);

        _logger.LogInformation("Shutdown confirmation sent to user {UserId}", Context.User.Id);
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
