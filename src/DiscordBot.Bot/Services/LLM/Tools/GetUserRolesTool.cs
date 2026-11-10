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
/// <c>get_user_roles</c> — a user's roles in the current guild.
/// </summary>
public sealed class GetUserRolesTool : IAgentTool
{
    private static readonly AgentToolDefinition ToolDefinition = AgentTools.Define(
        "get_user_roles",
        "Gets all roles for a user in the current guild, including role names, colors, and hierarchy positions. Use this when users ask about their permissions, roles, or what they can access.",
        JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "user_id": {
                        "type": "string",
                        "description": "The Discord user ID (snowflake) to get roles for. If not provided, returns roles for the requesting user."
                    }
                },
                "required": []
            }
            """).RootElement.Clone());

    private readonly DiscordSocketClient _discordClient;
    private readonly DiscordRunContext _run;
    private readonly ILogger<GetUserRolesTool> _logger;

    /// <summary>Creates the tool.</summary>
    public GetUserRolesTool(
        DiscordSocketClient discordClient,
        DiscordRunContext run,
        ILogger<GetUserRolesTool> logger)
    {
        _discordClient = discordClient ?? throw new ArgumentNullException(nameof(discordClient));
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
            _logger.LogError(ex, "Error executing tool {ToolName}", "get_user_roles");
            return Task.FromResult(ToolResults.Error($"Error executing tool: {ex.Message}"));
        }
    }

    private string Execute(JsonElement input)
    {
        // Get user ID from input or use requesting user
        ulong userId = _run.UserId;
        if (input.TryGetProperty("user_id", out var userIdElement))
        {
            var userIdString = userIdElement.GetString();
            if (!string.IsNullOrWhiteSpace(userIdString) && ulong.TryParse(userIdString, out var parsedUserId))
            {
                userId = parsedUserId;
            }
        }

        if (_run.GuildId == 0)
        {
            return ToolResults.Error("No guild context available. This tool requires a guild context.");
        }

        _logger.LogDebug("Getting roles for user {UserId} in guild {GuildId}", userId, _run.GuildId);

        var guild = _discordClient.GetGuild(_run.GuildId);
        if (guild == null)
        {
            return CreateJsonResult(new
            {
                error = true,
                message = $"Guild with ID {_run.GuildId} not found"
            });
        }

        var guildUser = guild.GetUser(userId);
        if (guildUser == null)
        {
            return CreateJsonResult(new
            {
                error = true,
                message = $"User with ID {userId} not found in guild"
            });
        }

        var roles = guildUser.Roles
            .OrderByDescending(r => r.Position)
            .Select(r => new
            {
                name = r.Name,
                id = r.Id.ToString(),
                position = r.Position,
                color = r.Color.RawValue != 0 ? $"#{r.Color.RawValue:X6}" : null,
                is_managed = r.IsManaged,
                is_mentionable = r.IsMentionable,
                is_everyone = r.IsEveryone
            })
            .ToList();

        var highestRole = guildUser.Roles
            .Where(r => !r.IsEveryone)
            .OrderByDescending(r => r.Position)
            .FirstOrDefault();

        _logger.LogDebug("Retrieved {RoleCount} roles for user {UserId}", roles.Count, userId);

        return CreateJsonResult(new
        {
            user_id = userId.ToString(),
            guild_id = _run.GuildId.ToString(),
            roles,
            highest_role_position = highestRole?.Position ?? 0,
            highest_role_name = highestRole?.Name
        });
    }

    private static string CreateJsonResult(object data) =>
        JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        });
}
