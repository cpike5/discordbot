using DiscordBot.Bot.Blazor.Common;
using FluentAssertions;

namespace DiscordBot.Tests.Bot.Blazor.Common;

/// <summary>
/// Unit tests for <see cref="PagedQuery"/>, the standardised paging/sort state for guild list
/// pages (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4b).
/// </summary>
public class PagedQueryTests
{
    [Fact]
    public void FromQuery_WithNoValues_DefaultsToPageOneAndDefaultSize()
    {
        var query = PagedQuery.FromQuery(null, null);

        query.PageNumber.Should().Be(1);
        query.PageSize.Should().Be(PagedQuery.DefaultPageSize);
        query.SortBy.Should().BeNull();
        query.SortDescending.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void FromQuery_ClampsNonPositivePageNumber_ToOne(int pageNumber)
    {
        PagedQuery.FromQuery(pageNumber, null).PageNumber.Should().Be(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void FromQuery_ClampsOutOfRangePageSize_ToDefault(int pageSize)
    {
        PagedQuery.FromQuery(null, pageSize, defaultPageSize: 20).PageSize.Should().Be(20);
    }

    [Fact]
    public void FromQuery_AcceptsAnInRangePageSize()
    {
        PagedQuery.FromQuery(null, 100).PageSize.Should().Be(100);
    }

    [Fact]
    public void FromQuery_PreferPageNumber_OverLegacyPage_WhenBothPresent()
    {
        PagedQuery.FromQuery(3, null, legacyPage: 7).PageNumber.Should().Be(3);
    }

    [Fact]
    public void FromQuery_FallsBackToLegacyPage_WhenPageNumberAbsent()
    {
        PagedQuery.FromQuery(null, null, legacyPage: 7).PageNumber.Should().Be(7);
    }

    [Fact]
    public void FromQuery_PassesThroughSortByAndDescending()
    {
        var query = PagedQuery.FromQuery(1, 20, sortBy: "CreatedAt", sortDescending: true);

        query.SortBy.Should().Be("CreatedAt");
        query.SortDescending.Should().BeTrue();
    }

    [Fact]
    public void ToQueryString_AtDefaults_IsEmpty()
    {
        new PagedQuery(1, PagedQuery.DefaultPageSize).ToQueryString().Should().BeEmpty();
    }

    [Fact]
    public void ToQueryString_WithNonDefaultPage_IncludesPageNumber()
    {
        new PagedQuery(3, PagedQuery.DefaultPageSize).ToQueryString().Should().Be("?pageNumber=3");
    }

    [Fact]
    public void ToQueryString_WithNonDefaultPageSize_IncludesPageSize()
    {
        new PagedQuery(1, 50).ToQueryString().Should().Be("?pageSize=50");
    }

    [Fact]
    public void ToQueryString_WithSort_IncludesBothSortFields()
    {
        var query = new PagedQuery(2, 50, "Name", true);

        query.ToQueryString().Should().Be("?pageNumber=2&pageSize=50&sortBy=Name&sortDescending=true");
    }

    [Fact]
    public void ToQueryString_HonoursCustomParameterNames()
    {
        var query = new PagedQuery(2, PagedQuery.DefaultPageSize);

        query.ToQueryString(pageParameterName: "page").Should().Be("?page=2");
    }

    [Fact]
    public void RoundTrip_FromQueryStringBackThroughFromQuery_IsStable()
    {
        var original = PagedQuery.FromQuery(4, 50, "Name", true);
        var queryString = original.ToQueryString();

        queryString.Should().Be("?pageNumber=4&pageSize=50&sortBy=Name&sortDescending=true");

        // A page reading that query string back would parse pageNumber=4, pageSize=50 the same way.
        var roundTripped = PagedQuery.FromQuery(4, 50, "Name", true);
        roundTripped.Should().Be(original);
    }
}
