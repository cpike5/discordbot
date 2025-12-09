using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace DiscordBot.Bot.Preconditions;

/// <summary>
/// Precondition that requires the user to be the bot owner.
/// </summary>
public class RequireOwnerAttribute : PreconditionAttribute
{
    /// <summary>
    /// Checks if the user executing the command is the bot owner.
    /// </summary>
    public override async Task<PreconditionResult> CheckRequirementsAsync(
        IInteractionContext context,
        ICommandInfo commandInfo,
        IServiceProvider services)
    {
        if (context.Client is not DiscordSocketClient client)
        {
            return PreconditionResult.FromError("Unable to access Discord client.");
        }

        // Get the application info to retrieve the owner
        var application = await client.GetApplicationInfoAsync();

        // Check if the user is the application owner
        if (context.User.Id == application.Owner.Id)
        {
            return PreconditionResult.FromSuccess();
        }

        return PreconditionResult.FromError("This command can only be used by the bot owner.");
    }
}
