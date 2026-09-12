using DiscordBot.Agents;
using DiscordBot.Agents.Contracts;
using FluentAssertions;

namespace DiscordBot.Tests.Agents;

/// <summary>
/// Unit tests for <see cref="SkillSession"/> — one run's skills, and which of them it has loaded.
/// The two things worth pinning are the ordering (the roster built from <c>Available</c> sits in the
/// cached system-prompt prefix, so its order has to be the same on every process) and the tolerance
/// of what a model actually sends: a key in the wrong case, or the same key twice.
/// </summary>
public class SkillSessionTests
{
    private static AgentSkill Skill(string key, string? summary = null, params string[] tools) =>
        new()
        {
            Key = key,
            Summary = summary ?? $"Summary for {key}.",
            Tools = tools,
            Instructions = $"Instructions for {key}."
        };

    #region Available

    [Fact]
    public void Available_IsOrderedByKeyOrdinal()
    {
        // Ordinal, not culture or case-insensitive: 'M' sorts before 'a'. A culture-aware sort would
        // reorder the roster on a differently configured host and throw away the cached prefix.
        var session = new SkillSession(new[] { Skill("zulu"), Skill("alpha"), Skill("Mike") });

        session.Available.Select(s => s.Key).Should().Equal("Mike", "alpha", "zulu");
    }

    [Fact]
    public void Available_DeDuplicatesByKeyCaseInsensitively_KeepingTheFirst()
    {
        var session = new SkillSession(new[]
        {
            Skill("notes", "The first one wins."),
            Skill("NOTES", "The second one is dropped.")
        });

        session.Available.Should().ContainSingle()
            .Which.Summary.Should().Be("The first one wins.");
    }

    [Fact]
    public void Available_IsEmpty_WhenTheHostOffersNoSkills()
    {
        new SkillSession(Array.Empty<AgentSkill>()).Available.Should().BeEmpty();
    }

    #endregion

    #region Activate

    [Fact]
    public void Activate_WithADifferentCase_ReturnsTheSkillAndActivatesIt()
    {
        var session = new SkillSession(new[] { Skill("moderation") });

        var activated = session.Activate("MODERATION");

        activated.Should().NotBeNull();
        activated!.Key.Should().Be("moderation");
        session.Activated.Select(s => s.Key).Should().Equal("moderation");
    }

    [Fact]
    public void Activate_CalledTwice_LeavesOneEntry()
    {
        // A model that loads the same skill again wants to re-read the instructions, so the call
        // succeeds — it just must not double the entry, which would double the prompt block.
        var session = new SkillSession(new[] { Skill("moderation") });

        session.Activate("moderation").Should().NotBeNull();
        session.Activate("moderation").Should().NotBeNull();

        session.Activated.Should().ContainSingle();
    }

    [Fact]
    public void Activate_WithAnUnknownKey_ReturnsNullAndActivatesNothing()
    {
        var session = new SkillSession(new[] { Skill("moderation") });

        session.Activate("nonsense").Should().BeNull();

        session.Activated.Should().BeEmpty();
    }

    [Fact]
    public void Activate_PreservesActivationOrder()
    {
        var session = new SkillSession(new[] { Skill("alpha"), Skill("beta") });

        session.Activate("beta");
        session.Activate("alpha");

        // Activation order, not key order: Available is sorted, Activated is a history.
        session.Activated.Select(s => s.Key).Should().Equal("beta", "alpha");
    }

    [Fact]
    public void Find_WithAnUnknownOrBlankKey_ReturnsNull()
    {
        var session = new SkillSession(new[] { Skill("moderation") });

        session.Find("nope").Should().BeNull();
        session.Find("   ").Should().BeNull();
        session.Find(" moderation ").Should().NotBeNull();
    }

    #endregion

    #region Pre-activation

    [Fact]
    public void Constructor_WithPreActivatedKeys_ActivatesThemInTheGivenOrder()
    {
        // A multi-turn host replays the previous turn's activations here, which is what makes a
        // skill nearly free from the second turn onward.
        var session = new SkillSession(
            new[] { Skill("alpha"), Skill("beta"), Skill("gamma") },
            preActivated: new[] { "gamma", "ALPHA" });

        session.Activated.Select(s => s.Key).Should().Equal("gamma", "alpha");
    }

    [Fact]
    public void Constructor_WithUnknownPreActivatedKeys_IgnoresThem()
    {
        // The stored keys come from a previous turn, so a skill file that has since been deleted or
        // renamed must not fail the run.
        var session = new SkillSession(
            new[] { Skill("alpha") },
            preActivated: new[] { "retired", "alpha" });

        session.Activated.Select(s => s.Key).Should().Equal("alpha");
    }

    #endregion

    #region IsActivated, LoaderToolName, snapshot

    [Fact]
    public void IsActivated_ReflectsState()
    {
        var session = new SkillSession(new[] { Skill("alpha"), Skill("beta") });

        session.IsActivated("alpha").Should().BeFalse();

        session.Activate("alpha");

        session.IsActivated("ALPHA").Should().BeTrue();
        session.IsActivated("beta").Should().BeFalse();
        session.IsActivated("nonsense").Should().BeFalse();
    }

    [Fact]
    public void LoaderToolName_DefaultsToTheConventionalName()
    {
        new SkillSession(Array.Empty<AgentSkill>()).LoaderToolName.Should().Be("load_skill");
        SkillSession.DefaultLoaderToolName.Should().Be("load_skill");
    }

    [Fact]
    public void LoaderToolName_IsHonoured_WhenTheHostAdvertisesADifferentName()
    {
        new SkillSession(Array.Empty<AgentSkill>(), loaderToolName: "open_playbook")
            .LoaderToolName.Should().Be("open_playbook");
    }

    [Fact]
    public void Activated_IsASnapshot()
    {
        // The loop reads Activated after every tool round; handing out the live list would let a
        // caller mutate a run's state by accident.
        var session = new SkillSession(new[] { Skill("alpha") });
        session.Activate("alpha");

        var snapshot = session.Activated;
        ((List<AgentSkill>)snapshot).Clear();

        session.Activated.Should().ContainSingle();
    }

    #endregion
}
