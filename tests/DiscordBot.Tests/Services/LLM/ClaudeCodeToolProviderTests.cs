using System.Text.Json;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Services.LLM.Implementations;
using DiscordBot.Infrastructure.Services.LLM.Providers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Services.LLM;

/// <summary>
/// Unit tests for <see cref="ClaudeCodeToolProvider"/> (Mogwai). Focuses on the security
/// guards: the owner re-check that gates this process-spawning tool, and the cumulative
/// budget ceiling enforcement.
/// </summary>
public class ClaudeCodeToolProviderTests
{
    private const ulong OwnerId = 111;
    private const ulong NonOwnerId = 222;

    private readonly Mock<ILogger<ClaudeCodeToolProvider>> _logger = new();
    private readonly Mock<IBotOwnerResolver> _ownerResolver = new();

    private ClaudeCodeToolProvider CreateProvider(MogwaiOptions? options = null)
    {
        return new ClaudeCodeToolProvider(
            _logger.Object,
            Options.Create(options ?? new MogwaiOptions { Enabled = true }),
            _ownerResolver.Object);
    }

    private static JsonElement RunInput(string prompt = "do something")
    {
        var json = JsonSerializer.Serialize(new { prompt });
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    [Fact]
    public async Task ExecuteToolAsync_WhenDisabled_ReturnsError()
    {
        var provider = CreateProvider(new MogwaiOptions { Enabled = false });

        var result = await provider.ExecuteToolAsync(
            ClaudeCodeTools.RunClaudeCode, RunInput(), new ToolContext { UserId = OwnerId });

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("disabled");
        _ownerResolver.Verify(r => r.GetOwnerIdAsync(), Times.Never);
    }

    [Fact]
    public async Task ExecuteToolAsync_NonOwner_IsRejectedBeforeSpawning()
    {
        _ownerResolver.Setup(r => r.GetOwnerIdAsync()).ReturnsAsync(OwnerId);
        var provider = CreateProvider();

        var result = await provider.ExecuteToolAsync(
            ClaudeCodeTools.RunClaudeCode, RunInput(), new ToolContext { UserId = NonOwnerId });

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("owner");
    }

    [Fact]
    public async Task ExecuteToolAsync_StatusForNonOwner_IsRejected()
    {
        _ownerResolver.Setup(r => r.GetOwnerIdAsync()).ReturnsAsync(OwnerId);
        var provider = CreateProvider();

        var result = await provider.ExecuteToolAsync(
            ClaudeCodeTools.GetClaudeCodeStatus,
            JsonDocument.Parse("{}").RootElement.Clone(),
            new ToolContext { UserId = NonOwnerId });

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("owner");
    }

    [Fact]
    public async Task ExecuteToolAsync_WhenOwnerResolverThrows_FailsClosed()
    {
        _ownerResolver.Setup(r => r.GetOwnerIdAsync()).ThrowsAsync(new InvalidOperationException("boom"));
        var provider = CreateProvider();

        var result = await provider.ExecuteToolAsync(
            ClaudeCodeTools.RunClaudeCode, RunInput(), new ToolContext { UserId = OwnerId });

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("owner");
    }

    [Theory]
    [InlineData(0.0, 5.0, false)]   // nothing spent yet
    [InlineData(4.99, 5.0, false)]  // under budget
    [InlineData(5.0, 5.0, true)]    // exactly at budget
    [InlineData(6.0, 5.0, true)]    // over budget
    [InlineData(100.0, 0.0, false)] // zero ceiling disables the limit
    [InlineData(100.0, -1.0, false)] // negative ceiling disables the limit
    public void IsBudgetExhausted_EnforcesCeiling(double cumulative, double budget, bool expected)
    {
        ClaudeCodeToolProvider.IsBudgetExhausted((decimal)cumulative, (decimal)budget)
            .Should().Be(expected);
    }
}
