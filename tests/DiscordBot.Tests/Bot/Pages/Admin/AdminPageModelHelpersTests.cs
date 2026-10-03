using DiscordBot.Bot.Pages.Admin;
using DiscordBot.Bot.Pages.Admin.Users;
using DiscordBot.Core.DTOs;
using FluentAssertions;
using Xunit;

namespace DiscordBot.Tests.Bot.Pages.Admin;

/// <summary>
/// The small decisions Phase 11 moved into the page models: which tab shows, and which form
/// field a service failure belongs to.
/// </summary>
public class AdminPageModelHelpersTests
{
    [Theory]
    [InlineData("General", false, "General")]
    [InlineData("commands", false, "Commands")]
    [InlineData("BotControl", false, "BotControl")]
    [InlineData("AiModels", false, "AiModels")]
    [InlineData("Appearance", true, "Appearance")]
    [InlineData("Appearance", false, "General")]
    [InlineData("Nope", true, "General")]
    [InlineData("", true, "General")]
    [InlineData(null, true, "General")]
    public void ResolveTab_AcceptsOnlyTabsTheUserCanSee(string? requested, bool superAdmin, string expected)
    {
        SettingsModel.ResolveTab(requested, superAdmin).Should().Be(expected);
    }

    [Theory]
    [InlineData(UserManagementResult.EmailAlreadyExists, "Input.Email")]
    [InlineData(UserManagementResult.PasswordValidationFailed, "Input.Password")]
    [InlineData(UserManagementResult.InsufficientPermissions, "Input.Role")]
    [InlineData(UserManagementResult.InvalidRole, "Input.Role")]
    [InlineData(UserManagementResult.UserNotFound, "")]
    [InlineData(null, "")]
    public void CreateUser_PutsServiceErrorsOnTheirField(string? errorCode, string expectedKey)
    {
        CreateModel.FieldForError(errorCode).Should().Be(expectedKey);
    }

    [Theory]
    [InlineData(UserManagementResult.EmailAlreadyExists, "Input.Email")]
    [InlineData(UserManagementResult.InvalidRole, "Input.Role")]
    [InlineData(UserManagementResult.SelfModificationDenied, "")]
    public void EditUser_PutsServiceErrorsOnTheirField(string errorCode, string expectedKey)
    {
        EditModel.FieldForError(errorCode).Should().Be(expectedKey);
    }

    [Fact]
    public void TidyIdentityMessage_TurnsJoinedErrorsIntoSentences()
    {
        var joined = "Passwords must have at least one digit ('0'-'9')., Passwords must have at least one uppercase ('A'-'Z').";

        CreateModel.TidyIdentityMessage(joined)
            .Should().Be("Passwords must have at least one digit ('0'-'9'). Passwords must have at least one uppercase ('A'-'Z').");
        CreateModel.TidyIdentityMessage(null).Should().BeNull();
        CreateModel.TidyIdentityMessage("A user with this email already exists").Should().Be("A user with this email already exists");
    }
}
