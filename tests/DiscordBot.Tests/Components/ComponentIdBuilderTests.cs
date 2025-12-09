using DiscordBot.Bot.Components;
using FluentAssertions;

namespace DiscordBot.Tests.Components;

/// <summary>
/// Unit tests for <see cref="ComponentIdBuilder"/>.
/// </summary>
public class ComponentIdBuilderTests
{
    [Fact]
    public void Build_WithAllParameters_ReturnsCorrectFormat()
    {
        // Arrange
        const string handler = "shutdown";
        const string action = "confirm";
        const ulong userId = 123456789UL;
        const string correlationId = "abc123de";
        const string data = "extraData";

        // Act
        var result = ComponentIdBuilder.Build(handler, action, userId, correlationId, data);

        // Assert
        result.Should().Be("shutdown:confirm:123456789:abc123de:extraData",
            "the component ID should be formatted with all parts separated by colons");
    }

    [Fact]
    public void Build_WithoutData_OmitsDataSegment()
    {
        // Arrange
        const string handler = "guilds";
        const string action = "page";
        const ulong userId = 987654321UL;
        const string correlationId = "xyz789ab";

        // Act
        var result = ComponentIdBuilder.Build(handler, action, userId, correlationId);

        // Assert
        result.Should().Be("guilds:page:987654321:xyz789ab:",
            "the component ID should have an empty data segment when data is not provided");
    }

    [Fact]
    public void Build_WithEmptyData_HasEmptyDataSegment()
    {
        // Arrange
        const string handler = "test";
        const string action = "action";
        const ulong userId = 111222333UL;
        const string correlationId = "corr1234";
        const string data = "";

        // Act
        var result = ComponentIdBuilder.Build(handler, action, userId, correlationId, data);

        // Assert
        result.Should().Be("test:action:111222333:corr1234:",
            "the component ID should have an empty data segment when data is empty string");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Build_WithInvalidHandler_ThrowsArgumentException(string? invalidHandler)
    {
        // Arrange
        const string action = "action";
        const ulong userId = 123456789UL;
        const string correlationId = "abc123de";

        // Act & Assert
        FluentActions.Invoking(() => ComponentIdBuilder.Build(invalidHandler!, action, userId, correlationId))
            .Should().Throw<ArgumentException>()
            .WithMessage("*Handler cannot be null or whitespace*",
                "handler is a required parameter");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Build_WithInvalidAction_ThrowsArgumentException(string? invalidAction)
    {
        // Arrange
        const string handler = "handler";
        const ulong userId = 123456789UL;
        const string correlationId = "abc123de";

        // Act & Assert
        FluentActions.Invoking(() => ComponentIdBuilder.Build(handler, invalidAction!, userId, correlationId))
            .Should().Throw<ArgumentException>()
            .WithMessage("*Action cannot be null or whitespace*",
                "action is a required parameter");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Build_WithInvalidCorrelationId_ThrowsArgumentException(string? invalidCorrelationId)
    {
        // Arrange
        const string handler = "handler";
        const string action = "action";
        const ulong userId = 123456789UL;

        // Act & Assert
        FluentActions.Invoking(() => ComponentIdBuilder.Build(handler, action, userId, invalidCorrelationId!))
            .Should().Throw<ArgumentException>()
            .WithMessage("*Correlation ID cannot be null or whitespace*",
                "correlation ID is a required parameter");
    }

    [Fact]
    public void Parse_ValidId_ReturnsAllParts()
    {
        // Arrange
        const string customId = "shutdown:confirm:123456789:abc123de:extraData";

        // Act
        var result = ComponentIdBuilder.Parse(customId);

        // Assert
        result.Should().NotBeNull();
        result.Handler.Should().Be("shutdown");
        result.Action.Should().Be("confirm");
        result.UserId.Should().Be(123456789UL);
        result.CorrelationId.Should().Be("abc123de");
        result.Data.Should().Be("extraData");
    }

    [Fact]
    public void Parse_ValidIdWithoutData_ReturnsEmptyData()
    {
        // Arrange
        const string customId = "guilds:page:987654321:xyz789ab:";

        // Act
        var result = ComponentIdBuilder.Parse(customId);

        // Assert
        result.Should().NotBeNull();
        result.Handler.Should().Be("guilds");
        result.Action.Should().Be("page");
        result.UserId.Should().Be(987654321UL);
        result.CorrelationId.Should().Be("xyz789ab");
        result.Data.Should().BeNull("empty data segment should result in null data");
    }

    [Fact]
    public void Parse_ValidIdWithMinimumParts_ReturnsNullData()
    {
        // Arrange
        const string customId = "handler:action:111222333:corr1234";

        // Act
        var result = ComponentIdBuilder.Parse(customId);

        // Assert
        result.Should().NotBeNull();
        result.Handler.Should().Be("handler");
        result.Action.Should().Be("action");
        result.UserId.Should().Be(111222333UL);
        result.CorrelationId.Should().Be("corr1234");
        result.Data.Should().BeNull("missing data segment should result in null data");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("only:three:parts")]
    [InlineData("invalid:user:id:corr")]
    [InlineData("handler:action:notanumber:corr")]
    public void Parse_InvalidFormat_ThrowsFormatException(string invalidCustomId)
    {
        // Act & Assert
        FluentActions.Invoking(() => ComponentIdBuilder.Parse(invalidCustomId))
            .Should().Throw<FormatException>()
            .WithMessage("*Invalid component custom ID format*",
                "malformed custom IDs should throw a format exception");
    }

    [Fact]
    public void TryParse_ValidId_ReturnsTrueAndParts()
    {
        // Arrange
        const string customId = "shutdown:confirm:123456789:abc123de:extraData";

        // Act
        var success = ComponentIdBuilder.TryParse(customId, out var result);

        // Assert
        success.Should().BeTrue("parsing should succeed for valid custom ID");
        result.Should().NotBeNull();
        result.Handler.Should().Be("shutdown");
        result.Action.Should().Be("confirm");
        result.UserId.Should().Be(123456789UL);
        result.CorrelationId.Should().Be("abc123de");
        result.Data.Should().Be("extraData");
    }

    [Fact]
    public void TryParse_ValidIdWithoutData_ReturnsTrueAndNullData()
    {
        // Arrange
        const string customId = "guilds:page:987654321:xyz789ab";

        // Act
        var success = ComponentIdBuilder.TryParse(customId, out var result);

        // Assert
        success.Should().BeTrue("parsing should succeed for valid custom ID without data");
        result.Should().NotBeNull();
        result.Data.Should().BeNull("missing data segment should result in null data");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("only:three:parts")]
    [InlineData("invalid:user:id:corr")]
    [InlineData("handler:action:notanumber:corr")]
    [InlineData("handler:action:99999999999999999999:corr")] // ulong overflow
    public void TryParse_InvalidFormat_ReturnsFalseAndNull(string? invalidCustomId)
    {
        // Act
        var success = ComponentIdBuilder.TryParse(invalidCustomId!, out var result);

        // Assert
        success.Should().BeFalse("parsing should fail for invalid custom ID");
    }

    [Fact]
    public void Parse_AndBuild_RoundTrip_ProducesOriginalId()
    {
        // Arrange
        const string handler = "shutdown";
        const string action = "confirm";
        const ulong userId = 123456789UL;
        const string correlationId = "abc123de";
        const string data = "extraData";

        // Act
        var builtId = ComponentIdBuilder.Build(handler, action, userId, correlationId, data);
        var parsed = ComponentIdBuilder.Parse(builtId);
        var rebuiltId = ComponentIdBuilder.Build(
            parsed.Handler,
            parsed.Action,
            parsed.UserId,
            parsed.CorrelationId,
            parsed.Data);

        // Assert
        rebuiltId.Should().Be(builtId,
            "building and parsing should be reversible operations");
    }

    [Fact]
    public void Build_WithColonInData_CreatesIdWithMultipleParts()
    {
        // Arrange
        const string handler = "test";
        const string action = "action";
        const ulong userId = 123456789UL;
        const string correlationId = "abc123de";
        const string data = "value:with:colons";

        // Act
        var result = ComponentIdBuilder.Build(handler, action, userId, correlationId, data);

        // Assert
        result.Should().Be("test:action:123456789:abc123de:value:with:colons",
            "colons in data should be preserved in the component ID");
    }

    [Fact]
    public void Parse_WithColonInData_ExtractsOnlyFirstDataSegment()
    {
        // Arrange
        const string customId = "test:action:123456789:abc123de:value:with:colons";

        // Act
        var result = ComponentIdBuilder.Parse(customId);

        // Assert
        result.Data.Should().Be("value",
            "only the first data segment is extracted when data contains colons");
    }
}
