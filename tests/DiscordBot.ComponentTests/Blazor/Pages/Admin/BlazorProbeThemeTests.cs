using System.Security.Claims;
using Bunit;
using DiscordBot.Bot.Blazor.Pages.Admin;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DiscordBot.ComponentTests.Blazor.Pages.Admin;

/// <summary>
/// Covers section (i) "Theme" of <c>Blazor/Pages/Admin/BlazorProbe.razor</c> (plan §5 Phase 3,
/// the deferred-from-Phase-1 <c>IThemeInterop</c> deliverable's demo caller - see
/// <c>docs/articles/blazor-interop.md</c>'s Theme section). <see cref="BlazorProbeTests"/>
/// registers an empty theme catalog for every other section's tests; this class overrides that
/// with real entries to exercise the select/apply/persist path itself.
/// </summary>
public class BlazorProbeThemeTests : BlazorComponentTestContext
{
    private const string UserId = "user-123";

    private readonly Mock<IThemeService> _themeService = new();

    public BlazorProbeThemeTests()
    {
        _themeService
            .Setup(s => s.GetActiveThemesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ThemeDto { Id = 1, ThemeKey = "discord-dark", DisplayName = "Discord Dark" },
                new ThemeDto { Id = 2, ThemeKey = "purple-dusk", DisplayName = "Purple Dusk" }
            });
        Services.AddSingleton(_themeService.Object);

        AddAuthorization().SetAuthorized("admin").SetRoles("Admin")
            .SetClaims(new Claim(ClaimTypes.NameIdentifier, UserId));
    }

    [Fact]
    public void RendersOneOptionPerActiveTheme()
    {
        var cut = Render<BlazorProbe>();

        var options = cut.FindAll("[data-testid='theme-select'] option");
        options.Should().HaveCount(2);
        options[0].TextContent.Should().Be("Discord Dark");
        options[1].TextContent.Should().Be("Purple Dusk");
    }

    [Fact]
    public void SelectingATheme_AppliesItAndPersistsViaThemeService()
    {
        _themeService
            .Setup(s => s.SetUserThemeAsync(UserId, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var cut = Render<BlazorProbe>();

        cut.Find("[data-testid='theme-select']").Change("purple-dusk");

        _themeService.Verify(s => s.SetUserThemeAsync(UserId, 2, It.IsAny<CancellationToken>()), Times.Once);
        cut.Find("[data-testid='theme-result']").TextContent.Should().Contain("Purple Dusk");
    }

    [Fact]
    public void SelectingATheme_WhenPersistFails_ReportsTheFailure_ButStillAppliedClientSide()
    {
        _themeService
            .Setup(s => s.SetUserThemeAsync(UserId, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var cut = Render<BlazorProbe>();

        cut.Find("[data-testid='theme-select']").Change("discord-dark");

        cut.Find("[data-testid='theme-result']").TextContent.Should().Contain("server-side save failed");
    }

    [Fact]
    public void NoActiveThemes_ShowsEmptyState_InsteadOfASelect()
    {
        _themeService
            .Setup(s => s.GetActiveThemesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ThemeDto>());

        var cut = Render<BlazorProbe>();

        cut.Find("[data-testid='theme-empty']").Should().NotBeNull();
        cut.FindAll("[data-testid='theme-select']").Should().BeEmpty();
    }
}
