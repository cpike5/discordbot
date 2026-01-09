using Microsoft.AspNetCore.Authorization;

namespace DiscordBot.Bot.Authorization;

/// <summary>
/// Authorization requirement for portal pages that require guild membership.
/// Lighter weight than admin UI authorization - only requires Discord OAuth
/// authentication and guild membership, no role checks needed.
/// </summary>
public class PortalGuildMemberRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// Gets the parameter name for the guild ID in route data.
    /// Default: "guildId"
    /// </summary>
    public string GuildIdParameterName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PortalGuildMemberRequirement"/> class.
    /// </summary>
    /// <param name="guildIdParameterName">The route parameter name for the guild ID. Defaults to "guildId".</param>
    public PortalGuildMemberRequirement(string guildIdParameterName = "guildId")
    {
        GuildIdParameterName = guildIdParameterName;
    }
}
