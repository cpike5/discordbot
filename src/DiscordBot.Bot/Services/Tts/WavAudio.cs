namespace DiscordBot.Bot.Services.Tts;

/// <summary>
/// Wraps the raw PCM the synthesizer produces in a WAV container so a browser can play it.
/// Shared by the member portal's preview endpoint and the guild admin TTS page's Preview.
/// </summary>
public static class WavAudio
{
    /// <summary>
    /// Wraps raw PCM audio data in a WAV container for browser playback.
    /// </summary>
    /// <param name="pcmStream">The raw PCM audio stream.</param>
    /// <param name="sampleRate">Sample rate in Hz (default: 48000).</param>
    /// <param name="bitsPerSample">Bits per sample (default: 16).</param>
    /// <param name="channels">Number of audio channels (default: 2 for stereo).</param>
    /// <returns>A MemoryStream, positioned at the start, containing valid WAV data.</returns>
    public static MemoryStream WrapPcm(Stream pcmStream, int sampleRate = 48000, int bitsPerSample = 16, int channels = 2)
    {
        var pcmData = new MemoryStream();
        pcmStream.CopyTo(pcmData);
        var dataLength = (int)pcmData.Length;

        var wav = new MemoryStream(44 + dataLength);
        using var writer = new BinaryWriter(wav, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataLength);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);                                           // PCM chunk size
        writer.Write((short)1);                                     // Audio format (PCM)
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * bitsPerSample / 8);    // Byte rate
        writer.Write((short)(channels * bitsPerSample / 8));        // Block align
        writer.Write((short)bitsPerSample);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(dataLength);
        pcmData.Position = 0;
        pcmData.CopyTo(wav);
        wav.Position = 0;
        return wav;
    }
}
