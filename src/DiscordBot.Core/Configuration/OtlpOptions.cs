namespace DiscordBot.Core.Configuration;

/// <summary>
/// OTLP export settings shared by logs, traces and metrics, bound from <c>OpenTelemetry:Otlp</c>.
/// </summary>
/// <remarks>
/// Point <see cref="Endpoint"/> at an OpenTelemetry Collector (the stack in
/// <c>deployment/observability</c> listens on <c>otel-collector:4317</c>). When it is empty the
/// standard <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> variable is used; when both are empty nothing is
/// exported over OTLP. Traces additionally honour the older
/// <c>OpenTelemetry:Tracing:OtlpEndpoint</c> setting.
/// </remarks>
public class OtlpOptions
{
    /// <summary>The configuration section name for binding.</summary>
    public const string SectionName = "OpenTelemetry:Otlp";

    /// <summary>
    /// Gets or sets the collector base URL, e.g. <c>http://otel-collector:4317</c> for gRPC or
    /// <c>http://otel-collector:4318</c> for HTTP. Signal paths (<c>/v1/logs</c> etc.) are added
    /// for HTTP; do not include them.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>Gets or sets the protocol: <c>grpc</c> (default) or <c>http</c> (HTTP/protobuf).</summary>
    public string Protocol { get; set; } = "grpc";

    /// <summary>
    /// Gets or sets optional request headers as <c>key=value</c> pairs separated by commas, for a
    /// collector or vendor endpoint that needs an API key.
    /// </summary>
    public string? Headers { get; set; }

    /// <summary>Gets or sets whether logs are exported over OTLP. Default true.</summary>
    public bool ExportLogs { get; set; } = true;

    /// <summary>Gets or sets whether metrics are exported over OTLP. Default true.</summary>
    public bool ExportMetrics { get; set; } = true;

    /// <summary>Gets or sets whether traces are exported over OTLP. Default true.</summary>
    public bool ExportTraces { get; set; } = true;

    /// <summary>Gets or sets how often metrics are pushed, in seconds. Default 15.</summary>
    public int MetricExportIntervalSeconds { get; set; } = 15;

    /// <summary>Gets whether the protocol is HTTP/protobuf rather than gRPC.</summary>
    public bool UseHttpProtobuf => string.Equals(Protocol, "http", StringComparison.OrdinalIgnoreCase)
                                   || string.Equals(Protocol, "http/protobuf", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Builds the URL a signal is sent to: the endpoint itself for gRPC, or the endpoint plus the
    /// signal path (<c>v1/logs</c>, <c>v1/traces</c>, <c>v1/metrics</c>) for HTTP.
    /// </summary>
    /// <param name="endpoint">The resolved base endpoint.</param>
    /// <param name="signal">"logs", "traces" or "metrics".</param>
    /// <returns>The absolute URI to export to.</returns>
    public Uri GetSignalEndpoint(string endpoint, string signal)
    {
        if (!UseHttpProtobuf)
        {
            return new Uri(endpoint);
        }

        var trimmed = endpoint.TrimEnd('/');
        var suffix = $"/v1/{signal}";
        return new Uri(trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ? trimmed : trimmed + suffix);
    }

    /// <summary>Parses <see cref="Headers"/> into a dictionary; malformed pairs are skipped.</summary>
    public IReadOnlyDictionary<string, string> ParseHeaders()
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(Headers))
        {
            return headers;
        }

        foreach (var pair in Headers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = pair.IndexOf('=');
            if (separator > 0)
            {
                headers[pair[..separator].Trim()] = pair[(separator + 1)..].Trim();
            }
        }

        return headers;
    }
}
