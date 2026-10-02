using System.Net;
using System.Text.RegularExpressions;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DiscordBot.Tests.Integration;

/// <summary>
/// Phase 0b of the UX polish plan: stored user text renders inert on the pages that show
/// it, admin-only and guild-scoped actions refuse other callers. Runs against the real app
/// (<see cref="OfflineAppHost"/>) with hostile names seeded into the database.
/// </summary>
[Collection(OfflineAppHostCollection.Name)]
public class SecurityHardeningTests : IClassFixture<SecurityHardeningTests.AppFixture>
{
    /// <summary>
    /// A name that breaks out of a quoted JavaScript string, a quoted attribute and element
    /// content. "Pwn" marks it so the test can find every place it lands.
    /// </summary>
    private const string Hostile = "Pwn'\"<img src=x onerror=alert(1)>";

    private static readonly Regex InlineHandler = new("\\son[a-z]+=\"([^\"]*)\"", RegexOptions.IgnoreCase);

    private readonly AppFixture _app;

    public SecurityHardeningTests(AppFixture app)
    {
        _app = app;
    }

    [Theory]
    [InlineData("/Guilds/Soundboard/{0}")]
    [InlineData("/Guilds/ScheduledMessages/{0}")]
    [InlineData("/Guilds/TextToSpeech/{0}")]
    [InlineData("/Guilds/ModerationSettings/{0}")]
    public async Task StoredUserText_RendersInert(string urlFormat)
    {
        var url = string.Format(urlFormat, OfflineAppHost.GuildId);

        var html = await _app.Host.Client.GetStringAsync(url);

        html.Should().Contain("Pwn", "the seeded hostile text should be on the page, or this test proves nothing");
        html.Should().NotContain("<img src=x onerror=alert(1)>", "user text must be HTML-encoded");

        foreach (Match handler in InlineHandler.Matches(html))
        {
            handler.Groups[1].Value.Should().NotContainEquivalentOf(
                "Pwn",
                $"user text must reach handlers through data-* attributes, not inline JavaScript ({url})");
        }
    }

    [Fact]
    public async Task SyncAllGuilds_FromNonAdmin_IsForbidden()
    {
        using var viewer = await _app.Host.CreateSignedInClientAsync("viewer@example.com", "Viewer");
        var token = OfflineAppHost.ReadAntiforgeryToken(await viewer.GetStringAsync("/"));

        var response = await viewer.PostAsync("/?handler=SyncAllGuilds", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteScheduledMessage_FromAnotherGuild_IsNotFound_AndKeepsTheMessage()
    {
        var response = await PostWithTokenAsync(
            $"/Guilds/ScheduledMessages/{OfflineAppHost.GuildId}",
            $"/Guilds/ScheduledMessages/{OfflineAppHost.GuildId}?handler=Delete",
            new() { ["messageId"] = AppFixture.OtherGuildMessageId.ToString() });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await OtherGuildMessageAsync()).Should().NotBeNull();
    }

    [Fact]
    public async Task ToggleScheduledMessage_FromAnotherGuild_IsNotFound_AndKeepsItsState()
    {
        var response = await PostWithTokenAsync(
            $"/Guilds/ScheduledMessages/{OfflineAppHost.GuildId}",
            $"/Guilds/ScheduledMessages/{OfflineAppHost.GuildId}?handler=Toggle&messageId={AppFixture.OtherGuildMessageId}",
            new());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await OtherGuildMessageAsync())!.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task EditScheduledMessage_FromAnotherGuild_IsNotFound_AndKeepsTheContent()
    {
        var response = await PostWithTokenAsync(
            $"/Guilds/ScheduledMessages/{OfflineAppHost.GuildId}",
            $"/Guilds/ScheduledMessages/Edit/{OfflineAppHost.GuildId}/{AppFixture.OtherGuildMessageId}",
            new()
            {
                ["Input.Title"] = "Hijacked",
                ["Input.Content"] = "Hijacked",
                ["Input.ChannelId"] = "1",
                ["Input.Frequency"] = ScheduleFrequency.Daily.ToString(),
                ["Input.NextExecutionAt"] = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-ddTHH:mm"),
                ["Input.IsEnabled"] = "true"
            });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await OtherGuildMessageAsync())!.Content.Should().Be("Belongs to another guild");
    }

    [Theory]
    [InlineData("https://evil.example/")]
    [InlineData("//evil.example")]
    [InlineData("javascript:alert(1)")]
    public async Task LoginWithForeignReturnUrl_StaysOnSite(string returnUrl)
    {
        // Already signed in, so Login redirects to the return URL straight away
        var response = await _app.Host.Client.GetAsync($"/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK, "a foreign return URL must not throw");
        response.RequestMessage!.RequestUri!.Host.Should().Be("localhost");
    }

    private async Task<HttpResponseMessage> PostWithTokenAsync(string tokenPage, string url, Dictionary<string, string> form)
    {
        var token = OfflineAppHost.ReadAntiforgeryToken(await _app.Host.Client.GetStringAsync(tokenPage));
        form["__RequestVerificationToken"] = token;
        return await _app.Host.Client.PostAsync(url, new FormUrlEncodedContent(form));
    }

    private async Task<ScheduledMessage?> OtherGuildMessageAsync()
    {
        using var scope = _app.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();
        return await db.ScheduledMessages.AsNoTracking().SingleOrDefaultAsync(m => m.Id == AppFixture.OtherGuildMessageId);
    }

    /// <summary>The app with hostile names seeded, plus a second guild's scheduled message.</summary>
    public sealed class AppFixture : IAsyncLifetime
    {
        public const ulong OtherGuildId = 876543210987654321UL;
        public static readonly Guid OtherGuildMessageId = Guid.Parse("0b0b0b0b-0000-4000-8000-000000000001");

        public OfflineAppHost Host { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            Host = await OfflineAppHost.StartAsync(Seed);
        }

        public async Task DisposeAsync()
        {
            await Host.DisposeAsync();
        }

        private static Task Seed(BotDbContext db)
        {
            var now = DateTime.UtcNow;
            const ulong guildId = OfflineAppHost.GuildId;

            db.Sounds.Add(new Sound
            {
                Id = Guid.NewGuid(),
                GuildId = guildId,
                Name = Hostile,
                FileName = Hostile + ".mp3",
                FileSizeBytes = 1024,
                DurationSeconds = 1,
                UploadedAt = now
            });

            db.ScheduledMessages.Add(new ScheduledMessage
            {
                Id = Guid.NewGuid(),
                GuildId = guildId,
                ChannelId = 1,
                Title = Hostile,
                Content = Hostile,
                Frequency = ScheduleFrequency.Daily,
                NextExecutionAt = now.AddDays(1),
                CreatedAt = now,
                CreatedBy = "seed",
                UpdatedAt = now
            });

            db.TtsMessages.Add(new TtsMessage
            {
                Id = Guid.NewGuid(),
                GuildId = guildId,
                UserId = 1,
                Username = Hostile,
                Message = Hostile,
                Voice = "en-US-JennyNeural",
                DurationSeconds = 1,
                CreatedAt = now
            });

            db.ModTags.Add(new ModTag
            {
                Id = Guid.NewGuid(),
                GuildId = guildId,
                Name = Hostile,
                Color = "#2fb3cc",
                Category = TagCategory.Neutral,
                CreatedAt = now
            });

            // A message that belongs to a guild the request is not scoped to
            db.Guilds.Add(new Guild { Id = OtherGuildId, Name = "Other Guild", JoinedAt = now, IsActive = true });
            db.ScheduledMessages.Add(new ScheduledMessage
            {
                Id = OtherGuildMessageId,
                GuildId = OtherGuildId,
                ChannelId = 2,
                Title = "Other guild",
                Content = "Belongs to another guild",
                Frequency = ScheduleFrequency.Daily,
                IsEnabled = true,
                NextExecutionAt = now.AddDays(1),
                CreatedAt = now,
                CreatedBy = "seed",
                UpdatedAt = now
            });

            return Task.CompletedTask;
        }
    }
}
