using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;

namespace DiscordBot.Tests.Blazor.Services;

/// <summary>
/// Unit tests for <see cref="ToastService"/>: the 5-toast cap, dismiss, and the
/// <see cref="IToastService.Changed"/> notification.
/// </summary>
public class ToastServiceTests : IDisposable
{
    private readonly ToastService _service = new();

    public void Dispose() => _service.Dispose();

    [Fact]
    public void Show_AddsAToastToTheQueue()
    {
        // Act
        _service.Show(ToastLevel.Info, "Hello");

        // Assert
        _service.Toasts.Should().ContainSingle();
        _service.Toasts[0].Message.Should().Be("Hello");
        _service.Toasts[0].Level.Should().Be(ToastLevel.Info);
    }

    [Fact]
    public void Show_SixthToast_EvictsTheOldest()
    {
        // Arrange - no auto-dismiss so the queue only shrinks from the cap, not a timer
        for (var i = 0; i < 5; i++)
        {
            _service.Show(ToastLevel.Info, $"Toast {i}", duration: TimeSpan.Zero);
        }

        // Act
        _service.Show(ToastLevel.Info, "Toast 5", duration: TimeSpan.Zero);

        // Assert
        _service.Toasts.Should().HaveCount(5);
        _service.Toasts.Select(t => t.Message).Should().NotContain("Toast 0");
        _service.Toasts.Select(t => t.Message).Should().Contain("Toast 5");
    }

    [Fact]
    public void Success_Info_Warning_Error_SetExpectedLevel()
    {
        _service.Success("s");
        _service.Info("i");
        _service.Warning("w");
        _service.Error("e");

        _service.Toasts.Select(t => t.Level).Should().Equal(
            ToastLevel.Success, ToastLevel.Info, ToastLevel.Warning, ToastLevel.Error);
    }

    [Fact]
    public void Dismiss_RemovesTheMatchingToast()
    {
        // Arrange
        _service.Show(ToastLevel.Info, "First", duration: TimeSpan.Zero);
        _service.Show(ToastLevel.Info, "Second", duration: TimeSpan.Zero);
        var idToRemove = _service.Toasts[0].Id;

        // Act
        _service.Dismiss(idToRemove);

        // Assert
        _service.Toasts.Should().ContainSingle();
        _service.Toasts[0].Message.Should().Be("Second");
    }

    [Fact]
    public void Dismiss_UnknownId_IsANoOpAndDoesNotRaiseChanged()
    {
        // Arrange
        _service.Show(ToastLevel.Info, "First", duration: TimeSpan.Zero);
        var changedCount = 0;
        _service.Changed += () => changedCount++;

        // Act
        _service.Dismiss(Guid.NewGuid());

        // Assert
        _service.Toasts.Should().ContainSingle();
        changedCount.Should().Be(0);
    }

    [Fact]
    public void Show_RaisesChanged()
    {
        // Arrange
        var changedCount = 0;
        _service.Changed += () => changedCount++;

        // Act
        _service.Show(ToastLevel.Info, "Hello");

        // Assert
        changedCount.Should().Be(1);
    }

    [Fact]
    public void Dismiss_RaisesChanged()
    {
        // Arrange
        _service.Show(ToastLevel.Info, "Hello", duration: TimeSpan.Zero);
        var id = _service.Toasts[0].Id;
        var changedCount = 0;
        _service.Changed += () => changedCount++;

        // Act
        _service.Dismiss(id);

        // Assert
        changedCount.Should().Be(1);
    }

    [Fact]
    public async Task Show_WithDuration_AutoDismissesAfterTheDuration()
    {
        // Arrange
        _service.Show(ToastLevel.Info, "Ephemeral", duration: TimeSpan.FromMilliseconds(20));

        // Act / Assert - poll off the xUnit context per docs/lessons-learned/flaky-tests-thread-pool-starvation.md
        var dismissed = await LogTestHelper.WaitUntilAsync(
            () => _service.Toasts.Count == 0,
            TimeSpan.FromSeconds(5));

        dismissed.Should().BeTrue("the toast should auto-dismiss once its duration elapses");
    }

    [Fact]
    public void Show_NullOrWhitespaceMessage_Throws()
    {
        var act = () => _service.Show(ToastLevel.Info, "   ");
        act.Should().Throw<ArgumentException>();
    }
}
