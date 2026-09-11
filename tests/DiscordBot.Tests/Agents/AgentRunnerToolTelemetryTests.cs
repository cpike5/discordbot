using System.Diagnostics;
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
/// Unit tests for the per-tool spans the loop emits. The outcome tag is the whole point of these:
/// house style reports an expected tool failure as a successful result, so a span that only read
/// <c>ToolExecutionResult.Success</c> would show a clean trace while most calls were failing.
/// </summary>
public class AgentRunnerToolTelemetryTests : IDisposable
{
    private readonly Mock<ILlmClient> _mockLlmClient = new();
    private readonly Mock<IToolRegistry> _mockRegistry = new();
    private readonly AgentRunner _agentRunner;
    private readonly ActivityListener _listener;
    private readonly List<Activity> _activities = new();

    /// <summary>
    /// Unique per test instance. An <see cref="ActivityListener"/> is process-wide, so in a full
    /// parallel run this listener also sees the tool spans other test classes emit; filtering on a
    /// name only this instance uses is what keeps the assertions about this run's spans.
    /// </summary>
    private readonly string _toolName = "lookup_" + Guid.NewGuid().ToString("N");

    public AgentRunnerToolTelemetryTests()
    {
        _agentRunner = new AgentRunner(_mockLlmClient.Object, Mock.Of<ILogger<AgentRunner>>());
        _mockRegistry.Setup(r => r.GetEnabledTools()).Returns(new List<LlmToolDefinition>());
        _mockRegistry.Setup(r => r.FindProviderName(It.IsAny<string>())).Returns("Documentation");

        // Without a listener that samples AllData, StartActivity returns null and nothing is
        // recorded - which is also why the runner must tolerate a null activity.
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AgentsActivitySource.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                if (activity.GetTagItem(AgentsActivitySource.ToolTags.Name)?.ToString() != _toolName)
                {
                    return;
                }

                // Other test classes' runs stop spans on their own threads.
                lock (_activities)
                {
                    _activities.Add(activity);
                }
            }
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();

    private List<Activity> ToolSpans
    {
        get
        {
            lock (_activities)
            {
                return _activities.ToList();
            }
        }
    }

    private Activity ToolSpan => ToolSpans.Should().ContainSingle().Subject;

    private static string? Tag(Activity activity, string name) =>
        activity.GetTagItem(name)?.ToString();

    [Fact]
    public async Task RunAsync_TagsTheSpanWithTheToolNameCallIdAndProvider()
    {
        SetUpSingleToolRound(ToolExecutionResult.CreateSuccess(Parse("""{"count":1}""")));

        await _agentRunner.RunAsync("hi", NewContext());

        var span = ToolSpan;
        span.OperationName.Should().Be(AgentsActivitySource.ToolActivityPrefix + _toolName);
        Tag(span, AgentsActivitySource.ToolTags.Name).Should().Be(_toolName);
        Tag(span, AgentsActivitySource.ToolTags.CallId).Should().Be("call-1");
        Tag(span, AgentsActivitySource.ToolTags.Provider).Should().Be("Documentation");
    }

    [Fact]
    public async Task RunAsync_RecordsOk_AndTheCharsThatEnteredHistory()
    {
        const string payload = """{"count":1}""";
        SetUpSingleToolRound(ToolExecutionResult.CreateSuccess(Parse(payload)));

        await _agentRunner.RunAsync("hi", NewContext());

        var span = ToolSpan;
        Tag(span, AgentsActivitySource.ToolTags.Outcome).Should().Be(ToolOutcomes.Ok);
        Tag(span, AgentsActivitySource.ToolTags.ResultChars).Should().Be(payload.Length.ToString());
        span.Status.Should().Be(ActivityStatusCode.Unset);
    }

    [Fact]
    public async Task RunAsync_MeasuresResultCharsAfterTheCap()
    {
        // The cap is what the run actually pays for on every later iteration, so that is the number
        // worth having in the trace.
        var payload = $$"""{"text":"{{new string('x', 500)}}"}""";
        SetUpSingleToolRound(ToolExecutionResult.CreateSuccess(Parse(payload)));

        await _agentRunner.RunAsync("hi", NewContext(c => c.MaxToolResultChars = 100));

        int.Parse(Tag(ToolSpan, AgentsActivitySource.ToolTags.ResultChars)!)
            .Should().BeLessThan(payload.Length);
    }

    [Fact]
    public async Task RunAsync_RecordsFailedResult_WhenASuccessfulResultReportsAnExpectedFailure()
    {
        SetUpSingleToolRound(ToolExecutionResult.CreateSuccess(Parse("""{"found":false}""")));

        await _agentRunner.RunAsync("hi", NewContext());

        var span = ToolSpan;
        Tag(span, AgentsActivitySource.ToolTags.Outcome).Should().Be(ToolOutcomes.FailedResult);
        // An expected failure is normal traffic, not a malfunction - it must not inflate the error
        // rate a trace backend computes from span status.
        span.Status.Should().Be(ActivityStatusCode.Unset);
    }

    [Fact]
    public async Task RunAsync_RecordsError_WhenTheToolReturnsAFailedExecutionResult()
    {
        SetUpSingleToolRound(ToolExecutionResult.CreateError("boom"));

        await _agentRunner.RunAsync("hi", NewContext());

        Tag(ToolSpan, AgentsActivitySource.ToolTags.Outcome).Should().Be(ToolOutcomes.Error);
    }

    [Fact]
    public async Task RunAsync_RecordsError_WhenTheToolThrows()
    {
        SetUpToolRounds("Done.");
        _mockRegistry
            .Setup(r => r.ExecuteToolAsync(
                It.IsAny<string>(), It.IsAny<JsonElement>(), It.IsAny<ToolContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        await _agentRunner.RunAsync("hi", NewContext());

        var span = ToolSpan;
        Tag(span, AgentsActivitySource.ToolTags.Outcome).Should().Be(ToolOutcomes.Error);
        Tag(span, AgentsActivitySource.ToolTags.FailureCode).Should().Be(nameof(InvalidOperationException));
        span.Status.Should().Be(ActivityStatusCode.Error);
    }

    [Fact]
    public async Task RunAsync_RecordsUnknownTool_WhenTheRegistryRefusesTheName()
    {
        // What a FilteredToolRegistry throws when the model calls a tool this scope is not allowed.
        SetUpToolRounds("Done.");
        _mockRegistry
            .Setup(r => r.ExecuteToolAsync(
                It.IsAny<string>(), It.IsAny<JsonElement>(), It.IsAny<ToolContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotSupportedException($"Tool '{_toolName}' is not enabled in this scope"));

        await _agentRunner.RunAsync("hi", NewContext());

        var span = ToolSpan;
        Tag(span, AgentsActivitySource.ToolTags.Outcome).Should().Be(ToolOutcomes.UnknownTool);
        span.Status.Should().Be(ActivityStatusCode.Error);
    }

    [Fact]
    public async Task RunAsync_RecordsTimeout_SeparatelyFromAToolThatFailedOnItsOwnTerms()
    {
        SetUpToolRounds("Done.");
        _mockRegistry
            .Setup(r => r.ExecuteToolAsync(
                It.IsAny<string>(), It.IsAny<JsonElement>(), It.IsAny<ToolContext>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, JsonElement _, ToolContext _, CancellationToken ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return ToolExecutionResult.CreateSuccess(Parse("{}"));
            });

        await _agentRunner.RunAsync("hi", NewContext(c => c.ToolExecutionTimeoutMs = 50));

        var span = ToolSpan;
        Tag(span, AgentsActivitySource.ToolTags.Outcome).Should().Be(ToolOutcomes.Timeout);
        Tag(span, AgentsActivitySource.ToolTags.FailureCode).Should().Be("50ms");
        // Abandoning a slow tool is a designed outcome, not a fault.
        span.Status.Should().Be(ActivityStatusCode.Unset);
    }

    [Fact]
    public async Task RunAsync_RecordsRepeatedCall_ForARefusedDuplicate()
    {
        SetUpToolRounds(finalText: "Done.", argumentRounds: new[] { """{"q":"x"}""", """{"q":"x"}""" });
        _mockRegistry
            .Setup(r => r.ExecuteToolAsync(
                It.IsAny<string>(), It.IsAny<JsonElement>(), It.IsAny<ToolContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolExecutionResult.CreateSuccess(Parse("""{"count":1}""")));

        await _agentRunner.RunAsync("hi", NewContext(c => c.DuplicateToolCallLimit = 1));

        var outcomes = ToolSpans
            .Select(a => Tag(a, AgentsActivitySource.ToolTags.Outcome))
            .ToList();

        // A refused repeat still gets a span: it is the thing you go looking for when a run burns
        // its budget without progressing.
        outcomes.Should().Equal(ToolOutcomes.Ok, ToolOutcomes.RepeatedCall);
    }

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private AgentContext NewContext(Action<AgentContext>? configure = null)
    {
        var context = new AgentContext
        {
            SystemPrompt = "You are a helpful assistant.",
            ToolRegistry = _mockRegistry.Object,
            ExecutionContext = new ToolContext { UserId = 1, GuildId = 2 },
            MaxToolCallIterations = 10,
            MaxToolResultChars = 0,
            ToolExecutionTimeoutMs = 0,
            DuplicateToolCallLimit = 0,
        };

        configure?.Invoke(context);
        return context;
    }

    private void SetUpSingleToolRound(ToolExecutionResult executionResult)
    {
        SetUpToolRounds(finalText: "Done.");
        _mockRegistry
            .Setup(r => r.ExecuteToolAsync(
                It.IsAny<string>(), It.IsAny<JsonElement>(), It.IsAny<ToolContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(executionResult);
    }

    private void SetUpToolRounds(string finalText, string[]? argumentRounds = null)
    {
        argumentRounds ??= new[] { "{}" };
        var round = 0;

        _mockLlmClient
            .Setup(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmRequest _, CancellationToken _) =>
            {
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
                        new() { Id = $"call-{round}", Name = _toolName, Input = Parse(arguments) },
                    },
                    Usage = new LlmUsage { InputTokens = 10, OutputTokens = 5 },
                };
            });
    }
}
