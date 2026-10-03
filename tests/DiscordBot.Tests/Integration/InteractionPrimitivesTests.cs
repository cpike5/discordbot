using System.Net;
using System.Text.RegularExpressions;
using DiscordBot.Core.Entities;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DiscordBot.Tests.Integration;

/// <summary>
/// UX plan Phase 3: the interaction primitives render in every state on /Components, and the
/// confirmation modal's form addresses the real handler (B-8). Runs against the real app.
/// </summary>
[Collection(OfflineAppHostCollection.Name)]
public class InteractionPrimitivesTests : IClassFixture<InteractionPrimitivesTests.AppFixture>
{
    private readonly AppFixture _app;

    public InteractionPrimitivesTests(AppFixture app)
    {
        _app = app;
    }

    private async Task<string> ComponentsHtmlAsync() => await _app.Host.Client.GetStringAsync("/Components");

    [Fact]
    public async Task Components_RendersTheShowcase_WithoutSettingsScript()
    {
        var html = await ComponentsHtmlAsync();

        html.Should().Contain("Radio Cards").And.Contain("Toggle Switch").And.Contain("Form Textarea");
        html.Should().NotContain("settings.js", "the typed modal must not depend on the Settings page script");
        html.Should().NotContain("settingsManager");
        html.Should().Contain("/js/components-showcase.js");
    }

    [Fact]
    public async Task Layouts_LoadTheNewPrimitiveScripts()
    {
        var html = await ComponentsHtmlAsync();

        html.Should().Contain("/js/empty-state.js").And.Contain("/js/skeleton.js").And.Contain("/js/unsaved-changes.js");
    }

    [Fact]
    public async Task Toggle_IsASwitch_AndPostsFalseWhenOff()
    {
        var html = await ComponentsHtmlAsync();

        // The hidden false input follows the checkbox so the binder reads the first value
        html.Should().MatchRegex("<input type=\"checkbox\"\\s+id=\"demo-notify\"[^>]*role=\"switch\"[^>]*/>\\s*<span class=\"toggle-slider\"");
        html.Should().MatchRegex("id=\"demo-digest\"[\\s\\S]*?<input type=\"hidden\" name=\"Digest\" value=\"false\" />");
        html.Should().Contain("aria-describedby=\"demo-notify-desc\"");
    }

    [Fact]
    public async Task DisabledToggle_PostsNothing_NoHiddenFalse()
    {
        var html = await ComponentsHtmlAsync();

        html.Should().NotContain("name=\"tg-disabled-on\" value=\"false\"");
        Regex.Matches(html, "<input type=\"hidden\" name=\"tg-disabled-off\"").Should().BeEmpty();
        Regex.Matches(html, "<input type=\"hidden\" name=\"tg-off\" value=\"false\"").Should().HaveCount(1);
    }

    [Fact]
    public async Task FormInput_EmitsAutocompleteInputmodeAndNumericConstraints()
    {
        var html = await ComponentsHtmlAsync();

        html.Should().Contain("autocomplete=\"new-password\"");
        html.Should().Contain("autocomplete=\"email\"");
        html.Should().MatchRegex("id=\"attr-number\"[^>]*min=\"0\"[^>]*max=\"1000\"[^>]*step=\"0.01\"");
        html.Should().Contain("inputmode=\"decimal\"").And.Contain("inputmode=\"numeric\"");
        html.Should().MatchRegex("id=\"attr-describedby\"[^>]*aria-describedby=\"attr-describedby-help attr-describedby-note\"");
    }

    [Fact]
    public async Task FormInput_ErrorState_UsesTheValidationClass_AndAnnouncesTheMessage()
    {
        var html = await ComponentsHtmlAsync();

        html.Should().MatchRegex("id=\"error\"[^>]*class=\"form-input\\s+input-validation-error\"[^>]*aria-invalid=\"true\"");
        html.Should().Contain("role=\"alert\"");
    }

    [Fact]
    public async Task Textarea_RendersLabelledWithDescribedBy()
    {
        var html = await ComponentsHtmlAsync();

        html.Should().Contain("<textarea");
        html.Should().MatchRegex("<label for=\"ta-default\" class=\"form-label block\">");
        html.Should().MatchRegex("id=\"ta-error\"[^>]*class=\"form-textarea input-validation-error\"[^>]*aria-describedby=\"ta-error-error\"");
    }

    [Fact]
    public async Task RadioCards_AreRealRadios_WithAGroupLegendAndDisabledOption()
    {
        var html = await ComponentsHtmlAsync();

        html.Should().Contain("<legend class=\"form-label\">");
        html.Should().MatchRegex("<input type=\"radio\"\\s+class=\"radio-card-input\"\\s+id=\"rc-mode-recent\"[\\s\\S]*?checked");
        html.Should().MatchRegex("id=\"rc-mode-all\"[\\s\\S]*?disabled");
        html.Should().Contain("radio-card-group-invalid");
        html.Should().Contain("Choose how long to keep the data.");
    }

    [Fact]
    public async Task Pagination_DisabledEnds_AreSpans_NotLinks()
    {
        var html = await ComponentsHtmlAsync();

        html.Should().MatchRegex("<span[^>]*aria-disabled=\"true\"[^>]*aria-label=\"Previous page\"|<span[^>]*aria-label=\"Previous page\"[^>]*aria-disabled=\"true\"");
        html.Should().NotMatchRegex("<a[^>]*aria-label=\"Previous page\"[^>]*href=\"[^\"]*page=0");
        html.Should().Contain("aria-current=\"page\"");
        html.Should().Contain("aria-label=\"First page example\"");
    }

    [Fact]
    public async Task Pagination_EmptyList_SaysNoResults_NeverShowingOneToZero()
    {
        var html = await ComponentsHtmlAsync();

        html.Should().Contain("No results");
        html.Should().NotMatchRegex("Showing\\s*(<[^>]+>)?\\s*1-0");
        html.Should().NotMatchRegex("Showing\\s*(<[^>]+>)?\\s*\\d+-\\d+(</[^>]+>)?\\s*of\\s*(<[^>]+>)?\\s*0\\b");
    }

    [Fact]
    public async Task Pagination_PageSizeSelector_IsAForm_NotAnInlineHandlerCarryingTheUrl()
    {
        var html = await ComponentsHtmlAsync();

        html.Should().NotContain("window.location.href=", "the base URL carries filter values and must not reach script text");
        html.Should().MatchRegex("<form method=\"get\" action=\"/Components\"[^>]*>\\s*<input type=\"hidden\" name=\"search\" value=\"a(&#x27;|&#39;)b\" />");
        html.Should().Contain("name=\"role\" value=\"admin\"");
        html.Should().NotContain("requestSubmit", "changing the page size must not submit on its own (WCAG 3.2.2)");
        html.Should().MatchRegex("</select>\\s*<button type=\"submit\" class=\"btn btn-secondary btn-sm\">Apply</button>");
    }

    [Fact]
    public async Task EmptyState_VariantsRenderTheirActionsAndHeadingLevel()
    {
        var html = await ComponentsHtmlAsync();

        html.Should().Contain("data-action=\"retry-demo\"");
        html.Should().Contain("<h4 class=\"text-base font-semibold text-text-primary mb-1\">Could not load the log</h4>");
        html.Should().Contain("role=\"status\"");
        html.Should().Contain("Schedule a message");
    }

    [Fact]
    public async Task Skeletons_AnnounceThroughAStatus_AndHideTheirShapes()
    {
        var html = await ComponentsHtmlAsync();

        html.Should().Contain("<span class=\"sr-only\" role=\"status\">Loading users</span>");
        html.Should().MatchRegex("<div class=\"space-y-3\" aria-hidden=\"true\">");
    }

    [Fact]
    public async Task UnsavedChangesDemo_OptsInByAttribute()
    {
        var html = await ComponentsHtmlAsync();

        html.Should().MatchRegex("<form id=\"unsaved-demo\"[^>]*data-unsaved-changes");
        html.Should().MatchRegex("<form id=\"unsaved-dirty-demo\"[^>]*data-unsaved-dirty-on-load");
    }

    [Fact]
    public async Task ConfirmationModals_UseDismissAttributes_NotInlineHandlers()
    {
        var html = await ComponentsHtmlAsync();

        html.Should().Contain("data-confirm-modal");
        html.Should().Contain("data-modal-dismiss");
        html.Should().NotContain("hideConfirmationModal(");
        html.Should().NotContain("hideTypedModal");
        html.Should().MatchRegex("id=\"showcase-typed-modal\"[\\s\\S]*?data-typed-input");
    }

    [Fact]
    public async Task ConfirmationForm_PostsToTheHandlerInTheUrl()
    {
        var html = await ComponentsHtmlAsync();

        html.Should().Contain("action=\"/Components?handler=ShowcaseConfirm\"");
        html.Should().Contain("action=\"/Components?handler=ShowcaseRedirect\"");
    }

    [Fact]
    public async Task ShowcaseRedirectHandler_RedirectsWithAToastThatSurvivesToTheNextPage()
    {
        var token = OfflineAppHost.ReadAntiforgeryToken(await ComponentsHtmlAsync());

        var response = await _app.Host.Client.PostAsync(
            "/Components?handler=ShowcaseRedirect",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));

        response.RequestMessage!.RequestUri!.AbsolutePath.Should().Be("/Components");
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("The redirecting handler finished");
        html.Should().Contain("id=\"serverToasts\"");
    }

    [Fact]
    public async Task ShowcaseFailHandler_Answers400Json()
    {
        var token = OfflineAppHost.ReadAntiforgeryToken(await ComponentsHtmlAsync());
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Components?handler=ShowcaseFail")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token })
        };
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");

        var response = await _app.Host.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"success\":false");
    }

    [Fact]
    public async Task UsersEdit_ResetPasswordModal_PostsToTheUsersHandler_AndTheResetWorksEndToEnd()
    {
        var targetId = await CreateUserAsync("reset-target@example.com");
        var editPath = $"/Admin/Users/Edit?id={targetId}";
        var editHtml = await _app.Host.Client.GetStringAsync(editPath);

        // B-8: the modal's own address names the user and the handler. Before, the script posted to
        // "?handler=ResetPassword" and lost the user id, so Reset Password and Unlink never worked.
        var action = $"/Admin/Users/Edit?userId={targetId}&amp;handler=ResetPassword";
        editHtml.Should().Contain($"action=\"{action}\"");
        editHtml.Should().MatchRegex("id=\"resetPasswordModal\"[\\s\\S]*?data-confirm-modal|data-confirm-modal[\\s\\S]*?id=\"resetPasswordModal\"");

        var token = OfflineAppHost.ReadAntiforgeryToken(editHtml);
        var response = await _app.Host.Client.PostAsync(
            WebUtility.HtmlDecode(action),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["handler"] = "ResetPassword"
            }));

        // The handler redirects back to the edit page, which shows the toast and the one-time password
        response.RequestMessage!.RequestUri!.AbsolutePath.Should().Be("/Admin/Users/Edit");
        var after = await response.Content.ReadAsStringAsync();
        after.Should().Contain("Password reset successfully.");
        after.Should().Contain("id=\"serverToasts\"");
    }

    private async Task<string> CreateUserAsync(string email)
    {
        using var scope = _app.Host.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, IsActive = true };
        (await users.CreateAsync(user, "Target-Pass-123!")).Succeeded.Should().BeTrue();
        (await users.AddToRoleAsync(user, "Viewer")).Succeeded.Should().BeTrue();
        return user.Id;
    }

    public sealed class AppFixture : IAsyncLifetime
    {
        public OfflineAppHost Host { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            Host = await OfflineAppHost.StartAsync();
        }

        public async Task DisposeAsync()
        {
            await Host.DisposeAsync();
        }
    }
}
