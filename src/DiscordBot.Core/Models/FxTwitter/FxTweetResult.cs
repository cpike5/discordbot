using System.Text.Json.Serialization;

namespace DiscordBot.Core.Models.FxTwitter;

/// <summary>
/// Top-level wrapper returned by the fxtwitter API.
/// </summary>
public record FxTweetResponse(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("tweet")] FxTweetResult? Tweet
);

/// <summary>
/// Core tweet data returned by the fxtwitter API.
/// </summary>
public record FxTweetResult(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("author")] FxTweetAuthor Author,
    [property: JsonPropertyName("media")] FxTweetMedia? Media,
    [property: JsonPropertyName("created_at")] string CreatedAt,
    [property: JsonPropertyName("likes")] int Likes,
    [property: JsonPropertyName("retweets")] int Retweets,
    [property: JsonPropertyName("replies")] int Replies,
    [property: JsonPropertyName("possibly_sensitive")] bool PossiblySensitive
);

/// <summary>
/// Tweet author information returned by the fxtwitter API.
/// </summary>
public record FxTweetAuthor(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("screen_name")] string ScreenName,
    [property: JsonPropertyName("avatar_url")] string? AvatarUrl,
    [property: JsonPropertyName("url")] string Url
);

/// <summary>
/// Media container for photos and videos attached to a tweet.
/// </summary>
public record FxTweetMedia(
    [property: JsonPropertyName("photos")] List<FxTweetPhoto>? Photos,
    [property: JsonPropertyName("videos")] List<FxTweetVideo>? Videos
);

/// <summary>
/// A photo attached to a tweet.
/// </summary>
public record FxTweetPhoto(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("width")] int Width,
    [property: JsonPropertyName("height")] int Height
);

/// <summary>
/// A video attached to a tweet.
/// </summary>
public record FxTweetVideo(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("thumbnail_url")] string ThumbnailUrl,
    [property: JsonPropertyName("width")] int Width,
    [property: JsonPropertyName("height")] int Height,
    [property: JsonPropertyName("duration_ms")] double DurationMs
);
