using DiscordBot.Bot.Extensions;
using FluentAssertions;

namespace DiscordBot.Tests.Bot.Extensions;

/// <summary>
/// Unit tests for <see cref="PaginationQueryExtensions"/>.
/// Tests cover edge cases for page and pageSize normalization.
/// </summary>
public class PaginationQueryExtensionsTests
{
    #region Page Normalization

    [Fact]
    public void Normalize_PageZero_NormalizesToOne()
    {
        // Arrange
        var query = (page: 0, pageSize: 20);

        // Act
        var result = query.Normalize();

        // Assert
        result.Page.Should().Be(1);
    }

    [Fact]
    public void Normalize_PageNegative_NormalizesToOne()
    {
        // Arrange
        var query = (page: -1, pageSize: 20);

        // Act
        var result = query.Normalize();

        // Assert
        result.Page.Should().Be(1);
    }

    [Fact]
    public void Normalize_PageFive_StaysFive()
    {
        // Arrange
        var query = (page: 5, pageSize: 20);

        // Act
        var result = query.Normalize();

        // Assert
        result.Page.Should().Be(5);
    }

    #endregion

    #region PageSize Normalization

    [Fact]
    public void Normalize_PageSizeZero_NormalizesToDefaultPageSize()
    {
        // Arrange
        var query = (page: 1, pageSize: 0);

        // Act
        var result = query.Normalize();

        // Assert
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public void Normalize_PageSizeNegative_NormalizesToDefaultPageSize()
    {
        // Arrange
        var query = (page: 1, pageSize: -1);

        // Act
        var result = query.Normalize();

        // Assert
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public void Normalize_PageSizeExceedsMaximum_NormalizesToDefaultPageSize()
    {
        // Arrange
        var query = (page: 1, pageSize: 101);

        // Act
        var result = query.Normalize();

        // Assert
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public void Normalize_PageSizeFifty_StaysFifty()
    {
        // Arrange
        var query = (page: 1, pageSize: 50);

        // Act
        var result = query.Normalize();

        // Assert
        result.PageSize.Should().Be(50);
    }

    #endregion

    #region Custom maxPageSize and defaultPageSize

    [Fact]
    public void Normalize_CustomMaxPageSize_EnforcesCustomLimit()
    {
        // Arrange
        var query = (page: 1, pageSize: 51);

        // Act
        var result = query.Normalize(maxPageSize: 50, defaultPageSize: 10);

        // Assert
        result.PageSize.Should().Be(10);
    }

    [Fact]
    public void Normalize_CustomDefaultPageSize_UsesCustomDefault()
    {
        // Arrange
        var query = (page: 1, pageSize: 0);

        // Act
        var result = query.Normalize(maxPageSize: 100, defaultPageSize: 25);

        // Assert
        result.PageSize.Should().Be(25);
    }

    [Fact]
    public void Normalize_PageSizeAtExactMaximum_StaysAtMaximum()
    {
        // Arrange
        var query = (page: 1, pageSize: 50);

        // Act
        var result = query.Normalize(maxPageSize: 50, defaultPageSize: 10);

        // Assert
        result.PageSize.Should().Be(50);
    }

    [Fact]
    public void Normalize_BothPageAndPageSizeInvalid_NormalizesBoth()
    {
        // Arrange
        var query = (page: -5, pageSize: 0);

        // Act
        var result = query.Normalize(maxPageSize: 100, defaultPageSize: 15);

        // Assert
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(15);
    }

    #endregion
}
