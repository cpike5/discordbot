namespace DiscordBot.Core.Enums;

/// <summary>
/// Specifies the input format for TTS synthesis.
/// </summary>
public enum SynthesisMode
{
    /// <summary>
    /// Plain text input (default). System will wrap in basic SSML with prosody.
    /// </summary>
    PlainText = 0,

    /// <summary>
    /// SSML markup input. System will validate and pass directly to Azure.
    /// </summary>
    Ssml = 1,

    /// <summary>
    /// Auto-detect mode. System attempts to detect SSML by looking for &lt;speak&gt; tag.
    /// </summary>
    Auto = 2
}
