using Discord;
using Discord.Interactions;
using DiscordBot.Bot.Collections;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Core.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using System.Reflection;
using Xunit;

namespace DiscordBot.Tests.Preconditions;

/// <summary>
/// Unit tests for <see cref="RateLimitTransfersAttribute"/>, which reads its limit from
/// <c>Currency:MaxTransferPerMinute</c> because an attribute argument cannot.
/// </summary>
public class RateLimitTransfersAttributeTests
{
    private readonly Mock<IInteractionContext> _mockContext = new();
    private readonly Mock<ICommandInfo> _mockCommandInfo = new();
    private readonly Mock<IServiceProvider> _mockServiceProvider = new();

    public RateLimitTransfersAttributeTests()
    {
        var field = typeof(RateLimitAttribute).GetField("_invocations", BindingFlags.NonPublic | BindingFlags.Static);
        (field?.GetValue(null) as LruConcurrentDictionary<string, List<DateTime>>)?.Clear();

        _mockContext.Setup(c => c.User).Returns(Mock.Of<IUser>(u => u.Id == 4242UL));
        _mockCommandInfo.Setup(c => c.Name).Returns("pay");
    }

    private void WithConfiguredLimit(int? maxPerMinute)
    {
        _mockServiceProvider
            .Setup(sp => sp.GetService(typeof(IOptions<CurrencyOptions>)))
            .Returns(maxPerMinute.HasValue
                ? Options.Create(new CurrencyOptions { MaxTransferPerMinute = maxPerMinute.Value })
                : null!);
    }

    private async Task<int> CountAllowedAsync(int attempts)
    {
        var attribute = new RateLimitTransfersAttribute();
        var allowed = 0;

        for (var i = 0; i < attempts; i++)
        {
            var result = await attribute.CheckRequirementsAsync(
                _mockContext.Object, _mockCommandInfo.Object, _mockServiceProvider.Object);

            if (result.IsSuccess)
            {
                allowed++;
            }
        }

        return allowed;
    }

    [Fact]
    public async Task CheckRequirementsAsync_UsesTheConfiguredLimit()
    {
        WithConfiguredLimit(2);

        (await CountAllowedAsync(5)).Should().Be(2, "Currency:MaxTransferPerMinute is the limit");
    }

    [Fact]
    public async Task CheckRequirementsAsync_WithoutOptions_FallsBackToTheDefault()
    {
        WithConfiguredLimit(null);

        (await CountAllowedAsync(10)).Should().Be(new CurrencyOptions().MaxTransferPerMinute);
    }
}
