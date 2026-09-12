using System.Text.Json;
using DiscordBot.Agents;
using DiscordBot.Agents.Abstractions;
using DiscordBot.Agents.Contracts;
using DiscordBot.Infrastructure.Services.LLM;
using DiscordBot.Infrastructure.Services.LLM.Tools;
using FluentAssertions;

namespace DiscordBot.Tests.Services.LLM;

/// <summary>
/// Unit tests for <see cref="LoadSkillTool"/>, the model-facing half of the skill mechanism.
/// </summary>
/// <remarks>
/// Every failure path here is a <em>successful</em> <see cref="ToolExecutionResult"/> carrying an
/// explanation, which is the house convention — <c>CreateError</c> prefixes <c>Error: </c> on the
/// wire and reads as a malfunction to retry around. The assertions pin both halves: the result is
/// successful, and <see cref="ToolOutcomes.Classify"/> still counts it as a failed result so the
/// traces can see it.
/// </remarks>
public class LoadSkillToolTests
{
    private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement.Clone();

    private static AgentSkill Skill(
        string key,
        string summary = "When the request is about this.",
        string instructions = "Do the thing, carefully.",
        params string[] tools) => new()
        {
            Key = key,
            Summary = summary,
            Instructions = instructions,
            Tools = tools
        };

    private static ToolContext ContextWith(SkillSession? skills)
    {
        var context = new ToolContext { UserId = 7UL };
        context.SetSkills(skills);
        return context;
    }

    [Fact]
    public void ToolName_IsLoadSkill()
    {
        LoadSkillTool.ToolName.Should().Be("load_skill");
        new LoadSkillTool().Definition.Name.Should().Be("load_skill");
    }

    [Fact]
    public void Mutation_IsNull_BecauseLoadingASkillWritesNothing()
    {
        // Mutation is a default interface member, and the tool does not override it — reading it
        // through the interface is what AgentToolProvider does before deciding to refuse a call.
        ((IAgentTool)new LoadSkillTool()).Mutation.Should().BeNull();
    }

    [Fact]
    public async Task InvokeAsync_WithoutKey_ReturnsSuccessfulResultCarryingError()
    {
        var tool = new LoadSkillTool();
        var context = ContextWith(new SkillSession(new[] { Skill("weather") }));

        var result = await tool.InvokeAsync(Json("{}"), context);

        result.Success.Should().BeTrue("an expected failure is reported as a successful result");
        result.Data!.Value.GetProperty("error").GetString().Should().Contain("key");
        ToolOutcomes.Classify(result.Data!.Value).Should().Be(ToolOutcomes.FailedResult);
    }

    [Fact]
    public async Task InvokeAsync_WithNoSessionInContext_ReturnsNotFoundAndActivatesNothing()
    {
        var tool = new LoadSkillTool();
        var context = new ToolContext { UserId = 7UL };

        var result = await tool.InvokeAsync(Json("""{"key":"weather"}"""), context);

        result.Success.Should().BeTrue();
        result.Data!.Value.GetProperty("found").GetBoolean().Should().BeFalse();
        result.Data!.Value.GetProperty("error").GetString().Should().Contain("No skills are available");
        context.GetSkills().Should().BeNull();
    }

    [Fact]
    public async Task InvokeAsync_WithUnknownKey_ReturnsNotFoundListingAvailableKeys()
    {
        var tool = new LoadSkillTool();
        var skills = new SkillSession(new[] { Skill("moderation"), Skill("weather") });
        var context = ContextWith(skills);

        var result = await tool.InvokeAsync(Json("""{"key":"astrology"}"""), context);

        result.Data!.Value.GetProperty("found").GetBoolean().Should().BeFalse();
        var error = result.Data!.Value.GetProperty("error").GetString();
        error.Should().Contain("astrology").And.Contain("moderation").And.Contain("weather");
        skills.Activated.Should().BeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WithKnownKey_ActivatesSkillAndReturnsInstructionsAndTools()
    {
        var tool = new LoadSkillTool();
        var skills = new SkillSession(new[]
        {
            Skill("moderation", "Look up moderation cases.", "Search cases before answering.", "get_cases", "get_case")
        });
        var context = ContextWith(skills);

        var result = await tool.InvokeAsync(Json("""{"key":"moderation"}"""), context);

        result.Success.Should().BeTrue();
        var payload = result.Data!.Value;
        payload.GetProperty("skill").GetString().Should().Be("moderation");
        payload.GetProperty("instructions").GetString().Should().Be("Search cases before answering.");
        payload.GetProperty("tools_now_available").EnumerateArray()
            .Select(t => t.GetString()).Should().Equal("get_cases", "get_case");

        skills.Activated.Select(s => s.Key).Should().Equal("moderation");
    }

    [Fact]
    public async Task InvokeAsync_WithSkillThatHasNoTools_OmitsToolsNowAvailable()
    {
        var tool = new LoadSkillTool();
        var context = ContextWith(new SkillSession(new[] { Skill("standing-orders") }));

        var result = await tool.InvokeAsync(Json("""{"key":"standing-orders"}"""), context);

        // ToolJson.Compact drops nulls, so an absent optional field costs nothing on the wire —
        // the property should not be there at all rather than be present and null.
        result.Data!.Value.TryGetProperty("tools_now_available", out _).Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_OnFirstLoad_OmitsAlreadyLoaded()
    {
        var tool = new LoadSkillTool();
        var context = ContextWith(new SkillSession(new[] { Skill("weather") }));

        var result = await tool.InvokeAsync(Json("""{"key":"weather"}"""), context);

        result.Data!.Value.TryGetProperty("already_loaded", out _).Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_Twice_IsIdempotentAndReportsAlreadyLoaded()
    {
        var tool = new LoadSkillTool();
        var skills = new SkillSession(new[] { Skill("weather", instructions: "Check the forecast first.") });
        var context = ContextWith(skills);

        await tool.InvokeAsync(Json("""{"key":"weather"}"""), context);
        var second = await tool.InvokeAsync(Json("""{"key":"weather"}"""), context);

        skills.Activated.Should().HaveCount(1);
        second.Data!.Value.GetProperty("already_loaded").GetBoolean().Should().BeTrue();

        // A model that loads a skill again wants to re-read the instructions, not an error.
        second.Data!.Value.GetProperty("instructions").GetString().Should().Be("Check the forecast first.");
    }

    [Fact]
    public async Task InvokeAsync_WithDifferentlyCasedKey_MatchesTheSkill()
    {
        var tool = new LoadSkillTool();
        var skills = new SkillSession(new[] { Skill("moderation") });
        var context = ContextWith(skills);

        var result = await tool.InvokeAsync(Json("""{"key":"MoDeRaTiOn"}"""), context);

        result.Data!.Value.GetProperty("skill").GetString().Should().Be("moderation");
        skills.Activated.Select(s => s.Key).Should().Equal("moderation");
    }
}
