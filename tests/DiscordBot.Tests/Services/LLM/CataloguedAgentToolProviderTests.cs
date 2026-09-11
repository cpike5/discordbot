using System.Text.Json;
using DiscordBot.Agents;
using DiscordBot.Agents.Abstractions;
using DiscordBot.Agents.Contracts;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Services.LLM.Providers;
using DiscordBot.Infrastructure.Services.LLM.Tools;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services.LLM;

/// <summary>
/// Unit tests for the two surface providers over individually authored tools. What they establish is
/// that <c>ToolCatalog</c> decides where a tool is advertised — which is what makes a catalogue
/// entry part of writing a tool rather than a rule beside it.
/// </summary>
public class CataloguedAgentToolProviderTests
{
    /// <summary>A tool that answers to whatever name it is constructed with.</summary>
    private sealed class NamedTool : IAgentTool
    {
        /// <summary>Needed only so the test assembly's scan can construct it; see AgentToolRegistrationTests.</summary>
        public NamedTool() : this("list_notes")
        {
        }

        public NamedTool(string name)
        {
            Definition = new LlmToolDefinition
            {
                Name = name,
                Description = $"Does {name}.",
                InputSchema = ToolInput.ObjectSchema(new { })
            };
        }

        public LlmToolDefinition Definition { get; }

        public Task<ToolExecutionResult> InvokeAsync(
            JsonElement input, ToolContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(ToolResults.Json(new { ok = true }));
    }

    // Real catalogue names: save_note is Dm-only, get_guild_info is Guild-only, and
    // get_feature_documentation is on both.
    private static IAgentTool[] Tools() =>
    [
        new NamedTool("save_note"),
        new NamedTool("get_guild_info"),
        new NamedTool("get_feature_documentation"),
        new NamedTool("not_in_the_catalogue")
    ];

    [Fact]
    public void TheDmProvider_AdvertisesTheCataloguesDmTools()
    {
        var provider = new DmAgentToolProvider(Tools(), Mock.Of<ILogger<DmAgentToolProvider>>());

        provider.GetTools().Select(t => t.Name)
            .Should().BeEquivalentTo("save_note", "get_feature_documentation");
    }

    [Fact]
    public void TheGuildProvider_AdvertisesTheCataloguesGuildTools()
    {
        var provider = new GuildAgentToolProvider(Tools(), Mock.Of<ILogger<GuildAgentToolProvider>>());

        provider.GetTools().Select(t => t.Name)
            .Should().BeEquivalentTo("get_guild_info", "get_feature_documentation");
    }

    [Fact]
    public void AnUncataloguedToolReachesNoSurface_AndIsReportedRatherThanJustMissing()
    {
        var logger = new Mock<ILogger<DmAgentToolProvider>>();

        var provider = new DmAgentToolProvider(Tools(), logger.Object);

        provider.GetTools().Select(t => t.Name).Should().NotContain("not_in_the_catalogue");
        logger.Invocations.Should().Contain(invocation =>
            invocation.Method.Name == nameof(ILogger.Log)
            && (LogLevel)invocation.Arguments[0] == LogLevel.Warning
            && invocation.Arguments[2]!.ToString()!.Contains("not_in_the_catalogue"));
    }

    [Fact]
    public async Task TheProviderRefusesAWriteInThisBotsVoice()
    {
        // The gate lives in AgentToolProvider; the wording is the bot's, so the surface providers
        // hand it ToolPermissions.MutationForbidden rather than the engine's neutral default.
        var notes = new Mock<IDmAssistantNoteRepository>();
        var provider = new DmAgentToolProvider(
            new IAgentTool[] { new SaveNoteTool(notes.Object, Mock.Of<ILogger<SaveNoteTool>>()) },
            Mock.Of<ILogger<DmAgentToolProvider>>());

        var result = await provider.ExecuteToolAsync(
            "save_note",
            JsonDocument.Parse("""{"content":"x"}""").RootElement.Clone(),
            new ToolContext { CanMutate = false });

        result.Data!.Value.GetProperty("message").GetString()
            .Should().Contain("server administrator");
        notes.VerifyNoOtherCalls();
    }

    [Fact]
    public void TheTwoSurfacesHaveDistinctProviderNames()
    {
        // FindProviderName tags every tool span with it, so they have to be tellable apart.
        new DmAgentToolProvider([], Mock.Of<ILogger<DmAgentToolProvider>>()).Name
            .Should().NotBe(new GuildAgentToolProvider([], Mock.Of<ILogger<GuildAgentToolProvider>>()).Name);
    }
}
