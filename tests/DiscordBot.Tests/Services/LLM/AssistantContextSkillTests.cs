using cpike.Agent;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs.Llm.Reporting;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Core.Models.Llm;
using DiscordBot.Infrastructure.Services.LLM;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services.LLM;

/// <summary>
/// The skill-aware parts of the assistant surfaces: the DM context's sticky activations (replayed as
/// <see cref="DmAssistantContext.PreloadedSkills"/>, recorded after the turn), and the skill rosters
/// <see cref="CatalogPromptStore"/> gives each mode over the shipped prompt files.
/// </summary>
public class AssistantContextSkillTests
{
    private const ulong TestUserId = 111222333UL;

    #region DM context

    private static DmAssistantContext BuildDmContext(
        IReadOnlyCollection<string> preloaded,
        IDmSkillActivationStore? activations)
    {
        var options = new DmAssistantOptions
        {
            LogInteractions = false,
            EnableCostTracking = false
        };

        return new DmAssistantContext(
            TestUserId,
            activeGuildId: null,
            new List<ConversationTurn>(),
            preloaded,
            Mock.Of<IDmConversationMessageRepository>(),
            Mock.Of<IDmAssistantInteractionLogRepository>(),
            Mock.Of<IDmAssistantUsageMetricsRepository>(),
            options,
            Mock.Of<ILogger>(),
            "test/model",
            resolvedPricing: null,
            usageRecorder: null,
            skillActivations: activations);
    }

    [Fact]
    public void DmContext_CarriesTheStoredSkillsAsPreloaded()
    {
        var context = BuildDmContext(new[] { "moderation" }, Mock.Of<IDmSkillActivationStore>());

        context.PreloadedSkills.Should().Equal("moderation");
        context.ModeKey.Should().Be(DmAssistantContext.ModeName);
    }

    [Fact]
    public async Task DmRecordUsageAsync_StoresTheSkillsTheTurnEndedWith()
    {
        var activations = new Mock<IDmSkillActivationStore>();
        var context = BuildDmContext(Array.Empty<string>(), activations.Object);

        await context.RecordUsageAsync("q", new AssistantPipelineResult
        {
            Success = true,
            Response = "a",
            LoadedSkills = new List<string> { "analytics", "moderation" }
        }, CancellationToken.None);

        activations.Verify(a => a.Set(TestUserId, It.Is<IEnumerable<string>>(k =>
            k.SequenceEqual(new[] { "analytics", "moderation" }))), Times.Once);
        activations.Verify(a => a.Clear(It.IsAny<ulong>()), Times.Never);
    }

    [Fact]
    public async Task DmRecordUsageAsync_ClearsTheSkills_WhenTheConversationWasCleared()
    {
        // Clearing the conversation clears what its skills put in the prompt too.
        var activations = new Mock<IDmSkillActivationStore>();
        var context = BuildDmContext(new[] { "moderation" }, activations.Object);

        await context.RecordUsageAsync("start over", new AssistantPipelineResult
        {
            Success = true,
            Response = "Cleared.",
            ConversationCleared = true,
            LoadedSkills = new List<string> { "moderation" }
        }, CancellationToken.None);

        activations.Verify(a => a.Clear(TestUserId), Times.Once);
        activations.Verify(a => a.Set(It.IsAny<ulong>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact]
    public async Task DmRecordUsageAsync_LeavesTheSkillsAlone_WhenTheTurnFailed()
    {
        var activations = new Mock<IDmSkillActivationStore>();
        var context = BuildDmContext(new[] { "moderation" }, activations.Object);

        await context.RecordUsageAsync("q", new AssistantPipelineResult { Success = false, ErrorMessage = "boom" },
            CancellationToken.None);

        activations.Verify(a => a.Set(It.IsAny<ulong>(), It.IsAny<IEnumerable<string>>()), Times.Never);
        activations.Verify(a => a.Clear(It.IsAny<ulong>()), Times.Never);
    }

    [Fact]
    public async Task DmRecordUsageAsync_SwallowsAStoreFailure()
    {
        var activations = new Mock<IDmSkillActivationStore>();
        activations.Setup(a => a.Set(It.IsAny<ulong>(), It.IsAny<IEnumerable<string>>()))
            .Throws(new InvalidOperationException("cache down"));
        var context = BuildDmContext(Array.Empty<string>(), activations.Object);

        var act = () => context.RecordUsageAsync("q", new AssistantPipelineResult
        {
            Success = true,
            Response = "a",
            LoadedSkills = new List<string> { "analytics" }
        }, CancellationToken.None);

        await act.Should().NotThrowAsync("bookkeeping never fails the user-facing reply");
    }

    [Fact]
    public void GuildContext_PreloadsNothing()
    {
        // The guild assistant is single-turn; a skill costs a round every time it is used there.
        var context = new GuildAssistantContext(
            1, 2, TestUserId, 3, rateLimit: 5, question: "q", callerCanMutate: false, Array.Empty<string>(),
            Mock.Of<IGuildService>(), Mock.Of<IAssistantUsageMetricsRepository>(),
            Mock.Of<IAssistantInteractionLogRepository>(), new AssistantOptions(), Mock.Of<ILogger>(), "test/model");

        context.PreloadedSkills.Should().BeEmpty();
        context.ModeKey.Should().Be(GuildAssistantContext.ModeName);
    }

    #endregion

    #region Catalog prompt store

    [Fact]
    public void EveryShippedPromptFileParses()
    {
        // The markdown store logs and skips a file it cannot parse, so a broken file is a mode (or,
        // for bot.md, the base persona every run needs) that silently is not there.
        var files = Directory.EnumerateFiles(PromptFiles.PromptsDirectory, "*.md", SearchOption.TopDirectoryOnly).ToList();
        files.Should().NotBeEmpty();

        foreach (var file in files)
        {
            var act = () => cpike.Agent.Prompting.PromptFileParser.Parse(File.ReadAllText(file), Path.GetFileNameWithoutExtension(file));
            act.Should().NotThrow($"{Path.GetFileName(file)} must parse: front matter and a non-empty body");
        }
    }

    [Fact]
    public void TheBasePersonaExists_AndEverySurfaceModeResolves()
    {
        var store = PromptFiles.CatalogStore();

        store.GetDefault().Key.Should().Be("bot");
        foreach (var mode in CatalogPromptStore.SurfaceModes.Keys)
        {
            store.TryGet(mode, out _).Should().BeTrue($"mode '{mode}' must be in the shipped prompts");
        }
    }

    [Fact]
    public void DmMode_HasTheAnalyticsAndModerationSkills()
    {
        var dm = PromptFiles.CatalogStore().Get(DmAssistantContext.ModeName);

        dm.Skills.Should().BeEquivalentTo("analytics", "moderation");
    }

    [Fact]
    public void DmMode_WithholdsTheSkillToolsFromItsBaseList_AndOffersLoadSkill()
    {
        var skills = PromptFiles.SkillStore();
        var dm = PromptFiles.CatalogStore(skills).Get(DmAssistantContext.ModeName);

        var skillTools = dm.Skills
            .SelectMany(key => skills.TryGet(key, out var skill) ? skill.Tools : [])
            .ToList();

        skillTools.Should().NotBeEmpty();
        dm.Tools.Should().NotIntersectWith(skillTools,
            "a skill's tools stay out of the cached prefix until the skill is loaded");
        dm.Tools.Should().Contain("load_skill");
        dm.Tools.Should().Contain(new[] { "save_note", "list_guilds", "fetch_url" });
    }

    [Fact]
    public void DmMode_ListsEveryDmCatalogueToolExceptTheSkillOnes()
    {
        var skills = PromptFiles.SkillStore();
        var dm = PromptFiles.CatalogStore(skills).Get(DmAssistantContext.ModeName);
        var skillTools = dm.Skills.SelectMany(key => skills.TryGet(key, out var skill) ? skill.Tools : []).ToHashSet();

        var expected = ToolCatalog.ForScope(ToolScopes.Dm).Select(e => e.Name).Where(n => !skillTools.Contains(n));

        dm.Tools.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void GuildMode_HasNoSkills_AndSoNoLoadSkill()
    {
        var guild = PromptFiles.CatalogStore().Get(GuildAssistantContext.ModeName);

        guild.Skills.Should().BeEmpty();
        guild.Tools.Should().NotContain("load_skill");
        guild.Tools.Should().BeEquivalentTo(
            ToolCatalog.ForScope(ToolScopes.Guild).Select(e => e.Name).Where(n => n != "load_skill"));
    }

    [Fact]
    public void FeatureRequestMode_OffersOnlySubmitFeatureRequest()
    {
        var mode = PromptFiles.CatalogStore().Get(CatalogPromptStore.FeatureRequestModeName);

        mode.Tools.Should().Equal("submit_feature_request");
    }

    [Fact]
    public void TheStore_RendersTheBaseUrl()
    {
        var store = PromptFiles.CatalogStore(baseUrl: "https://bot.example.com/");

        foreach (var key in store.Keys)
        {
            store.Get(key).Template.Should().NotContain("{{BASE_URL}}");
        }
    }

    #endregion
}
