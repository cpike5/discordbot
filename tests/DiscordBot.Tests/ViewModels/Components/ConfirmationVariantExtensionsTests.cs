using DiscordBot.Bot.ViewModels.Components;
using FluentAssertions;

namespace DiscordBot.Tests.ViewModels.Components;

/// <summary>
/// The address a confirmation modal's form posts to. Razor Pages reads the handler from the
/// query string, so a native submit and the AJAX submit both depend on it being in the URL
/// (B-8: Users/Edit Reset Password and LinkDiscord Unlink lost their user id and handler).
/// </summary>
public class ConfirmationVariantExtensionsTests
{
    [Fact]
    public void ConfiguredAction_KeepsItsQuery_AndGainsTheHandler()
    {
        var result = ConfirmationVariantExtensions.ResolveFormAction(
            "/Admin/Users/Edit?userId=abc-123", "ResetPassword", "/Admin/Users/Edit?id=abc-123");

        result.Should().Be("/Admin/Users/Edit?userId=abc-123&handler=ResetPassword");
    }

    [Fact]
    public void EmptyAction_UsesTheCurrentPageAndItsQuery()
    {
        var result = ConfirmationVariantExtensions.ResolveFormAction(
            "", "RestartBot", "/Admin/Settings?category=BotControl");

        result.Should().Be("/Admin/Settings?category=BotControl&handler=RestartBot");
    }

    [Fact]
    public void NullAction_UsesTheCurrentPage_WithNoQuery()
    {
        var result = ConfirmationVariantExtensions.ResolveFormAction(null, "Clear", "/Commands");

        result.Should().Be("/Commands?handler=Clear");
    }

    [Fact]
    public void AnExistingHandlerIsReplaced_NotDuplicated()
    {
        var result = ConfirmationVariantExtensions.ResolveFormAction(
            null, "Reset", "/Admin/Settings?handler=Other&tab=1");

        result.Should().Be("/Admin/Settings?tab=1&handler=Reset");
    }

    [Fact]
    public void NoHandler_ReturnsTheTargetUntouched()
    {
        ConfirmationVariantExtensions.ResolveFormAction("/Account/LinkDiscord", null, "/ignored")
            .Should().Be("/Account/LinkDiscord");
        ConfirmationVariantExtensions.ResolveFormAction(null, "", "/Page?x=1")
            .Should().Be("/Page?x=1");
    }

    [Fact]
    public void QueryValues_AreEncoded_SoTheyCannotBreakOutOfTheAttribute()
    {
        var result = ConfirmationVariantExtensions.ResolveFormAction(
            null, "Go", "/Page?search=a%22%20onmouseover%3D%22x&q=a%26b");

        result.Should().Be("/Page?search=a%22%20onmouseover%3D%22x&q=a%26b&handler=Go");
    }

    [Fact]
    public void AFragmentIsDropped()
    {
        ConfirmationVariantExtensions.ResolveFormAction("/Page?x=1#section", "Go", "/")
            .Should().Be("/Page?x=1&handler=Go");
    }

    [Theory]
    [InlineData(ConfirmationVariant.Info, "accent-blue", "btn btn-accent")]
    [InlineData(ConfirmationVariant.Danger, "error", "btn btn-danger")]
    public void Variants_MapToTokensAndComponentClasses(ConfirmationVariant variant, string color, string button)
    {
        variant.ColorToken().Should().Be(color);
        variant.ConfirmButtonClass().Should().Be(button);
    }

    [Fact]
    public void WarningConfirm_UsesTheDarkInk_NotWhite()
    {
        ConfirmationVariant.Warning.ConfirmButtonClass().Should().Contain("text-on-warning");
        ConfirmationVariant.Warning.ConfirmButtonClass().Should().NotContain("text-white");
    }
}
