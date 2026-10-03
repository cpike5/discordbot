using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DiscordBot.Tests.Integration;

/// <summary>
/// UX plan Phase 11 against the real app: a purge redirects (so a refresh cannot run it again),
/// and the Settings tab lives in the address and saves only itself.
/// </summary>
[Collection(OfflineAppHostCollection.Name)]
public class AdminPurgeAndSettingsTests : IClassFixture<AdminPurgeAndSettingsTests.AppFixture>
{
    private const ulong TargetUserId = 111111111111111111UL;

    private readonly AppFixture _app;

    public AdminPurgeAndSettingsTests(AppFixture app)
    {
        _app = app;
    }

    private HttpClient Client => _app.Host.Client;

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string pageHtml, string url, Dictionary<string, string> fields)
    {
        var body = new Dictionary<string, string>(fields)
        {
            ["__RequestVerificationToken"] = OfflineAppHost.ReadAntiforgeryToken(pageHtml)
        };
        return await client.PostAsync(url, new FormUrlEncodedContent(body));
    }

    // ---- Bulk purge

    [Fact]
    public async Task BulkPurge_Execute_RedirectsToTheGet_AndARefreshDoesNotPurgeAgain()
    {
        await SeedAuditLogsAsync(count: 4);
        var previewUrl = "/Admin/BulkPurge?Preview=true&EntityType=AuditLogs";
        var previewHtml = await Client.GetStringAsync(previewUrl);
        previewHtml.Should().Contain("Purge ").And.Contain("audit logs");

        var response = await PostAsync(Client, previewHtml, "/Admin/BulkPurge?handler=Execute", new Dictionary<string, string>
        {
            ["EntityType"] = "AuditLogs"
        });

        // The browser ends on a GET of the page itself, not on the POST
        response.RequestMessage!.Method.Should().Be(HttpMethod.Get);
        response.RequestMessage.RequestUri!.AbsolutePath.Should().Be("/Admin/BulkPurge");
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Purge complete").And.Contain("audit logs");

        // A refresh repeats the GET only. A row written after the purge would be deleted by a
        // second run (the criteria are "all time"), so it surviving shows nothing re-ran.
        await SeedAuditLogsAsync(count: 1, targetType: "after-purge");
        var refreshed = await Client.GetStringAsync(response.RequestMessage.RequestUri.PathAndQuery);
        refreshed.Should().NotContain("Purge complete");
        (await AuditLogCountAsync("after-purge")).Should().Be(1, "refreshing after a purge must not run it again");
    }

    [Fact]
    public async Task BulkPurge_Preview_IsAGet_WithFriendlyNames_AndLabelledRadios()
    {
        await SeedAuditLogsAsync(count: 2);

        var html = await Client.GetStringAsync("/Admin/BulkPurge?Preview=true&EntityType=AuditLogs");

        html.Should().Contain("Audit logs").And.Contain("Message logs").And.Contain("Moderation cases");
        html.Should().NotContain(">ModerationCases<").And.NotContain(">AuditLogs<");
        html.Should().Contain("class=\"radio-card-input\"", "the record types are real radios (visible focus, keyboard reachable)");
        html.Should().Contain("data-submit-guard");
        html.Should().Contain("data-confirm-required=\"CONFIRM\"");
    }

    [Fact]
    public async Task BulkPurge_WithNoRecordType_ShowsTheErrorOnTheGroup()
    {
        var html = await Client.GetStringAsync("/Admin/BulkPurge?Preview=true");

        html.Should().Contain("Choose which records to purge.");
        html.Should().Contain("radio-card-group-invalid");
    }

    // ---- User purge

    [Fact]
    public async Task UserPurge_Execute_RedirectsWithoutTheUserId_AndShowsTheResultOnce()
    {
        await SeedDiscordUserAsync(TargetUserId + 1, "purge_me");
        var previewHtml = await Client.GetStringAsync($"/Admin/UserPurge?DiscordUserId={TargetUserId + 1}");

        // The username is in the confirm text, from a data attribute (never an inline script)
        previewHtml.Should().Contain("purge_me");
        previewHtml.Should().Contain($"data-confirm-required=\"{TargetUserId + 1}\"");
        previewHtml.Should().NotContain("onclick=\"confirmPurge");

        var response = await PostAsync(Client, previewHtml, "/Admin/UserPurge", new Dictionary<string, string>
        {
            ["DiscordUserId"] = (TargetUserId + 1).ToString()
        });

        response.RequestMessage!.Method.Should().Be(HttpMethod.Get);
        response.RequestMessage.RequestUri!.PathAndQuery.Should().Be("/Admin/UserPurge");
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Purge complete").And.Contain("purge_me");
        html.Should().NotContain("RatRecords_Anonymized", "counts use plain names");

        (await Client.GetStringAsync("/Admin/UserPurge")).Should().NotContain("Purge complete");
    }

    [Fact]
    public async Task UserPurge_PreviewShowsPlainDataNames()
    {
        await SeedDiscordUserAsync(TargetUserId + 2, "preview_user");

        var html = await Client.GetStringAsync($"/Admin/UserPurge?DiscordUserId={TargetUserId + 2}");

        html.Should().Contain("Message logs").And.Contain("Rat Watch records");
        html.Should().Contain("Anonymized, not deleted");
        html.Should().NotContain("_Anonymized").And.NotContain("RatRecords");
    }

    // ---- Settings

    [Theory]
    [InlineData("Commands", "Commands")]
    [InlineData("BotControl", "BotControl")]
    [InlineData("advanced", "Advanced")]
    [InlineData("nonsense", "General")]
    [InlineData(null, "General")]
    public async Task Settings_CategoryInTheAddress_PicksTheActiveTab(string? category, string expectedTab)
    {
        var url = category == null ? "/Admin/Settings" : $"/Admin/Settings?category={category}";
        var html = await Client.GetStringAsync(url);

        html.Should().MatchRegex($"<button[^>]*id=\"settingsTabs-tab-{expectedTab}\"[^>]*aria-selected=\"true\"|aria-selected=\"true\"[^>]*id=\"settingsTabs-tab-{expectedTab}\"");
        html.Should().MatchRegex($"id=\"settingsTabs-panel-{expectedTab}\"[^>]*class=\"active\"");
        // Only the active panel is visible
        Regex.Matches(html, "data-tab-panel-for=\"settingsTabs\"[^>]*class=\"active\"").Should().HaveCount(1);
    }

    [Fact]
    public async Task Settings_RendersLabelledTabs_AndPerTabForms()
    {
        var html = await Client.GetStringAsync("/Admin/Settings");

        html.Should().Contain("role=\"tablist\"");
        html.Should().MatchRegex("id=\"settingsTabs-panel-General\"[^>]*aria-labelledby=\"settingsTabs-tab-General\"");
        foreach (var tab in new[] { "General", "Features", "Commands", "Advanced", "AiModels" })
        {
            html.Should().Contain($"data-settings-form=\"{tab}\"");
        }
        html.Should().Contain("data-settings-form=\"Appearance\"", "the seeded admin is a SuperAdmin");
        html.Should().NotContain("form-toggle", "the toggles are the shared partial now");
        html.Should().NotContain("onclick=\"window.settingsManager");
        html.Should().Contain("id=\"restartBanner\"");
    }

    [Fact]
    public async Task Settings_Appearance_FallsBackToGeneral_ForANonSuperAdmin()
    {
        using var admin = await _app.Host.CreateSignedInClientAsync("settings-admin@example.com", "Admin");

        var html = await admin.GetStringAsync("/Admin/Settings?category=Appearance");

        html.Should().NotContain("data-settings-form=\"Appearance\"");
        html.Should().MatchRegex("id=\"settingsTabs-tab-General\"[^>]*aria-selected=\"true\"|aria-selected=\"true\"[^>]*id=\"settingsTabs-tab-General\"");
    }

    [Fact]
    public async Task Settings_SaveCategory_KeepsOnlyThatTabsKeys()
    {
        var page = await Client.GetStringAsync("/Admin/Settings");
        var advancedBefore = await ReadSettingAsync(SettingCategory.Advanced);
        var advancedKey = advancedBefore.Key;

        // A whole-page style post to the General tab that also carries an Advanced key
        var response = await PostAsync(Client, page, "/Admin/Settings?handler=SaveCategory&category=General", new Dictionary<string, string>
        {
            [$"FormSettings[{advancedKey}]"] = "9999"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        json.GetProperty("success").GetBoolean().Should().BeTrue();
        json.GetProperty("changeCount").GetInt32().Should().Be(0, "no General key was sent, so nothing changed");
        (await ReadSettingAsync(SettingCategory.Advanced)).Value.Should().Be(advancedBefore.Value);
    }

    [Fact]
    public async Task Settings_ResetCategory_RedirectsBackToTheTab_WithAToast()
    {
        var page = await Client.GetStringAsync("/Admin/Settings?category=Advanced");

        var response = await PostAsync(Client, page, "/Admin/Settings?category=Advanced&handler=ResetCategory", new Dictionary<string, string>());

        response.RequestMessage!.Method.Should().Be(HttpMethod.Get);
        response.RequestMessage.RequestUri!.PathAndQuery.Should().Be("/Admin/Settings?category=Advanced");
        (await response.Content.ReadAsStringAsync()).Should().Contain("Advanced settings were reset to their defaults.");
    }

    // ---- helpers

    private async Task<DiscordBot.Core.DTOs.SettingDto> ReadSettingAsync(SettingCategory category)
    {
        using var scope = _app.Host.Services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        return (await settings.GetSettingsByCategoryAsync(category)).First();
    }

    private async Task SeedAuditLogsAsync(int count, string? targetType = null)
    {
        using var scope = _app.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DiscordBot.Infrastructure.Data.BotDbContext>();
        for (var i = 0; i < count; i++)
        {
            db.AuditLogs.Add(new AuditLog
            {
                Timestamp = DateTime.UtcNow.AddDays(-i - 1),
                Category = AuditLogCategory.User,
                Action = AuditLogAction.Created,
                ActorType = AuditLogActorType.System,
                TargetType = targetType
            });
        }
        await db.SaveChangesAsync();
    }

    private async Task<int> AuditLogCountAsync(string targetType)
    {
        using var scope = _app.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DiscordBot.Infrastructure.Data.BotDbContext>();
        return await db.AuditLogs.CountAsync(a => a.TargetType == targetType);
    }

    private async Task SeedDiscordUserAsync(ulong id, string username)
    {
        using var scope = _app.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DiscordBot.Infrastructure.Data.BotDbContext>();
        db.Users.Add(new User { Id = id, Username = username, FirstSeenAt = DateTime.UtcNow, LastSeenAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
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
