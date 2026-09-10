using System.Net;
using System.Text;
using System.Text.Json;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.DTOs.LLM.Enums;
using DiscordBot.Infrastructure.Services.LLM.OpenRouter;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DiscordBot.Tests.Infrastructure.LLM;

/// <summary>
/// Unit tests for OpenRouterLlmClient: request construction, retry policy, and error mapping.
/// Drives the client against a stub HttpMessageHandler rather than the network.
/// </summary>
public class OpenRouterLlmClientTests
{
    private const string SuccessBody = """
        {"id":"gen-1","choices":[{"finish_reason":"stop","message":{"content":"Hello"}}],
         "usage":{"prompt_tokens":10,"completion_tokens":4}}
        """;

    /// <summary>Serves queued responses in order and records every request body it was sent.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _responses = new();

        public List<string> RequestBodies { get; } = new();

        public List<HttpRequestMessage> Requests { get; } = new();

        public int CallCount { get; private set; }

        public StubHandler Enqueue(HttpStatusCode status, string body)
        {
            _responses.Enqueue(() => new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
            return this;
        }

        public StubHandler EnqueueThrow(Exception exception)
        {
            _responses.Enqueue(() => throw exception);
            return this;
        }

        public StubHandler EnqueueSuccess() => Enqueue(HttpStatusCode.OK, SuccessBody);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            Requests.Add(request);
            RequestBodies.Add(request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));

            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No stub response queued.");
            }

            return _responses.Dequeue()();
        }

        /// <summary>The last request body, parsed.</summary>
        public JsonElement LastRequest() =>
            JsonDocument.Parse(RequestBodies[^1]).RootElement;
    }

    private static OpenRouterLlmClient CreateClient(
        StubHandler handler, Action<OpenRouterOptions>? configure = null)
    {
        var options = new OpenRouterOptions
        {
            ApiKey = "test-key",
            DefaultModel = "anthropic/claude-sonnet-4",
            // Keep retry backoff negligible so the tests stay fast.
            RetryBaseDelayMs = 1,
            MaxRetries = 2
        };
        configure?.Invoke(options);

        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://openrouter.ai/api/v1/")
        };

        return new OpenRouterLlmClient(
            http, Options.Create(options), NullLogger<OpenRouterLlmClient>.Instance);
    }

    private static LlmRequest SimpleRequest() => new()
    {
        SystemPrompt = "You are a bot.",
        Messages = new List<LlmMessage>
        {
            new() { Role = LlmRole.User, Content = "Hi" }
        },
        MaxTokens = 512,
        Temperature = 0.4
    };

    #region Request construction

    [Fact]
    public async Task CompleteAsync_PostsToChatCompletionsWithBearerAuth()
    {
        var handler = new StubHandler().EnqueueSuccess();
        var client = CreateClient(handler);

        await client.CompleteAsync(SimpleRequest());

        var request = handler.Requests[0];
        request.Method.Should().Be(HttpMethod.Post);
        request.RequestUri!.ToString().Should().Be("https://openrouter.ai/api/v1/chat/completions");
        request.Headers.Authorization!.Scheme.Should().Be("Bearer");
        request.Headers.Authorization.Parameter.Should().Be("test-key");
    }

    [Fact]
    public async Task CompleteAsync_SendsModelMaxTokensAndTemperature()
    {
        var handler = new StubHandler().EnqueueSuccess();
        var client = CreateClient(handler);

        await client.CompleteAsync(SimpleRequest());

        var body = handler.LastRequest();
        body.GetProperty("model").GetString().Should().Be("anthropic/claude-sonnet-4");
        body.GetProperty("max_tokens").GetInt32().Should().Be(512);
        // Temperature was silently dropped by the Anthropic client this replaced.
        body.GetProperty("temperature").GetDouble().Should().Be(0.4);
    }

    [Fact]
    public async Task CompleteAsync_WithRequestModel_OverridesDefaultModel()
    {
        var handler = new StubHandler().EnqueueSuccess();
        var client = CreateClient(handler);

        var request = SimpleRequest();
        request.Model = "openai/gpt-4o";

        await client.CompleteAsync(request);

        handler.LastRequest().GetProperty("model").GetString().Should().Be("openai/gpt-4o");
    }

    [Fact]
    public async Task CompleteAsync_WithoutRequestModel_UsesDefaultModel()
    {
        var handler = new StubHandler().EnqueueSuccess();
        var client = CreateClient(handler, o => o.DefaultModel = "meta-llama/llama-3.1-70b-instruct");

        var request = SimpleRequest();
        request.Model = null;

        await client.CompleteAsync(request);

        handler.LastRequest().GetProperty("model").GetString()
            .Should().Be("meta-llama/llama-3.1-70b-instruct");
    }

    /// <summary>
    /// Without require_parameters a slug can route to a provider with no native function calling,
    /// and the model then fakes a tool call in plain text.
    /// </summary>
    [Fact]
    public async Task CompleteAsync_WithTools_RequiresProviderParameterSupport()
    {
        var handler = new StubHandler().EnqueueSuccess();
        var client = CreateClient(handler);

        var request = SimpleRequest();
        request.Tools = new List<LlmToolDefinition>
        {
            new()
            {
                Name = "get_roles",
                Description = "Gets roles",
                InputSchema = JsonDocument.Parse("""{"type":"object"}""").RootElement.Clone()
            }
        };

        await client.CompleteAsync(request);

        var body = handler.LastRequest();
        body.GetProperty("provider").GetProperty("require_parameters").GetBoolean().Should().BeTrue();
        body.GetProperty("tools")[0].GetProperty("function").GetProperty("name").GetString()
            .Should().Be("get_roles");
    }

    [Fact]
    public async Task CompleteAsync_WithoutTools_OmitsToolsAndProvider()
    {
        var handler = new StubHandler().EnqueueSuccess();
        var client = CreateClient(handler);

        await client.CompleteAsync(SimpleRequest());

        var body = handler.LastRequest();
        body.TryGetProperty("tools", out _).Should().BeFalse();
        body.TryGetProperty("provider", out _).Should().BeFalse();
    }

    [Fact]
    public async Task CompleteAsync_WithCachingEnabled_MarksSystemPromptAsCacheBreakpoint()
    {
        var handler = new StubHandler().EnqueueSuccess();
        var client = CreateClient(handler);

        var request = SimpleRequest();
        request.EnablePromptCaching = true;

        await client.CompleteAsync(request);

        var systemContent = handler.LastRequest().GetProperty("messages")[0].GetProperty("content");
        systemContent[0].GetProperty("cache_control").GetProperty("type").GetString()
            .Should().Be("ephemeral");
    }

    [Fact]
    public async Task CompleteAsync_WithCachingDisabledOnRequest_SendsPlainSystemPrompt()
    {
        var handler = new StubHandler().EnqueueSuccess();
        var client = CreateClient(handler);

        var request = SimpleRequest();
        request.EnablePromptCaching = false;

        await client.CompleteAsync(request);

        handler.LastRequest().GetProperty("messages")[0].GetProperty("content")
            .ValueKind.Should().Be(JsonValueKind.String);
    }

    [Fact]
    public async Task CompleteAsync_WithCachingDisabledGlobally_SendsPlainSystemPrompt()
    {
        var handler = new StubHandler().EnqueueSuccess();
        var client = CreateClient(handler, o => o.EnablePromptCachingByDefault = false);

        var request = SimpleRequest();
        request.EnablePromptCaching = true;

        await client.CompleteAsync(request);

        handler.LastRequest().GetProperty("messages")[0].GetProperty("content")
            .ValueKind.Should().Be(JsonValueKind.String);
    }

    #endregion

    #region Success mapping

    [Fact]
    public async Task CompleteAsync_WithSuccessfulReply_MapsContentAndUsage()
    {
        var handler = new StubHandler().EnqueueSuccess();
        var client = CreateClient(handler);

        var result = await client.CompleteAsync(SimpleRequest());

        result.Success.Should().BeTrue();
        result.Content.Should().Be("Hello");
        result.StopReason.Should().Be(LlmStopReason.EndTurn);
        result.Usage.InputTokens.Should().Be(10);
        result.Usage.OutputTokens.Should().Be(4);
    }

    #endregion

    #region Configuration guards

    [Fact]
    public async Task CompleteAsync_WithoutApiKey_FailsWithoutCallingTheApi()
    {
        var handler = new StubHandler();
        var client = CreateClient(handler, o => o.ApiKey = null);

        var result = await client.CompleteAsync(SimpleRequest());

        result.Success.Should().BeFalse();
        result.StopReason.Should().Be(LlmStopReason.Error);
        result.ErrorMessage.Should().Contain("API key is not configured");
        handler.CallCount.Should().Be(0);
    }

    #endregion

    #region Retry policy

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    public async Task CompleteAsync_WithTransientStatus_RetriesThenSucceeds(HttpStatusCode status)
    {
        var handler = new StubHandler()
            .Enqueue(status, """{"error":{"message":"transient"}}""")
            .EnqueueSuccess();
        var client = CreateClient(handler);

        var result = await client.CompleteAsync(SimpleRequest());

        result.Success.Should().BeTrue();
        handler.CallCount.Should().Be(2);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.PaymentRequired)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task CompleteAsync_WithPermanentStatus_DoesNotRetry(HttpStatusCode status)
    {
        var handler = new StubHandler().Enqueue(status, """{"error":{"message":"nope"}}""");
        var client = CreateClient(handler);

        var result = await client.CompleteAsync(SimpleRequest());

        result.Success.Should().BeFalse();
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task CompleteAsync_WhenTransientErrorsExhaustRetries_ReportsFailure()
    {
        var handler = new StubHandler()
            .Enqueue(HttpStatusCode.ServiceUnavailable, "unavailable")
            .Enqueue(HttpStatusCode.ServiceUnavailable, "unavailable")
            .Enqueue(HttpStatusCode.ServiceUnavailable, "unavailable");
        var client = CreateClient(handler, o => o.MaxRetries = 2);

        var result = await client.CompleteAsync(SimpleRequest());

        result.Success.Should().BeFalse();
        result.StopReason.Should().Be(LlmStopReason.Error);
        // Initial attempt plus two retries.
        handler.CallCount.Should().Be(3);
        result.ErrorMessage.Should().Contain("after 3 attempts");
    }

    [Fact]
    public async Task CompleteAsync_WithNetworkFailure_Retries()
    {
        var handler = new StubHandler()
            .EnqueueThrow(new HttpRequestException("connection reset"))
            .EnqueueSuccess();
        var client = CreateClient(handler);

        var result = await client.CompleteAsync(SimpleRequest());

        result.Success.Should().BeTrue();
        handler.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task CompleteAsync_WithMaxRetriesZero_CallsOnce()
    {
        var handler = new StubHandler().Enqueue(HttpStatusCode.ServiceUnavailable, "unavailable");
        var client = CreateClient(handler, o => o.MaxRetries = 0);

        var result = await client.CompleteAsync(SimpleRequest());

        result.Success.Should().BeFalse();
        handler.CallCount.Should().Be(1);
    }

    #endregion

    #region Error reporting

    [Fact]
    public async Task CompleteAsync_WithApiErrorMessage_SurfacesItWithStatusAndHint()
    {
        var handler = new StubHandler().Enqueue(
            HttpStatusCode.NotFound, """{"error":{"message":"No endpoints found for model","code":404}}""");
        var client = CreateClient(handler);

        var result = await client.CompleteAsync(SimpleRequest());

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("404");
        result.ErrorMessage.Should().Contain("No endpoints found for model");
        result.ErrorMessage.Should().Contain("model slug may be wrong");
    }

    [Fact]
    public async Task CompleteAsync_WithUnauthorized_HintsAtTheApiKey()
    {
        var handler = new StubHandler().Enqueue(
            HttpStatusCode.Unauthorized, """{"error":{"message":"Invalid credentials"}}""");
        var client = CreateClient(handler);

        var result = await client.CompleteAsync(SimpleRequest());

        result.ErrorMessage.Should().Contain("API key is valid");
    }

    [Fact]
    public async Task CompleteAsync_WithPaymentRequired_HintsAtCredits()
    {
        var handler = new StubHandler().Enqueue(
            HttpStatusCode.PaymentRequired, """{"error":{"message":"Insufficient credits"}}""");
        var client = CreateClient(handler);

        var result = await client.CompleteAsync(SimpleRequest());

        result.ErrorMessage.Should().Contain("out of credits");
    }

    [Fact]
    public async Task CompleteAsync_WithNonJsonErrorBody_FallsBackToRawBody()
    {
        var handler = new StubHandler().Enqueue(HttpStatusCode.BadRequest, "upstream exploded");
        var client = CreateClient(handler);

        var result = await client.CompleteAsync(SimpleRequest());

        result.ErrorMessage.Should().Contain("upstream exploded");
    }

    /// <summary>
    /// OpenRouter surfaces some provider failures as an error object on a 200 body. A permanent
    /// error code there is not retried, and its message reaches the caller.
    /// </summary>
    [Fact]
    public async Task CompleteAsync_WithPermanentErrorObjectOnSuccessStatus_ReportsFailureWithoutRetrying()
    {
        var handler = new StubHandler().Enqueue(
            HttpStatusCode.OK, """{"error":{"message":"Provider rejected the request","code":400}}""");
        var client = CreateClient(handler);

        var result = await client.CompleteAsync(SimpleRequest());

        result.Success.Should().BeFalse();
        result.StopReason.Should().Be(LlmStopReason.Error);
        result.ErrorMessage.Should().Contain("Provider rejected the request");
        handler.CallCount.Should().Be(1);
    }

    /// <summary>
    /// The error code on a 200 body is treated like an HTTP status for retry purposes, so a
    /// transient one (a provider 502) gets the same backoff an HTTP 502 would.
    /// </summary>
    [Fact]
    public async Task CompleteAsync_WithTransientErrorObjectOnSuccessStatus_Retries()
    {
        var handler = new StubHandler()
            .Enqueue(HttpStatusCode.OK, """{"error":{"message":"Provider returned an error","code":502}}""")
            .EnqueueSuccess();
        var client = CreateClient(handler);

        var result = await client.CompleteAsync(SimpleRequest());

        result.Success.Should().BeTrue();
        handler.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task CompleteAsync_WithNoChoices_ReportsFailure()
    {
        var handler = new StubHandler().Enqueue(HttpStatusCode.OK, """{"id":"gen-1","choices":[]}""");
        var client = CreateClient(handler);

        var result = await client.CompleteAsync(SimpleRequest());

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("no choices");
    }

    [Fact]
    public async Task CompleteAsync_WithUnparseableBody_ReportsFailure()
    {
        var handler = new StubHandler().Enqueue(HttpStatusCode.OK, "<html>not json</html>");
        var client = CreateClient(handler);

        var result = await client.CompleteAsync(SimpleRequest());

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("could not parse");
    }

    #endregion

    #region Cancellation

    [Fact]
    public async Task CompleteAsync_WhenCallerCancels_ReturnsCancelledWithoutRetrying()
    {
        var handler = new StubHandler()
            .EnqueueThrow(new OperationCanceledException());
        var client = CreateClient(handler);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await client.CompleteAsync(SimpleRequest(), cts.Token);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Request was cancelled");
        handler.CallCount.Should().Be(1);
    }

    #endregion

    #region Provider metadata

    [Fact]
    public void ProviderMetadata_ReportsOpenRouterCapabilities()
    {
        var client = CreateClient(new StubHandler());

        client.ProviderName.Should().Be("OpenRouter");
        client.SupportsToolUse.Should().BeTrue();
        client.SupportsPromptCaching.Should().BeTrue();
    }

    #endregion
}
