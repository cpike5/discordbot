using Microsoft.Extensions.DependencyInjection;
using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Moq;

namespace DiscordBot.ComponentTests.Blazor.Shared.Overlays;

/// <summary>Tests for the <see cref="GuildPreview"/> convenience wrapper - see
/// <see cref="UserPreviewTests"/>'s top-of-file note for the scope-per-open rationale.</summary>
public class GuildPreviewTests : BlazorComponentTestContext
{
    private readonly Mock<IPreviewService> _service = new();

    public GuildPreviewTests()
    {
        Services.AddSingleton(_service.Object);
    }

    [Fact]
    public void Click_LoadsThroughPreviewService_WithGivenGuildId_AndRendersContent()
    {
        _service.Setup(s => s.GetGuildPreviewAsync(789UL, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildPreviewDto { GuildId = 789, Name = "Test Guild", OwnerUsername = "owner" });

        var cut = Render<GuildPreview>(p => p
            .Add(x => x.GuildId, 789UL)
            .AddChildContent("Test Guild"));

        cut.Find("[tabindex='0']").Click();

        cut.Find(".preview-username").TextContent.Should().Be("Test Guild");
        _service.Verify(s => s.GetGuildPreviewAsync(789UL, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Click_ServiceReturnsNull_RendersErrorState()
    {
        _service.Setup(s => s.GetGuildPreviewAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildPreviewDto?)null);

        var cut = Render<GuildPreview>(p => p
            .Add(x => x.GuildId, 789UL)
            .AddChildContent("Test Guild"));

        cut.Find("[tabindex='0']").Click();

        cut.Markup.Should().Contain("Guild Unavailable");
    }
}
