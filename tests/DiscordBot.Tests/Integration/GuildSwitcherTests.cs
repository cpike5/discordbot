using DiscordBot.Core.Authorization;
using DiscordBot.Core.Entities;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace DiscordBot.Tests.Integration;

/// <summary>
/// The guild switcher in the guild header and the sidebar's recent guilds list against the real app
/// (offline mode, PostgreSQL). Offline there is no Discord client, so the GuildAccess policy lets
/// only SuperAdmins into a guild; anyone else must be offered nothing.
/// </summary>
[Collection(OfflineAppHostCollection.Name)]
public class GuildSwitcherTests : IClassFixture<GuildSwitcherTests.AppFixture>
{
    private const ulong Current = OfflineAppHost.GuildId;
    private const ulong Second = 700000000000000002UL;
    private const ulong Inactive = 700000000000000004UL;

    private readonly AppFixture _app;

    public GuildSwitcherTests(AppFixture app)
    {
        _app = app;
    }

    [Fact]
    public async Task SuperAdmin_OnTheSoundboard_IsOfferedTheOtherGuildsSoundboard()
    {
        var html = await _app.Host.Client.GetStringAsync($"/Guilds/Soundboard/{Current}");

        html.Should().Contain("id=\"guildSwitcherButton\"");
        html.Should().Contain("aria-haspopup=\"listbox\"");
        html.Should().Contain($"href=\"/Guilds/Soundboard/{Second}\"");
        html.Should().Contain($"href=\"/Guilds/Soundboard/{Current}\"");
        html.Should().NotContain(Inactive.ToString(), "an inactive guild is not offered");
        html.Should().Contain($"data-guild-id=\"{Current}\"", "the page records itself as a recent guild, ID as a string");
    }

    [Fact]
    public async Task SuperAdmin_OnADetailsPage_IsSentToTheSameSectionOfTheOtherGuild()
    {
        var html = await _app.Host.Client.GetStringAsync($"/Guilds/{Current}/Members");

        html.Should().Contain($"href=\"/Guilds/{Second}/Members\"");
    }

    [Fact]
    public async Task SuperAdmin_SidebarCarriesTheAccessibleGuildIdsAsStrings()
    {
        var html = await _app.Host.Client.GetStringAsync("/Guilds");

        html.Should().Contain("data-recent-guilds");
        html.Should().MatchRegex($"data-accessible-guild-ids=\"[0-9,]*{Second}[0-9,]*\"");
    }

    [Fact]
    public async Task Moderator_WithNoGuildAccess_IsOfferedNoGuilds()
    {
        using var client = await _app.Host.CreateSignedInClientAsync("switcher-moderator@example.com", Roles.Moderator);

        var html = await client.GetStringAsync("/Guilds");

        html.Should().Contain("href=\"/Guilds\"", "a Moderator still sees the Servers link");
        html.Should().NotContain("data-recent-guilds");
        html.Should().NotContain("data-accessible-guild-ids");
    }

    public sealed class AppFixture : IAsyncLifetime
    {
        public OfflineAppHost Host { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            Host = await OfflineAppHost.StartAsync(db =>
            {
                db.Guilds.Add(new Guild { Id = Second, Name = "Switcher Second", JoinedAt = DateTime.UtcNow, IsActive = true });
                db.Guilds.Add(new Guild { Id = Inactive, Name = "Switcher Inactive", JoinedAt = DateTime.UtcNow, IsActive = false });
                return Task.CompletedTask;
            });
        }

        public async Task DisposeAsync() => await Host.DisposeAsync();
    }
}
