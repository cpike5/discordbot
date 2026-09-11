using DiscordBot.Agents;
using DiscordBot.Agents.Contracts;
using FluentAssertions;

namespace DiscordBot.Tests.Agents;

/// <summary>
/// Unit tests for <see cref="SkillRoster"/> — the skills block that goes into the system prompt: a
/// one-line-per-skill roster of what can be loaded, and the full instructions of anything already
/// loaded. This is the expensive kind of prompt surface, so the tests care about two things the
/// rendered text has to get right every time: that it is empty when there is nothing to say (a
/// surface with no skills must cost nothing), and that its wording and order are stable.
/// </summary>
public class SkillRosterTests
{
    private static AgentSkill Skill(string key, string summary, string instructions) =>
        new() { Key = key, Summary = summary, Instructions = instructions };

    private static SkillSession Session(
        IEnumerable<AgentSkill> skills,
        IEnumerable<string>? preActivated = null,
        string loaderToolName = SkillSession.DefaultLoaderToolName) =>
        new(skills, preActivated, loaderToolName);

    #region Nothing To Render

    [Fact]
    public void Render_WithNoSkillState_ReturnsEmpty()
    {
        SkillRoster.Render(null).Should().BeEmpty();
    }

    [Fact]
    public void Render_WithAnEmptySession_ReturnsEmpty()
    {
        SkillRoster.Render(Session(Array.Empty<AgentSkill>())).Should().BeEmpty();
    }

    #endregion

    #region Loadable Roster

    [Fact]
    public void Render_ListsOneLinePerLoadableSkill_InKeyOrder()
    {
        var session = Session(new[]
        {
            Skill("zulu", "Last alphabetically.", "Zulu instructions."),
            Skill("alpha", "First alphabetically.", "Alpha instructions.")
        });

        var block = SkillRoster.Render(session);

        block.Should().Contain("## Skills");
        block.Should().Contain("- `alpha` — First alphabetically.\n");
        block.Should().Contain("- `zulu` — Last alphabetically.\n");
        block.IndexOf("`alpha`", StringComparison.Ordinal)
            .Should().BeLessThan(block.IndexOf("`zulu`", StringComparison.Ordinal));
        // Only the summary is paid for on every request; the instructions stay behind the loader.
        block.Should().NotContain("Alpha instructions.");
    }

    [Fact]
    public void Render_NamesTheLoaderToolTheHostAdvertises()
    {
        var session = Session(
            new[] { Skill("alpha", "A summary.", "Instructions.") },
            loaderToolName: "open_playbook");

        SkillRoster.Render(session).Should().Contain("`open_playbook`");
    }

    #endregion

    #region Loaded Skills

    [Fact]
    public void Render_WithAnActivatedSkill_MovesItOutOfTheRosterAndIncludesItsInstructions()
    {
        var session = Session(new[]
        {
            Skill("alpha", "Still loadable.", "Alpha instructions."),
            Skill("beta", "Already loaded.", "Beta instructions in full.")
        });
        session.Activate("beta");

        var block = SkillRoster.Render(session);

        block.Should().Contain("- `alpha` — Still loadable.");
        block.Should().NotContain("- `beta` —");
        block.Should().Contain("## Loaded skills");
        block.Should().Contain("### beta");
        block.Should().Contain("Beta instructions in full.");
    }

    [Fact]
    public void Render_WithEverythingActivated_OmitsTheLoadableHeading()
    {
        var session = Session(
            new[] { Skill("alpha", "A summary.", "Alpha instructions.") },
            preActivated: new[] { "alpha" });

        var block = SkillRoster.Render(session);

        block.Should().NotContain("## Skills");
        block.Should().Contain("## Loaded skills");
        block.Should().Contain("Alpha instructions.");
    }

    #endregion

    #region Pinned Wording

    [Fact]
    public void Render_MatchesThePinnedBlock_ForASmallFixture()
    {
        // This text sits in the cached system-prompt prefix. Changing a word of it invalidates every
        // cache breakpoint behind it — correct answers at roughly ten times the input price, with no
        // other symptom — so an edit here has to be a deliberate one that fails this test first.
        var session = Session(new[]
        {
            Skill("alpha", "Alpha things.", "Do alpha."),
            Skill("beta", "Beta things.", "Do beta.")
        });
        session.Activate("beta");

        var expected = """
            ## Skills

            Extra instructions, and the tools that go with them, that you can load when a request needs them. Load one by calling `load_skill` with its key; it costs a step, so load one only when the request is actually about it, and don't load one you were not asked for.

            - `alpha` — Alpha things.

            ## Loaded skills

            You already have these; their instructions follow. Don't load them again.

            ### beta

            Do beta.
            """.Replace("\r\n", "\n") + "\n";

        SkillRoster.Render(session).Should().Be(expected);
    }

    #endregion

    #region Append

    [Fact]
    public void Append_WithNothingToRender_LeavesThePromptUnchanged()
    {
        const string prompt = "You are a helpful assistant.";

        SkillRoster.Append(prompt, null).Should().Be(prompt);
        SkillRoster.Append(prompt, Session(Array.Empty<AgentSkill>())).Should().Be(prompt);
    }

    [Fact]
    public void Append_PutsTheBlockAfterThePrompt()
    {
        const string prompt = "You are a helpful assistant.";
        var session = Session(new[] { Skill("alpha", "Alpha things.", "Do alpha.") });

        var composed = SkillRoster.Append(prompt, session);

        // The rest of the prompt has to stay byte-identical to what it was before skills existed,
        // or the whole prefix it shares with every other request is thrown away.
        composed.Should().StartWith(prompt);
        composed.Should().EndWith(SkillRoster.Render(session));
    }

    [Fact]
    public void Append_WithAnEmptyPrompt_IsJustTheBlock()
    {
        var session = Session(new[] { Skill("alpha", "Alpha things.", "Do alpha.") });

        SkillRoster.Append(string.Empty, session).Should().Be(SkillRoster.Render(session));
    }

    #endregion
}
