using System.Net;
using System.Text;
using DiscordBot.Infrastructure.Services.LLM.OpenRouter;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace DiscordBot.Tests.Infrastructure.LLM;

/// <summary>
/// Unit tests for OpenRouterModelCatalogClient: catalog mapping, price parsing, and error handling.
/// Drives the client against a stub HttpMessageHandler rather than the network.
/// </summary>
public class OpenRouterModelCatalogClientTests
{
    /// <summary>Serves one queued response and records the request it was sent.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public HttpRequestMessage? LastRequest { get; private set; }

        public StubHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private static OpenRouterModelCatalogClient CreateClient(HttpStatusCode status, string body, out StubHandler handler)
    {
        handler = new StubHandler(status, body);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://openrouter.ai/api/v1/"),
        };
        return new OpenRouterModelCatalogClient(http, NullLogger<OpenRouterModelCatalogClient>.Instance);
    }

    [Fact]
    public async Task GetModelsAsync_MapsFieldsAndConvertsPricingToPerMillion()
    {
        const string body = """
            {"data":[{
                "id":"anthropic/claude-sonnet-4",
                "name":"Claude Sonnet 4",
                "description":"A capable model.",
                "created":1700000000,
                "context_length":200000,
                "pricing":{"prompt":"0.000003","completion":"0.000015"},
                "architecture":{"input_modalities":["text","image"]},
                "supported_parameters":["tools","temperature"]
            }]}
            """;
        var client = CreateClient(HttpStatusCode.OK, body, out var handler);

        var models = await client.GetModelsAsync();

        models.Should().HaveCount(1);
        var model = models[0];
        model.Id.Should().Be("anthropic/claude-sonnet-4");
        model.Name.Should().Be("Claude Sonnet 4");
        model.Vendor.Should().Be("anthropic");
        model.ContextLength.Should().Be(200000);
        model.PromptPricePerMillion.Should().Be(3.0m);
        model.CompletionPricePerMillion.Should().Be(15.0m);
        model.SupportsTools.Should().BeTrue();
        model.SupportsImages.Should().BeTrue();
        model.ReleasedAt.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1700000000).UtcDateTime);

        handler.LastRequest!.RequestUri!.ToString().Should().Be("https://openrouter.ai/api/v1/models");
    }

    [Fact]
    public async Task GetModelsAsync_UnknownPriceSentinelBecomesNull()
    {
        const string body = """
            {"data":[{
                "id":"vendor/unknown-price-model",
                "name":"Unknown Price Model",
                "pricing":{"prompt":"-1","completion":"-1"}
            }]}
            """;
        var client = CreateClient(HttpStatusCode.OK, body, out _);

        var models = await client.GetModelsAsync();

        models.Should().HaveCount(1);
        models[0].PromptPricePerMillion.Should().BeNull();
        models[0].CompletionPricePerMillion.Should().BeNull();
    }

    [Fact]
    public async Task GetModelsAsync_SkipsAliasEntries()
    {
        const string body = """
            {"data":[
                {"id":"~anthropic/claude-latest","name":"Claude (latest alias)"},
                {"id":"anthropic/claude-sonnet-4","name":"Claude Sonnet 4"}
            ]}
            """;
        var client = CreateClient(HttpStatusCode.OK, body, out _);

        var models = await client.GetModelsAsync();

        models.Should().ContainSingle();
        models[0].Id.Should().Be("anthropic/claude-sonnet-4");
    }

    [Fact]
    public async Task GetModelsAsync_DetectsToolsAndImageSupportIndependently()
    {
        const string body = """
            {"data":[
                {"id":"vendor/no-tools-no-images","name":"Plain",
                 "supported_parameters":["temperature"],
                 "architecture":{"input_modalities":["text"]}},
                {"id":"vendor/tools-only","name":"ToolsOnly",
                 "supported_parameters":["tools"],
                 "architecture":{"input_modalities":["text"]}}
            ]}
            """;
        var client = CreateClient(HttpStatusCode.OK, body, out _);

        var models = await client.GetModelsAsync();

        var plain = models.Single(m => m.Id == "vendor/no-tools-no-images");
        plain.SupportsTools.Should().BeFalse();
        plain.SupportsImages.Should().BeFalse();

        var toolsOnly = models.Single(m => m.Id == "vendor/tools-only");
        toolsOnly.SupportsTools.Should().BeTrue();
        toolsOnly.SupportsImages.Should().BeFalse();
    }

    [Fact]
    public async Task GetModelsAsync_LongDescriptionIsTruncatedToColumnMaxLength()
    {
        var longDescription = new string('x', 1500);
        var body = $$"""
            {"data":[{
                "id":"vendor/long-description-model",
                "name":"Long Description Model",
                "description":"{{longDescription}}"
            }]}
            """;
        var client = CreateClient(HttpStatusCode.OK, body, out _);

        var models = await client.GetModelsAsync();

        models.Should().HaveCount(1);
        models[0].Description.Should().NotBeNull();
        models[0].Description!.Length.Should().BeLessThanOrEqualTo(1000, "the Description column is HasMaxLength(1000)");
        models[0].Description!.Should().EndWith("...");
    }

    [Fact]
    public async Task GetModelsAsync_NonSuccessStatusThrowsOpenRouterException()
    {
        var client = CreateClient(HttpStatusCode.InternalServerError, "{\"error\":\"boom\"}", out _);

        var act = async () => await client.GetModelsAsync();

        var exception = await act.Should().ThrowAsync<OpenRouterException>();
        exception.Which.StatusCode.Should().Be(500);
    }
}
