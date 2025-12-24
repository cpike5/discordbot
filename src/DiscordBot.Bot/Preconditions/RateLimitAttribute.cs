using System.Collections.Concurrent;
using Discord;
using Discord.Interactions;
using DiscordBot.Bot.Metrics;
using DiscordBot.Core.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Bot.Preconditions;

/// <summary>
/// Precondition that applies rate limiting to commands.
/// </summary>
public class RateLimitAttribute : PreconditionAttribute
{
    private static readonly ConcurrentDictionary<string, List<DateTime>> _invocations = new();

    private readonly int _times;
    private readonly double _periodSeconds;
    private readonly RateLimitTarget _target;

    /// <summary>
    /// Initializes a new instance of the <see cref="RateLimitAttribute"/> class.
    /// </summary>
    /// <param name="times">The number of times the command can be invoked within the period.</param>
    /// <param name="periodSeconds">The period in seconds during which the limit applies.</param>
    /// <param name="target">The target scope for the rate limit (User, Guild, or Global).</param>
    public RateLimitAttribute(int times, double periodSeconds, RateLimitTarget target = RateLimitTarget.User)
    {
        _times = times;
        _periodSeconds = periodSeconds;
        _target = target;
    }

    /// <summary>
    /// Checks if the rate limit has been exceeded for this command invocation.
    /// </summary>
    public override Task<PreconditionResult> CheckRequirementsAsync(
        IInteractionContext context,
        ICommandInfo commandInfo,
        IServiceProvider services)
    {
        var now = DateTime.UtcNow;
        var key = GetRateLimitKey(context, commandInfo);
        var commandName = commandInfo.Name;
        var userId = context.User.Id;
        var guildId = context.Guild?.Id;

        // Get logger and metrics from service provider
        var loggerFactory = services.GetService<ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger<RateLimitAttribute>();
        var botMetrics = services.GetService<BotMetrics>();

        // Get or add the invocation list for this key
        var invocations = _invocations.GetOrAdd(key, _ => new List<DateTime>());

        lock (invocations)
        {
            // Remove old invocations outside the time window
            invocations.RemoveAll(time => (now - time).TotalSeconds > _periodSeconds);

            var currentCount = invocations.Count;

            // Check if rate limit is exceeded
            if (currentCount >= _times)
            {
                var oldestInvocation = invocations.Min();
                var timeUntilReset = _periodSeconds - (now - oldestInvocation).TotalSeconds;

                // Record rate limit violation metric
                botMetrics?.RecordRateLimitViolation(
                    commandName,
                    _target.ToString().ToLowerInvariant());

                // Log rate limit violation at Warning level for abuse detection
                logger?.LogWarning(
                    "Rate limit exceeded for user {UserId} on command {CommandName} in guild {GuildId}. " +
                    "Limit: {MaxTimes} per {PeriodSeconds}s. Reset in {ResetSeconds:F1}s. " +
                    "Current invocations: {InvocationCount}. Target: {RateLimitTarget}",
                    userId,
                    commandName,
                    guildId,
                    _times,
                    _periodSeconds,
                    timeUntilReset,
                    currentCount,
                    _target);

                return Task.FromResult(
                    PreconditionResult.FromError(
                        $"Rate limit exceeded. Please wait {timeUntilReset:F1} seconds before using this command again."
                    )
                );
            }

            // Add current invocation
            invocations.Add(now);

            // Log successful rate limit check at Trace level
            logger?.LogTrace(
                "Rate limit check passed for user {UserId} on command {CommandName}. " +
                "Invocations: {InvocationCount}/{MaxTimes} in {PeriodSeconds}s window",
                userId,
                commandName,
                currentCount + 1,
                _times,
                _periodSeconds);
        }

        return Task.FromResult(PreconditionResult.FromSuccess());
    }

    /// <summary>
    /// Generates a unique key for rate limiting based on the target scope.
    /// </summary>
    private string GetRateLimitKey(IInteractionContext context, ICommandInfo commandInfo)
    {
        var commandName = commandInfo.Name;

        return _target switch
        {
            RateLimitTarget.User => $"user:{context.User.Id}:{commandName}",
            RateLimitTarget.Guild => $"guild:{context.Guild?.Id ?? 0}:{commandName}",
            RateLimitTarget.Global => $"global:{commandName}",
            _ => throw new InvalidOperationException($"Unknown rate limit target: {_target}")
        };
    }
}
