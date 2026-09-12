using DiscordBot.Agents;
using DiscordBot.Agents.Abstractions;
using DiscordBot.Agents.Contracts;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs.Llm.Reporting;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Infrastructure.Services.LLM;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services.LLM;

/// <summary>
/// Tests for the skill-aware parts of <see cref="GuildAssistantContext"/> and
/// <see cref="DmAssistantContext"/>: the roster each appends to its system prompt, and the DM
/// surface's persistence of a turn's activations.
/// </summary>
public class AssistantContextSkillTests
{
    private const ulong TestUserId = 111222333UL;
    private const ulong TestGuildId = 123456789UL;

    private const string RenderedGuildPrompt = "You are the guild assistant.";
    private const string DmPromptTemplate = "You are the owner's assistant.";

    private static AgentSkill Skill(string key, string summary, string instructions, params string[] tools) => new()
    {
        Key = key,
        Summary = summary,
        Instructions = instructions,
        Tools = tools
    };

    #region Guild

    private static GuildAssistantContext BuildGuildContext(ISkillActivationState? skills)
    {
        var mockPromptTemplate = new Mock<IPromptTemplate>();
        mockPromptTemplate
            .Setup(p => p.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("template");
        mockPromptTemplate
            .Setup(p => p.Render(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
            .Returns(RenderedGuildPrompt);

        var options = new AssistantOptions
        {
            IncludeGuildContext = false,
            Cost = new() { EnableCostTracking = false },
            Privacy = new() { LogInteractions = false }
        };

        return new GuildAssistantContext(
            TestGuildId,
            channelId: 1,
            TestUserId,
            messageId: 1,
            rateLimit: 5,
            question: "question",
            callerCanMutate: false,
            toolRegistry: null,
            Mock.Of<IGuildService>(),
            mockPromptTemplate.Object,
            Mock.Of<IAssistantUsageMetricsRepository>(),
            Mock.Of<IAssistantInteractionLogRepository>(),
            options,
            Mock.Of<ILogger>(),
            "test/model",
            resolvedPricing: null,
            usageRecorder: null,
            skills: skills);
    }

    [Fact]
    public async Task GuildBuildSystemPromptAsync_AppendsSkillRosterAfterRenderedPrompt()
    {
        var skills = new SkillSession(new[]
        {
            Skill("moderation", "Look up moderation cases.", "Search cases before answering.", "get_cases")
        });

        var prompt = await BuildGuildContext(skills).BuildSystemPromptAsync(CancellationToken.None);

        prompt.Should().StartWith(RenderedGuildPrompt, "everything above the roster is unchanged");
        prompt.Should().Contain("## Skills");
        prompt.Should().Contain("`moderation`").And.Contain("Look up moderation cases.");

        // Only the summary is paid for on every request; the body waits for the loader.
        prompt.Should().NotContain("Search cases before answering.");
    }

    [Fact]
    public async Task GuildBuildSystemPromptAsync_WithNoSkills_IsByteIdenticalToTheNoSessionCase()
    {
        var withEmptySession = await BuildGuildContext(new SkillSession(Array.Empty<AgentSkill>()))
            .BuildSystemPromptAsync(CancellationToken.None);
        var withNoSession = await BuildGuildContext(skills: null)
            .BuildSystemPromptAsync(CancellationToken.None);

        withEmptySession.Should().Be(RenderedGuildPrompt);
        withEmptySession.Should().Be(withNoSession,
            "a surface with no skill files must cost exactly what it did before skills existed");
    }

    #endregion

    #region DM

    private static DmAssistantContext BuildDmContext(
        ISkillActivationState? skills,
        IDmSkillActivationStore? activationStore = null,
        IDmConversationMessageRepository? conversationRepo = null)
    {
        var mockPromptTemplate = new Mock<IPromptTemplate>();
        mockPromptTemplate
            .Setup(p => p.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DmPromptTemplate);

        var options = new DmAssistantOptions
        {
            EnableCostTracking = false,
            LogInteractions = false
        };

        return new DmAssistantContext(
            TestUserId,
            activeGuildId: null,
            Mock.Of<IToolRegistry>(),
            new List<LlmMessage>(),
            mockPromptTemplate.Object,
            conversationRepo ?? Mock.Of<IDmConversationMessageRepository>(),
            Mock.Of<IDmAssistantInteractionLogRepository>(),
            Mock.Of<IDmAssistantUsageMetricsRepository>(),
            options,
            Mock.Of<ILogger>(),
            "test/model",
            resolvedPricing: null,
            usageRecorder: null,
            skills: skills,
            skillActivations: activationStore);
    }

    [Fact]
    public async Task DmBuildSystemPromptAsync_AppendsSkillRoster()
    {
        var skills = new SkillSession(new[]
        {
            Skill("notes", "Manage the owner's notes.", "Search before saving.", "save_note")
        });

        var prompt = await BuildDmContext(skills).BuildSystemPromptAsync(CancellationToken.None);

        prompt.Should().StartWith(DmPromptTemplate);
        prompt.Should().Contain("## Skills").And.Contain("`notes`").And.Contain("Manage the owner's notes.");
    }

    [Fact]
    public async Task DmBuildSystemPromptAsync_WithPreActivatedSkill_IncludesItsFullInstructions()
    {
        var skills = new SkillSession(
            new[] { Skill("notes", "Manage the owner's notes.", "Search before saving, then tag the note.", "save_note") },
            preActivated: new[] { "notes" });

        var prompt = await BuildDmContext(skills).BuildSystemPromptAsync(CancellationToken.None);

        // The tool result that carried the instructions on the turn that loaded the skill is not in
        // the sliding-window history, so the prompt is what keeps them in force on later turns.
        prompt.Should().Contain("## Loaded skills");
        prompt.Should().Contain("Search before saving, then tag the note.");
    }

    [Fact]
    public async Task DmRecordUsageAsync_OnSuccess_WritesActivatedSkillKeysToTheStore()
    {
        var skills = new SkillSession(
            new[]
            {
                Skill("notes", "Manage the owner's notes.", "Search before saving."),
                Skill("weather", "Check the forecast.", "Use the forecast tool.")
            },
            preActivated: new[] { "weather" });
        skills.Activate("notes");

        var mockStore = new Mock<IDmSkillActivationStore>();
        var context = BuildDmContext(skills, mockStore.Object);

        await context.RecordUsageAsync(
            "question",
            new AssistantPipelineResult { Success = true, Response = "answer" },
            CancellationToken.None);

        mockStore.Verify(
            s => s.Set(TestUserId, It.Is<IEnumerable<string>>(keys => keys.SequenceEqual(new[] { "weather", "notes" }))),
            Times.Once);
        mockStore.Verify(s => s.Clear(It.IsAny<ulong>()), Times.Never);
    }

    [Fact]
    public async Task DmRecordUsageAsync_WhenConversationCleared_ClearsTheStore()
    {
        var skills = new SkillSession(
            new[] { Skill("notes", "Manage the owner's notes.", "Search before saving.") },
            preActivated: new[] { "notes" });

        var mockStore = new Mock<IDmSkillActivationStore>();
        var context = BuildDmContext(skills, mockStore.Object);

        await context.RecordUsageAsync(
            "/clear",
            new AssistantPipelineResult { Success = true, ConversationCleared = true },
            CancellationToken.None);

        // The instructions a loaded skill puts in the prompt are part of what "start again" means.
        mockStore.Verify(s => s.Clear(TestUserId), Times.Once);
        mockStore.Verify(s => s.Set(It.IsAny<ulong>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    #endregion
}
