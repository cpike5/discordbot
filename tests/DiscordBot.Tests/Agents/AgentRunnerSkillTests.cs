using System.Text.Json;
using DiscordBot.Agents;
using DiscordBot.Agents.Abstractions;
using DiscordBot.Agents.Contracts;
using DiscordBot.Agents.Contracts.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Agents;

/// <summary>
/// Unit tests for the loop's half of the skill mechanism: the advertised tool array is composed from
/// the run's skills, and re-composed after any round that loaded one.
/// </summary>
/// <remarks>
/// The tool the model calls to load a skill is the host's, not the engine's, so these tests stand a
/// fake one in its place — all the loop knows is that <see cref="ISkillActivationState.Activated"/>
/// changed. That is deliberate: the engine never learns what a skill is for, only that the set it
/// advertises has moved.
/// </remarks>
public class AgentRunnerSkillTests
{
    private static readonly JsonElement EmptySchema = JsonDocument.Parse("{}").RootElement.Clone();

    private readonly Mock<ILlmClient> _mockLlmClient = new();
    private readonly Mock<IToolRegistry> _mockRegistry = new();
    private readonly AgentRunner _agentRunner;

    /// <summary>The tool names advertised on each outgoing request, in order.</summary>
    private readonly List<IReadOnlyList<string>> _advertised = new();

    public AgentRunnerSkillTests()
    {
        _agentRunner = new AgentRunner(_mockLlmClient.Object, Mock.Of<ILogger<AgentRunner>>());
    }

    [Fact]
    public async Task RunAsync_WithNoSkills_AdvertisesTheRegistrySetUnchanged()
    {
        SetUpRegistry("alpha", "beta");
        SetUpModel(toolCallsOnFirstRound: null);

        await _agentRunner.RunAsync("hi", NewContext(skills: null));

        _advertised.Should().ContainSingle().Which.Should().Equal("alpha", "beta");
    }

    [Fact]
    public async Task RunAsync_WithASkillUnloaded_HidesItsToolsOnTheFirstRequest()
    {
        SetUpRegistry("alpha", "get_cases", "load_skill");
        SetUpModel(toolCallsOnFirstRound: null);

        await _agentRunner.RunAsync("hi", NewContext(Session(Skill("moderation", "get_cases"))));

        _advertised.Should().ContainSingle().Which.Should().Equal("alpha", "load_skill");
    }

    [Fact]
    public async Task RunAsync_WhenARoundLoadsASkill_AdvertisesItsToolsOnTheNextRequest()
    {
        SetUpRegistry("alpha", "get_cases", "load_skill");
        var session = Session(Skill("moderation", "get_cases"));
        SetUpModel(toolCallsOnFirstRound: new[] { "load_skill" });
        SetUpLoaderTool(session, "moderation");

        await _agentRunner.RunAsync("hi", NewContext(session));

        _advertised.Should().HaveCount(2);

        // The skill's tools are not paid for until it is loaded, and they arrive on the round after.
        _advertised[0].Should().Equal("alpha", "load_skill");
        _advertised[1].Should().Equal("alpha", "get_cases", "load_skill");
    }

    [Fact]
    public async Task RunAsync_WhenARoundLoadsNothing_LeavesTheToolArrayAlone()
    {
        // Re-composing would rewrite the array at position 0 of the request and throw away the
        // cached prefix behind it, so it has to happen only when the set actually changed.
        SetUpRegistry("alpha", "get_cases", "load_skill");
        var session = Session(Skill("moderation", "get_cases"));
        SetUpModel(toolCallsOnFirstRound: new[] { "alpha" });
        SetUpNoOpTool();

        await _agentRunner.RunAsync("hi", NewContext(session));

        _advertised.Should().HaveCount(2);
        _advertised[1].Should().Equal("alpha", "load_skill");
        _mockRegistry.Verify(r => r.GetEnabledTools(), Times.Once,
            "the registry is walked once when nothing changed");
    }

    [Fact]
    public async Task RunAsync_WithAPreActivatedSkill_AdvertisesItsToolsOnTheFirstRequest()
    {
        // The DM assistant's stickiness, from the loop's side: turn 2 spends no round on loading.
        SetUpRegistry("get_cases", "load_skill");
        SetUpModel(toolCallsOnFirstRound: null);

        var session = new SkillSession(new[] { Skill("moderation", "get_cases") }, new[] { "moderation" });

        await _agentRunner.RunAsync("hi", NewContext(session));

        _advertised.Should().ContainSingle().Which.Should().Equal("get_cases", "load_skill");
    }

    private static AgentSkill Skill(string key, params string[] tools) => new()
    {
        Key = key,
        Summary = $"The {key} skill.",
        Tools = tools,
        Instructions = $"How to use {key}."
    };

    private static SkillSession Session(params AgentSkill[] skills) => new(skills);

    private AgentContext NewContext(ISkillActivationState? skills) => new()
    {
        SystemPrompt = "You are a test agent.",
        ToolRegistry = _mockRegistry.Object,
        ExecutionContext = new ToolContext { UserId = 1, GuildId = 2 },
        Skills = skills,
        MaxToolCallIterations = 5
    };

    private void SetUpRegistry(params string[] toolNames)
    {
        _mockRegistry.Setup(r => r.GetEnabledTools()).Returns(() => toolNames
            .Select(n => new LlmToolDefinition { Name = n, Description = n, InputSchema = EmptySchema })
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList());
    }

    /// <summary>
    /// A model that asks for <paramref name="toolCallsOnFirstRound"/> once, then answers.
    /// </summary>
    private void SetUpModel(string[]? toolCallsOnFirstRound)
    {
        var round = 0;

        _mockLlmClient
            .Setup(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmRequest request, CancellationToken _) =>
            {
                _advertised.Add(request.Tools?.Select(t => t.Name).ToList() ?? new List<string>());

                if (++round == 1 && toolCallsOnFirstRound is { Length: > 0 })
                {
                    return new LlmResponse
                    {
                        Success = true,
                        StopReason = LlmStopReason.ToolUse,
                        ToolCalls = toolCallsOnFirstRound
                            .Select((name, i) => new LlmToolCall
                            {
                                Id = $"call-{i}",
                                Name = name,
                                Input = EmptySchema
                            })
                            .ToList()
                    };
                }

                return new LlmResponse
                {
                    Success = true,
                    Content = "Done.",
                    StopReason = LlmStopReason.EndTurn
                };
            });
    }

    /// <summary>Stands in for the host's loader tool: activating is all the loop can observe.</summary>
    private void SetUpLoaderTool(SkillSession session, string key)
    {
        _mockRegistry
            .Setup(r => r.ExecuteToolAsync(
                It.IsAny<string>(), It.IsAny<JsonElement>(), It.IsAny<ToolContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var skill = session.Activate(key);
                return ToolExecutionResult.CreateSuccess(
                    JsonSerializer.SerializeToElement(new { instructions = skill!.Instructions }));
            });
    }

    private void SetUpNoOpTool()
    {
        _mockRegistry
            .Setup(r => r.ExecuteToolAsync(
                It.IsAny<string>(), It.IsAny<JsonElement>(), It.IsAny<ToolContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolExecutionResult.CreateSuccess(
                JsonSerializer.SerializeToElement(new { ok = true })));
    }
}
