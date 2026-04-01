using DiscordBot.Core.Models.FxTwitter;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Client interface for fetching tweet data from the fxtwitter API.
/// </summary>
public interface IFxTwitterClient
{
    /// <summary>
    /// Fetches tweet data for the given screen name and tweet ID from the fxtwitter API.
    /// </summary>
    /// <param name="screenName">The Twitter/X screen name (handle) of the tweet author.</param>
    /// <param name="tweetId">The tweet's numeric ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The tweet result, or null if the tweet could not be fetched (not found, private, API error).</returns>
    Task<FxTweetResult?> FetchTweetAsync(string screenName, string tweetId, CancellationToken ct = default);
}
