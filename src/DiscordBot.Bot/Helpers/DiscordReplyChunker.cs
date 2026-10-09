using System.Text;
using Discord;

namespace DiscordBot.Bot.Helpers;

/// <summary>
/// Sends an assistant reply within Discord's message limit: one message when it fits, several split
/// on line boundaries when it does not, and a <c>response.md</c> attachment when it is long enough
/// that a dozen messages would be worse. Shared by the DM and guild assistant handlers so the two
/// never drift.
/// </summary>
public static class DiscordReplyChunker
{
    /// <summary>Discord's message length limit.</summary>
    public const int MaxMessageLength = 2000;

    /// <summary>Above this length the reply goes as a file rather than a run of messages.</summary>
    public const int FileAttachmentThreshold = 8000;

    /// <summary>The text that precedes an attached reply.</summary>
    public const string AttachmentLead = "Here's the full response:";

    /// <summary>
    /// Sends <paramref name="response"/> to <paramref name="channel"/>. <paramref name="reference"/>,
    /// when given, goes on the first message only, so a reply threads under the question without
    /// every chunk doing so.
    /// </summary>
    public static async Task SendAsync(IMessageChannel channel, string response, MessageReference? reference = null)
    {
        ArgumentNullException.ThrowIfNull(channel);

        if (string.IsNullOrWhiteSpace(response))
        {
            return;
        }

        foreach (var (text, isFirst) in Chunks(response).Select((t, i) => (t, i == 0)))
        {
            if (text is null)
            {
                await channel.SendMessageAsync(AttachmentLead, messageReference: isFirst ? reference : null);
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(response));
                await channel.SendFileAsync(stream, "response.md", text: null);
                return;
            }

            await channel.SendMessageAsync(text, messageReference: isFirst ? reference : null);
        }
    }

    /// <summary>
    /// The messages a reply becomes, in order. A single <c>null</c> means "send as a file". Pure, so
    /// the split rules are tested without a channel.
    /// </summary>
    public static IReadOnlyList<string?> Chunks(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return Array.Empty<string?>();
        }

        if (response.Length <= MaxMessageLength)
        {
            return new string?[] { response };
        }

        if (response.Length > FileAttachmentThreshold)
        {
            return new string?[] { null };
        }

        var chunks = new List<string?>();
        var chunk = new StringBuilder();

        foreach (var line in response.Split('\n'))
        {
            // A single line over the limit is hard-split at the limit
            if (line.Length > MaxMessageLength)
            {
                if (chunk.Length > 0)
                {
                    chunks.Add(chunk.ToString());
                    chunk.Clear();
                }

                for (var i = 0; i < line.Length; i += MaxMessageLength)
                {
                    chunks.Add(line.Substring(i, Math.Min(MaxMessageLength, line.Length - i)));
                }

                continue;
            }

            var addition = chunk.Length == 0 ? line.Length : line.Length + 1;
            if (chunk.Length + addition > MaxMessageLength)
            {
                chunks.Add(chunk.ToString());
                chunk.Clear();
            }

            if (chunk.Length > 0)
            {
                chunk.Append('\n');
            }

            chunk.Append(line);
        }

        if (chunk.Length > 0)
        {
            chunks.Add(chunk.ToString());
        }

        return chunks;
    }
}
