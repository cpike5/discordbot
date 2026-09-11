using DiscordBot.Infrastructure.Services.LLM;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;

namespace DiscordBot.Tests.Services.LLM;

/// <summary>
/// Unit tests for <see cref="DmSkillActivationStore"/>, the per-user memory of which skills the DM
/// assistant has loaded — what makes a skill cost one round on the turn that loads it and nothing
/// afterwards.
/// </summary>
public class DmSkillActivationStoreTests
{
    private const ulong UserId = 4242UL;

    private static DmSkillActivationStore BuildStore() =>
        new(new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public void Get_ForUnknownUser_ReturnsEmpty()
    {
        BuildStore().Get(UserId).Should().BeEmpty();
    }

    [Fact]
    public void Set_ThenGet_RoundTripsInOrder()
    {
        var store = BuildStore();

        store.Set(UserId, new[] { "moderation", "weather" });

        // Activation order is preserved: the roster renders loaded skills in it, and re-ordering the
        // prompt costs the cached prefix behind it for nothing.
        store.Get(UserId).Should().Equal("moderation", "weather");
    }

    [Fact]
    public void Set_DeDuplicatesCaseInsensitivelyAndDropsBlanks()
    {
        var store = BuildStore();

        store.Set(UserId, new[] { "moderation", "  ", "MODERATION", "", "weather" });

        store.Get(UserId).Should().Equal("moderation", "weather");
    }

    [Fact]
    public void Set_WithEmptySequence_ClearsTheUser()
    {
        var store = BuildStore();
        store.Set(UserId, new[] { "moderation" });

        store.Set(UserId, Array.Empty<string>());

        store.Get(UserId).Should().BeEmpty();
    }

    [Fact]
    public void Set_WithOnlyBlanks_ClearsTheUser()
    {
        var store = BuildStore();
        store.Set(UserId, new[] { "moderation" });

        store.Set(UserId, new[] { " ", "" });

        store.Get(UserId).Should().BeEmpty();
    }

    [Fact]
    public void Clear_RemovesTheUsersActivations()
    {
        var store = BuildStore();
        store.Set(UserId, new[] { "moderation" });

        store.Clear(UserId);

        store.Get(UserId).Should().BeEmpty();
    }

    [Fact]
    public void Set_ForOneUser_DoesNotAffectAnother()
    {
        var store = BuildStore();

        store.Set(UserId, new[] { "moderation" });
        store.Set(UserId + 1, new[] { "weather" });

        store.Get(UserId).Should().Equal("moderation");
        store.Get(UserId + 1).Should().Equal("weather");

        store.Clear(UserId);

        store.Get(UserId).Should().BeEmpty();
        store.Get(UserId + 1).Should().Equal("weather");
    }
}
