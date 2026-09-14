using Bunit;
using DiscordBot.Bot.Services.Realtime;
using DiscordBot.Bot.Services.Realtime.Events;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using IndexPage = DiscordBot.Bot.Blazor.Pages.Admin.BulkPurge.Index;

namespace DiscordBot.ComponentTests.Blazor.Pages.Admin.BulkPurge;

/// <summary>
/// Component tests for <see cref="IndexPage"/>, the routable replacement for
/// <c>Pages/Admin/BulkPurge.cshtml</c> + <c>BulkPurgeModel</c>
/// (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4d) - validation, preview, the typed
/// confirm gate, and the live progress bar driven by <see cref="IDashboardEventBus"/>.
/// </summary>
public class IndexTests : BlazorComponentTestContext
{
    private readonly Mock<IBulkPurgeService> _service = new();

    public IndexTests()
    {
        Services.AddSingleton(_service.Object);
        AddAuthorization().SetAuthorized("superadmin").SetRoles("SuperAdmin");
        SetInteractiveRendererInfo();
    }

    [Fact]
    public void Preview_WithNoEntityTypeSelected_ShowsValidationError()
    {
        var cut = Render<IndexPage>();

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Preview").Click();

        cut.Markup.Should().Contain("Please select an entity type.");
        _service.Verify(s => s.PreviewPurgeAsync(It.IsAny<BulkPurgeCriteriaDto>()), Times.Never);
    }

    [Fact]
    public void Preview_WithEntitySelected_CallsServiceAndRendersEstimate()
    {
        _service.Setup(s => s.PreviewPurgeAsync(It.Is<BulkPurgeCriteriaDto>(c => c.EntityType == BulkPurgeEntityType.Messages)))
            .ReturnsAsync(BulkPurgePreviewDto.Succeeded(BulkPurgeEntityType.Messages, 42, "all time"));

        var cut = Render<IndexPage>();
        cut.FindAll("[data-testid='entity-type-card']")[0].Click();
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Preview").Click();

        cut.WaitForAssertion(() => cut.Find("[data-testid='preview-result']").TextContent.Should().Contain("42"));
    }

    [Fact]
    public void Execute_RequiresTypedConfirmation_ThenCallsServiceAndUpdatesProgressFromTheEventBus()
    {
        _service.Setup(s => s.PreviewPurgeAsync(It.IsAny<BulkPurgeCriteriaDto>()))
            .ReturnsAsync(BulkPurgePreviewDto.Succeeded(BulkPurgeEntityType.Messages, 10, "all time"));

        var eventBus = Services.GetRequiredService<IDashboardEventBus>();
        var executeStarted = new TaskCompletionSource();
        _service.Setup(s => s.ExecutePurgeAsync(It.IsAny<BulkPurgeCriteriaDto>(), It.IsAny<string>()))
            .Returns(async () =>
            {
                await eventBus.PublishAsync(new BulkPurgeProgressEvent
                {
                    Progress = BulkPurgeProgressDto.Create(BulkPurgeEntityType.Messages, 5, 10)
                });
                executeStarted.TrySetResult();
                return BulkPurgeResultDto.Succeeded(BulkPurgeEntityType.Messages, 10, "corr-1");
            });

        var cut = Render<IndexPage>();
        cut.FindAll("[data-testid='entity-type-card']")[0].Click();
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Preview").Click();
        cut.WaitForAssertion(() => cut.Find("[data-testid='preview-result']"));

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Execute Purge").Click();

        var confirmButton = cut.FindAll("#executePurgeModal button").Single(b => b.TextContent.Trim() == "Execute Purge");
        confirmButton.HasAttribute("disabled").Should().BeTrue();

        cut.Find("#executePurgeModalInput").Input("CONFIRM");
        confirmButton = cut.FindAll("#executePurgeModal button").Single(b => b.TextContent.Trim() == "Execute Purge");
        confirmButton.Click();

        cut.WaitForAssertion(() => executeStarted.Task.IsCompleted.Should().BeTrue());
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("corr-1"));
        _service.Verify(s => s.ExecutePurgeAsync(It.Is<BulkPurgeCriteriaDto>(c => c.EntityType == BulkPurgeEntityType.Messages), It.IsAny<string>()), Times.Once);
    }
}
