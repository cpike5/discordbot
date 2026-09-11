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
/// Unit tests for the loop's guard rails: the tool-result cap, the per-tool execution deadline,
/// the duplicate-call guard, and the two text-only follow-up calls (budget wrap-up and
/// blank-final-text recovery). Each of these protects a cost or a failure mode that is invisible
/// in a single-turn happy path, so they are exercised through whole runs rather than in isolation.
/// </summary>
public class AgentRunnerHardeningTests
{
    private readonly Mock<ILlmClient> _mockLlmClient = new();
    private readonly Mock<IToolRegistry> _mockRegistry = new();
    private readonly AgentRunner _agentRunner;

    /// <summary>Every tool result that entered conversation history, in order.</summary>
    private readonly List<LlmToolResult> _recordedToolResults = new();

    /// <summary>One snapshot per completion call, taken as the request went out.</summary>
    private readonly List<RequestSnapshot> _sentRequests = new();

    public AgentRunnerHardeningTests()
    {
        _agentRunner = new AgentRunner(_mockLlmClient.Object, Mock.Of<ILogger<AgentRunner>>());
        _mockRegistry.Setup(r => r.GetEnabledTools()).Returns(new List<LlmToolDefinition>());
    }

    #region Tool Result Cap

    [Fact]
    public async Task RunAsync_WithResultUnderCap_LeavesItByteIdentical()
    {
        const string payload = """{"answer":"short"}""";
        SetUpSingleToolRound(ToolExecutionResult.CreateSuccess(Parse(payload)));

        await _agentRunner.RunAsync("hi", NewContext(c => c.MaxToolResultChars = 8000));

        _recordedToolResults.Should().ContainSingle()
            .Which.Content.GetRawText().Should().Be(payload);
    }

    [Fact]
    public async Task RunAsync_WithResultOverCap_ReplacesItWithTheTruncationEnvelope()
    {
        var payload = $$"""{"text":"{{new string('x', 500)}}"}""";
        SetUpSingleToolRound(ToolExecutionResult.CreateSuccess(Parse(payload)));

        await _agentRunner.RunAsync("hi", NewContext(c => c.MaxToolResultChars = 100));

        var content = _recordedToolResults.Should().ContainSingle().Subject.Content;
        content.GetProperty("truncated").GetBoolean().Should().BeTrue();
        content.GetProperty("shown_chars").GetInt32().Should().Be(100);
        content.GetProperty("total_chars").GetInt32().Should().Be(payload.Length);
        content.GetProperty("content").GetString().Should().Be(payload[..100]);
        // The instruction matters as much as the cap: without it the model re-calls the same tool
        // hoping for the rest, which is the loop the duplicate guard then has to refuse.
        content.GetProperty("message").GetString().Should().Be(ToolResultLimiter.TruncationMessage);
    }

    [Fact]
    public async Task RunAsync_WithCapDisabled_LeavesAnOversizedResultAlone()
    {
        var payload = $$"""{"text":"{{new string('x', 500)}}"}""";
        SetUpSingleToolRound(ToolExecutionResult.CreateSuccess(Parse(payload)));

        await _agentRunner.RunAsync("hi", NewContext(c => c.MaxToolResultChars = 0));

        _recordedToolResults.Should().ContainSingle()
            .Which.Content.GetRawText().Should().Be(payload);
    }

    [Fact]
    public async Task RunAsync_WithOversizedToolError_CapsTheErrorToo()
    {
        SetUpSingleToolRound(ToolExecutionResult.CreateError(new string('e', 500)));

        await _agentRunner.RunAsync("hi", NewContext(c => c.MaxToolResultChars = 100));

        var result = _recordedToolResults.Should().ContainSingle().Subject;
        result.Content.GetProperty("truncated").GetBoolean().Should().BeTrue();
        result.IsError.Should().BeTrue();
    }

    #endregion

    #region Tool Execution Deadline

    [Fact]
    public async Task RunAsync_WhenToolOverrunsItsDeadline_ReturnsDirectiveResultAndContinues()
    {
        SetUpToolRounds(finalText: "Answered without it.");
        _mockRegistry
            .Setup(r => r.ExecuteToolAsync(
                It.IsAny<string>(), It.IsAny<JsonElement>(), It.IsAny<ToolContext>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, JsonElement _, ToolContext _, CancellationToken token) =>
            {
                await Task.Delay(Timeout.Infinite, token);
                return ToolExecutionResult.CreateSuccess(Parse("{}"));
            });

        var result = await _agentRunner.RunAsync("hi", NewContext(c => c.ToolExecutionTimeoutMs = 50));

        result.Success.Should().BeTrue();
        result.Response.Should().Be("Answered without it.");
        var content = _recordedToolResults.Should().ContainSingle().Subject.Content;
        content.GetProperty("error").GetString().Should().Contain("timed out after 50ms");
        content.GetProperty("error").GetString().Should().Contain("Do not retry it");
    }

    [Fact]
    public async Task RunAsync_WhenTheCallerCancels_StillThrowsRatherThanReportingAToolTimeout()
    {
        // The caller's cancellation (the interaction expiring, the host shutting down) must not be
        // laundered into a tool result the way the tool's own deadline is.
        using var cts = new CancellationTokenSource();
        SetUpToolRounds(finalText: "unreachable");
        _mockRegistry
            .Setup(r => r.ExecuteToolAsync(
                It.IsAny<string>(), It.IsAny<JsonElement>(), It.IsAny<ToolContext>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, JsonElement _, ToolContext _, CancellationToken token) =>
            {
                cts.Cancel();
                await Task.Delay(Timeout.Infinite, token);
                return ToolExecutionResult.CreateSuccess(Parse("{}"));
            });

        var run = () => _agentRunner.RunAsync("hi", NewContext(c => c.ToolExecutionTimeoutMs = 30_000), cts.Token);

        await run.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RunAsync_WithDeadlineDisabled_LetsASlowToolFinish()
    {
        SetUpToolRounds(finalText: "Done.");
        _mockRegistry
            .Setup(r => r.ExecuteToolAsync(
                It.IsAny<string>(), It.IsAny<JsonElement>(), It.IsAny<ToolContext>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, JsonElement _, ToolContext _, CancellationToken token) =>
            {
                await Task.Delay(20, token);
                return ToolExecutionResult.CreateSuccess(Parse("""{"ok":true}"""));
            });

        var result = await _agentRunner.RunAsync("hi", NewContext(c => c.ToolExecutionTimeoutMs = 0));

        result.Success.Should().BeTrue();
        _recordedToolResults.Should().ContainSingle()
            .Which.Content.GetRawText().Should().Be("""{"ok":true}""");
    }

    #endregion

    #region Duplicate Call Guard

    [Fact]
    public async Task RunAsync_WhenTheSameCallRepeatsPastTheLimit_RefusesWithoutEnteringTheTool()
    {
        SetUpRepeatedToolRounds("""{"topic":"audio"}""", rounds: 4, finalText: "Here is what I found.");
        var executions = 0;
        _mockRegistry
            .Setup(r => r.ExecuteToolAsync(
                It.IsAny<string>(), It.IsAny<JsonElement>(), It.IsAny<ToolContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                executions++;
                return ToolExecutionResult.CreateSuccess(Parse("""{"ok":true}"""));
            });

        var result = await _agentRunner.RunAsync("hi", NewContext(c => c.DuplicateToolCallLimit = 2));

        executions.Should().Be(2, "the third identical call is refused before the tool runs");
        result.TotalToolCalls.Should().Be(2, "a refused call never executed, so it is not counted");
        result.ToolNames.Should().HaveCount(2);

        var refusals = _recordedToolResults.Skip(2).ToList();
        refusals.Should().HaveCount(2);
        foreach (var refusal in refusals)
        {
            refusal.Content.GetProperty("error").GetString().Should().Be("repeated_call");
            refusal.Content.GetProperty("message").GetString().Should().Contain("2 time(s) in this run");
            // A directive, not a fault: flagging it would inflate tool-error metrics and prepend
            // "Error: " on the wire, which reads as a malfunction rather than an instruction.
            refusal.IsError.Should().BeFalse();
        }
    }

    [Fact]
    public async Task RunAsync_KeysDuplicatesIgnoringPropertyOrder()
    {
        SetUpToolRounds(
            finalText: "Done.",
            argumentRounds: new[] { """{"a":1,"b":2}""", """{"b":2,"a":1}""" });
        var executions = 0;
        _mockRegistry
            .Setup(r => r.ExecuteToolAsync(
                It.IsAny<string>(), It.IsAny<JsonElement>(), It.IsAny<ToolContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                executions++;
                return ToolExecutionResult.CreateSuccess(Parse("""{"ok":true}"""));
            });

        await _agentRunner.RunAsync("hi", NewContext(c => c.DuplicateToolCallLimit = 1));

        executions.Should().Be(1, "re-ordered properties are the same call");
        _recordedToolResults[1].Content.GetProperty("error").GetString().Should().Be("repeated_call");
    }

    [Fact]
    public async Task RunAsync_WithDifferentArguments_DoesNotRefuse()
    {
        SetUpToolRounds(
            finalText: "Done.",
            argumentRounds: new[] { """{"topic":"audio"}""", """{"topic":"moderation"}""" });
        var executions = 0;
        _mockRegistry
            .Setup(r => r.ExecuteToolAsync(
                It.IsAny<string>(), It.IsAny<JsonElement>(), It.IsAny<ToolContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                executions++;
                return ToolExecutionResult.CreateSuccess(Parse("""{"ok":true}"""));
            });

        await _agentRunner.RunAsync("hi", NewContext(c => c.DuplicateToolCallLimit = 1));

        executions.Should().Be(2);
        _recordedToolResults.Select(r => r.Content.GetRawText())
            .Should().AllBe("""{"ok":true}""");
    }

    [Fact]
    public async Task RunAsync_WithGuardDisabled_RepeatsTheCallFreely()
    {
        SetUpRepeatedToolRounds("""{"topic":"audio"}""", rounds: 3, finalText: "Done.");
        var executions = 0;
        _mockRegistry
            .Setup(r => r.ExecuteToolAsync(
                It.IsAny<string>(), It.IsAny<JsonElement>(), It.IsAny<ToolContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                executions++;
                return ToolExecutionResult.CreateSuccess(Parse("""{"ok":true}"""));
            });

        await _agentRunner.RunAsync("hi", NewContext(c => c.DuplicateToolCallLimit = 0));

        executions.Should().Be(3);
    }

    #endregion

    #region Budget Wrap-Up And Blank-Text Recovery

    [Fact]
    public async Task RunAsync_WhenTheBudgetRunsOut_AnswersFromWhatItHasWithANotice()
    {
        SetUpEndlessToolUse(wrapUpText: "Audio settings live on the portal's audio page.");

        var result = await _agentRunner.RunAsync("hi", NewContext(c => c.MaxToolCallIterations = 2));

        result.Success.Should().BeTrue();
        result.StoppedOnMaxIterations.Should().BeTrue();
        result.Response.Should().StartWith(AgentRunner.MaxIterationsNotice);
        result.Response.Should().EndWith("Audio settings live on the portal's audio page.");
        // Two loop iterations plus the wrap-up completion, which the usage ledger bills for.
        result.LoopCount.Should().Be(3);
    }

    [Fact]
    public async Task RunAsync_WhenTheBudgetRunsOut_SendsToolChoiceNoneWithTheToolsStillAttached()
    {
        // Dropping the tools would change the serialized prefix and throw away the cached tool
        // schemas for this call, which costs more than the schemas do.
        _mockRegistry.Setup(r => r.GetEnabledTools()).Returns(new List<LlmToolDefinition>
        {
            new() { Name = "lookup", Description = "Look something up", InputSchema = Parse("{}") },
        });
        SetUpEndlessToolUse(wrapUpText: "Partial answer.");

        await _agentRunner.RunAsync("hi", NewContext(c => c.MaxToolCallIterations = 1));

        var wrapUpRequest = _sentRequests.Last();
        wrapUpRequest.ToolChoice.Should().Be("none");
        wrapUpRequest.Tools.Should().NotBeNullOrEmpty();
        wrapUpRequest.Messages.Last().Content.Should().Contain("run out of tool-use steps");
    }

    [Fact]
    public async Task RunAsync_WhenTheWrapUpCallFails_FallsBackToTheBudgetError()
    {
        SetUpEndlessToolUse(wrapUpText: null);

        var result = await _agentRunner.RunAsync("hi", NewContext(c => c.MaxToolCallIterations = 2));

        result.Success.Should().BeFalse();
        result.StoppedOnMaxIterations.Should().BeTrue();
        result.ErrorMessage.Should().Contain("Exceeded maximum tool call iterations (2)");
    }

    [Fact]
    public async Task RunAsync_WhenTheModelEndsItsTurnBlank_AsksOnceForWordsAndUsesThem()
    {
        var calls = 0;
        _mockLlmClient
            .Setup(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmRequest request, CancellationToken _) =>
            {
                _sentRequests.Add(Snapshot(request));
                return new LlmResponse
                {
                    Success = true,
                    Content = ++calls == 1 ? "   " : "Here is the answer.",
                    StopReason = LlmStopReason.EndTurn,
                    Usage = new LlmUsage { InputTokens = 10, OutputTokens = 5 },
                };
            });

        var result = await _agentRunner.RunAsync("hi", NewContext());

        result.Success.Should().BeTrue();
        result.Response.Should().Be("Here is the answer.");
        result.StoppedOnMaxIterations.Should().BeFalse();
        calls.Should().Be(2, "exactly one recovery call, never two");
        _sentRequests.Last().ToolChoice.Should().Be("none");
        result.TotalUsage.InputTokens.Should().Be(20, "the recovery call's usage is billed like any other");
    }

    [Fact]
    public async Task RunAsync_WhenTheRecoveryCallIsAlsoBlank_ReturnsTheEmptyReplyWithoutRetrying()
    {
        var calls = 0;
        _mockLlmClient
            .Setup(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmRequest request, CancellationToken _) =>
            {
                _sentRequests.Add(Snapshot(request));
                calls++;
                return new LlmResponse
                {
                    Success = true,
                    Content = null,
                    StopReason = LlmStopReason.EndTurn,
                    Usage = new LlmUsage { InputTokens = 10, OutputTokens = 5 },
                };
            });

        var result = await _agentRunner.RunAsync("hi", NewContext());

        result.Success.Should().BeTrue();
        result.Response.Should().BeEmpty();
        calls.Should().Be(2);
    }

    #endregion

    #region Helpers

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();

    /// <summary>
    /// What one outgoing request looked like. The runner mutates a single request object across
    /// iterations, so anything asserted on has to be copied out as the call is made.
    /// </summary>
    private sealed record RequestSnapshot(
        string? ToolChoice,
        IReadOnlyList<LlmToolDefinition>? Tools,
        IReadOnlyList<LlmMessage> Messages);

    private static RequestSnapshot Snapshot(LlmRequest request) =>
        new(request.ToolChoice, request.Tools?.ToList(), request.Messages.ToList());

    /// <summary>
    /// A model that asks for the same tool forever, so the run always ends on its budget. The
    /// wrap-up call is answered with <paramref name="wrapUpText"/>, or refused when it is null.
    /// </summary>
    private void SetUpEndlessToolUse(string? wrapUpText)
    {
        _mockRegistry
            .Setup(r => r.ExecuteToolAsync(
                It.IsAny<string>(), It.IsAny<JsonElement>(), It.IsAny<ToolContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolExecutionResult.CreateSuccess(Parse("""{"ok":true}""")));

        _mockLlmClient
            .Setup(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmRequest request, CancellationToken _) =>
            {
                _sentRequests.Add(Snapshot(request));

                // tool_choice is what distinguishes the wrap-up call from a loop iteration.
                if (request.ToolChoice == "none")
                {
                    return wrapUpText is null
                        ? new LlmResponse { Success = false, ErrorMessage = "provider unavailable" }
                        : new LlmResponse
                        {
                            Success = true,
                            Content = wrapUpText,
                            StopReason = LlmStopReason.EndTurn,
                            Usage = new LlmUsage { InputTokens = 10, OutputTokens = 5 },
                        };
                }

                return new LlmResponse
                {
                    Success = true,
                    StopReason = LlmStopReason.ToolUse,
                    ToolCalls = new List<LlmToolCall>
                    {
                        new() { Id = $"call-{_sentRequests.Count}", Name = "lookup", Input = Parse("{}") },
                    },
                    Usage = new LlmUsage { InputTokens = 10, OutputTokens = 5 },
                };
            });
    }

    private AgentContext NewContext(Action<AgentContext>? configure = null)
    {
        var context = new AgentContext
        {
            SystemPrompt = "You are a helpful assistant.",
            ToolRegistry = _mockRegistry.Object,
            ExecutionContext = new ToolContext { UserId = 1, GuildId = 2 },
            MaxToolCallIterations = 10,
            // Off unless the test under way turns one on, so each guard is exercised alone.
            MaxToolResultChars = 0,
            ToolExecutionTimeoutMs = 0,
            DuplicateToolCallLimit = 0,
        };

        configure?.Invoke(context);
        return context;
    }

    /// <summary>One tool round with an empty argument object, then a final answer.</summary>
    private void SetUpSingleToolRound(ToolExecutionResult executionResult)
    {
        SetUpToolRounds(finalText: "Done.");
        _mockRegistry
            .Setup(r => r.ExecuteToolAsync(
                It.IsAny<string>(), It.IsAny<JsonElement>(), It.IsAny<ToolContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(executionResult);
    }

    /// <summary>The same arguments <paramref name="rounds"/> times, then a final answer.</summary>
    private void SetUpRepeatedToolRounds(string arguments, int rounds, string finalText) =>
        SetUpToolRounds(finalText, Enumerable.Repeat(arguments, rounds).ToArray());

    /// <summary>
    /// Scripts the LLM: one tool-use response per entry in <paramref name="argumentRounds"/>,
    /// then an <see cref="LlmStopReason.EndTurn"/> answer. Every tool result that reaches
    /// conversation history is recorded as the request for the next round goes out.
    /// </summary>
    private void SetUpToolRounds(string finalText, string[]? argumentRounds = null)
    {
        argumentRounds ??= new[] { "{}" };
        var round = 0;

        _mockLlmClient
            .Setup(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmRequest request, CancellationToken _) =>
            {
                // The runner mutates one request object across iterations, so results are
                // snapshotted as each request goes out rather than read back afterwards.
                _recordedToolResults.Clear();
                _recordedToolResults.AddRange(request.Messages
                    .Where(m => m.ToolResults is { Count: > 0 })
                    .SelectMany(m => m.ToolResults!));

                if (round >= argumentRounds.Length)
                {
                    return new LlmResponse
                    {
                        Success = true,
                        Content = finalText,
                        StopReason = LlmStopReason.EndTurn,
                        Usage = new LlmUsage { InputTokens = 10, OutputTokens = 5 },
                    };
                }

                var arguments = argumentRounds[round++];
                return new LlmResponse
                {
                    Success = true,
                    StopReason = LlmStopReason.ToolUse,
                    ToolCalls = new List<LlmToolCall>
                    {
                        new() { Id = $"call-{round}", Name = "lookup", Input = Parse(arguments) },
                    },
                    Usage = new LlmUsage { InputTokens = 10, OutputTokens = 5 },
                };
            });
    }

    #endregion
}
