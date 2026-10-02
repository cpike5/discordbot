using DiscordBot.Bot.Helpers;
using FluentAssertions;
using Xunit;

namespace DiscordBot.Tests.Bot.Helpers;

public class CsvFieldTests
{
    [Theory]
    [InlineData("=HYPERLINK(\"https://evil.example\")", "'=HYPERLINK(\"https://evil.example\")")]
    [InlineData("+1+1", "'+1+1")]
    [InlineData("-2+3", "'-2+3")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("\t=1", "'\t=1")]
    [InlineData("\r=1", "'\r=1")]
    public void NeutralizeFormula_PrefixesFormulaStarts(string value, string expected)
    {
        CsvField.NeutralizeFormula(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("a=b")]
    [InlineData("1-2")]
    [InlineData(" =not a formula start")]
    public void NeutralizeFormula_LeavesOtherTextAlone(string value)
    {
        CsvField.NeutralizeFormula(value).Should().Be(value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NeutralizeFormula_ReturnsEmpty_ForEmpty(string? value)
    {
        CsvField.NeutralizeFormula(value).Should().BeEmpty();
    }
}
