namespace DiscordBot.Core.ViewModels.Portal;

/// <summary>
/// View model for the TTS Portal interface.
/// Contains guild information, available voices, and voice channel data
/// for guild members to send TTS messages.
/// </summary>
public class PortalTtsViewModel
{
    /// <summary>
    /// Gets or sets the guild's Discord snowflake ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the guild display name.
    /// </summary>
    public string GuildName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the guild icon URL, or null if no icon is set.
    /// </summary>
    public string? GuildIconUrl { get; set; }

    /// <summary>
    /// Gets or sets the list of available voice channels in the guild.
    /// </summary>
    public List<VoiceChannelInfo> VoiceChannels { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of available Azure Speech voices.
    /// </summary>
    public List<TtsVoiceInfo> AvailableVoices { get; set; } = new();

    /// <summary>
    /// Gets or sets the maximum allowed message length for TTS.
    /// Default is 200 characters.
    /// </summary>
    public int MaxMessageLength { get; set; } = 200;

    /// <summary>
    /// Gets or sets whether the bot is connected to a voice channel in this guild.
    /// </summary>
    public bool IsConnected { get; set; }

    /// <summary>
    /// Gets or sets the currently connected voice channel ID, or null if not connected.
    /// </summary>
    public ulong? CurrentChannelId { get; set; }

    /// <summary>
    /// Information about a voice channel in the guild.
    /// </summary>
    public class VoiceChannelInfo
    {
        /// <summary>
        /// Gets or sets the Discord snowflake ID of the voice channel.
        /// </summary>
        public ulong Id { get; set; }

        /// <summary>
        /// Gets or sets the name of the voice channel.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the number of members currently in the channel.
        /// </summary>
        public int MemberCount { get; set; }
    }

    /// <summary>
    /// Information about an available TTS voice.
    /// </summary>
    public class TtsVoiceInfo
    {
        /// <summary>
        /// Gets or sets the voice identifier (e.g., "en-US-JennyNeural").
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the display name for the voice (e.g., "Jenny (Female)").
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the locale code for the voice (e.g., "en-US").
        /// </summary>
        public string Locale { get; set; } = string.Empty;
    }
}
