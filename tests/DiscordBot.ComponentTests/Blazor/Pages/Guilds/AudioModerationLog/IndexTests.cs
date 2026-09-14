using Bunit;
using Bunit.TestDoubles;
using Discord.WebSocket;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Bot.Blazor.Pages.Guilds.AudioModerationLog;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DiscordBot.ComponentTests.Blazor.Pages.Guilds.AudioModerationLog;

/// <summary>
/// Component tests for <see cref="Index"/> (Guilds/AudioModerationLog), the routable replacement
/// for <c>Pages/Guilds/AudioModerationLog/Index.cshtml</c> + <c>IndexModel</c>
/// (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4b): filter/paging round trip and the
/// feature-type badge mapping.
/// </summary>
public class IndexTests : BlazorComponentTestContext
{
    private const ulong GuildId = 123456789012345678UL;

    private readonly Mock<IGuildContextProvider> _contextProvider = new();
    private readonly Mock<IAudioPlaybackLogRepository> _repository = new();

    public IndexTests()
    {
        Services.AddScoped(_ => _contextProvider.Object);
        Services.AddSingleton(_repository.Object);
        // DiscordSocketClient has no usable public constructor for a plain new(); real hosts always
        // resolve one from DI (see Extensions/BotServiceExtensions.cs) - a default-configured
        // instance is enough here since every guild lookup on it just returns null (not connected),
        // which Index.ResolveUserName/ResolveChannelName already fall back on to the raw id.
        Services.AddSingleton(new DiscordSocketClient());

        _repository.Setup(r => r.GetPagedAsync(
                GuildId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<AudioFeatureType?>(), It.IsAny<ulong?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<Core.Entities.AudioPlaybackLog>(), 0));

        AddBunitPersistentComponentState();
        AddAuthorizedAdmin();

        _contextProvider.Setup(p => p.GetAsync(GuildId, It.IsAny<System.Security.Claims.ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildContextResult.Ok(new GuildContext(
                Guild: new GuildDto { Id = GuildId, Name = "Test Guild" },
                GuildId: GuildId,
                GuildIdString: GuildId.ToString(),
                IsAppAdmin: true,
                IsGuildAdmin: false,
                CanEdit: true,
                AudioEnabled: true,
                RatWatchEnabled: true,
                Tabs: DiscordBot.Bot.Configuration.GuildNavigationConfig.GetTabs())));
    }

    private void NavigateTo(string path) =>
        ((BunitNavigationManager)Services.GetRequiredService<NavigationManager>()).NavigateTo(path);

    private IRenderedComponent<Bot.Blazor.Pages.Guilds.AudioModerationLog.Index> RenderPage(string path)
    {
        NavigateTo(path);
        SetInteractiveRendererInfo();
        return Render<Bot.Blazor.Pages.Guilds.AudioModerationLog.Index>(p => p.Add(x => x.GuildId, (long)GuildId));
    }

    [Fact]
    public void NoEntries_RendersEmptyState()
    {
        var cut = RenderPage($"/Guilds/AudioModerationLog/{GuildId}");

        cut.Markup.Should().Contain("No audio playback events found");
        cut.Markup.Should().Contain("No audio has been played in this guild yet.");
    }

    [Fact]
    public void WithEntries_RendersFeatureBadgesAndUnknownUser()
    {
        _repository.Setup(r => r.GetPagedAsync(
                GuildId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<AudioFeatureType?>(), It.IsAny<ulong?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[]
            {
                new Core.Entities.AudioPlaybackLog { UserId = 111, FeatureType = AudioFeatureType.Soundboard, ContentName = "airhorn", PlayedAt = DateTime.UtcNow },
                new Core.Entities.AudioPlaybackLog { UserId = 0, FeatureType = AudioFeatureType.Tts, ContentName = "hello there", PlayedAt = DateTime.UtcNow },
                new Core.Entities.AudioPlaybackLog { UserId = 222, FeatureType = AudioFeatureType.Vox, ContentName = "vox clip", PlayedAt = DateTime.UtcNow }
            }, 3));

        var cut = RenderPage($"/Guilds/AudioModerationLog/{GuildId}");

        cut.Markup.Should().Contain("Soundboard");
        cut.Markup.Should().Contain("TTS");
        cut.Markup.Should().Contain("VOX");
        cut.Markup.Should().Contain("Unknown");
        cut.Markup.Should().Contain("airhorn");
    }

    [Fact]
    public void InitialQuery_SeedsFiltersAndPageFromUrl()
    {
        var from = DateTime.UtcNow.Date.AddDays(-5);
        RenderPage($"/Guilds/AudioModerationLog/{GuildId}?FeatureFilter={(int)AudioFeatureType.Tts}&UserFilter=42&DateFrom={from:yyyy-MM-dd}&pageNumber=2");

        _repository.Verify(r => r.GetPagedAsync(
            GuildId, 2, 25, AudioFeatureType.Tts, 42UL, from, It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ApplyFilters_ResetsToPageOne_AndReloads()
    {
        var cut = RenderPage($"/Guilds/AudioModerationLog/{GuildId}?pageNumber=3");
        _repository.Invocations.Clear();

        cut.Find("#UserFilter").Input("999");
        cut.Find("form").Submit();

        _repository.Verify(r => r.GetPagedAsync(
            GuildId, 1, 25, null, 999UL, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ClearFilters_ResetsStateAndReloads()
    {
        var cut = RenderPage($"/Guilds/AudioModerationLog/{GuildId}?FeatureFilter={(int)AudioFeatureType.Soundboard}&pageNumber=2");
        _repository.Invocations.Clear();

        cut.FindAll("a").Single(a => a.TextContent.Contains("Clear Filters")).Click();

        _repository.Verify(r => r.GetPagedAsync(
            GuildId, 1, 25, null, null, null, null, It.IsAny<CancellationToken>()), Times.Once);
        cut.Markup.Should().NotContain("Try adjusting your filters");
    }

    [Fact]
    public void UnknownGuild_RendersNotFoundGate()
    {
        _contextProvider.Setup(p => p.GetAsync(GuildId, It.IsAny<System.Security.Claims.ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildContextResult.NotFound());

        var cut = RenderPage($"/Guilds/AudioModerationLog/{GuildId}");

        cut.Markup.Should().Contain("Server Not Found");
        _repository.Verify(r => r.GetPagedAsync(
            It.IsAny<ulong>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<AudioFeatureType?>(), It.IsAny<ulong?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
