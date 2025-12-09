using System.Collections.Concurrent;
using Discord;
using Discord.Interactions;
using DiscordBot.Core.Enums;

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

        // Get or add the invocation list for this key
        var invocations = _invocations.GetOrAdd(key, _ => new List<DateTime>());

        lock (invocations)
        {
            // Remove old invocations outside the time window
            invocations.RemoveAll(time => (now - time).TotalSeconds > _periodSeconds);

            // Check if rate limit is exceeded
            if (invocations.Count >= _times)
            {
                var oldestInvocation = invocations.Min();
                var timeUntilReset = _periodSeconds - (now - oldestInvocation).TotalSeconds;

                return Task.FromResult(
                    PreconditionResult.FromError(
                        $"Rate limit exceeded. Please wait {timeUntilReset:F1} seconds before using this command again."
                    )
                );
            }

            // Add current invocation
            invocations.Add(now);
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
