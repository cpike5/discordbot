using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Models.Llm;
using DiscordBot.Infrastructure.Services.LLM;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services.LLM;

/// <summary>
/// Unit tests for <see cref="ToolAccessResolver"/>: the empty-selection fallback to the house
/// default set, caching, and invalidation on save.
/// </summary>
public class ToolAccessResolverTests
{
    private const ulong GuildId = 123456789UL;

    private readonly Mock<IAssistantGuildSettingsRepository> _repository = new();
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
    private readonly ToolAccessResolver _resolver;

    public ToolAccessResolverTests()
    {
        _resolver = new ToolAccessResolver(
            _repository.Object, _cache, Mock.Of<ILogger<ToolAccessResolver>>());
    }

    private void SetSettings(AssistantGuildSettings? settings) =>
        _repository
            .Setup(r => r.GetByGuildIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

    [Fact]
    public async Task ResolveAsync_ReturnsTheHouseDefaultSet_WhenTheGuildHasSelectedNothing()
    {
        SetSettings(new AssistantGuildSettings { GuildId = GuildId, EnabledTools = "[]" });

        var allowed = await _resolver.ResolveAsync(GuildId);

        allowed.Should().BeEquivalentTo(ToolCatalog.DefaultsForScope(ToolScopes.Guild));
        allowed.Should().NotBeEmpty("an empty selection means the default set, not no tools");
    }

    [Fact]
    public async Task ResolveAsync_ReturnsTheHouseDefaultSet_WhenTheGuildHasNoSettingsRow()
    {
        SetSettings(null);

        var allowed = await _resolver.ResolveAsync(GuildId);

        allowed.Should().BeEquivalentTo(ToolCatalog.DefaultsForScope(ToolScopes.Guild));
    }

    [Fact]
    public async Task ResolveAsync_ReturnsTheGuildSelection_WhenOneIsSaved()
    {
        var settings = new AssistantGuildSettings { GuildId = GuildId };
        settings.SetEnabledToolsList(new List<string> { "search_commands" });
        SetSettings(settings);

        var allowed = await _resolver.ResolveAsync(GuildId);

        allowed.Should().BeEquivalentTo(new[] { "search_commands" });
    }

    [Fact]
    public async Task ResolveAsync_MatchesToolNamesCaseInsensitively()
    {
        var settings = new AssistantGuildSettings { GuildId = GuildId };
        settings.SetEnabledToolsList(new List<string> { "Search_Commands" });
        SetSettings(settings);

        var allowed = await _resolver.ResolveAsync(GuildId);

        allowed.Contains("search_commands").Should().BeTrue();
    }

    [Fact]
    public async Task ResolveAsync_CachesSoTheSettingsRowIsReadOncePerGuild()
    {
        SetSettings(new AssistantGuildSettings { GuildId = GuildId, EnabledTools = "[]" });

        await _resolver.ResolveAsync(GuildId);
        await _resolver.ResolveAsync(GuildId);

        _repository.Verify(
            r => r.GetByGuildIdAsync(GuildId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Invalidate_MakesTheNextResolveReadTheRowAgain()
    {
        var settings = new AssistantGuildSettings { GuildId = GuildId };
        settings.SetEnabledToolsList(new List<string> { "search_commands" });
        SetSettings(settings);

        (await _resolver.ResolveAsync(GuildId)).Should().BeEquivalentTo(new[] { "search_commands" });

        settings.SetEnabledToolsList(new List<string> { "list_features" });
        _resolver.Invalidate(GuildId);

        (await _resolver.ResolveAsync(GuildId)).Should().BeEquivalentTo(new[] { "list_features" });
    }
}
