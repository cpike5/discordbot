using Discord;
using DiscordBot.Bot.Components;
using DiscordBot.Bot.Helpers;
using FluentAssertions;

namespace DiscordBot.Tests.Bot.Helpers;

/// <summary>
/// Unit tests for <see cref="PaginationHelper"/>.
/// </summary>
public class PaginationHelperTests
{
    #region CalculatePages

    [Fact]
    public void CalculatePages_ZeroItems_ReturnsPageOneOfOne()
    {
        // Act
        var (page, totalPages) = PaginationHelper.CalculatePages(totalItems: 0, pageSize: 10, requestedPage: 1);

        // Assert
        page.Should().Be(1, "there is always at least 1 page even with no items");
        totalPages.Should().Be(1, "there is always at least 1 page even with no items");
    }

    [Fact]
    public void CalculatePages_ExactlyOnePage_ReturnsPageOneOfOne()
    {
        // Act
        var (page, totalPages) = PaginationHelper.CalculatePages(totalItems: 5, pageSize: 5, requestedPage: 1);

        // Assert
        page.Should().Be(1);
        totalPages.Should().Be(1);
    }

    [Fact]
    public void CalculatePages_TenItemsPageFive_ReturnsTwoTotalPages()
    {
        // Act
        var (page, totalPages) = PaginationHelper.CalculatePages(totalItems: 10, pageSize: 5, requestedPage: 1);

        // Assert
        totalPages.Should().Be(2, "10 items with page size 5 yields 2 pages");
        page.Should().Be(1);
    }

    [Fact]
    public void CalculatePages_TenItemsPageFive_Page2WithinRange_ReturnsClamped()
    {
        // Act
        var (page, totalPages) = PaginationHelper.CalculatePages(totalItems: 10, pageSize: 5, requestedPage: 2);

        // Assert
        totalPages.Should().Be(2);
        page.Should().Be(2, "page 2 is within the valid range of 1-2");
    }

    [Fact]
    public void CalculatePages_RequestedPageBeyondTotal_ClampsToLastPage()
    {
        // Act
        var (page, totalPages) = PaginationHelper.CalculatePages(totalItems: 10, pageSize: 5, requestedPage: 99);

        // Assert
        totalPages.Should().Be(2);
        page.Should().Be(2, "page 99 should be clamped down to the last page (2)");
    }

    [Fact]
    public void CalculatePages_RequestedPageZero_ClampsToFirstPage()
    {
        // Act
        var (page, totalPages) = PaginationHelper.CalculatePages(totalItems: 10, pageSize: 5, requestedPage: 0);

        // Assert
        totalPages.Should().Be(2);
        page.Should().Be(1, "page 0 should be clamped up to the first page (1)");
    }

    [Fact]
    public void CalculatePages_RequestedPageNegative_ClampsToFirstPage()
    {
        // Act
        var (page, totalPages) = PaginationHelper.CalculatePages(totalItems: 10, pageSize: 5, requestedPage: -5);

        // Assert
        totalPages.Should().Be(2);
        page.Should().Be(1, "negative page numbers should be clamped to 1");
    }

    [Fact]
    public void CalculatePages_ItemsNotEvenlyDivisible_RoundsUpTotalPages()
    {
        // Act
        var (page, totalPages) = PaginationHelper.CalculatePages(totalItems: 11, pageSize: 5, requestedPage: 1);

        // Assert
        totalPages.Should().Be(3, "11 items with page size 5 requires 3 pages (ceil(11/5) = 3)");
    }

    #endregion

    #region BuildPaginationButtons

    [Fact]
    public void BuildPaginationButtons_ReturnsComponentBuilderWithTwoButtons()
    {
        // Arrange
        const ulong userId = 123456789UL;

        // Act
        var builder = PaginationHelper.BuildPaginationButtons("modlog", currentPage: 2, totalPages: 5, userId);

        // Assert
        builder.Should().NotBeNull();
        builder.ActionRows.Should().HaveCount(1, "both buttons fit in a single action row");
        builder.ActionRows[0].Components.Should().HaveCount(2, "there should be Previous and Next buttons");
    }

    [Fact]
    public void BuildPaginationButtons_PreviousButtonDisabledOnFirstPage()
    {
        // Arrange
        const ulong userId = 123456789UL;

        // Act
        var builder = PaginationHelper.BuildPaginationButtons("modlog", currentPage: 1, totalPages: 5, userId);
        var prevButton = (ButtonBuilder)builder.ActionRows[0].Components[0];

        // Assert
        prevButton.IsDisabled.Should().BeTrue("the Previous button should be disabled on the first page");
    }

    [Fact]
    public void BuildPaginationButtons_PreviousButtonEnabledOnSecondPage()
    {
        // Arrange
        const ulong userId = 123456789UL;

        // Act
        var builder = PaginationHelper.BuildPaginationButtons("modlog", currentPage: 2, totalPages: 5, userId);
        var prevButton = (ButtonBuilder)builder.ActionRows[0].Components[0];

        // Assert
        prevButton.IsDisabled.Should().BeFalse("the Previous button should be enabled when not on the first page");
    }

    [Fact]
    public void BuildPaginationButtons_NextButtonDisabledOnLastPage()
    {
        // Arrange
        const ulong userId = 123456789UL;

        // Act
        var builder = PaginationHelper.BuildPaginationButtons("modlog", currentPage: 5, totalPages: 5, userId);
        var nextButton = (ButtonBuilder)builder.ActionRows[0].Components[1];

        // Assert
        nextButton.IsDisabled.Should().BeTrue("the Next button should be disabled on the last page");
    }

    [Fact]
    public void BuildPaginationButtons_NextButtonEnabledWhenNotOnLastPage()
    {
        // Arrange
        const ulong userId = 123456789UL;

        // Act
        var builder = PaginationHelper.BuildPaginationButtons("modlog", currentPage: 2, totalPages: 5, userId);
        var nextButton = (ButtonBuilder)builder.ActionRows[0].Components[1];

        // Assert
        nextButton.IsDisabled.Should().BeFalse("the Next button should be enabled when not on the last page");
    }

    [Fact]
    public void BuildPaginationButtons_ButtonLabels_AreCorrect()
    {
        // Arrange
        const ulong userId = 123456789UL;

        // Act
        var builder = PaginationHelper.BuildPaginationButtons("modlog", currentPage: 2, totalPages: 5, userId);
        var prevButton = (ButtonBuilder)builder.ActionRows[0].Components[0];
        var nextButton = (ButtonBuilder)builder.ActionRows[0].Components[1];

        // Assert
        prevButton.Label.Should().Be("◀ Previous");
        nextButton.Label.Should().Be("Next ▶");
    }

    [Fact]
    public void BuildPaginationButtons_ButtonStyles_AreSecondary()
    {
        // Arrange
        const ulong userId = 123456789UL;

        // Act
        var builder = PaginationHelper.BuildPaginationButtons("modlog", currentPage: 2, totalPages: 5, userId);
        var prevButton = (ButtonBuilder)builder.ActionRows[0].Components[0];
        var nextButton = (ButtonBuilder)builder.ActionRows[0].Components[1];

        // Assert
        prevButton.Style.Should().Be(ButtonStyle.Secondary);
        nextButton.Style.Should().Be(ButtonStyle.Secondary);
    }

    [Fact]
    public void BuildPaginationButtons_CustomIdsContainHandlerPrefix()
    {
        // Arrange
        const ulong userId = 123456789UL;
        const string correlationId = "abc123de";

        // Act
        var builder = PaginationHelper.BuildPaginationButtons("modlog", currentPage: 2, totalPages: 5, userId, correlationId);
        var prevButton = (ButtonBuilder)builder.ActionRows[0].Components[0];
        var nextButton = (ButtonBuilder)builder.ActionRows[0].Components[1];

        // Assert
        prevButton.CustomId.Should().StartWith("modlog:page:", "the custom ID should start with the handler prefix and action");
        nextButton.CustomId.Should().StartWith("modlog:page:", "the custom ID should start with the handler prefix and action");
    }

    [Fact]
    public void BuildPaginationButtons_CustomIdsContainUserId()
    {
        // Arrange
        const ulong userId = 987654321UL;
        const string correlationId = "abc123de";

        // Act
        var builder = PaginationHelper.BuildPaginationButtons("modlog", currentPage: 2, totalPages: 5, userId, correlationId);
        var prevButton = (ButtonBuilder)builder.ActionRows[0].Components[0];
        var nextButton = (ButtonBuilder)builder.ActionRows[0].Components[1];

        // Assert
        prevButton.CustomId.Should().Contain("987654321", "the custom ID should embed the user ID");
        nextButton.CustomId.Should().Contain("987654321", "the custom ID should embed the user ID");
    }

    [Fact]
    public void BuildPaginationButtons_WithExplicitCorrelationId_CustomIdsContainCorrelationId()
    {
        // Arrange
        const ulong userId = 123456789UL;
        const string correlationId = "testcorr01";

        // Act
        var builder = PaginationHelper.BuildPaginationButtons("modlog", currentPage: 2, totalPages: 5, userId, correlationId);
        var prevButton = (ButtonBuilder)builder.ActionRows[0].Components[0];
        var nextButton = (ButtonBuilder)builder.ActionRows[0].Components[1];

        // Assert
        prevButton.CustomId.Should().Contain(correlationId, "the custom ID should embed the provided correlation ID");
        nextButton.CustomId.Should().Contain(correlationId, "the custom ID should embed the provided correlation ID");
    }

    [Fact]
    public void BuildPaginationButtons_WithNullCorrelationId_GeneratesCorrelationId()
    {
        // Arrange
        const ulong userId = 123456789UL;

        // Act
        var builder = PaginationHelper.BuildPaginationButtons("modlog", currentPage: 2, totalPages: 5, userId, correlationId: null);
        var prevButton = (ButtonBuilder)builder.ActionRows[0].Components[0];

        // Assert
        // Custom ID format: handler:page:userId:correlationId:pageNumber
        // Parse the generated custom ID to verify it is valid
        ComponentIdBuilder.TryParse(prevButton.CustomId, out var parts).Should().BeTrue("the generated custom ID should be parseable");
        parts.CorrelationId.Should().NotBeNullOrWhiteSpace("a correlation ID should be auto-generated when null is passed");
    }

    [Fact]
    public void BuildPaginationButtons_PreviousButtonCustomId_ContainsPreviousPageNumber()
    {
        // Arrange
        const ulong userId = 123456789UL;
        const string correlationId = "abc123de";

        // Act
        var builder = PaginationHelper.BuildPaginationButtons("modlog", currentPage: 3, totalPages: 5, userId, correlationId);
        var prevButton = (ButtonBuilder)builder.ActionRows[0].Components[0];

        // Assert: expected custom ID is "modlog:page:123456789:abc123de:2"
        var expectedPrevId = ComponentIdBuilder.Build("modlog", "page", userId, correlationId, "2");
        prevButton.CustomId.Should().Be(expectedPrevId, "the Previous button should target page currentPage - 1");
    }

    [Fact]
    public void BuildPaginationButtons_NextButtonCustomId_ContainsNextPageNumber()
    {
        // Arrange
        const ulong userId = 123456789UL;
        const string correlationId = "abc123de";

        // Act
        var builder = PaginationHelper.BuildPaginationButtons("modlog", currentPage: 3, totalPages: 5, userId, correlationId);
        var nextButton = (ButtonBuilder)builder.ActionRows[0].Components[1];

        // Assert: expected custom ID is "modlog:page:123456789:abc123de:4"
        var expectedNextId = ComponentIdBuilder.Build("modlog", "page", userId, correlationId, "4");
        nextButton.CustomId.Should().Be(expectedNextId, "the Next button should target page currentPage + 1");
    }

    [Fact]
    public void BuildPaginationButtons_OnFirstPage_PreviousButtonClampedToPageOne()
    {
        // Arrange
        const ulong userId = 123456789UL;
        const string correlationId = "abc123de";

        // Act
        var builder = PaginationHelper.BuildPaginationButtons("modlog", currentPage: 1, totalPages: 5, userId, correlationId);
        var prevButton = (ButtonBuilder)builder.ActionRows[0].Components[0];

        // Assert: previous page is clamped to 1, not 0
        var expectedPrevId = ComponentIdBuilder.Build("modlog", "page", userId, correlationId, "1");
        prevButton.CustomId.Should().Be(expectedPrevId, "the Previous button page number should be clamped to 1 on the first page");
    }

    [Fact]
    public void BuildPaginationButtons_OnLastPage_NextButtonClampedToLastPage()
    {
        // Arrange
        const ulong userId = 123456789UL;
        const string correlationId = "abc123de";

        // Act
        var builder = PaginationHelper.BuildPaginationButtons("modlog", currentPage: 5, totalPages: 5, userId, correlationId);
        var nextButton = (ButtonBuilder)builder.ActionRows[0].Components[1];

        // Assert: next page is clamped to totalPages, not totalPages + 1
        var expectedNextId = ComponentIdBuilder.Build("modlog", "page", userId, correlationId, "5");
        nextButton.CustomId.Should().Be(expectedNextId, "the Next button page number should be clamped to totalPages on the last page");
    }

    #endregion
}
