using Bunit;
using DiscordBot.Bot.Blazor.Pages.Admin.MessageLogs;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DiscordBot.ComponentTests.Blazor.Pages.Admin.MessageLogs;

/// <summary>
/// Component tests for <see cref="Details"/>, the routable replacement for
/// <c>Pages/Admin/MessageLogs/Details.cshtml</c> + <c>DetailsModel</c>
/// (docs/plans/blazor-port-plan.md Phase 4 cluster 4a). Covers: DM vs. server badge, every
/// section, the not-found EmptyState, and metadata badges.
/// </summary>
public class DetailsTests : BlazorComponentTestContext
{
    private readonly Mock<IMessageLogService> _messageLogService = new();

    public DetailsTests()
    {
        Services.AddSingleton(_messageLogService.Object);
    }

    private static MessageLogDto BuildMessage(long id = 1, bool isDm = false) => new()
    {
        Id = id,
        DiscordMessageId = 111222333UL,
        AuthorId = 555666777UL,
        AuthorUsername = "alice",
        ChannelId = 999888777UL,
        ChannelName = "general",
        GuildId = isDm ? null : 42UL,
        GuildName = isDm ? null : "Test Guild",
        Source = isDm ? MessageSource.DirectMessage : MessageSource.ServerChannel,
        Content = "Hello world",
        Timestamp = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc),
        LoggedAt = new DateTime(2026, 1, 15, 10, 30, 5, DateTimeKind.Utc),
        HasAttachments = true,
        HasEmbeds = false,
        ReplyToMessageId = null
    };

    private IRenderedComponent<Details> RenderDetails(long id)
    {
        AddBunitPersistentComponentState();
        return Render<Details>(p => p.Add(c => c.Id, id));
    }

    [Fact]
    public void ServerMessage_RendersServerBadgeAndGuildLocation()
    {
        _messageLogService.Setup(s => s.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(BuildMessage(1, isDm: false));

        var cut = RenderDetails(1);

        cut.Markup.Should().Contain("Server");
        cut.Markup.Should().Contain("Test Guild");
        cut.Markup.Should().Contain("general");
        cut.Markup.Should().Contain("Hello world");
        cut.Markup.Should().Contain("alice");
    }

    [Fact]
    public void DirectMessage_RendersDmBadgeAndDirectMessageNotice()
    {
        _messageLogService.Setup(s => s.GetByIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(BuildMessage(2, isDm: true));

        var cut = RenderDetails(2);

        cut.Markup.Should().Contain("DM");
        cut.Markup.Should().Contain("Direct Message");
        cut.Markup.Should().Contain("sent via DM to the bot");
    }

    [Fact]
    public void MessageNotFound_RendersEmptyState()
    {
        _messageLogService.Setup(s => s.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((MessageLogDto?)null);

        var cut = RenderDetails(99);

        cut.Markup.Should().Contain("Message Not Found");
        cut.Markup.Should().NotContain("Message Info");
    }

    [Fact]
    public void Metadata_RendersAttachmentEmbedAndReplyBadges()
    {
        var message = BuildMessage(1);
        message.ReplyToMessageId = 12345UL;
        _messageLogService.Setup(s => s.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(message);

        var cut = RenderDetails(1);

        cut.Markup.Should().Contain("Has Attachments");
        cut.Markup.Should().Contain("Has Embeds");
        cut.Markup.Should().Contain("Is Reply");
        cut.Markup.Should().Contain("12345");
    }

    /// <summary>Declared route authorization - see the sibling note on <c>AuditLogs.DetailsTests</c>.</summary>
    [Fact]
    public void Page_RequiresAdminPolicy()
    {
        var attribute = typeof(Details).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Should().ContainSingle().Subject;

        attribute.Policy.Should().Be("RequireAdmin");
    }
}
