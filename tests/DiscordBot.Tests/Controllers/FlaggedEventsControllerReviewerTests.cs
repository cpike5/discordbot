using System.Security.Claims;
using DiscordBot.Bot.Controllers;
using DiscordBot.Bot.Extensions;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DiscordBot.Tests.Controllers;

/// <summary>
/// The reviewer of a flagged event is the signed-in user's linked Discord account, never a value
/// the request body supplies, and an account with no link is refused rather than recorded as 0.
/// </summary>
public class FlaggedEventsControllerReviewerTests
{
    private const ulong GuildId = 123456789UL;
    private const ulong SignedInDiscordId = 555000555000555000UL;
    private const ulong ClaimedInBody = 111UL;

    private readonly Mock<IFlaggedEventService> _service = new();
    private readonly FlaggedEventsController _controller;
    private readonly FlaggedEventDto _event;

    public FlaggedEventsControllerReviewerTests()
    {
        _event = new FlaggedEventDto
        {
            Id = Guid.NewGuid(),
            GuildId = GuildId,
            Status = FlaggedEventStatus.Pending
        };
        _service.Setup(s => s.GetEventAsync(_event.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_event);
        _service.Setup(s => s.DismissEventAsync(_event.Id, It.IsAny<ulong>(), It.IsAny<CancellationToken>())).ReturnsAsync(_event);
        _service.Setup(s => s.AcknowledgeEventAsync(_event.Id, It.IsAny<ulong>(), It.IsAny<CancellationToken>())).ReturnsAsync(_event);
        _service.Setup(s => s.TakeActionAsync(_event.Id, It.IsAny<string>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>())).ReturnsAsync(_event);

        _controller = new FlaggedEventsController(_service.Object, NullLogger<FlaggedEventsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private void SignIn(bool linkedToDiscord)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "identity-user") };
        if (linkedToDiscord)
        {
            claims.Add(new Claim(ClaimsPrincipalExtensions.DiscordUserIdClaimType, SignedInDiscordId.ToString()));
        }

        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    [Fact]
    public async Task Dismiss_RecordsTheClaimsDiscordId_AndIgnoresTheBody()
    {
        SignIn(linkedToDiscord: true);

        var result = await _controller.DismissEvent(GuildId, _event.Id, new FlaggedEventReviewDto { ReviewerId = ClaimedInBody });

        result.Result.Should().BeOfType<OkObjectResult>();
        _service.Verify(s => s.DismissEventAsync(_event.Id, SignedInDiscordId, It.IsAny<CancellationToken>()), Times.Once);
        _service.Verify(s => s.DismissEventAsync(It.IsAny<Guid>(), ClaimedInBody, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Acknowledge_RecordsTheClaimsDiscordId_AndIgnoresTheBody()
    {
        SignIn(linkedToDiscord: true);

        var result = await _controller.AcknowledgeEvent(GuildId, _event.Id, new FlaggedEventReviewDto { ReviewerId = ClaimedInBody });

        result.Result.Should().BeOfType<OkObjectResult>();
        _service.Verify(s => s.AcknowledgeEventAsync(_event.Id, SignedInDiscordId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TakeAction_RecordsTheClaimsDiscordId_AndIgnoresTheBody()
    {
        SignIn(linkedToDiscord: true);

        var result = await _controller.TakeAction(
            GuildId, _event.Id, new FlaggedEventTakeActionDto { Action = "Warned", ReviewerId = ClaimedInBody });

        result.Result.Should().BeOfType<OkObjectResult>();
        _service.Verify(s => s.TakeActionAsync(_event.Id, "Warned", SignedInDiscordId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EveryReviewAction_WithoutALinkedDiscordAccount_IsRefusedWithAClearMessage()
    {
        SignIn(linkedToDiscord: false);
        var body = new FlaggedEventReviewDto { ReviewerId = ClaimedInBody };

        var results = new[]
        {
            (await _controller.DismissEvent(GuildId, _event.Id, body)).Result,
            (await _controller.AcknowledgeEvent(GuildId, _event.Id, body)).Result,
            (await _controller.TakeAction(GuildId, _event.Id,
                new FlaggedEventTakeActionDto { Action = "Warned", ReviewerId = ClaimedInBody })).Result
        };

        foreach (var result in results)
        {
            var refused = result.Should().BeOfType<ObjectResult>().Subject;
            refused.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
            refused.Value.Should().BeOfType<ApiErrorDto>().Which.Message
                .Should().Be("Link your Discord account to review events.");
        }

        _service.Verify(s => s.DismissEventAsync(It.IsAny<Guid>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()), Times.Never);
        _service.Verify(s => s.AcknowledgeEventAsync(It.IsAny<Guid>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()), Times.Never);
        _service.Verify(s => s.TakeActionAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
