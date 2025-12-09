using System.Security.Claims;
using System.Text.Encodings.Web;
using DiscordBot.Bot.TagHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Moq;

namespace DiscordBot.Tests.Bot.TagHelpers;

/// <summary>
/// Unit tests for AuthorizeViewTagHelper and RequireRoleTagHelper.
/// </summary>
public class AuthorizeTagHelperTests
{
    #region AuthorizeViewTagHelper Tests

    [Fact]
    public async Task AuthorizeViewTagHelper_UnauthenticatedUser_SuppressesOutput()
    {
        // Arrange
        var mockAuthService = new Mock<IAuthorizationService>();
        var tagHelper = new AuthorizeViewTagHelper(mockAuthService.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(); // Unauthenticated

        tagHelper.ViewContext = new ViewContext
        {
            HttpContext = httpContext
        };
        tagHelper.Policy = "RequireAdmin";

        var tagHelperContext = CreateTagHelperContext();
        var tagHelperOutput = CreateTagHelperOutput("authorize-view");
        tagHelperOutput.Content.SetHtmlContent("<p>Admin content</p>");

        // Act
        await tagHelper.ProcessAsync(tagHelperContext, tagHelperOutput);

        // Assert
        tagHelperOutput.TagName.Should().BeNull("tag should not be rendered");
        tagHelperOutput.Content.GetContent().Should().BeEmpty("output should be suppressed");

        mockAuthService.Verify(
            s => s.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), It.IsAny<string>()),
            Times.Never,
            "authorization service should not be called for unauthenticated users");
    }

    [Fact]
    public async Task AuthorizeViewTagHelper_WithPolicyThatSucceeds_RendersContent()
    {
        // Arrange
        var mockAuthService = new Mock<IAuthorizationService>();
        mockAuthService
            .Setup(s => s.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<object?>(),
                It.IsAny<string>()))
            .ReturnsAsync(AuthorizationResult.Success());

        var tagHelper = new AuthorizeViewTagHelper(mockAuthService.Object);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "user123"),
            new Claim(ClaimTypes.Name, "testuser")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = user };
        tagHelper.ViewContext = new ViewContext { HttpContext = httpContext };
        tagHelper.Policy = "RequireAdmin";

        var tagHelperContext = CreateTagHelperContext();
        var tagHelperOutput = CreateTagHelperOutput("authorize-view");
        tagHelperOutput.Content.SetHtmlContent("<p>Admin content</p>");

        // Act
        await tagHelper.ProcessAsync(tagHelperContext, tagHelperOutput);

        // Assert
        tagHelperOutput.TagName.Should().BeNull("wrapper tag should not be rendered");
        tagHelperOutput.Content.GetContent(HtmlEncoder.Default).Should().Be("<p>Admin content</p>",
            "content should be rendered when authorization succeeds");

        mockAuthService.Verify(
            s => s.AuthorizeAsync(
                It.Is<ClaimsPrincipal>(p => p == user),
                It.IsAny<object?>(),
                It.IsAny<string>()),
            Times.Once,
            "authorization service should be called once");
    }

    [Fact]
    public async Task AuthorizeViewTagHelper_WithPolicyThatFails_SuppressesOutput()
    {
        // Arrange
        var mockAuthService = new Mock<IAuthorizationService>();
        mockAuthService
            .Setup(s => s.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<object?>(),
                It.IsAny<string>()))
            .ReturnsAsync(AuthorizationResult.Failed());

        var tagHelper = new AuthorizeViewTagHelper(mockAuthService.Object);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "user123"),
            new Claim(ClaimTypes.Name, "testuser")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = user };
        tagHelper.ViewContext = new ViewContext { HttpContext = httpContext };
        tagHelper.Policy = "RequireAdmin";

        var tagHelperContext = CreateTagHelperContext();
        var tagHelperOutput = CreateTagHelperOutput("authorize-view");
        tagHelperOutput.Content.SetHtmlContent("<p>Admin content</p>");

        // Act
        await tagHelper.ProcessAsync(tagHelperContext, tagHelperOutput);

        // Assert
        tagHelperOutput.TagName.Should().BeNull("wrapper tag should not be rendered");
        tagHelperOutput.Content.GetContent().Should().BeEmpty("output should be suppressed");

        mockAuthService.Verify(
            s => s.AuthorizeAsync(
                It.Is<ClaimsPrincipal>(p => p == user),
                It.IsAny<object?>(),
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task AuthorizeViewTagHelper_WithRolesThatMatch_RendersContent()
    {
        // Arrange
        var mockAuthService = new Mock<IAuthorizationService>();
        var tagHelper = new AuthorizeViewTagHelper(mockAuthService.Object);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "user123"),
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Role, "Admin")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = user };
        tagHelper.ViewContext = new ViewContext { HttpContext = httpContext };
        tagHelper.Roles = "Admin,Moderator";

        var tagHelperContext = CreateTagHelperContext();
        var tagHelperOutput = CreateTagHelperOutput("authorize-view");
        tagHelperOutput.Content.SetHtmlContent("<p>Admin content</p>");

        // Act
        await tagHelper.ProcessAsync(tagHelperContext, tagHelperOutput);

        // Assert
        tagHelperOutput.TagName.Should().BeNull();
        tagHelperOutput.Content.GetContent(HtmlEncoder.Default).Should().Be("<p>Admin content</p>",
            "content should be rendered for matching role");

        mockAuthService.Verify(
            s => s.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), It.IsAny<string>()),
            Times.Never,
            "authorization service should not be called when using roles");
    }

    [Fact]
    public async Task AuthorizeViewTagHelper_WithRolesThatDoNotMatch_SuppressesOutput()
    {
        // Arrange
        var mockAuthService = new Mock<IAuthorizationService>();
        var tagHelper = new AuthorizeViewTagHelper(mockAuthService.Object);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "user123"),
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Role, "Viewer")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = user };
        tagHelper.ViewContext = new ViewContext { HttpContext = httpContext };
        tagHelper.Roles = "Admin,Moderator";

        var tagHelperContext = CreateTagHelperContext();
        var tagHelperOutput = CreateTagHelperOutput("authorize-view");
        tagHelperOutput.Content.SetHtmlContent("<p>Admin content</p>");

        // Act
        await tagHelper.ProcessAsync(tagHelperContext, tagHelperOutput);

        // Assert
        tagHelperOutput.TagName.Should().BeNull();
        tagHelperOutput.Content.GetContent().Should().BeEmpty("output should be suppressed for non-matching role");
    }

    [Fact]
    public async Task AuthorizeViewTagHelper_WithMultipleRoles_MatchesAny()
    {
        // Arrange
        var mockAuthService = new Mock<IAuthorizationService>();
        var tagHelper = new AuthorizeViewTagHelper(mockAuthService.Object);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "user123"),
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Role, "Moderator")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = user };
        tagHelper.ViewContext = new ViewContext { HttpContext = httpContext };
        tagHelper.Roles = "SuperAdmin, Admin, Moderator"; // With spaces

        var tagHelperContext = CreateTagHelperContext();
        var tagHelperOutput = CreateTagHelperOutput("authorize-view");
        tagHelperOutput.Content.SetHtmlContent("<p>Content</p>");

        // Act
        await tagHelper.ProcessAsync(tagHelperContext, tagHelperOutput);

        // Assert
        tagHelperOutput.Content.GetContent(HtmlEncoder.Default).Should().Be("<p>Content</p>",
            "content should be rendered if user has any of the specified roles");
    }

    [Fact]
    public async Task AuthorizeViewTagHelper_WithNoPolicyOrRoles_RequiresOnlyAuthentication()
    {
        // Arrange
        var mockAuthService = new Mock<IAuthorizationService>();
        var tagHelper = new AuthorizeViewTagHelper(mockAuthService.Object);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "user123"),
            new Claim(ClaimTypes.Name, "testuser")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = user };
        tagHelper.ViewContext = new ViewContext { HttpContext = httpContext };
        // No policy or roles set

        var tagHelperContext = CreateTagHelperContext();
        var tagHelperOutput = CreateTagHelperOutput("authorize-view");
        tagHelperOutput.Content.SetHtmlContent("<p>Content</p>");

        // Act
        await tagHelper.ProcessAsync(tagHelperContext, tagHelperOutput);

        // Assert
        tagHelperOutput.Content.GetContent(HtmlEncoder.Default).Should().Be("<p>Content</p>",
            "authenticated user should see content when no policy or roles specified");
    }

    [Fact]
    public async Task AuthorizeViewTagHelper_PolicyTakesPrecedenceOverRoles()
    {
        // Arrange
        var mockAuthService = new Mock<IAuthorizationService>();
        mockAuthService
            .Setup(s => s.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<object?>(),
                It.IsAny<string>()))
            .ReturnsAsync(AuthorizationResult.Success());

        var tagHelper = new AuthorizeViewTagHelper(mockAuthService.Object);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "user123"),
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Role, "Viewer") // Has Viewer role
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = user };
        tagHelper.ViewContext = new ViewContext { HttpContext = httpContext };
        tagHelper.Policy = "RequireAdmin"; // Policy is set
        tagHelper.Roles = "Admin"; // Roles also set but should be ignored

        var tagHelperContext = CreateTagHelperContext();
        var tagHelperOutput = CreateTagHelperOutput("authorize-view");
        tagHelperOutput.Content.SetHtmlContent("<p>Content</p>");

        // Act
        await tagHelper.ProcessAsync(tagHelperContext, tagHelperOutput);

        // Assert
        tagHelperOutput.Content.GetContent(HtmlEncoder.Default).Should().Be("<p>Content</p>",
            "policy should take precedence and succeed even though role doesn't match");

        mockAuthService.Verify(
            s => s.AuthorizeAsync(
                It.Is<ClaimsPrincipal>(p => p == user),
                It.IsAny<object?>(),
                It.IsAny<string>()),
            Times.Once,
            "policy should be checked");
    }

    #endregion

    #region RequireRoleTagHelper Tests

    [Fact]
    public void RequireRoleTagHelper_UnauthenticatedUser_SuppressesOutput()
    {
        // Arrange
        var tagHelper = new RequireRoleTagHelper();

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(); // Unauthenticated

        tagHelper.ViewContext = new ViewContext
        {
            HttpContext = httpContext
        };
        tagHelper.Roles = "Admin";

        var tagHelperContext = CreateTagHelperContext();
        var tagHelperOutput = CreateTagHelperOutput("require-role");
        tagHelperOutput.Content.SetHtmlContent("<button>Admin Action</button>");

        // Act
        tagHelper.Process(tagHelperContext, tagHelperOutput);

        // Assert
        tagHelperOutput.TagName.Should().BeNull("tag should not be rendered");
        tagHelperOutput.Content.GetContent().Should().BeEmpty("output should be suppressed");
    }

    [Fact]
    public void RequireRoleTagHelper_WithMatchingRole_RendersContent()
    {
        // Arrange
        var tagHelper = new RequireRoleTagHelper();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "user123"),
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Role, "Admin")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = user };
        tagHelper.ViewContext = new ViewContext { HttpContext = httpContext };
        tagHelper.Roles = "Admin";

        var tagHelperContext = CreateTagHelperContext();
        var tagHelperOutput = CreateTagHelperOutput("require-role");
        tagHelperOutput.Content.SetHtmlContent("<button>Admin Action</button>");

        // Act
        tagHelper.Process(tagHelperContext, tagHelperOutput);

        // Assert
        tagHelperOutput.TagName.Should().BeNull("wrapper tag should not be rendered");
        tagHelperOutput.Content.GetContent(HtmlEncoder.Default).Should().Be("<button>Admin Action</button>",
            "content should be rendered for matching role");
    }

    [Fact]
    public void RequireRoleTagHelper_WithNonMatchingRole_SuppressesOutput()
    {
        // Arrange
        var tagHelper = new RequireRoleTagHelper();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "user123"),
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Role, "Viewer")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = user };
        tagHelper.ViewContext = new ViewContext { HttpContext = httpContext };
        tagHelper.Roles = "Admin,Moderator";

        var tagHelperContext = CreateTagHelperContext();
        var tagHelperOutput = CreateTagHelperOutput("require-role");
        tagHelperOutput.Content.SetHtmlContent("<button>Admin Action</button>");

        // Act
        tagHelper.Process(tagHelperContext, tagHelperOutput);

        // Assert
        tagHelperOutput.TagName.Should().BeNull();
        tagHelperOutput.Content.GetContent().Should().BeEmpty("output should be suppressed for non-matching role");
    }

    [Fact]
    public void RequireRoleTagHelper_WithMultipleRoles_MatchesAny()
    {
        // Arrange
        var tagHelper = new RequireRoleTagHelper();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "user123"),
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Role, "Moderator")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = user };
        tagHelper.ViewContext = new ViewContext { HttpContext = httpContext };
        tagHelper.Roles = "SuperAdmin,Admin,Moderator"; // Comma-separated, no spaces

        var tagHelperContext = CreateTagHelperContext();
        var tagHelperOutput = CreateTagHelperOutput("require-role");
        tagHelperOutput.Content.SetHtmlContent("<button>Moderator Action</button>");

        // Act
        tagHelper.Process(tagHelperContext, tagHelperOutput);

        // Assert
        tagHelperOutput.Content.GetContent(HtmlEncoder.Default).Should().Be("<button>Moderator Action</button>",
            "content should be rendered if user has any of the specified roles");
    }

    [Fact]
    public void RequireRoleTagHelper_WithRolesContainingSpaces_TrimsCorrectly()
    {
        // Arrange
        var tagHelper = new RequireRoleTagHelper();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "user123"),
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Role, "Admin")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = user };
        tagHelper.ViewContext = new ViewContext { HttpContext = httpContext };
        tagHelper.Roles = " SuperAdmin , Admin , Moderator "; // With extra spaces

        var tagHelperContext = CreateTagHelperContext();
        var tagHelperOutput = CreateTagHelperOutput("require-role");
        tagHelperOutput.Content.SetHtmlContent("<p>Content</p>");

        // Act
        tagHelper.Process(tagHelperContext, tagHelperOutput);

        // Assert
        tagHelperOutput.Content.GetContent(HtmlEncoder.Default).Should().Be("<p>Content</p>",
            "spaces should be trimmed and Admin role should match");
    }

    [Fact]
    public void RequireRoleTagHelper_WithEmptyRoles_SuppressesOutput()
    {
        // Arrange
        var tagHelper = new RequireRoleTagHelper();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "user123"),
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Role, "Admin")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = user };
        tagHelper.ViewContext = new ViewContext { HttpContext = httpContext };
        tagHelper.Roles = ""; // Empty roles string

        var tagHelperContext = CreateTagHelperContext();
        var tagHelperOutput = CreateTagHelperOutput("require-role");
        tagHelperOutput.Content.SetHtmlContent("<p>Content</p>");

        // Act
        tagHelper.Process(tagHelperContext, tagHelperOutput);

        // Assert
        tagHelperOutput.Content.GetContent().Should().BeEmpty(
            "output should be suppressed when no roles are specified");
    }

    [Fact]
    public void RequireRoleTagHelper_WithSingleRole_Works()
    {
        // Arrange
        var tagHelper = new RequireRoleTagHelper();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "user123"),
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Role, "SuperAdmin")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = user };
        tagHelper.ViewContext = new ViewContext { HttpContext = httpContext };
        tagHelper.Roles = "SuperAdmin";

        var tagHelperContext = CreateTagHelperContext();
        var tagHelperOutput = CreateTagHelperOutput("require-role");
        tagHelperOutput.Content.SetHtmlContent("<p>Content</p>");

        // Act
        tagHelper.Process(tagHelperContext, tagHelperOutput);

        // Assert
        tagHelperOutput.Content.GetContent(HtmlEncoder.Default).Should().Be("<p>Content</p>",
            "content should be rendered for matching single role");
    }

    [Fact]
    public void RequireRoleTagHelper_UserWithMultipleRoles_MatchesCorrectly()
    {
        // Arrange
        var tagHelper = new RequireRoleTagHelper();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "user123"),
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(ClaimTypes.Role, "Moderator")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = user };
        tagHelper.ViewContext = new ViewContext { HttpContext = httpContext };
        tagHelper.Roles = "Admin";

        var tagHelperContext = CreateTagHelperContext();
        var tagHelperOutput = CreateTagHelperOutput("require-role");
        tagHelperOutput.Content.SetHtmlContent("<p>Content</p>");

        // Act
        tagHelper.Process(tagHelperContext, tagHelperOutput);

        // Assert
        tagHelperOutput.Content.GetContent(HtmlEncoder.Default).Should().Be("<p>Content</p>",
            "content should be rendered when user has multiple roles and one matches");
    }

    #endregion

    #region Helper Methods

    private static TagHelperContext CreateTagHelperContext()
    {
        return new TagHelperContext(
            tagName: "test",
            allAttributes: new TagHelperAttributeList(),
            items: new Dictionary<object, object>(),
            uniqueId: Guid.NewGuid().ToString());
    }

    private static TagHelperOutput CreateTagHelperOutput(string tagName)
    {
        return new TagHelperOutput(
            tagName: tagName,
            attributes: new TagHelperAttributeList(),
            getChildContentAsync: (useCachedResult, encoder) =>
            {
                var tagHelperContent = new DefaultTagHelperContent();
                return Task.FromResult<TagHelperContent>(tagHelperContent);
            });
    }

    #endregion
}
