using Discord.WebSocket;
using DiscordBot.Bot.Helpers;
using DiscordBot.Bot.Pages.Guilds;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DiscordBot.Tests.Bot.Pages.Guilds;

/// <summary>
/// The public leaderboard names people the same way the Rat Watch service does: the guild cache,
/// then the user resolver (which falls back to the stored username), and "Unknown user" last.
/// </summary>
public class PublicLeaderboardNameTests
{
    private const ulong GuildId = 987654321098765432;

    private readonly Mock<IDiscordUserResolver> _resolver = new();

    private PublicLeaderboardModel CreateModel(bool withResolver = true)
    {
        var userManager = new Mock<UserManager<ApplicationUser>>(
            Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);

        return new PublicLeaderboardModel(
            Mock.Of<IRatRecordRepository>(),
            Mock.Of<IRatWatchRepository>(),
            Mock.Of<IGuildRatWatchSettingsRepository>(),
            Mock.Of<IGuildService>(),
            // An unconnected client has no guilds, so every name has to come from the resolver
            new Mock<DiscordSocketClient>(new DiscordSocketConfig()).Object,
            userManager.Object,
            new ConfigurationBuilder().Build(),
            NullLogger<PublicLeaderboardModel>.Instance,
            withResolver ? _resolver.Object : null);
    }

    [Fact]
    public async Task ResolveUsernames_WhenTheGuildCacheHasNobody_UsesTheResolverInOneBatch()
    {
        _resolver.Setup(r => r.ResolveUsersAsync(It.IsAny<IEnumerable<ulong>>()))
            .ReturnsAsync(new Dictionary<ulong, (string Username, string? AvatarUrl)>
            {
                [1] = ("stored_name", null),
                [2] = ("Unknown#2", null)
            });

        var names = await CreateModel().ResolveUsernamesAsync(new ulong[] { 1, 2, 1 }, GuildId);

        names[1].Should().Be("stored_name");
        names[2].Should().Be(UserDisplay.UnknownName);
        _resolver.Verify(r => r.ResolveUsersAsync(It.Is<IEnumerable<ulong>>(ids => ids.Count() == 2)), Times.Once);
    }

    [Fact]
    public async Task ResolveUsernames_WhenTheResolverThrows_ShowsUnknownUserForEveryone()
    {
        _resolver.Setup(r => r.ResolveUsersAsync(It.IsAny<IEnumerable<ulong>>())).ThrowsAsync(new HttpRequestException());

        var names = await CreateModel().ResolveUsernamesAsync(new ulong[] { 1, 2 }, GuildId);

        names.Should().HaveCount(2).And.OnlyContain(n => n.Value == UserDisplay.UnknownName);
    }

    [Fact]
    public async Task ResolveUsernames_WithNoResolver_ShowsUnknownUser()
    {
        var names = await CreateModel(withResolver: false).ResolveUsernamesAsync(new ulong[] { 7 }, GuildId);

        names[7].Should().Be(UserDisplay.UnknownName);
    }
}
