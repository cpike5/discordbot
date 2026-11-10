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
/// <c>list_guilds</c> — lists the guilds the bot is in.
/// </summary>
public sealed class ListGuildsTool : IAgentTool
{
    private static readonly AgentToolDefinition ToolDefinition = AgentTools.Define(
        "list_guilds",
        "Lists all Discord guilds (servers) the bot is currently a member of. Returns each guild's ID, name, and member count. Use this to help the user identify which guild to set as active.",
        JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {},
                "required": []
            }
            """).RootElement.Clone());

    private readonly DiscordSocketClient _client;
    private readonly ILogger<ListGuildsTool> _logger;

    /// <summary>Creates the tool.</summary>
    public ListGuildsTool(
        DiscordSocketClient client,
        ILogger<ListGuildsTool> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
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
            _logger.LogError(ex, "Error executing tool {ToolName}", "list_guilds");
            return Task.FromResult(ToolResults.Error($"Error executing tool: {ex.Message}"));
        }
    }

    private string Execute(JsonElement input)
    {
        var guilds = _client.Guilds
            .OrderBy(g => g.Name)
            .Select(g => new
            {
                id = g.Id.ToString(),
                name = g.Name,
                member_count = g.MemberCount
            })
            .ToList();

        return CreateJsonResult(new
        {
            guilds,
            total_count = guilds.Count
        });
    }

    private static string CreateJsonResult(object data) =>
        JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        });
}
