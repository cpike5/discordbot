using System.Net;
using System.Text.Json;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Models.FxTwitter;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services.NotX;

/// <summary>
/// HTTP client for the fxtwitter JSON API. Fetches tweet metadata including text,
/// media, and sensitivity flags. All errors are treated as non-actionable and
/// logged at Debug level to avoid log spam.
/// </summary>
public class FxTwitterClient : IFxTwitterClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly NotXOptions _options;
    private readonly ILogger<FxTwitterClient> _logger;

    public FxTwitterClient(
        IHttpClientFactory httpClientFactory,
        IOptions<NotXOptions> options,
        ILogger<FxTwitterClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<FxTweetResult?> FetchTweetAsync(
        string screenName,
        string tweetId,
        CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("FxTwitter");
        var requestUri = $"{screenName}/status/{tweetId}";

        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync(
                requestUri,
                HttpCompletionOption.ResponseHeadersRead,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex,
                "fxtwitter request failed for tweet {TweetId} (screen_name={ScreenName})",
                tweetId, screenName);
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var statusCode = (int)response.StatusCode;
            if (statusCode is 401 or 404)
            {
                _logger.LogDebug(
                    "Tweet {TweetId} not available — fxtwitter returned {StatusCode}",
                    tweetId, statusCode);
            }
            else
            {
                _logger.LogDebug(
                    "fxtwitter returned non-success status {StatusCode} for tweet {TweetId}",
                    statusCode, tweetId);
            }

            return null;
        }

        // Guard against oversized responses before reading the body
        var contentLength = response.Content.Headers.ContentLength;
        if (contentLength.HasValue && contentLength.Value > _options.MaxResponseBytes)
        {
            _logger.LogDebug(
                "fxtwitter response for tweet {TweetId} exceeds max size ({ResponseBytes} bytes)",
                tweetId, contentLength.Value);
            return null;
        }

        string body;
        try
        {
            // Read with a size cap even when Content-Length is absent
            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var limitedStream = new LimitedStream(stream, _options.MaxResponseBytes);
            using var reader = new StreamReader(limitedStream);
            body = await reader.ReadToEndAsync(ct);
        }
        catch (LimitExceededException)
        {
            _logger.LogDebug(
                "fxtwitter response body for tweet {TweetId} exceeded {MaxBytes} bytes; discarding",
                tweetId, _options.MaxResponseBytes);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex,
                "Failed to read fxtwitter response body for tweet {TweetId}",
                tweetId);
            return null;
        }

        FxTweetResponse? wrapper;
        try
        {
            wrapper = JsonSerializer.Deserialize<FxTweetResponse>(body, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex,
                "JSON deserialization failed for fxtwitter response for tweet {TweetId}",
                tweetId);
            return null;
        }

        if (wrapper is null || wrapper.Code != 200 || wrapper.Tweet is null)
        {
            _logger.LogDebug(
                "fxtwitter response for tweet {TweetId} has code {Code} or missing tweet object",
                tweetId, wrapper?.Code);
            return null;
        }

        return wrapper.Tweet;
    }

    // ── Private stream helpers ────────────────────────────────────────────────

    private sealed class LimitExceededException : IOException
    {
        public LimitExceededException() : base("Response body size limit exceeded.") { }
    }

    /// <summary>
    /// Wraps a stream and throws <see cref="LimitExceededException"/> once the configured
    /// byte limit has been reached.
    /// </summary>
    private sealed class LimitedStream : Stream
    {
        private readonly Stream _inner;
        private readonly long _maxBytes;
        private long _totalRead;

        public LimitedStream(Stream inner, long maxBytes)
        {
            _inner = inner;
            _maxBytes = maxBytes;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var allowed = (int)Math.Min(count, _maxBytes - _totalRead);
            if (allowed <= 0)
                throw new LimitExceededException();

            var read = _inner.Read(buffer, offset, allowed);
            _totalRead += read;
            return read;
        }

        public override async Task<int> ReadAsync(
            byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var allowed = (int)Math.Min(count, _maxBytes - _totalRead);
            if (allowed <= 0)
                throw new LimitExceededException();

            var read = await _inner.ReadAsync(buffer, offset, allowed, cancellationToken);
            _totalRead += read;
            return read;
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) _inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
