using Discord;
using DiscordBot.Bot.Helpers;
using FluentAssertions;

namespace DiscordBot.Tests.Bot.Helpers;

/// <summary>
/// Unit tests for <see cref="EmbedHelper"/>.
/// Verifies that each factory method produces an embed with the correct color, title, description, and timestamp.
/// </summary>
public class EmbedHelperTests
{
    #region Error

    [Fact]
    public void Error_SetsRedColor()
    {
        // Act
        var embed = EmbedHelper.Error("Error Title", "Error description.");

        // Assert
        embed.Color.Should().Be(Color.Red);
    }

    [Fact]
    public void Error_SetsTitleAndDescription()
    {
        // Act
        var embed = EmbedHelper.Error("Error Title", "Error description.");

        // Assert
        embed.Title.Should().Be("Error Title");
        embed.Description.Should().Be("Error description.");
    }

    [Fact]
    public void Error_SetsTimestamp()
    {
        // Act
        var embed = EmbedHelper.Error("Error Title", "Error description.");

        // Assert
        embed.Timestamp.Should().NotBeNull();
    }

    #endregion

    #region Success

    [Fact]
    public void Success_SetsGreenColor()
    {
        // Act
        var embed = EmbedHelper.Success("Success Title", "Success description.");

        // Assert
        embed.Color.Should().Be(Color.Green);
    }

    [Fact]
    public void Success_SetsTitleAndDescription()
    {
        // Act
        var embed = EmbedHelper.Success("Success Title", "Success description.");

        // Assert
        embed.Title.Should().Be("Success Title");
        embed.Description.Should().Be("Success description.");
    }

    [Fact]
    public void Success_SetsTimestamp()
    {
        // Act
        var embed = EmbedHelper.Success("Success Title", "Success description.");

        // Assert
        embed.Timestamp.Should().NotBeNull();
    }

    #endregion

    #region EmptyState

    [Fact]
    public void EmptyState_SetsBlueColor()
    {
        // Act
        var embed = EmbedHelper.EmptyState("Empty Title", "No results found.");

        // Assert
        embed.Color.Should().Be(Color.Blue);
    }

    [Fact]
    public void EmptyState_SetsTitleAndDescription()
    {
        // Act
        var embed = EmbedHelper.EmptyState("Empty Title", "No results found.");

        // Assert
        embed.Title.Should().Be("Empty Title");
        embed.Description.Should().Be("No results found.");
    }

    [Fact]
    public void EmptyState_SetsTimestamp()
    {
        // Act
        var embed = EmbedHelper.EmptyState("Empty Title", "No results found.");

        // Assert
        embed.Timestamp.Should().NotBeNull();
    }

    #endregion

    #region Confirmation

    [Fact]
    public void Confirmation_SetsOrangeColor()
    {
        // Act
        var embed = EmbedHelper.Confirmation("Confirm Action", "Are you sure?");

        // Assert
        embed.Color.Should().Be(Color.Orange);
    }

    [Fact]
    public void Confirmation_SetsTitleAndDescription()
    {
        // Act
        var embed = EmbedHelper.Confirmation("Confirm Action", "Are you sure?");

        // Assert
        embed.Title.Should().Be("Confirm Action");
        embed.Description.Should().Be("Are you sure?");
    }

    [Fact]
    public void Confirmation_SetsTimestamp()
    {
        // Act
        var embed = EmbedHelper.Confirmation("Confirm Action", "Are you sure?");

        // Assert
        embed.Timestamp.Should().NotBeNull();
    }

    #endregion

    #region Info

    [Fact]
    public void Info_SetsBlueColor()
    {
        // Act
        var embed = EmbedHelper.Info("Info Title", "Informational message.");

        // Assert
        embed.Color.Should().Be(Color.Blue);
    }

    [Fact]
    public void Info_SetsTitleAndDescription()
    {
        // Act
        var embed = EmbedHelper.Info("Info Title", "Informational message.");

        // Assert
        embed.Title.Should().Be("Info Title");
        embed.Description.Should().Be("Informational message.");
    }

    [Fact]
    public void Info_SetsTimestamp()
    {
        // Act
        var embed = EmbedHelper.Info("Info Title", "Informational message.");

        // Assert
        embed.Timestamp.Should().NotBeNull();
    }

    #endregion
}
