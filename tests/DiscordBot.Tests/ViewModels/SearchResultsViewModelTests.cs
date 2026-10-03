using DiscordBot.Bot.Controllers;
using DiscordBot.Bot.Pages.CommandLogs;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using FluentAssertions;
using Xunit;

namespace DiscordBot.Tests.ViewModels;

public class SearchResultsViewModelTests
{
    private static SearchResultItemDto Item(string title) => new() { Id = title, Title = title };

    [Fact]
    public void TotalMatches_SumsTheTotals_NotTheLengthsOfTheFirstFiveLists()
    {
        var vm = new SearchResultsViewModel
        {
            CanViewUsers = false,
            TotalGuildResults = 3,
            TotalCommandLogResults = 40,
            TotalCommands = 8,
            TotalPages = 2,
            Commands = new[] { Item("a"), Item("b"), Item("c"), Item("d"), Item("e") }
        };

        vm.TotalMatches.Should().Be(53);
    }

    [Fact]
    public void TotalMatches_LeavesOutAdminCategories_ForAViewer()
    {
        var vm = new SearchResultsViewModel
        {
            CanViewUsers = false,
            TotalGuildResults = 1,
            TotalUserResults = 9,
            TotalAuditLogs = 9,
            TotalMessageLogs = 9,
            TotalReminders = 9,
            TotalScheduledMessages = 9
        };

        vm.TotalMatches.Should().Be(1);
    }

    [Fact]
    public void TotalMatches_IncludesAdminCategories_ForAnAdmin()
    {
        var vm = new SearchResultsViewModel
        {
            CanViewUsers = true,
            TotalGuildResults = 1,
            TotalUserResults = 2,
            TotalAuditLogs = 3,
            TotalMessageLogs = 4,
            TotalReminders = 5,
            TotalScheduledMessages = 6
        };

        vm.TotalMatches.Should().Be(21);
    }

    [Fact]
    public void HasResults_IgnoresAdminOnlyResults_ForAViewer()
    {
        var vm = new SearchResultsViewModel
        {
            CanViewUsers = false,
            AuditLogs = new[] { Item("x") }
        };

        vm.HasResults.Should().BeFalse("the viewer would see an empty page under a 'results' header");
    }

    [Theory]
    [InlineData("/Commands?tab=execution-logs&pageNumber=3", "/Commands?tab=execution-logs&pageNumber=3")]
    [InlineData("https://evil.example/", "/fallback")]
    [InlineData("//evil.example/", "/fallback")]
    [InlineData("", "/fallback")]
    [InlineData(null, "/fallback")]
    public void BackUrl_KeepsALocalReturnUrl_AndFallsBackOtherwise(string? returnUrl, string expected)
    {
        DetailsModel.ResolveBackUrl(returnUrl, "/fallback").Should().Be(expected);
    }

    [Theory]
    [InlineData("2026-01-01", "2026-03-31", null)]
    [InlineData("2026-01-01", "2026-04-01", null)]
    [InlineData("2026-01-01", "2026-04-02", "Choose a date range of 90 days or less.")]
    [InlineData("2026-10-02", "2026-10-01", "The start date must be on or before the end date.")]
    public void ValidateDateRange_MatchesTheRuleTheScriptChecks(string start, string end, string? expected)
    {
        CommandsApiController.ValidateDateRange(DateTime.Parse(start), DateTime.Parse(end)).Should().Be(expected);
    }

    [Fact]
    public void ValidateDateRange_AllowsAnOpenEnd()
    {
        CommandsApiController.ValidateDateRange(null, DateTime.Today).Should().BeNull();
        CommandsApiController.ValidateDateRange(DateTime.Today, null).Should().BeNull();
    }
}
