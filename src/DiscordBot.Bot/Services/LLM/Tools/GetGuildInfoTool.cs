using System.Text.Json;
using Discord.WebSocket;
using Discord;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Core.Interfaces;
using cpike.Agent;
using DiscordBot.Infrastructure.Services.LLM.Tooling;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Bot.Services.LLM.Tools;

/// <summary>
/// <c>get_guild_info</c> — information about a Discord guild.
/// </summary>
public sealed class GetGuildInfoTool : IAgentTool
{
    private static readonly AgentToolDefinition ToolDefinition = AgentTools.Define(
        "get_guild_info",
        "Gets information about a Discord guild (server) including name, icon, creation date, member count, and owner. Use this when users ask about the server or its configuration.",
        JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "guild_id": {
                        "type": "string",
                        "description": "The Discord guild ID (snowflake) to get information for. If not provided, returns info for the current guild."
                    }
                },
                "required": []
            }
            """).RootElement.Clone());

    private readonly DiscordSocketClient _discordClient;
    private readonly IGuildService _guildService;
    private readonly DiscordRunContext _run;
    private readonly ILogger<GetGuildInfoTool> _logger;

    /// <summary>Creates the tool.</summary>
    public GetGuildInfoTool(
        DiscordSocketClient discordClient,
        IGuildService guildService,
        DiscordRunContext run,
        ILogger<GetGuildInfoTool> logger)
    {
        _discordClient = discordClient ?? throw new ArgumentNullException(nameof(discordClient));
        _guildService = guildService ?? throw new ArgumentNullException(nameof(guildService));
        _run = run ?? throw new ArgumentNullException(nameof(run));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public AgentToolDefinition Definition => ToolDefinition;

    /// <inheritdoc />
    public async Task<string> InvokeAsync(
        IReadOnlyDictionary<string, JsonElement> arguments,
        CancellationToken cancellationToken)
    {
        var input = ToolInput.ToElement(arguments);

        try
        {
            return await ExecuteAsync(input, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error executing tool {ToolName}", "get_guild_info");
            return ToolResults.Error($"Error executing tool: {ex.Message}");
        }
    }

    private async Task<string> ExecuteAsync(JsonElement input, CancellationToken cancellationToken)
    {
        // Get guild ID from input or use context guild
        ulong guildId = _run.GuildId;
        if (input.TryGetProperty("guild_id", out var guildIdElement))
        {
            var guildIdString = guildIdElement.GetString();
            if (!string.IsNullOrWhiteSpace(guildIdString) && ulong.TryParse(guildIdString, out var parsedGuildId))
            {
                guildId = parsedGuildId;
            }
        }

        if (guildId == 0)
        {
            return ToolResults.Error("No guild context available. This tool requires a guild context.");
        }

        _logger.LogDebug("Getting guild info for guild {GuildId}", guildId);

        var guild = _discordClient.GetGuild(guildId);
        if (guild == null)
        {
            // Try to get from database
            var dbGuild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
            if (dbGuild != null)
            {
                return CreateJsonResult(new
                {
                    guild_id = guildId.ToString(),
                    name = dbGuild.Name,
                    icon_url = dbGuild.IconUrl,
                    member_count = dbGuild.MemberCount,
                    is_active = dbGuild.IsActive,
                    bot_connected = false,
                    note = "Bot is not currently connected to this guild. Data may be stale."
                });
            }

            return CreateJsonResult(new
            {
                error = true,
                message = $"Guild with ID {guildId} not found"
            });
        }

        _logger.LogDebug("Successfully retrieved guild info for {GuildName}", guild.Name);

        return CreateJsonResult(new
        {
            guild_id = guildId.ToString(),
            name = guild.Name,
            icon_url = guild.IconUrl,
            created_at = guild.CreatedAt.UtcDateTime.ToString("o"),
            member_count = guild.MemberCount,
            owner = new
            {
                id = guild.OwnerId.ToString(),
                username = guild.Owner?.Username
            },
            bot_connected = true,
            bot_joined_at = guild.CurrentUser?.JoinedAt?.UtcDateTime.ToString("o"),
            premium_tier = guild.PremiumTier.ToString(),
            verification_level = guild.VerificationLevel.ToString()
        });
    }

    private static string CreateJsonResult(object data) =>
        JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        });
}
