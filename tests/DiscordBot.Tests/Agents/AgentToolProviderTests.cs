using System.Text.Json;
using DiscordBot.Agents;
using DiscordBot.Agents.Abstractions;
using DiscordBot.Agents.Contracts;
using FluentAssertions;

namespace DiscordBot.Tests.Agents;

/// <summary>
/// Unit tests for <see cref="AgentToolProvider"/>, the one adapter between the registry's unit (a
/// provider) and the authoring unit (a tool), and the place the caller-access check is enforced.
/// </summary>
public class AgentToolProviderTests
{
    private static readonly JsonElement NoInput = JsonDocument.Parse("{}").RootElement.Clone();

    /// <summary>A tool that records whether it ran and reports its own name back.</summary>
    private sealed class FakeTool : IAgentTool
    {
        /// <summary>
        /// Exists so the container can construct this type: <c>AddAgentTools</c> scans the whole test
        /// assembly, and <c>AgentToolRegistrationTests</c> resolves everything it finds.
        /// </summary>
        public FakeTool() : this("fake")
        {
        }

        public FakeTool(string name, string? mutation = null)
        {
            Definition = new LlmToolDefinition
            {
                Name = name,
                Description = $"Does {name}.",
                InputSchema = ToolInput.ObjectSchema(new { })
            };
            Mutation = mutation;
        }

        public LlmToolDefinition Definition { get; }

        public string? Mutation { get; }

        public bool Ran { get; private set; }

        public Task<ToolExecutionResult> InvokeAsync(
            JsonElement input, ToolContext context, CancellationToken cancellationToken = default)
        {
            Ran = true;
            return Task.FromResult(ToolResults.Json(new { ran = Definition.Name }));
        }
    }

    private static AgentToolProvider Provider(
        params IAgentTool[] tools) => new("Fakes", "Fake tools", tools);

    [Fact]
    public void GetTools_AdvertisesEveryToolsDefinition()
    {
        var provider = Provider(new FakeTool("alpha"), new FakeTool("beta"));

        provider.GetTools().Select(t => t.Name).Should().Equal("alpha", "beta");
    }

    [Fact]
    public async Task ExecuteToolAsync_RoutesToTheNamedTool()
    {
        var alpha = new FakeTool("alpha");
        var beta = new FakeTool("beta");

        var result = await Provider(alpha, beta).ExecuteToolAsync("beta", NoInput, new ToolContext());

        result.Data!.Value.GetProperty("ran").GetString().Should().Be("beta");
        alpha.Ran.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteToolAsync_MatchesTheNameCaseInsensitively()
    {
        var result = await Provider(new FakeTool("alpha"))
            .ExecuteToolAsync("ALPHA", NoInput, new ToolContext());

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteToolAsync_ThrowsForAToolItDoesNotOwn()
    {
        // The registry's contract: NotSupportedException is how a provider disclaims a tool.
        var act = () => Provider(new FakeTool("alpha"))
            .ExecuteToolAsync("gamma", NoInput, new ToolContext());

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public async Task ExecuteToolAsync_RefusesAWriteBeforeEnteringTheTool_WhenTheCallerMayNotMutate()
    {
        // Declaring Mutation is the whole of the check, so the tool must not run at all.
        var writer = new FakeTool("write_thing", mutation: "write things");

        var result = await Provider(writer)
            .ExecuteToolAsync("write_thing", NoInput, new ToolContext { CanMutate = false });

        writer.Ran.Should().BeFalse();
        result.Success.Should().BeTrue();
        result.Data!.Value.GetProperty("forbidden").GetBoolean().Should().BeTrue();
        result.Data!.Value.GetProperty("message").GetString().Should().Contain("write things");
    }

    [Fact]
    public async Task ExecuteToolAsync_RunsAWrite_WhenTheCallerMayMutate()
    {
        var writer = new FakeTool("write_thing", mutation: "write things");

        var result = await Provider(writer)
            .ExecuteToolAsync("write_thing", NoInput, new ToolContext { CanMutate = true });

        writer.Ran.Should().BeTrue();
        result.Data!.Value.TryGetProperty("forbidden", out _).Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteToolAsync_LeavesAReadAlone_WhenTheCallerMayNotMutate()
    {
        var reader = new FakeTool("read_thing");

        await Provider(reader).ExecuteToolAsync("read_thing", NoInput, new ToolContext { CanMutate = false });

        reader.Ran.Should().BeTrue();
    }

    [Fact]
    public void Mutation_DefaultsToNull_SoAToolIsReadOnlyUntilItSaysOtherwise()
    {
        new FakeTool("read_thing").Mutation.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteToolAsync_UsesTheHostsOwnRefusal_WhenOneIsSupplied()
    {
        var provider = new AgentToolProvider(
            "Fakes", "Fake tools",
            new[] { new FakeTool("write_thing", mutation: "write things") },
            action => ToolResults.Json(new { nope = action }));

        var result = await provider.ExecuteToolAsync("write_thing", NoInput, new ToolContext());

        result.Data!.Value.GetProperty("nope").GetString().Should().Be("write things");
    }

    [Fact]
    public void Constructor_RejectsTwoToolsWithOneName()
    {
        // Loud, and at construction: the registry's first-match walk would otherwise pick one of
        // them silently, and which one would depend on scan order.
        var act = () => Provider(new FakeTool("alpha"), new FakeTool("ALPHA"));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*alpha*FakeTool*");
    }
}
