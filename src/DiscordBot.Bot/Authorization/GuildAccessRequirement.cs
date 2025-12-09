using Microsoft.AspNetCore.Authorization;

namespace DiscordBot.Bot.Authorization;

/// <summary>
/// Authorization requirement for guild-specific access.
/// Users must either be SuperAdmin or have their Discord account linked
/// and be a member/admin of the specified guild.
/// </summary>
public class GuildAccessRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// Gets the parameter name for the guild ID in route data.
    /// Default: "guildId"
    /// </summary>
    public string GuildIdParameterName { get; }

    public GuildAccessRequirement(string guildIdParameterName = "guildId")
    {
        GuildIdParameterName = guildIdParameterName;
    }
}
