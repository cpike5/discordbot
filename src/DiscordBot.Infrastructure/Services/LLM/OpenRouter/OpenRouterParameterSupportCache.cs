using System.Collections.Concurrent;

namespace DiscordBot.Infrastructure.Services.LLM.OpenRouter;

/// <summary>
/// Remembers, per model slug, that OpenRouter has no endpoint accepting <c>temperature</c>. Reasoning
/// models (the GPT-5 family among them) omit <c>temperature</c> from their supported parameters, and
/// because every tool-carrying request sends <c>provider.require_parameters</c>, a request with a
/// temperature is refused outright (404, "No endpoints found that can handle the requested
/// parameters") instead of having the parameter dropped. <see cref="OpenRouterLlmClient"/> resends
/// once without the temperature and records the slug here, so later requests - the typed client is
/// transient, this cache is a singleton - omit it up front instead of paying the failed round trip
/// again. Process-lifetime only: after a restart the first request to such a model pays it once more.
/// </summary>
public sealed class OpenRouterParameterSupportCache
{
    private readonly ConcurrentDictionary<string, byte> _temperatureUnsupported =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether <paramref name="model"/> is known to reject <c>temperature</c>.</summary>
    public bool IsTemperatureUnsupported(string model) =>
        _temperatureUnsupported.ContainsKey(model);

    /// <summary>Records that <paramref name="model"/> rejects <c>temperature</c>.</summary>
    public void MarkTemperatureUnsupported(string model) =>
        _temperatureUnsupported.TryAdd(model, 0);
}
