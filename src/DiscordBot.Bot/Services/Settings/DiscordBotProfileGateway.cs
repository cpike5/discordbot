using Discord;
using Discord.Net;
using Discord.WebSocket;
using DiscordBot.Bot.Interfaces;

namespace DiscordBot.Bot.Services.Settings;

/// <summary>
/// <see cref="IBotProfileGateway"/> over the bot's <see cref="DiscordSocketClient"/>. Reads go to
/// Discord's REST API rather than the gateway cache, so a change shows as soon as it is saved.
/// </summary>
public class DiscordBotProfileGateway : IBotProfileGateway
{
    private const ushort AvatarSize = 256;
    private const ushort BannerSize = 1024;
    private const int RequestTimeoutMs = 20000;

    private readonly DiscordSocketClient _client;
    private readonly ILogger<DiscordBotProfileGateway> _logger;

    public DiscordBotProfileGateway(DiscordSocketClient client, ILogger<DiscordBotProfileGateway> logger)
    {
        _client = client;
        _logger = logger;
    }

    /// <inheritdoc/>
    public bool IsAvailable => _client.LoginState == LoginState.LoggedIn && _client.Rest.CurrentUser != null;

    /// <inheritdoc/>
    public Task<BotProfile> GetAsync(CancellationToken cancellationToken = default) =>
        CallAsync("read the bot profile", async options =>
        {
            var self = _client.Rest.CurrentUser;
            await self.UpdateAsync(options);
            var application = await _client.Rest.GetCurrentBotInfoAsync(options);

            return new BotProfile(
                self.Username,
                self.GetDisplayAvatarUrl(size: AvatarSize),
                self.AvatarId != null,
                self.GetBannerUrl(size: BannerSize),
                application.Description ?? string.Empty);
        }, cancellationToken);

    /// <inheritdoc/>
    public Task ModifyUserAsync(BotUserChanges changes, CancellationToken cancellationToken = default) =>
        CallAsync("update the bot profile", async options =>
        {
            await _client.Rest.CurrentUser.ModifyAsync(properties =>
            {
                if (changes.Username != null)
                {
                    properties.Username = changes.Username;
                }

                if (changes.Avatar != null)
                {
                    properties.Avatar = new Image(changes.Avatar);
                }
                else if (changes.RemoveAvatar)
                {
                    properties.Avatar = new Optional<Image?>(null);
                }

                if (changes.Banner != null)
                {
                    properties.Banner = new Image(changes.Banner);
                }
                else if (changes.RemoveBanner)
                {
                    properties.Banner = new Optional<Image?>(null);
                }
            }, options);
            return true;
        }, cancellationToken);

    /// <inheritdoc/>
    public Task SetAboutMeAsync(string aboutMe, CancellationToken cancellationToken = default) =>
        CallAsync("update the bot's About me", async options =>
        {
            await _client.Rest.ModifyCurrentBotApplicationAsync(properties => properties.Description = aboutMe, options);
            return true;
        }, cancellationToken);

    /// <summary>
    /// Runs one Discord call without Discord.NET's built-in rate-limit waiting (an admin is waiting
    /// on the page), and turns Discord's failures into a message for that admin.
    /// </summary>
    private async Task<T> CallAsync<T>(string what, Func<RequestOptions, Task<T>> call, CancellationToken cancellationToken)
    {
        if (!IsAvailable)
        {
            throw new BotProfileGatewayException("The bot is not logged in to Discord.");
        }

        var options = new RequestOptions
        {
            CancelToken = cancellationToken,
            RetryMode = RetryMode.AlwaysFail,
            Timeout = RequestTimeoutMs
        };

        try
        {
            return await call(options);
        }
        catch (RateLimitedException ex)
        {
            _logger.LogWarning(ex, "Discord rate limit hit trying to {Operation}", what);
            throw new BotProfileGatewayException("Discord is rate limiting profile changes. Wait a few minutes and try again.", ex);
        }
        catch (HttpException ex)
        {
            _logger.LogWarning(ex, "Discord refused to {Operation}: {HttpCode} {DiscordCode}", what, ex.HttpCode, ex.DiscordCode);
            throw new BotProfileGatewayException(Describe(ex), ex);
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning(ex, "Timed out trying to {Operation}", what);
            throw new BotProfileGatewayException("Discord did not answer in time. Try again.", ex);
        }
    }

    /// <summary>
    /// Discord's own wording where it gave any: the per-field errors (this is where "you are
    /// changing your username too fast" arrives), otherwise the reason line.
    /// </summary>
    internal static string Describe(HttpException exception)
    {
        var fieldErrors = exception.Errors
            .SelectMany(error => error.Errors)
            .Select(error => error.Message)
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Distinct()
            .ToList();

        if (fieldErrors.Count > 0)
        {
            return string.Join(" ", fieldErrors);
        }

        return string.IsNullOrWhiteSpace(exception.Reason)
            ? $"Discord refused the change (HTTP {(int)exception.HttpCode})."
            : exception.Reason;
    }
}
