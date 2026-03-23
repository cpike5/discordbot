using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace DiscordBot.Bot.Helpers;

/// <summary>
/// Static helper for voice channel validation used by command modules.
/// Centralizes the common pattern of verifying a user is in a voice channel before executing audio commands.
/// </summary>
public static class VoiceChannelHelper
{
    /// <summary>
    /// Validates that the invoking user is currently in a voice channel.
    /// </summary>
    /// <param name="context">The interaction context from the executing command module.</param>
    /// <returns>
    /// A tuple containing:
    /// <list type="bullet">
    ///   <item><description><c>IsValid</c> — <see langword="true"/> when the user is in a voice channel.</description></item>
    ///   <item><description><c>Channel</c> — The voice channel the user is in, or <see langword="null"/> on failure.</description></item>
    ///   <item><description><c>ErrorEmbed</c> — A pre-built error embed to send back to the user, or <see langword="null"/> on success.</description></item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// The embed text matches the existing pattern used in <c>SoundboardModule</c>, <c>TtsModule</c>,
    /// and other audio command modules so responses are consistent across commands.
    /// </remarks>
    public static (bool IsValid, IVoiceChannel? Channel, Embed? ErrorEmbed)
        ValidateUserInVoiceChannel(SocketInteractionContext context)
    {
        var guildUser = context.User as SocketGuildUser;
        var voiceChannel = guildUser?.VoiceChannel;

        if (voiceChannel == null)
        {
            var errorEmbed = new EmbedBuilder()
                .WithTitle("Not in Voice Channel")
                .WithDescription("You need to be in a voice channel to use this command.")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            return (false, null, errorEmbed);
        }

        return (true, voiceChannel, null);
    }
}
