using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Models.Llm;
using FluentAssertions;

namespace DiscordBot.Tests.Core;

/// <summary>
/// Unit tests for <see cref="ToolCatalog"/> and the JSON helpers on
/// <see cref="AssistantGuildSettings"/> that back the per-guild tool allow-list.
/// </summary>
public class ToolCatalogTests
{
    [Fact]
    public void All_HasNoDuplicateToolNames()
    {
        ToolCatalog.All.Select(e => e.Name)
            .Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void ForScope_ReturnsOnlyToolsAdvertisedOnThatScope()
    {
        ToolCatalog.ForScope(ToolScopes.Guild)
            .Should().OnlyContain(e => (e.Scopes & ToolScopes.Guild) != 0);
    }

    [Fact]
    public void ForScope_IncludesAToolAdvertisedOnMoreThanOneScope()
    {
        // The documentation tools are registered on both the guild and the DM assistant.
        ToolCatalog.ForScope(ToolScopes.Guild).Select(e => e.Name).Should().Contain("search_commands");
        ToolCatalog.ForScope(ToolScopes.Dm).Select(e => e.Name).Should().Contain("search_commands");
    }

    [Fact]
    public void DefaultsForScope_IsNotEmptyForTheGuildScope()
    {
        // The empty-selection fallback resolves to this, so an empty default set would silently
        // strip every tool from every guild that has not opened the settings page.
        ToolCatalog.DefaultsForScope(ToolScopes.Guild).Should().NotBeEmpty();
    }

    [Fact]
    public void Describe_ReturnsTheCataloguedEntry_ForAKnownTool()
    {
        var entry = ToolCatalog.Describe("get_guild_info");

        entry.Category.Should().NotBe(ToolCatalog.OtherCategory);
        entry.DisplayName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Describe_FallsBackToTheOtherBucket_ForAnUncataloguedTool()
    {
        // A new tool must show up unclassified rather than vanish from the metrics table.
        var entry = ToolCatalog.Describe("brand_new_tool");

        entry.Name.Should().Be("brand_new_tool");
        entry.Category.Should().Be(ToolCatalog.OtherCategory);
        entry.Scopes.Should().Be(ToolScopes.None);
    }

    [Fact]
    public void IsCatalogued_IsCaseInsensitive()
    {
        ToolCatalog.IsCatalogued("LIST_FEATURES").Should().BeTrue();
        ToolCatalog.IsCatalogued("not_a_tool").Should().BeFalse();
    }

    [Fact]
    public void NormalizeSelection_StoresEmpty_ForExactlyTheDefaultSet()
    {
        // The settings page shows the default set ticked when a guild has chosen nothing, so saving
        // it untouched posts every default tool back. Storing those names literally would pin the
        // guild to today's defaults and leave it behind when a new tool joins the set.
        var defaults = ToolCatalog.DefaultsForScope(ToolScopes.Guild).ToList();

        ToolCatalog.NormalizeSelection(defaults, ToolScopes.Guild).Should().BeEmpty();
    }

    [Fact]
    public void NormalizeSelection_KeepsADeliberateSubset()
    {
        var result = ToolCatalog.NormalizeSelection(
            new[] { "search_commands", "list_features" }, ToolScopes.Guild);

        result.Should().BeEquivalentTo(new[] { "search_commands", "list_features" });
    }

    [Fact]
    public void NormalizeSelection_DropsUncataloguedNames()
    {
        // A stale or hand-crafted form post must not widen the stored list.
        ToolCatalog.NormalizeSelection(new[] { "search_commands", "not_a_tool" }, ToolScopes.Guild)
            .Should().Equal("search_commands");
    }

    [Fact]
    public void NormalizeSelection_DropsToolsFromAnotherScope()
    {
        // execute_python is a DM tool; it has no business in a guild's allow-list.
        ToolCatalog.NormalizeSelection(new[] { "search_commands", "execute_python" }, ToolScopes.Guild)
            .Should().Equal("search_commands");
    }

    [Fact]
    public void NormalizeSelection_DeduplicatesCaseInsensitively()
    {
        ToolCatalog.NormalizeSelection(
            new[] { "search_commands", "SEARCH_COMMANDS" }, ToolScopes.Guild)
            .Should().ContainSingle();
    }

    [Fact]
    public void NormalizeSelection_HandlesNoSelection()
    {
        ToolCatalog.NormalizeSelection(null, ToolScopes.Guild).Should().BeEmpty();
        ToolCatalog.NormalizeSelection(Array.Empty<string>(), ToolScopes.Guild).Should().BeEmpty();
    }

    [Fact]
    public void EnabledToolsList_RoundTripsThroughJson()
    {
        var settings = new AssistantGuildSettings();
        settings.SetEnabledToolsList(new List<string> { "list_features", "get_guild_info" });

        settings.GetEnabledToolsList().Should().Equal("list_features", "get_guild_info");
    }

    [Fact]
    public void GetEnabledToolsList_ReturnsEmpty_ForTheDefaultColumnValue()
    {
        new AssistantGuildSettings().GetEnabledToolsList().Should().BeEmpty();
    }

    [Fact]
    public void GetEnabledToolsList_ReturnsEmpty_ForMalformedJson()
    {
        // A hand-edited row must degrade to "house default", not throw on every question.
        new AssistantGuildSettings { EnabledTools = "not json" }
            .GetEnabledToolsList().Should().BeEmpty();
    }
}
