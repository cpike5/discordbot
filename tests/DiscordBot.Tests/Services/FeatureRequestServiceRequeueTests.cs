using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Tests for <see cref="FeatureRequestService.RequeueDocGenAsync"/>: a failed documentation run goes
/// back to <see cref="FeatureRequestStatus.Submitted"/>, which is where the generator looks.
/// </summary>
public class FeatureRequestServiceRequeueTests
{
    private readonly Mock<IFeatureRequestRepository> _repo = new();
    private readonly FeatureRequestService _service;

    public FeatureRequestServiceRequeueTests()
    {
        _service = new FeatureRequestService(_repo.Object, Mock.Of<ILogger<FeatureRequestService>>());
    }

    [Fact]
    public async Task RequeueDocGenAsync_ForAFailedRun_ResetsTheStatusAndClearsTheError()
    {
        var request = new FeatureRequest
        {
            Id = Guid.NewGuid(),
            Status = FeatureRequestStatus.DocGenFailed,
            DocGenError = "The model returned nothing."
        };
        _repo.Setup(r => r.GetByIdAsync(request.Id)).ReturnsAsync(request);

        var queued = await _service.RequeueDocGenAsync(request.Id);

        queued.Should().BeTrue();
        request.Status.Should().Be(FeatureRequestStatus.Submitted);
        request.DocGenError.Should().BeNull();
        _repo.Verify(r => r.UpdateAsync(request), Times.Once);
    }

    [Theory]
    [InlineData(FeatureRequestStatus.Submitted)]
    [InlineData(FeatureRequestStatus.GeneratingDocs)]
    [InlineData(FeatureRequestStatus.DocsGenerated)]
    [InlineData(FeatureRequestStatus.Approved)]
    [InlineData(FeatureRequestStatus.Rejected)]
    public async Task RequeueDocGenAsync_ForARequestThatHasNotFailed_ChangesNothing(FeatureRequestStatus status)
    {
        var request = new FeatureRequest { Id = Guid.NewGuid(), Status = status };
        _repo.Setup(r => r.GetByIdAsync(request.Id)).ReturnsAsync(request);

        var queued = await _service.RequeueDocGenAsync(request.Id);

        queued.Should().BeFalse();
        request.Status.Should().Be(status);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<FeatureRequest>()), Times.Never);
    }

    [Fact]
    public async Task RequeueDocGenAsync_ForAMissingRequest_ReturnsFalse()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((FeatureRequest?)null);

        (await _service.RequeueDocGenAsync(Guid.NewGuid())).Should().BeFalse();
    }
}
