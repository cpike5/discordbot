using System.Text.Json;
using System.Diagnostics;
using Discord.WebSocket;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Services.LLM;
using DiscordBot.Infrastructure.Services;
using Microsoft.Extensions.Caching.Memory;
using cpike.Agent;
using DiscordBot.Infrastructure.Services.LLM.Tooling;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Bot.Services.LLM.Tools;

/// <summary>
/// <c>set_active_guild</c> — chooses the guild the DM assistant's guild-scoped tools act on.
/// </summary>
public sealed class SetActiveGuildTool : IAgentTool
{
    private static readonly AgentToolDefinition ToolDefinition = AgentTools.Define(
        "set_active_guild",
        "Sets the active guild context for subsequent DM commands that require a guild. Accepts a guild ID or a partial name for fuzzy matching. The active guild persists for 24 hours. Use list_guilds first if the user hasn't specified which guild.",
        JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "guild": {
                        "type": "string",
                        "description": "The guild ID (numeric string) or a partial guild name to fuzzy-match against the bot's guild list."
                    }
                },
                "required": ["guild"]
            }
            """).RootElement.Clone());

    private static readonly TimeSpan ActiveGuildTtl = TimeSpan.FromHours(24);

    private readonly DiscordSocketClient _client;
    private readonly IMemoryCache _memoryCache;
    private readonly DiscordRunContext _run;
    private readonly ILogger<SetActiveGuildTool> _logger;

    /// <summary>Creates the tool.</summary>
    public SetActiveGuildTool(
        DiscordSocketClient client,
        IMemoryCache memoryCache,
        DiscordRunContext run,
        ILogger<SetActiveGuildTool> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _run = run ?? throw new ArgumentNullException(nameof(run));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public AgentToolDefinition Definition => ToolDefinition;

    /// <inheritdoc />
    public Task<string> InvokeAsync(
        IReadOnlyDictionary<string, JsonElement> arguments,
        CancellationToken cancellationToken)
    {
        var input = ToolInput.ToElement(arguments);

        try
        {
            return Task.FromResult(Execute(input));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error executing tool {ToolName}", "set_active_guild");
            return Task.FromResult(ToolResults.Error($"Error executing tool: {ex.Message}"));
        }
    }

    private string Execute(JsonElement input)
    {
        if (!input.TryGetProperty("guild", out var guildElement))
        {
            return ToolResults.Error("Missing required parameter: guild");
        }

        var guildInput = guildElement.GetString();
        if (string.IsNullOrWhiteSpace(guildInput))
        {
            return ToolResults.Error("Parameter guild cannot be empty");
        }

        SocketGuild? matchedGuild = null;

        // Try exact ID match first
        if (ulong.TryParse(guildInput, out var guildId))
        {
            matchedGuild = _client.GetGuild(guildId);
        }

        // Fuzzy name match if no ID match
        if (matchedGuild is null)
        {
            var candidates = _client.Guilds
                .Where(g => g.Name.Contains(guildInput, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (candidates.Count == 1)
            {
                matchedGuild = candidates[0];
            }
            else if (candidates.Count > 1)
            {
                var options = candidates
                    .Select(g => new { id = g.Id.ToString(), name = g.Name })
                    .ToList();

                return CreateJsonResult(new
                {
                    error = true,
                    message = $"Multiple guilds match '{guildInput}'. Please be more specific or use the guild ID.",
                    matches = options
                });
            }
        }

        if (matchedGuild is null)
        {
            return ToolResults.Error($"No guild found matching '{guildInput}'. Use list_guilds to see available guilds.");
        }

        // Write to cache with 24h TTL
        var cacheKey = DmAssistantContextFactory.ActiveGuildCacheKeyPrefix + _run.UserId;
        _memoryCache.Set(cacheKey, (ulong?)matchedGuild.Id, ActiveGuildTtl);
        _run.ActiveGuildId = matchedGuild.Id;

        _logger.LogInformation("User {UserId} set active guild to {GuildId} ({GuildName})",
            _run.UserId, matchedGuild.Id, matchedGuild.Name);

        return CreateJsonResult(new
        {
            guild_id = matchedGuild.Id.ToString(),
            guild_name = matchedGuild.Name,
            member_count = matchedGuild.MemberCount,
            message = $"Active guild set to {matchedGuild.Name}. Subsequent commands will use this guild context."
        });
    }

    private static string CreateJsonResult(object data) =>
        JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        });
}
