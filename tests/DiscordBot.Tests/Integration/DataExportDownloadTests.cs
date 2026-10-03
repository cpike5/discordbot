using System.IO.Compression;
using System.Net;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DiscordBot.Tests.Integration;

/// <summary>
/// Personal-data exports against the real app: the archive is reachable only through the signed-in
/// download handler, only by its owner, and never as a static file.
/// </summary>
[Collection(OfflineAppHostCollection.Name)]
public class DataExportDownloadTests : IClassFixture<DataExportDownloadTests.AppFixture>
{
    private readonly AppFixture _app;

    public DataExportDownloadTests(AppFixture app)
    {
        _app = app;
    }

    private string DownloadUrl => $"/Account/Privacy?handler=DownloadExport&id={_app.ExportId}";

    [Fact]
    public async Task Owner_CanDownloadTheirExport_AsANonCachedZipWithAFriendlyName()
    {
        var response = await _app.Host.Client.GetAsync(DownloadUrl);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/zip");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        response.Content.Headers.ContentDisposition!.FileNameStar.Should().StartWith("discordbot-data-export-").And.EndWith(".zip");

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var archive = new ZipArchive(stream);
        archive.Entries.Select(e => e.Name).Should().Contain("README.txt");
    }

    [Fact]
    public async Task AnotherSignedInUser_GetsNotFound_ForSomeoneElsesExport()
    {
        using var other = _app.OtherUserClient;

        var response = await other.GetAsync(DownloadUrl);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AnExportId_ThatIsNotAGuid_OrIsUnknown_IsNotFound()
    {
        var client = _app.Host.Client;

        (await client.GetAsync("/Account/Privacy?handler=DownloadExport&id=..%2F..%2Fappsettings.json")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync($"/Account/Privacy?handler=DownloadExport&id={Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync("/Account/Privacy?handler=DownloadExport")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Anonymous_IsSentToSignIn_NotServedTheFile()
    {
        using var anonymous = _app.Host.CreateAnonymousClient();

        var response = await anonymous.GetAsync(DownloadUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("/Account/Login");
    }

    [Fact]
    public async Task TheOldStaticPath_IsNotFound_ForEveryone()
    {
        var path = $"/exports/{AppFixture.OwnerDiscordId}/{_app.ExportId}.zip";
        using var anonymous = _app.Host.CreateAnonymousClient();

        // Signed in, nothing is there; anonymous is sent to sign in by the fallback policy. Neither gets the file.
        (await _app.Host.Client.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var anonymousResponse = await anonymous.GetAsync(path);
        anonymousResponse.StatusCode.Should().NotBe(HttpStatusCode.OK);
        anonymousResponse.Content.Headers.ContentType?.MediaType.Should().NotBe("application/zip");
    }

    public sealed class AppFixture : IAsyncLifetime
    {
        public const ulong OwnerDiscordId = 700000000000000001UL;
        private const ulong OtherDiscordId = 700000000000000002UL;

        public OfflineAppHost Host { get; private set; } = null!;
        public Guid ExportId { get; private set; }
        private string _exportDirectory = null!;

        public HttpClient OtherUserClient => _otherClient!;
        private HttpClient? _otherClient;

        public async Task InitializeAsync()
        {
            Host = await OfflineAppHost.StartAsync(async context =>
            {
                context.Users.Add(new User { Id = OwnerDiscordId });
                context.Users.Add(new User { Id = OtherDiscordId });
                await Task.CompletedTask;
            });

            _otherClient = await Host.CreateSignedInClientAsync("export-other@example.com", "Viewer");

            using var scope = Host.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var admin = await users.FindByEmailAsync(OfflineAppHost.AdminEmail);
            admin!.DiscordUserId = OwnerDiscordId;
            (await users.UpdateAsync(admin)).Succeeded.Should().BeTrue();
            var other = await users.FindByEmailAsync("export-other@example.com");
            other!.DiscordUserId = OtherDiscordId;
            (await users.UpdateAsync(other)).Succeeded.Should().BeTrue();

            var result = await scope.ServiceProvider.GetRequiredService<IUserDataExportService>().ExportUserDataAsync(OwnerDiscordId);
            result.Success.Should().BeTrue(result.ErrorMessage);
            ExportId = result.ExportId!.Value;

            var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
            _exportDirectory = Path.Combine(env.ContentRootPath, "data", "exports", OwnerDiscordId.ToString());
        }

        public async Task DisposeAsync()
        {
            _otherClient?.Dispose();
            await Host.DisposeAsync();
            if (Directory.Exists(_exportDirectory))
            {
                Directory.Delete(_exportDirectory, recursive: true);
            }
        }
    }
}
