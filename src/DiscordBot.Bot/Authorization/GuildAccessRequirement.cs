using DiscordBot.Core.Entities;
using Microsoft.AspNetCore.Authorization;

namespace DiscordBot.Bot.Authorization;

/// <summary>
/// Authorization requirement for guild-specific access.
/// Users must either be SuperAdmin or have their Discord account linked
/// and be a member/admin of the specified guild with the required access level.
/// </summary>
public class GuildAccessRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// Gets the minimum access level required for this authorization check.
    /// Default: Viewer (read-only access)
    /// </summary>
    public GuildAccessLevel MinimumLevel { get; }

    /// <summary>
    /// Gets the parameter name for the guild ID in route data.
    /// Default: "guildId"
    /// </summary>
    public string GuildIdParameterName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GuildAccessRequirement"/> class.
    /// </summary>
    /// <param name="minimumLevel">The minimum access level required. Defaults to Viewer.</param>
    /// <param name="guildIdParameterName">The route parameter name for the guild ID. Defaults to "guildId".</param>
    public GuildAccessRequirement(
        GuildAccessLevel minimumLevel = GuildAccessLevel.Viewer,
        string guildIdParameterName = "guildId")
    {
        MinimumLevel = minimumLevel;
        GuildIdParameterName = guildIdParameterName;
    }
}
