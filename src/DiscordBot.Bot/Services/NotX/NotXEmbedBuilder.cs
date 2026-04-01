using Discord;
using DiscordBot.Core.Models.FxTwitter;

namespace DiscordBot.Bot.Services.NotX;

/// <summary>
/// Builds Discord <see cref="Embed"/> arrays from fxtwitter tweet data.
/// Supports multi-image layout (up to 4 embeds), video thumbnails, and
/// sensitive-content colour coding.
/// </summary>
public static class NotXEmbedBuilder
{
    private static readonly Color TwitterBlue = new(0x1D, 0x9B, 0xF0);
    private static readonly Color SensitiveRed = new(0xFF, 0x6B, 0x6B);

    private const int MaxDescriptionLength = 4096;
    private const int MaxPhotoEmbeds = 4;

    /// <summary>
    /// Builds an array of Discord embeds representing a tweet.
    /// </summary>
    /// <param name="tweet">The tweet data returned by fxtwitter.</param>
    /// <param name="isSensitive">
    /// When <see langword="true"/>, uses red colour and prepends a sensitive-content label in the footer.
    /// </param>
    /// <param name="sourceMessageId">
    /// When provided (cross-channel scenario), included in the footer as a jump-link.
    /// </param>
    /// <param name="sourceChannelId">
    /// The originating channel ID; required when <paramref name="sourceMessageId"/> is set so
    /// a valid jump URL can be constructed. Must be a guild channel — guild ID is derived from
    /// Discord's channel URL convention (the caller supplies it via <paramref name="guildId"/>).
    /// </param>
    /// <param name="guildId">
    /// The guild ID, used to build the cross-channel jump URL when both
    /// <paramref name="sourceMessageId"/> and <paramref name="sourceChannelId"/> are set.
    /// </param>
    /// <returns>A non-empty array of embeds (first contains tweet text; subsequent are image-only).</returns>
    public static Embed[] Build(
        FxTweetResult tweet,
        bool isSensitive,
        ulong? sourceMessageId = null,
        ulong? sourceChannelId = null,
        ulong? guildId = null)
    {
        var color = isSensitive ? SensitiveRed : TwitterBlue;
        var photos = tweet.Media?.Photos ?? new List<FxTweetPhoto>();
        var videos = tweet.Media?.Videos ?? new List<FxTweetVideo>();

        var description = BuildDescription(tweet, videos);
        var footerText = BuildFooter(tweet, isSensitive, sourceMessageId, sourceChannelId, guildId);

        // Parse CreatedAt — fxtwitter returns ISO 8601 strings
        DateTimeOffset? timestamp = null;
        if (DateTimeOffset.TryParse(tweet.CreatedAt, out var parsedTs))
            timestamp = parsedTs;

        // ── Primary embed ────────────────────────────────────────────────────
        var authorProfileUrl = $"https://twitter.com/{tweet.Author.ScreenName}";

        var primaryBuilder = new EmbedBuilder()
            .WithColor(color)
            .WithAuthor(author =>
            {
                author.Name = $"{tweet.Author.Name} (@{tweet.Author.ScreenName})";
                author.Url = authorProfileUrl;
                if (!string.IsNullOrEmpty(tweet.Author.AvatarUrl))
                    author.IconUrl = tweet.Author.AvatarUrl;
            })
            .WithDescription(description)
            .WithFooter(footerText)
            .WithUrl(tweet.Url);

        if (timestamp.HasValue)
            primaryBuilder.WithTimestamp(timestamp.Value);

        if (photos.Count > 0)
        {
            primaryBuilder.WithImageUrl(photos[0].Url);
        }
        else if (videos.Count > 0 && !string.IsNullOrEmpty(videos[0].ThumbnailUrl))
        {
            primaryBuilder.WithImageUrl(videos[0].ThumbnailUrl);
        }

        var embeds = new List<Embed> { primaryBuilder.Build() };

        // ── Additional image embeds (indexes 1–3) ────────────────────────────
        var additionalPhotos = photos.Skip(1).Take(MaxPhotoEmbeds - 1);
        foreach (var photo in additionalPhotos)
        {
            var extra = new EmbedBuilder()
                .WithColor(color)
                .WithImageUrl(photo.Url)
                .WithUrl(tweet.Url)
                .Build();

            embeds.Add(extra);
        }

        return embeds.ToArray();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static string BuildDescription(FxTweetResult tweet, IReadOnlyList<FxTweetVideo> videos)
    {
        var text = tweet.Text ?? string.Empty;

        // Append video link when there are no photos but there is a video
        var photos = tweet.Media?.Photos ?? new List<FxTweetPhoto>();
        if (photos.Count == 0 && videos.Count > 0)
        {
            var videoUrl = videos[0].Url;
            var videoNote = $"\n\n[▶ Video — view on X]({videoUrl})";
            text += videoNote;
        }

        // Truncate to Discord's embed description limit
        if (text.Length > MaxDescriptionLength)
            text = text[..(MaxDescriptionLength - 1)] + "…";

        return text;
    }

    private static string BuildFooter(
        FxTweetResult tweet,
        bool isSensitive,
        ulong? sourceMessageId,
        ulong? sourceChannelId,
        ulong? guildId)
    {
        var parts = new List<string>();

        if (isSensitive)
            parts.Add("🔞 Sensitive content");

        parts.Add($"🐦 @{tweet.Author.ScreenName}");
        parts.Add($"❤️ {FormatCount(tweet.Likes)}");
        parts.Add($"🔁 {FormatCount(tweet.Retweets)}");
        parts.Add($"💬 {FormatCount(tweet.Replies)}");

        // Cross-channel jump link
        if (sourceMessageId.HasValue && sourceChannelId.HasValue && guildId.HasValue)
        {
            var jumpUrl = $"https://discord.com/channels/{guildId}/{sourceChannelId}/{sourceMessageId}";
            parts.Add($"[Jump to message]({jumpUrl})");
        }

        return string.Join(" · ", parts);
    }

    private static string FormatCount(int count)
    {
        return count switch
        {
            >= 1_000_000 => $"{count / 1_000_000.0:F1}M",
            >= 1_000 => $"{count / 1_000.0:F1}k",
            _ => count.ToString()
        };
    }
}
