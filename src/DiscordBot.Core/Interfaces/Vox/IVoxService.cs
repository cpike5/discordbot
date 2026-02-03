using DiscordBot.Core.DTOs.Vox;
using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Interfaces.Vox;

/// <summary>
/// Service interface for high-level VOX message playback operations.
/// </summary>
public interface IVoxService
{
    /// <summary>
    /// Plays a VOX message in the bot's current voice channel for the specified guild.
    /// </summary>
    /// <param name="guildId">The Discord guild ID.</param>
    /// <param name="message">The text message to convert to VOX speech.</param>
    /// <param name="group">The VOX clip group to use.</param>
    /// <param name="options">Playback options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the playback operation.</returns>
    Task<VoxPlaybackResult> PlayAsync(
        ulong guildId,
        string message,
        VoxClipGroup group,
        VoxPlaybackOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tokenizes a message and returns a preview showing which words match clips.
    /// </summary>
    /// <param name="message">The text message to tokenize.</param>
    /// <param name="group">The VOX clip group to check against.</param>
    /// <returns>A preview of the tokenized message with match information.</returns>
    VoxTokenPreview TokenizePreview(string message, VoxClipGroup group);
}
