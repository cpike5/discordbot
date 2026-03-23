using DiscordBot.Bot.Controllers;
using DiscordBot.Core.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Tests.Controllers;

/// <summary>
/// Unit tests for <see cref="ApiControllerBase"/> error response helpers.
/// </summary>
public class ApiControllerBaseTests
{
    private readonly TestableApiController _controller;

    public ApiControllerBaseTests()
    {
        _controller = new TestableApiController();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Fact]
    public void NotFoundError_ShouldReturnNotFoundWithApiErrorDto()
    {
        // Act
        var result = _controller.TestNotFoundError("Resource not found", "No item with ID 123 exists.");

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
        var objectResult = (NotFoundObjectResult)result;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);

        var errorDto = objectResult.Value.Should().BeOfType<ApiErrorDto>().Subject;
        errorDto.Message.Should().Be("Resource not found");
        errorDto.Detail.Should().Be("No item with ID 123 exists.");
        errorDto.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        errorDto.TraceId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void NotFoundError_WithoutDetail_ShouldReturnNullDetail()
    {
        // Act
        var result = _controller.TestNotFoundError("Not found");

        // Assert
        var objectResult = (NotFoundObjectResult)result;
        var errorDto = objectResult.Value.Should().BeOfType<ApiErrorDto>().Subject;
        errorDto.Message.Should().Be("Not found");
        errorDto.Detail.Should().BeNull();
        errorDto.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public void BadRequestError_ShouldReturnBadRequestWithApiErrorDto()
    {
        // Act
        var result = _controller.TestBadRequestError("Invalid request", "Request body cannot be null.");

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var objectResult = (BadRequestObjectResult)result;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var errorDto = objectResult.Value.Should().BeOfType<ApiErrorDto>().Subject;
        errorDto.Message.Should().Be("Invalid request");
        errorDto.Detail.Should().Be("Request body cannot be null.");
        errorDto.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        errorDto.TraceId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void BadRequestError_WithoutDetail_ShouldReturnNullDetail()
    {
        // Act
        var result = _controller.TestBadRequestError("Bad request");

        // Assert
        var objectResult = (BadRequestObjectResult)result;
        var errorDto = objectResult.Value.Should().BeOfType<ApiErrorDto>().Subject;
        errorDto.Message.Should().Be("Bad request");
        errorDto.Detail.Should().BeNull();
        errorDto.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void ValidationError_ShouldReturn422WithApiErrorDto()
    {
        // Arrange
        var errors = new Dictionary<string, string[]>
        {
            { "Name", new[] { "Name is required", "Name must be at least 3 characters" } },
            { "Email", new[] { "Email is invalid" } }
        };

        // Act
        var result = _controller.TestValidationError("Validation failed", errors);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)result;
        objectResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);

        var errorDto = objectResult.Value.Should().BeOfType<ApiErrorDto>().Subject;
        errorDto.Message.Should().Be("Validation failed");
        errorDto.Detail.Should().Contain("Name");
        errorDto.Detail.Should().Contain("Name is required");
        errorDto.Detail.Should().Contain("Email is invalid");
        errorDto.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        errorDto.TraceId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ValidationError_WithoutErrors_ShouldReturnNullDetail()
    {
        // Act
        var result = _controller.TestValidationError("Validation failed");

        // Assert
        var objectResult = (ObjectResult)result;
        objectResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);

        var errorDto = objectResult.Value.Should().BeOfType<ApiErrorDto>().Subject;
        errorDto.Message.Should().Be("Validation failed");
        errorDto.Detail.Should().BeNull();
    }

    [Fact]
    public void AllHelpers_ShouldIncludeTraceId()
    {
        // Act
        var notFoundResult = (NotFoundObjectResult)_controller.TestNotFoundError("nf");
        var badRequestResult = (BadRequestObjectResult)_controller.TestBadRequestError("br");
        var validationResult = (ObjectResult)_controller.TestValidationError("ve");

        // Assert
        var nfDto = notFoundResult.Value.Should().BeOfType<ApiErrorDto>().Subject;
        var brDto = badRequestResult.Value.Should().BeOfType<ApiErrorDto>().Subject;
        var veDto = validationResult.Value.Should().BeOfType<ApiErrorDto>().Subject;

        nfDto.TraceId.Should().NotBeNullOrEmpty();
        brDto.TraceId.Should().NotBeNullOrEmpty();
        veDto.TraceId.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// Concrete test subclass to expose protected methods for testing.
    /// </summary>
    private class TestableApiController : ApiControllerBase
    {
        public NotFoundObjectResult TestNotFoundError(string message, string? detail = null)
            => NotFoundError(message, detail);

        public BadRequestObjectResult TestBadRequestError(string message, string? detail = null)
            => BadRequestError(message, detail);

        public ObjectResult TestValidationError(string message, IDictionary<string, string[]>? errors = null)
            => ValidationError(message, errors);
    }
}
