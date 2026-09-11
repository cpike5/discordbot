using DiscordBot.Bot.Blazor.Services;
using FluentAssertions;

namespace DiscordBot.Tests.Blazor.Services;

/// <summary>
/// Unit tests for <see cref="LoadingState"/>: reference-counted nesting and the
/// <see cref="ILoadingState.Changed"/> notification.
/// </summary>
public class LoadingStateTests
{
    private readonly LoadingState _state = new();

    [Fact]
    public void InitialState_IsNotLoading()
    {
        _state.IsLoading.Should().BeFalse();
        _state.Message.Should().BeNull();
    }

    [Fact]
    public void Begin_SetsIsLoadingTrue()
    {
        using var scope = _state.Begin("Loading...");

        _state.IsLoading.Should().BeTrue();
        _state.Message.Should().Be("Loading...");
    }

    [Fact]
    public void Dispose_WithSingleScope_ClearsIsLoading()
    {
        var scope = _state.Begin();

        scope.Dispose();

        _state.IsLoading.Should().BeFalse();
    }

    [Fact]
    public void NestedScopes_StayLoadingUntilAllDispose()
    {
        // Arrange
        var outer = _state.Begin("Outer");
        var inner = _state.Begin("Inner");

        // Act & Assert - still loading after disposing only the inner scope
        inner.Dispose();
        _state.IsLoading.Should().BeTrue("the outer scope is still open");
        _state.Message.Should().Be("Outer", "the most recently opened remaining scope's message is shown");

        outer.Dispose();
        _state.IsLoading.Should().BeFalse();
    }

    [Fact]
    public void Message_ReflectsMostRecentlyOpenedScope()
    {
        using var first = _state.Begin("First");
        using var second = _state.Begin("Second");

        _state.Message.Should().Be("Second");
    }

    [Fact]
    public void Dispose_CalledTwice_IsIdempotentAndDoesNotDoubleDecrement()
    {
        // Arrange
        var scope = _state.Begin();
        using var other = _state.Begin();

        // Act
        scope.Dispose();
        scope.Dispose();

        // Assert - only the first scope closed; the second is still open
        _state.IsLoading.Should().BeTrue();
    }

    [Fact]
    public void Begin_RaisesChanged()
    {
        var changedCount = 0;
        _state.Changed += () => changedCount++;

        using var scope = _state.Begin();

        changedCount.Should().Be(1);
    }

    [Fact]
    public void Dispose_RaisesChanged()
    {
        var scope = _state.Begin();
        var changedCount = 0;
        _state.Changed += () => changedCount++;

        scope.Dispose();

        changedCount.Should().Be(1);
    }

    [Fact]
    public void Dispose_OnAlreadyClosedScope_DoesNotRaiseChanged()
    {
        var scope = _state.Begin();
        scope.Dispose();
        var changedCount = 0;
        _state.Changed += () => changedCount++;

        scope.Dispose();

        changedCount.Should().Be(0);
    }
}
