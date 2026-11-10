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
/// <c>get_user_profile</c> — basic profile information for a Discord user.
/// </summary>
public sealed class GetUserProfileTool : IAgentTool
{
    private static readonly AgentToolDefinition ToolDefinition = AgentTools.Define(
        "get_user_profile",
        "Gets basic profile information for a Discord user including username, avatar URL, account creation date, and optionally their roles in the current guild. Use this when users ask about themselves or other users.",
        JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "user_id": {
                        "type": "string",
                        "description": "The Discord user ID (snowflake) to get profile information for. If not provided, returns info for the requesting user."
                    },
                    "include_roles": {
                        "type": "boolean",
                        "description": "Whether to include the user's roles in the current guild. Default is false.",
                        "default": false
                    }
                },
                "required": []
            }
            """).RootElement.Clone());

    private readonly DiscordSocketClient _discordClient;
    private readonly DiscordRunContext _run;
    private readonly ILogger<GetUserProfileTool> _logger;

    /// <summary>Creates the tool.</summary>
    public GetUserProfileTool(
        DiscordSocketClient discordClient,
        DiscordRunContext run,
        ILogger<GetUserProfileTool> logger)
    {
        _discordClient = discordClient ?? throw new ArgumentNullException(nameof(discordClient));
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
            _logger.LogError(ex, "Error executing tool {ToolName}", "get_user_profile");
            return ToolResults.Error($"Error executing tool: {ex.Message}");
        }
    }

    private async Task<string> ExecuteAsync(JsonElement input, CancellationToken cancellationToken)
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

        var includeRoles = false;
        if (input.TryGetProperty("include_roles", out var includeRolesElement))
        {
            includeRoles = includeRolesElement.GetBoolean();
        }

        _logger.LogDebug("Getting user profile for user {UserId}, includeRoles: {IncludeRoles}", userId, includeRoles);

        // Get user from Discord client - use IUser interface for compatibility
        IUser? user = _discordClient.GetUser(userId);
        if (user == null)
        {
            // Try to fetch from REST API
            try
            {
                user = await _discordClient.Rest.GetUserAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch user {UserId}", userId);
            }
        }

        if (user == null)
        {
            return CreateJsonResult(new
            {
                error = true,
                message = $"User with ID {userId} not found"
            });
        }

        // Build base profile
        var profile = new Dictionary<string, object?>
        {
            ["user_id"] = userId.ToString(),
            ["username"] = user.Username,
            ["global_name"] = user.GlobalName,
            ["discriminator"] = user.Discriminator,
            ["avatar_url"] = user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl(),
            ["created_at"] = user.CreatedAt.UtcDateTime.ToString("o"),
            ["is_bot"] = user.IsBot
        };

        // Include roles if requested and guild context is available
        if (includeRoles && _run.GuildId > 0)
        {
            var guild = _discordClient.GetGuild(_run.GuildId);
            if (guild != null)
            {
                var guildUser = guild.GetUser(userId);
                if (guildUser != null)
                {
                    profile["roles"] = guildUser.Roles
                        .Where(r => !r.IsEveryone)
                        .OrderByDescending(r => r.Position)
                        .Select(r => new
                        {
                            name = r.Name,
                            id = r.Id.ToString(),
                            position = r.Position,
                            color = r.Color.RawValue != 0 ? $"#{r.Color.RawValue:X6}" : null
                        })
                        .ToList();
                }
            }
        }

        _logger.LogDebug("Successfully retrieved profile for user {UserId}", userId);
        return CreateJsonResult(profile);
    }

    private static string CreateJsonResult(object data) =>
        JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        });
}
