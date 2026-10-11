# Grafana stack and OTLP export

What fought back while wiring the bot to Loki, Tempo and Prometheus over OTLP
(`deployment/observability/`, `docs/articles/grafana-observability.md`).

## Observable gauges need someone to construct their class

`AppStateMetrics` holds only observable gauges and nothing injects it, so its gauges never existed.
Registering a singleton and calling `AddMeter` is not enough: the meter only gets instruments when
the class is built. `metrics.AddInstrumentation(sp => sp.GetRequiredService<AppStateMetrics>())`
makes the meter provider build it at startup. Counters don't have this problem only because their
call sites resolve the class.

## Npgsql's histograms need explicit buckets

Npgsql reports `db.client.commands.duration` in seconds without bucket advice, so the SDK's default
boundaries (0, 5, 10, 25 ... meant for milliseconds) put every query in the first bucket and every
quantile came out at about 2.5 s. `AddView` with second-scale boundaries fixes it. Check any new
seconds-unit framework histogram the same way: compare its p50 with something you know.

## Loki's 4 MB gRPC limit makes log queries hang

Serilog.Exceptions turns every exception into dozens of structured-metadata fields, and the Elastic
APM agent's retry warnings carry the serialized payload. A page of those lines exceeded Loki's
default 4 MB internal gRPC message, and the query hung until Grafana's 30 s timeout instead of
failing. `server.grpc_server_max_{recv,send}_msg_size` in `loki.yaml` is raised to 64 MB.

## Serilog replaces the logging providers

`UseSerilog` takes over `ILoggerFactory`, so OpenTelemetry's `ILogger` provider would receive
nothing. Logs go through `Serilog.Sinks.OpenTelemetry`, which picks up `Activity.Current` for
trace and span IDs. Its `Endpoint` property also enables trace export; set `LogsEndpoint` and
leave `Endpoint` null. For HTTP/protobuf the .NET OTLP exporter uses a programmatic `Endpoint` as
is, so `OtlpOptions.GetSignalEndpoint` adds the `/v1/<signal>` path.

## Prometheus label names from OTLP

With Prometheus 3's native OTLP receiver, units in braces are dropped and real units are appended:
`discordbot.command.count` with unit `{commands}` becomes `discordbot_command_count_total`, and
`discordbot.command.duration` with unit `ms` becomes `discordbot_command_duration_milliseconds`. An
unknown unit such as `USD` would be appended verbatim, which is why the cost counter is named
`discordbot.llm.cost.usd` with unit `{USD}`. `service.version` is deliberately not promoted to a
label: it would start a new series for every deploy.

## Checking dashboards without a bot token

In offline mode most Discord panels are legitimately empty, so "no data" says nothing about a query.
Running every dashboard query against Prometheus, Loki and Tempo and failing only on errors (not on
empty results) caught the broken ones. Headless Chromium in the cloud sandbox also needs an
explicit `locale: 'en-US'`: the default `en-US@posix` crashes Grafana's bootstrap in
`Intl.NumberFormat`.
