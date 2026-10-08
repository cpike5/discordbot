using System.Net;
using System.Text.Json;
using DiscordBot.Agents.Contracts;
using DiscordBot.Bot.Extensions;
using DiscordBot.Infrastructure.Services.LLM.Providers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DiscordBot.Tests.Services.LLM;

/// <summary>
/// SSRF protection in <see cref="WebFetchToolProvider"/> across redirects. The URLs are IP
/// literals, which <c>Dns.GetHostAddressesAsync</c> returns without a lookup, so nothing here
/// touches DNS or the network: every request lands on <see cref="RecordingHandler"/>.
/// </summary>
public class WebFetchToolProviderTests
{
    // TEST-NET-3 (RFC 5737): public as far as the private-range check is concerned, routable nowhere.
    private const string PublicUrl = "http://203.0.113.5/start";
    private const string OtherPublicUrl = "http://203.0.113.6/landed";

    [Fact]
    public void NamedClient_DoesNotFollowRedirectsByItself()
    {
        // The provider follows redirects by hand so it can check every hop. If the primary handler
        // followed them too, a redirect would reach its target before the provider ever saw it.
        var services = new ServiceCollection();
        services.AddDmAssistant(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        HttpMessageHandler? handler = provider.GetRequiredService<IHttpMessageHandlerFactory>()
            .CreateHandler("DmAssistantWebFetch");
        while (handler is DelegatingHandler delegating)
        {
            handler = delegating.InnerHandler;
        }

        var allowAutoRedirect = handler switch
        {
            SocketsHttpHandler sockets => sockets.AllowAutoRedirect,
            HttpClientHandler client => client.AllowAutoRedirect,
            _ => throw new InvalidOperationException($"Unexpected primary handler {handler?.GetType()}")
        };
        allowAutoRedirect.Should().BeFalse();
    }

    [Fact]
    public async Task RedirectToLoopback_IsRefused_AndNeverRequested()
    {
        var handler = new RecordingHandler(request => request.RequestUri!.Host == "127.0.0.1"
            ? Html("internal secret")
            : Redirect(HttpStatusCode.Found, "http://127.0.0.1/admin"));

        var result = await Fetch(handler, PublicUrl);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("internal/private network");
        handler.Requested.Should().Equal(new Uri(PublicUrl));
    }

    [Fact]
    public async Task RedirectToMetadataAddress_ViaRelativeChain_IsRefused()
    {
        // A relative Location resolves against the hop that sent it; the second hop then points
        // at the cloud metadata address.
        var handler = new RecordingHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/start" => Redirect(HttpStatusCode.MovedPermanently, "/next"),
            "/next" => Redirect(HttpStatusCode.TemporaryRedirect, "http://169.254.169.254/latest/meta-data/"),
            _ => Html("metadata")
        });

        var result = await Fetch(handler, PublicUrl);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("internal/private network");
        handler.Requested.Should().Equal(new Uri(PublicUrl), new Uri("http://203.0.113.5/next"));
    }

    [Fact]
    public async Task RedirectToPublicAddress_IsFollowed()
    {
        var handler = new RecordingHandler(request => request.RequestUri!.ToString() == PublicUrl
            ? Redirect(HttpStatusCode.Found, OtherPublicUrl)
            : Html("<html><head><title>Landed</title></head><body>public content</body></html>"));

        var result = await Fetch(handler, PublicUrl);

        result.Success.Should().BeTrue(result.ErrorMessage);
        result.Data!.Value.GetProperty("content").GetString().Should().Contain("public content");
        handler.Requested.Should().Equal(new Uri(PublicUrl), new Uri(OtherPublicUrl));
    }

    [Fact]
    public async Task RedirectToNonHttpScheme_IsRefused()
    {
        var handler = new RecordingHandler(_ => Redirect(HttpStatusCode.Found, "file:///etc/passwd"));

        var result = await Fetch(handler, PublicUrl);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("HTTP and HTTPS");
        handler.Requested.Should().HaveCount(1);
    }

    [Fact]
    public async Task RedirectLoop_StopsAfterFiveRedirects()
    {
        var handler = new RecordingHandler(_ => Redirect(HttpStatusCode.Found, PublicUrl));

        var result = await Fetch(handler, PublicUrl);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Too many redirects");
        handler.Requested.Should().HaveCount(6, "the original request plus five followed redirects");
    }

    private static async Task<ToolExecutionResult> Fetch(RecordingHandler handler, string url)
    {
        var provider = new WebFetchToolProvider(
            NullLogger<WebFetchToolProvider>.Instance,
            new StubHttpClientFactory(handler));

        var input = JsonDocument.Parse(JsonSerializer.Serialize(new { url })).RootElement;
        return await provider.ExecuteToolAsync("fetch_url", input, new ToolContext());
    }

    private static HttpResponseMessage Redirect(HttpStatusCode status, string location)
    {
        var response = new HttpResponseMessage(status);
        response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
        return response;
    }

    private static HttpResponseMessage Html(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, System.Text.Encoding.UTF8, "text/html")
    };

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<Uri> Requested { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requested.Add(request.RequestUri!);
            return Task.FromResult(respond(request));
        }
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
