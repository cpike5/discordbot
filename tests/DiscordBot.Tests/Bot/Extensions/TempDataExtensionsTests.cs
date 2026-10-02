using DiscordBot.Bot.Extensions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;

namespace DiscordBot.Tests.Bot.Extensions;

/// <summary>
/// The TempData→toast bridge: what a page handler queues is what _ToastContainer reads, once.
/// </summary>
public class TempDataExtensionsTests
{
    private static TempDataDictionary NewTempData() =>
        new(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());

    [Fact]
    public void TakeToasts_ReturnsQueuedToasts_MostSevereFirst()
    {
        var tempData = NewTempData();
        tempData.SetSuccessToast("Saved.");
        tempData.SetErrorToast("Could not sync.", "Sync failed");
        tempData.SetInfoToast("Heads up.");

        var toasts = tempData.TakeToasts();

        toasts.Should().Equal(
            new ToastMessage("error", "Could not sync.", "Sync failed"),
            new ToastMessage("success", "Saved.", null),
            new ToastMessage("info", "Heads up.", null));
    }

    [Fact]
    public void TakeToasts_WithNothingQueued_IsEmpty()
    {
        NewTempData().TakeToasts().Should().BeEmpty();
    }

    [Fact]
    public void TakeToasts_MarksTheToastsRead_SoTheyAreNotKeptForTheNextRequest()
    {
        var tempData = NewTempData();
        tempData.SetWarningToast("Careful.");

        tempData.TakeToasts();
        tempData.Save();

        tempData.ContainsKey("ToastWarning").Should().BeFalse("a read TempData value is removed when TempData is saved");
    }

    [Fact]
    public void SettingAToastWithoutATitle_ClearsAnEarlierTitle()
    {
        var tempData = NewTempData();
        tempData.SetSuccessToast("First.", "Title");
        tempData.SetSuccessToast("Second.");

        tempData.TakeToasts().Should().ContainSingle().Which.Should().Be(new ToastMessage("success", "Second.", null));
    }
}
