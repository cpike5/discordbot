using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Bot.Preconditions;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// General purpose slash commands for the Discord bot.
/// </summary>
[RequireGuildActive]
public class GeneralModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger<GeneralModule> _logger;

    public GeneralModule(ILogger<GeneralModule> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Ping command to check bot latency and responsiveness.
    /// </summary>
    [SlashCommand("ping", "Check the bot's latency and responsiveness")]
    public async Task PingAsync()
    {
        var client = Context.Client as DiscordSocketClient;
        var latency = client?.Latency ?? 0;

        _logger.LogInformation(
            "Ping command executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId}). Latency: {Latency}ms",
            Context.User.Username,
            Context.User.Id,
            Context.Guild?.Name ?? "DM",
            Context.Guild?.Id ?? 0,
            latency);

        var embed = new EmbedBuilder()
            .WithTitle("🏓 Pong!")
            .WithDescription($"Bot latency: **{latency}ms**")
            .WithColor(Color.Green)
            .WithCurrentTimestamp()
            .Build();

        await RespondAsync(embed: embed, ephemeral: true);

        _logger.LogDebug("Ping command response sent successfully");
    }
}
