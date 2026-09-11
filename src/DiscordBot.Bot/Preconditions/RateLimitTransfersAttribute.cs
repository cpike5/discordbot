using DiscordBot.Core.Configuration;
using DiscordBot.Core.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Preconditions;

/// <summary>
/// Per-user rate limit on <c>/wallet pay</c>, read from <c>Currency:MaxTransferPerMinute</c>.
/// An attribute argument has to be a constant, so the limit is resolved at check time instead.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RateLimitTransfersAttribute : RateLimitAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RateLimitTransfersAttribute"/> class with the
    /// configured default as the fallback limit.
    /// </summary>
    public RateLimitTransfersAttribute()
        : base(new CurrencyOptions().MaxTransferPerMinute, 60, RateLimitTarget.User)
    {
    }

    /// <inheritdoc />
    protected override int GetLimit(IServiceProvider services)
    {
        var options = services.GetService<IOptions<CurrencyOptions>>()?.Value;
        return options?.MaxTransferPerMinute ?? base.GetLimit(services);
    }
}
