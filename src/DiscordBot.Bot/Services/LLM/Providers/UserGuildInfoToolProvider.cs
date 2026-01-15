using System.Text.Json;
using Discord;
using Discord.WebSocket;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Infrastructure.Services.LLM.Implementations;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Bot.Services.LLM.Providers;

/// <summary>
/// Tool provider for user and guild information access.
/// Provides tools for getting user profiles, guild info, and user roles.
/// </summary>
public class UserGuildInfoToolProvider : IToolProvider
{
    private readonly ILogger<UserGuildInfoToolProvider> _logger;
    private readonly DiscordSocketClient _discordClient;
    private readonly IGuildService _guildService;

    /// <inheritdoc />
    public string Name => "UserGuildInfo";

    /// <inheritdoc />
    public string Description => "Get information about Discord users and guilds";

    /// <summary>
    /// Initializes a new instance of the UserGuildInfoToolProvider.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="discordClient">Discord socket client for real-time data.</param>
    /// <param name="guildService">Guild service for database data.</param>
    public UserGuildInfoToolProvider(
        ILogger<UserGuildInfoToolProvider> logger,
        DiscordSocketClient discordClient,
        IGuildService guildService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _discordClient = discordClient ?? throw new ArgumentNullException(nameof(discordClient));
        _guildService = guildService ?? throw new ArgumentNullException(nameof(guildService));
    }

    /// <inheritdoc />
    public IEnumerable<LlmToolDefinition> GetTools()
    {
        return UserGuildInfoTools.GetAllTools();
    }

    /// <inheritdoc />
    public async Task<ToolExecutionResult> ExecuteToolAsync(
        string toolName,
        JsonElement input,
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Executing user/guild info tool {ToolName}", toolName);

        try
        {
            return toolName.ToLowerInvariant() switch
            {
                "get_user_profile" => await ExecuteGetUserProfileAsync(input, context, cancellationToken),
                "get_guild_info" => await ExecuteGetGuildInfoAsync(input, context, cancellationToken),
                "get_user_roles" => ExecuteGetUserRoles(input, context),
                _ => throw new NotSupportedException($"Tool '{toolName}' is not supported by this provider")
            };
        }
        catch (NotSupportedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing user/guild info tool {ToolName}", toolName);
            return ToolExecutionResult.CreateError($"Error executing tool: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes the get_user_profile tool.
    /// </summary>
    private async Task<ToolExecutionResult> ExecuteGetUserProfileAsync(
        JsonElement input,
        ToolContext context,
        CancellationToken cancellationToken)
    {
        // Get user ID from input or use requesting user
        ulong userId = context.UserId;
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
        if (includeRoles && context.GuildId > 0)
        {
            var guild = _discordClient.GetGuild(context.GuildId);
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

    /// <summary>
    /// Executes the get_guild_info tool.
    /// </summary>
    private async Task<ToolExecutionResult> ExecuteGetGuildInfoAsync(
        JsonElement input,
        ToolContext context,
        CancellationToken cancellationToken)
    {
        // Get guild ID from input or use context guild
        ulong guildId = context.GuildId;
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
            return ToolExecutionResult.CreateError("No guild context available. This tool requires a guild context.");
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

    /// <summary>
    /// Executes the get_user_roles tool.
    /// </summary>
    private ToolExecutionResult ExecuteGetUserRoles(JsonElement input, ToolContext context)
    {
        // Get user ID from input or use requesting user
        ulong userId = context.UserId;
        if (input.TryGetProperty("user_id", out var userIdElement))
        {
            var userIdString = userIdElement.GetString();
            if (!string.IsNullOrWhiteSpace(userIdString) && ulong.TryParse(userIdString, out var parsedUserId))
            {
                userId = parsedUserId;
            }
        }

        if (context.GuildId == 0)
        {
            return ToolExecutionResult.CreateError("No guild context available. This tool requires a guild context.");
        }

        _logger.LogDebug("Getting roles for user {UserId} in guild {GuildId}", userId, context.GuildId);

        var guild = _discordClient.GetGuild(context.GuildId);
        if (guild == null)
        {
            return CreateJsonResult(new
            {
                error = true,
                message = $"Guild with ID {context.GuildId} not found"
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
            guild_id = context.GuildId.ToString(),
            roles,
            highest_role_position = highestRole?.Position ?? 0,
            highest_role_name = highestRole?.Name
        });
    }

    /// <summary>
    /// Creates a JSON result from an object.
    /// </summary>
    private static ToolExecutionResult CreateJsonResult(object data)
    {
        var jsonString = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        });
        var jsonElement = JsonDocument.Parse(jsonString).RootElement.Clone();
        return ToolExecutionResult.CreateSuccess(jsonElement);
    }
}
