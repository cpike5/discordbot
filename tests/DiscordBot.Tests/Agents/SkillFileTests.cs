using DiscordBot.Agents;
using DiscordBot.Agents.Contracts;
using FluentAssertions;

namespace DiscordBot.Tests.Agents;

/// <summary>
/// Unit tests for <see cref="SkillFile"/> — the hand-rolled front-matter parser that turns one
/// markdown file into an <see cref="AgentSkill"/>. Two things make this
/// worth testing at this depth: a file that fails to parse is a skill nothing will ever load, so the
/// rejections have to be the ones the author expects; and the split between front matter and body is
/// done by line rather than by the first <c>---</c>, which is what keeps a horizontal rule inside the
/// instructions from silently eating half the skill.
/// </summary>
public class SkillFileTests
{
    /// <summary>Parses and asserts success, handing back the skill.</summary>
    private static AgentSkill Parse(string content, string defaultKey = "file-name")
    {
        SkillFile.TryParse(content, defaultKey, "source.md", out var skill, out var error)
            .Should().BeTrue("parsing should have succeeded but failed with: {0}", error);

        skill.Should().NotBeNull();
        return skill!;
    }

    #region Happy Path

    [Fact]
    public void TryParse_WithAWellFormedFile_ReadsEveryPart()
    {
        var content = """
            ---
            key:   moderation
            summary:   Look up moderation cases for a server.
            tools: get_moderation_cases, get_user_mod_history
            ---

            **Reading cases.** `get_moderation_cases` takes a guild.

            """;

        var skill = Parse(content);

        skill.Key.Should().Be("moderation");
        skill.Summary.Should().Be("Look up moderation cases for a server.");
        skill.Tools.Should().Equal("get_moderation_cases", "get_user_mod_history");
        // The body is trimmed at both ends, so the blank line under the fence is not instructions.
        skill.Instructions.Should().Be("**Reading cases.** `get_moderation_cases` takes a guild.");
        skill.Source.Should().Be("source.md");
    }

    [Fact]
    public void TryParse_WithNoKeyInFrontMatter_UsesTheSuppliedDefaultKey()
    {
        var content = """
            ---
            summary: Standing orders for release notes.
            ---
            Write them in past tense.
            """;

        Parse(content, defaultKey: "release-notes").Key.Should().Be("release-notes");
    }

    [Fact]
    public void TryParse_WithNoTools_YieldsAnEmptyToolList()
    {
        var content = """
            ---
            summary: Pure standing orders, no tools.
            ---
            Be brief.
            """;

        Parse(content).Tools.Should().BeEmpty();
    }

    #endregion

    #region Rejections

    [Fact]
    public void TryParse_WithNoSummary_IsRejectedForTheSummary()
    {
        var content = """
            ---
            key: moderation
            ---
            Some instructions.
            """;

        SkillFile.TryParse(content, "moderation", null, out var skill, out var error).Should().BeFalse();

        skill.Should().BeNull();
        // The reason is written into a log line an author reads, so it has to name the field.
        error.Should().NotBeNull();
        error.Should().Contain("summary");
    }

    [Fact]
    public void TryParse_WithNoFrontMatterFence_IsRejected()
    {
        SkillFile.TryParse("Just some markdown with no front matter.", "k", null, out var skill, out var error)
            .Should().BeFalse();

        skill.Should().BeNull();
        error.Should().Contain("front-matter");
    }

    [Fact]
    public void TryParse_WithFrontMatterButNoBody_IsRejected()
    {
        var content = """
            ---
            summary: A skill with nothing under it.
            ---

            """;

        SkillFile.TryParse(content, "k", null, out var skill, out var error).Should().BeFalse();

        skill.Should().BeNull();
        error.Should().Contain("instructions");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n\t  ")]
    public void TryParse_WithNoContent_IsRejected(string? content)
    {
        SkillFile.TryParse(content, "k", null, out var skill, out var error).Should().BeFalse();

        skill.Should().BeNull();
        error.Should().Contain("empty");
    }

    #endregion

    #region Front Matter Boundary

    [Fact]
    public void TryParse_WithAHorizontalRuleInTheBody_KeepsTheWholeBody()
    {
        // The closing fence is found by whole line, so a `---` rule in the instructions is body text.
        // Getting this wrong truncates a skill at its first rule and nothing about the result says so.
        var content = """
            ---
            summary: Has a rule in the middle.
            ---
            First section.

            ---

            Second section.
            """;

        var skill = Parse(content);

        skill.Instructions.Should().Be("First section.\n\n---\n\nSecond section.");
        skill.Instructions.Should().Contain("Second section.");
    }

    [Fact]
    public void TryParse_WithCarriageReturns_StillFindsTheClosingFence()
    {
        var content = "---\r\nsummary: Written on Windows.\r\n---\r\nDo the thing.\r\n";

        var skill = Parse(content);

        skill.Summary.Should().Be("Written on Windows.");
        skill.Instructions.Should().Be("Do the thing.");
    }

    #endregion

    #region Field Parsing

    [Fact]
    public void TryParse_WithAQuotedSummary_StripsTheQuotesAndKeepsTheColon()
    {
        // A summary is split at its first colon, so an unquoted "Rat Watch: leaderboards" would lose
        // its tail. One layer of quotes is the escape hatch.
        var content = """
            ---
            summary: "Rat Watch: leaderboards, streaks, and per-user stats."
            ---
            Body.
            """;

        Parse(content).Summary.Should().Be("Rat Watch: leaderboards, streaks, and per-user stats.");
    }

    [Fact]
    public void TryParse_WithSingleQuotedSummary_StripsTheQuotes()
    {
        var content = """
            ---
            summary: 'Quoted with apostrophes.'
            ---
            Body.
            """;

        Parse(content).Summary.Should().Be("Quoted with apostrophes.");
    }

    [Theory]
    [InlineData("tools: get_a, get_b")]
    [InlineData("tools: [get_a, get_b]")]
    [InlineData("tools:   get_a ,   get_b  ")]
    public void TryParse_ReadsBothToolListForms(string toolsLine)
    {
        var content = $"""
            ---
            summary: Two tools.
            {toolsLine}
            ---
            Body.
            """;

        Parse(content).Tools.Should().Equal("get_a", "get_b");
    }

    [Fact]
    public void TryParse_WithRepeatedTools_DeDuplicatesCaseInsensitivelyAndKeepsAuthorOrder()
    {
        var content = """
            ---
            summary: Repeats itself.
            tools: zulu, Zulu, alpha, ZULU
            ---
            Body.
            """;

        // Order is the author's — nothing downstream re-sorts it — and the first spelling wins.
        Parse(content).Tools.Should().Equal("zulu", "alpha");
    }

    [Fact]
    public void TryParse_IgnoresCommentsAndBlankLinesInFrontMatter()
    {
        var content = """
            ---
            # This file is generated. Do not hand-edit.

            summary: Survives the noise.

            # trailing note
            ---
            Body.
            """;

        var skill = Parse(content, defaultKey: "noisy");

        skill.Key.Should().Be("noisy");
        skill.Summary.Should().Be("Survives the noise.");
    }

    [Fact]
    public void TryParse_IgnoresAnUnknownFrontMatterKey()
    {
        // Unknown keys are forward compatibility, not a mistake: an older process must still read a
        // file written for a newer one rather than dropping the skill entirely.
        var content = """
            ---
            summary: Has a key from the future.
            audience: staff-only
            ---
            Body.
            """;

        var skill = Parse(content);

        skill.Summary.Should().Be("Has a key from the future.");
        skill.Tools.Should().BeEmpty();
    }

    #endregion
}
