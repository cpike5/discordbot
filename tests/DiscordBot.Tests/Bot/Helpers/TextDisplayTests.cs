using DiscordBot.Bot.Helpers;
using FluentAssertions;
using Xunit;

namespace DiscordBot.Tests.Bot.Helpers;

/// <summary>Cutting names and text without splitting an emoji or an accent (UX plan E-1).</summary>
public class TextDisplayTests
{
    [Theory]
    [InlineData("Test Server", 2, "TE")]
    [InlineData("a", 2, "A")]
    [InlineData("  padded", 2, "PA")]
    [InlineData("", 2, "?")]
    [InlineData(null, 2, "?")]
    [InlineData("zed", 1, "Z")]
    public void Initials_TakesTheFirstCharacters(string? text, int count, string expected)
    {
        TextDisplay.Initials(text, count).Should().Be(expected);
    }

    [Fact]
    public void Initials_KeepsAnEmojiWhole()
    {
        // 😀 is two UTF-16 code units: name[..2] would give the emoji and nothing else, [..1] half of it
        TextDisplay.Initials("\U0001F600Party", 2).Should().Be("\U0001F600P");
        TextDisplay.Initials("\U0001F600Party", 1).Should().Be("\U0001F600");
    }

    [Fact]
    public void Initials_KeepsAnAccentWithItsLetter()
    {
        TextDisplay.Initials("école", 2).Should().Be("ÉC");
    }

    [Fact]
    public void Initials_KeepsAFamilyEmojiWhole()
    {
        const string family = "\U0001F468‍\U0001F469‍\U0001F467";
        TextDisplay.Initials(family + "x", 1).Should().Be(family);
    }

    [Theory]
    [InlineData("Ada Lovelace", "AL")]
    [InlineData("ada", "AD")]
    [InlineData("  ", "?")]
    [InlineData("one two three", "OT")]
    public void WordInitials_UsesTheFirstLetterOfEachWord(string text, string expected)
    {
        TextDisplay.WordInitials(text).Should().Be(expected);
    }

    [Fact]
    public void WordInitials_KeepsAnEmojiWhole()
    {
        TextDisplay.WordInitials("\U0001F600 Ada").Should().Be("\U0001F600A");
    }

    [Theory]
    [InlineData("short", 10, "short")]
    [InlineData("exactly ten", 11, "exactly ten")]
    [InlineData("0123456789", 5, "01234...")]
    public void Truncate_CutsAtTheLimitAndAddsAnEllipsis(string text, int max, string expected)
    {
        TextDisplay.Truncate(text, max).Should().Be(expected);
    }

    [Fact]
    public void Truncate_NeverCutsInsideAnEmoji()
    {
        TextDisplay.Truncate("\U0001F600\U0001F600\U0001F600", 2).Should().Be("\U0001F600\U0001F600...");
        TextDisplay.Truncate("\U0001F600\U0001F600", 5).Should().Be("\U0001F600\U0001F600");
    }

    [Fact]
    public void Take_ReturnsAtMostTheAskedCharacters()
    {
        TextDisplay.Take("abc", 5).Should().Be("abc");
        TextDisplay.Take(null, 2).Should().BeEmpty();
        TextDisplay.Take("abc", 0).Should().BeEmpty();
    }
}

public class ConsentDisplayTests
{
    [Theory]
    [InlineData("WebUI", "the web portal")]
    [InlineData("SlashCommand", "a Discord command")]
    [InlineData(null, "an unknown source")]
    [InlineData("", "an unknown source")]
    [InlineData("BulkImport", "bulk import")]
    public void Via_NamesWhereTheChangeWasMade(string? source, string expected)
    {
        ConsentDisplay.Via(source).Should().Be(expected);
    }
}
