using DiscordBot.Bot.ViewModels.Components;
using FluentAssertions;

namespace DiscordBot.Tests.ViewModels.Components;

/// <summary>
/// Validation classes and aria-describedby, shared by the form partials.
/// </summary>
public class FormFieldHelpersTests
{
    [Theory]
    [InlineData(ValidationState.None, "")]
    [InlineData(ValidationState.Error, "input-validation-error")]
    [InlineData(ValidationState.Warning, "input-validation-warning")]
    [InlineData(ValidationState.Success, "input-validation-success")]
    public void ValidationClass_UsesTheNamesTagHelpersEmit(ValidationState state, string expected)
    {
        FormFieldHelpers.ValidationClass(state).Should().Be(expected);
    }

    [Fact]
    public void DescribedBy_PointsAtTheHelpText_WhileTheFieldHasNoState()
    {
        FormFieldHelpers.DescribedBy("email", "We never share it.", ValidationState.None, null, null)
            .Should().Be("email-help");
    }

    [Fact]
    public void DescribedBy_PointsAtTheMessage_NotTheHelpText_WhenTheFieldHasAState()
    {
        FormFieldHelpers.DescribedBy("email", "We never share it.", ValidationState.Error, "Enter an email.", null)
            .Should().Be("email-error");
        FormFieldHelpers.DescribedBy("email", "help", ValidationState.Warning, "Check this.", null)
            .Should().Be("email-warning");
        FormFieldHelpers.DescribedBy("email", "help", ValidationState.Success, "Looks good.", null)
            .Should().Be("email-success");
    }

    [Fact]
    public void DescribedBy_AppendsCallerIds_AfterItsOwn()
    {
        FormFieldHelpers.DescribedBy("pw", "At least 12 characters.", ValidationState.None, null, " strength-meter ")
            .Should().Be("pw-help strength-meter");
    }

    [Fact]
    public void DescribedBy_IsNull_WhenThereIsNothingToDescribe()
    {
        FormFieldHelpers.DescribedBy("x", null, ValidationState.None, null, null).Should().BeNull();
        FormFieldHelpers.DescribedBy("x", "help", ValidationState.Error, null, null)
            .Should().BeNull("a state with no message has no element to point at");
    }

    [Fact]
    public void RadioCard_Id_DefaultsToNameAndValue_MadeSafe()
    {
        new RadioCardViewModel { Name = "Purge.Mode", Value = "by user" }.ResolvedId.Should().Be("Purge_Mode-by_user");
        new RadioCardViewModel { Name = "mode", Value = "a", Id = "custom" }.ResolvedId.Should().Be("custom");
    }

    [Fact]
    public void RadioCardGroup_Id_DefaultsToTheName()
    {
        new RadioCardGroupViewModel { Name = "mode" }.ResolvedId.Should().Be("mode");
        new RadioCardGroupViewModel { Name = "mode", Id = "purge" }.ResolvedId.Should().Be("purge");
    }
}
