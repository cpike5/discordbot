using DiscordBot.Agents;
using DiscordBot.Agents.Abstractions;
using DiscordBot.Agents.Contracts;
using DiscordBot.Agents.Contracts.Enums;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs.Llm.Reporting;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Services.LLM;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services.LLM;

/// <summary>
/// <see cref="GuildAssistantContext"/> on a thread turn: the prompt paragraph switches, the turn
/// is saved and trimmed, the loaded skills are carried to the next turn, and the interaction log
/// row points at the thread. A single reply changes none of that.
/// </summary>
public class GuildAssistantContextThreadTests
{
    private const ulong GuildId = 100UL;
    private const ulong UserId = 300UL;
    private const ulong ThreadId = 777UL;

    private readonly Mock<IPromptTemplate> _promptTemplate = new();
    private readonly Mock<IAssistantThreadRepository> _threads = new();
    private readonly Mock<IAssistantThreadMessageRepository> _threadMessages = new();
    private readonly Mock<IAssistantInteractionLogRepository> _interactionLogs = new();
    private Dictionary<string, string>? _renderedVariables;

    public GuildAssistantContextThreadTests()
    {
        _promptTemplate
            .Setup(p => p.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("template");
        _promptTemplate
            .Setup(p => p.Render(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
            .Callback<string, Dictionary<string, string>>((_, v) => _renderedVariables = v)
            .Returns("rendered");
        _threadMessages
            .Setup(r => r.AddAsync(It.IsAny<AssistantThreadMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssistantThreadMessage m, CancellationToken _) => m);
        _interactionLogs
            .Setup(r => r.AddAsync(It.IsAny<AssistantInteractionLog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssistantInteractionLog l, CancellationToken _) => l);
    }

    private static AssistantThread Thread() => new()
    {
        ThreadId = ThreadId,
        GuildId = GuildId,
        ParentChannelId = 55,
        StarterUserId = UserId,
        CreatedAt = DateTime.UtcNow.AddMinutes(-5),
        LastActivityAt = DateTime.UtcNow.AddMinutes(-5),
        TurnCount = 2
    };

    private GuildAssistantContext Build(
        AssistantThread? thread,
        ISkillActivationState? skills = null,
        List<LlmMessage>? history = null,
        bool logInteractions = false)
    {
        var options = new AssistantOptions
        {
            IncludeGuildContext = false,
            Cost = new() { EnableCostTracking = false },
            Privacy = new() { LogInteractions = logInteractions },
            Threads = new() { MaxConversationMessages = 6 }
        };

        return new GuildAssistantContext(
            GuildId,
            channelId: thread?.ThreadId ?? 1,
            UserId,
            messageId: 9,
            rateLimit: 5,
            question: "what next?",
            callerCanMutate: false,
            toolRegistry: null,
            Mock.Of<IGuildService>(),
            _promptTemplate.Object,
            Mock.Of<IAssistantUsageMetricsRepository>(),
            _interactionLogs.Object,
            options,
            Mock.Of<ILogger>(),
            "test/model",
            resolvedPricing: null,
            usageRecorder: null,
            skills: skills,
            thread: thread,
            conversationHistory: history,
            threads: _threads.Object,
            threadMessages: _threadMessages.Object);
    }

    private static AssistantPipelineResult Success(string response = "here you go") => new()
    {
        Success = true,
        Response = response
    };

    [Fact]
    public async Task SingleReply_RendersTheNoHistoryParagraph_AndHasNoHistory()
    {
        var context = Build(thread: null);

        await context.BuildSystemPromptAsync(CancellationToken.None);

        _renderedVariables![GuildAssistantContext.ConversationModeVariable]
            .Should().Be(GuildAssistantContext.SingleReplyParagraph);
        context.ConversationHistory.Should().BeEmpty();
        context.ThreadId.Should().BeNull();
    }

    [Fact]
    public async Task ThreadTurn_RendersTheThreadParagraph_AndCarriesTheSeededHistory()
    {
        var history = new List<LlmMessage>
        {
            new() { Role = LlmRole.User, Content = "first" },
            new() { Role = LlmRole.Assistant, Content = "answer" }
        };
        var context = Build(Thread(), history: history);

        await context.BuildSystemPromptAsync(CancellationToken.None);

        _renderedVariables![GuildAssistantContext.ConversationModeVariable]
            .Should().Be(GuildAssistantContext.ThreadParagraph);
        context.ConversationHistory.Should().BeSameAs(history);
        context.ThreadId.Should().Be(ThreadId);
    }

    [Fact]
    public async Task ThreadTurn_OnSuccess_SavesBothTurns_TrimsToTheWindow_AndTouchesTheThread()
    {
        var thread = Thread();
        var context = Build(thread);
        var before = DateTime.UtcNow;

        await context.RecordUsageAsync("what next?", Success("here you go"), CancellationToken.None);

        _threadMessages.Verify(r => r.AddAsync(
            It.Is<AssistantThreadMessage>(m => m.ThreadId == ThreadId && m.UserId == UserId && m.Role == "user" && m.Content == "what next?"),
            It.IsAny<CancellationToken>()), Times.Once);
        _threadMessages.Verify(r => r.AddAsync(
            It.Is<AssistantThreadMessage>(m => m.ThreadId == ThreadId && m.UserId == UserId && m.Role == "assistant" && m.Content == "here you go"),
            It.IsAny<CancellationToken>()), Times.Once);
        _threadMessages.Verify(r => r.DeleteOldestByThreadAsync(ThreadId, 6, It.IsAny<CancellationToken>()), Times.Once);

        _threads.Verify(r => r.UpdateAsync(thread, It.IsAny<CancellationToken>()), Times.Once);
        thread.TurnCount.Should().Be(3);
        thread.LastActivityAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public async Task ThreadTurn_OnSuccess_StoresTheSkillsTheRunLoaded()
    {
        var thread = Thread();
        var skills = new SkillSession(new[]
        {
            new AgentSkill { Key = "moderation", Summary = "s", Instructions = "i", Tools = Array.Empty<string>() }
        });
        skills.Activate("moderation");
        var context = Build(thread, skills);

        await context.RecordUsageAsync("q", Success(), CancellationToken.None);

        thread.GetActiveSkillsList().Should().Equal("moderation");
    }

    [Fact]
    public async Task ThreadTurn_OnFailure_SavesNothing()
    {
        var thread = Thread();
        var context = Build(thread);

        await context.RecordUsageAsync("q", new AssistantPipelineResult { Success = false, ErrorMessage = "boom" }, CancellationToken.None);

        _threadMessages.Verify(r => r.AddAsync(It.IsAny<AssistantThreadMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        _threads.Verify(r => r.UpdateAsync(It.IsAny<AssistantThread>(), It.IsAny<CancellationToken>()), Times.Never);
        thread.TurnCount.Should().Be(2);
    }

    [Fact]
    public async Task ThreadTurn_WhenTheHistoryWriteThrows_TheExchangeStillCompletes()
    {
        _threadMessages
            .Setup(r => r.AddAsync(It.IsAny<AssistantThreadMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database down"));
        var context = Build(Thread());

        var act = () => context.RecordUsageAsync("q", Success(), CancellationToken.None);

        await act.Should().NotThrowAsync("a history failure costs the next turn its memory, not the member their answer");
    }

    [Fact]
    public async Task ThreadTurn_StampsTheThreadOnTheInteractionLog()
    {
        var context = Build(Thread(), logInteractions: true);

        await context.RecordUsageAsync("q", Success(), CancellationToken.None);

        _interactionLogs.Verify(r => r.AddAsync(
            It.Is<AssistantInteractionLog>(l => l.ThreadId == ThreadId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SingleReply_OnSuccess_TouchesNoThreadTables_AndLogsNoThread()
    {
        var context = Build(thread: null, logInteractions: true);

        await context.RecordUsageAsync("q", Success(), CancellationToken.None);

        _threadMessages.VerifyNoOtherCalls();
        _threads.VerifyNoOtherCalls();
        _interactionLogs.Verify(r => r.AddAsync(
            It.Is<AssistantInteractionLog>(l => l.ThreadId == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
