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
/// <c>get_bot_health</c> — bot uptime, memory, latency and connection state.
/// </summary>
public sealed class GetBotHealthTool : IAgentTool
{
    private static readonly AgentToolDefinition ToolDefinition = AgentTools.Define(
        "get_bot_health",
        "Returns bot health information including uptime, memory usage, WebSocket latency, connection state, and guild count. Useful for diagnosing connectivity or performance issues.",
        JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {},
                "required": []
            }
            """).RootElement.Clone());

    private readonly DiscordSocketClient _client;
    private readonly ILogger<GetBotHealthTool> _logger;

    /// <summary>Creates the tool.</summary>
    public GetBotHealthTool(
        DiscordSocketClient client,
        ILogger<GetBotHealthTool> logger)
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
            _logger.LogError(ex, "Error executing tool {ToolName}", "get_bot_health");
            return Task.FromResult(ToolResults.Error($"Error executing tool: {ex.Message}"));
        }
    }

    private string Execute(JsonElement input)
    {
        var process = Process.GetCurrentProcess();
        var uptime = DateTime.UtcNow - process.StartTime.ToUniversalTime();

        return CreateJsonResult(new
        {
            status = _client.ConnectionState.ToString(),
            uptime_hours = Math.Round(uptime.TotalHours, 2),
            uptime_formatted = $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m",
            memory_mb = Math.Round(process.WorkingSet64 / (1024.0 * 1024.0), 1),
            websocket_latency_ms = _client.Latency,
            guild_count = _client.Guilds.Count,
            connection_state = _client.ConnectionState.ToString()
        });
    }

    private static string CreateJsonResult(object data) =>
        JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        });
}
