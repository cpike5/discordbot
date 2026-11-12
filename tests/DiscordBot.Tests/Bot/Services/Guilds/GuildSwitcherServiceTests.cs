using System.Security.Claims;
using DiscordBot.Bot.Authorization;
using DiscordBot.Bot.Services;
using DiscordBot.Core.Authorization;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DiscordBot.Tests.Bot.Services.Guilds;

/// <summary>
/// GuildSwitcherService: the guilds offered are the active ones the GuildAccess requirement allows.
/// </summary>
public class GuildSwitcherServiceTests : IDisposable
{
    private const ulong Alpha = 100000000000000001UL;
    private const ulong Bravo = 100000000000000002UL;
    private const ulong Charlie = 100000000000000003UL;
    private const ulong Retired = 100000000000000004UL;
    private const string UserId = "switcher-user";

    private readonly Mock<IGuildService> _guildService = new();
    private readonly Mock<IAuthorizationService> _authorization = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly GuildSwitcherService _service;

    public GuildSwitcherServiceTests()
    {
        _guildService
            .Setup(s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GuildDto>
            {
                new() { Id = Charlie, Name = "Charlie", IsActive = true },
                new() { Id = Alpha, Name = "alpha", IsActive = true, IconUrl = "https://cdn.discordapp.com/icons/a.png" },
                new() { Id = Bravo, Name = "Bravo", IsActive = true },
                new() { Id = Retired, Name = "Retired", IsActive = false }
            });

        Allow(); // nothing, until a test says otherwise

        _service = new GuildSwitcherService(_guildService.Object, _authorization.Object, _cache, NullLogger<GuildSwitcherService>.Instance);
    }

    public void Dispose() => _cache.Dispose();

    private void Allow(params ulong[] guildIds)
    {
        _authorization
            .Setup(a => a.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<object?>(),
                It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal _, object? resource, IEnumerable<IAuthorizationRequirement> requirements) =>
                requirements.Single() is GuildAccessRequirement { MinimumLevel: DiscordBot.Core.Entities.GuildAccessLevel.Viewer }
                && resource is ulong id && guildIds.Contains(id)
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());
    }

    private static ClaimsPrincipal UserWithRole(string role, string userId = UserId) =>
        new(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Role, role) },
            "TestAuth"));

    [Fact]
    public async Task SuperAdmin_SeesEveryActiveGuild_ByName_WithoutPerGuildChecks()
    {
        var guilds = await _service.GetAccessibleGuildsAsync(UserWithRole(Roles.SuperAdmin));

        guilds.Select(g => g.Id).Should().Equal(Alpha, Bravo, Charlie);
        guilds[0].IconUrl.Should().Be("https://cdn.discordapp.com/icons/a.png");
        _authorization.Verify(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()), Times.Never);
    }

    [Theory]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.Moderator)]
    public async Task OtherRoles_SeeOnlyTheGuildsTheRequirementAllows(string role)
    {
        Allow(Charlie, Alpha);

        var guilds = await _service.GetAccessibleGuildsAsync(UserWithRole(role));

        guilds.Select(g => g.Id).Should().Equal(Alpha, Charlie);
    }

    [Fact]
    public async Task Moderator_WithOneGuild_SeesThatOneGuild()
    {
        Allow(Bravo);

        var guilds = await _service.GetAccessibleGuildsAsync(UserWithRole(Roles.Moderator));

        guilds.Select(g => g.Id).Should().Equal(Bravo);
    }

    [Fact]
    public async Task AnInactiveGuild_IsNeverOffered_EvenWhenAllowed()
    {
        Allow(Retired, Bravo);

        var guilds = await _service.GetAccessibleGuildsAsync(UserWithRole(Roles.Moderator));

        guilds.Select(g => g.Id).Should().Equal(Bravo);
    }

    [Fact]
    public async Task AnonymousOrIdLessUsers_SeeNothing()
    {
        (await _service.GetAccessibleGuildsAsync(new ClaimsPrincipal(new ClaimsIdentity()))).Should().BeEmpty();

        var noId = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, Roles.SuperAdmin) }, "TestAuth"));
        (await _service.GetAccessibleGuildsAsync(noId)).Should().BeEmpty();

        _guildService.Verify(s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TheListIsCachedPerUser()
    {
        await _service.GetAccessibleGuildsAsync(UserWithRole(Roles.SuperAdmin));
        await _service.GetAccessibleGuildsAsync(UserWithRole(Roles.SuperAdmin));
        await _service.GetAccessibleGuildsAsync(UserWithRole(Roles.SuperAdmin, "another-user"));

        _guildService.Verify(s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task AFailure_GivesAnEmptyList_AndIsNotCached()
    {
        _guildService
            .SetupSequence(s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database down"))
            .ReturnsAsync(new List<GuildDto> { new() { Id = Alpha, Name = "alpha", IsActive = true } });

        (await _service.GetAccessibleGuildsAsync(UserWithRole(Roles.SuperAdmin))).Should().BeEmpty();
        (await _service.GetAccessibleGuildsAsync(UserWithRole(Roles.SuperAdmin))).Select(g => g.Id).Should().Equal(Alpha);
    }
}
