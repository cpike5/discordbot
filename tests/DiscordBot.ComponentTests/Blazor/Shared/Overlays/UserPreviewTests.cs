using Microsoft.Extensions.DependencyInjection;
using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Moq;

namespace DiscordBot.ComponentTests.Blazor.Shared.Overlays;

/// <summary>
/// Tests for the <see cref="UserPreview"/> convenience wrapper (docs/plans/blazor-port-plan.md
/// Phase 4 cluster 4d) - <c>IPreviewService</c> is resolved through a fresh
/// <c>IServiceScopeFactory</c> scope per <c>PreviewPopover</c> open (see the component's own
/// top-of-file note), so a singleton mock registration is enough for bUnit's DI container to
/// resolve it from that child scope too.
/// </summary>
public class UserPreviewTests : BlazorComponentTestContext
{
    private readonly Mock<IPreviewService> _service = new();

    public UserPreviewTests()
    {
        Services.AddSingleton(_service.Object);
    }

    [Fact]
    public void Click_LoadsThroughPreviewService_WithGivenUserAndGuildIds_AndRendersContent()
    {
        _service.Setup(s => s.GetUserPreviewAsync(123UL, 456UL, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserPreviewDto { UserId = 123, Username = "exampleuser" });

        var cut = Render<UserPreview>(p => p
            .Add(x => x.UserId, 123UL)
            .Add(x => x.GuildId, 456L)
            .AddChildContent("Example User"));

        cut.Markup.Should().Contain("Example User");
        cut.Find("[tabindex='0']").Click();

        cut.Find(".preview-username").TextContent.Should().Be("exampleuser");
        _service.Verify(s => s.GetUserPreviewAsync(123UL, 456UL, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Click_NoGuildId_LoadsWithNullGuildContext()
    {
        _service.Setup(s => s.GetUserPreviewAsync(123UL, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserPreviewDto { UserId = 123, Username = "exampleuser" });

        var cut = Render<UserPreview>(p => p
            .Add(x => x.UserId, 123UL)
            .AddChildContent("Example User"));

        cut.Find("[tabindex='0']").Click();

        cut.Find(".preview-username").TextContent.Should().Be("exampleuser");
        _service.Verify(s => s.GetUserPreviewAsync(123UL, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Click_ServiceReturnsNull_RendersErrorState()
    {
        _service.Setup(s => s.GetUserPreviewAsync(It.IsAny<ulong>(), It.IsAny<ulong?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserPreviewDto?)null);

        var cut = Render<UserPreview>(p => p
            .Add(x => x.UserId, 123UL)
            .AddChildContent("Example User"));

        cut.Find("[tabindex='0']").Click();

        cut.Markup.Should().Contain("User Not Found");
    }
}
