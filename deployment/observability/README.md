# Grafana observability stack

Grafana, Loki, Tempo and Prometheus behind an OpenTelemetry Collector, with anonymous Grafana
access and dashboards for the bot provisioned on startup. The bot sends logs, traces and metrics
over OTLP. Full reference (architecture, configuration, metric catalogue):
[docs/articles/grafana-observability.md](../../docs/articles/grafana-observability.md). Querying it from
scripts or agent sessions (follow one request across logs and traces, check a new metric):
[docs/articles/grafana-agent-debugging.md](../../docs/articles/grafana-agent-debugging.md).

## Run it

From the repository root:

```bash
# 1. The stack (creates the shared Docker network "observability")
docker compose -f deployment/observability/docker-compose.yml up -d

# 2. The bot, which joins that network and exports to otel-collector:4317
docker compose up -d                       # or: docker compose -f docker-compose.ghcr.yml up -d
```

Open <http://localhost:3000>. The Overview dashboard is the home page; the rest are under
Dashboards > Discord Bot.

Bot running on the host rather than in Docker? Point it at the published collector port:

```bash
OpenTelemetry__Otlp__Endpoint=http://localhost:4317 dotnet run --project src/DiscordBot.Bot
```

Any other container can join the network (`networks: [observability]` with
`observability: { external: true }`) and send OTLP to `otel-collector:4317` (gRPC) or
`otel-collector:4318` (HTTP).

## What runs

| Service | Image | Published port | Role |
|---|---|---|---|
| `otel-collector` | `otel/opentelemetry-collector-contrib:0.135.0` | 4317, 4318 | OTLP entry point; routes each signal |
| `prometheus` | `quay.io/prometheus/prometheus:v3.6.0` | none | Metrics (OTLP receiver), alert rules, 30-day retention |
| `loki` | `grafana/loki:3.5.5` | none | Logs (OTLP endpoint), 30-day retention |
| `tempo` | `grafana/tempo:2.8.2` | none | Traces, 14-day retention; generates service-graph and span metrics into Prometheus |
| `grafana` | `grafana/grafana:12.2.0` | 3000 | UI, anonymous access, provisioned data sources and dashboards |

Data lives in the named volumes `prometheus-data`, `loki-data`, `tempo-data` and `grafana-data`.

## Settings (environment variables for this compose file)

| Variable | Default | Effect |
|---|---|---|
| `GF_ANONYMOUS_ROLE` | `Editor` | Role of the anonymous user. `Viewer` is read-only (no Explore); `Admin` can change data sources. Grafana 12 logs a deprecation warning for anything but `Viewer`. |
| `GF_ROOT_URL` | `http://localhost:3000` | Public URL, needed behind a reverse proxy. |
| `GRAFANA_PORT` | `3000` | Host port for Grafana. |
| `OTLP_GRPC_PORT` / `OTLP_HTTP_PORT` | `4317` / `4318` | Host ports for the collector. |
| `PROMETHEUS_RETENTION` | `30d` | Prometheus TSDB retention. |
| `DEPLOYMENT_ENVIRONMENT` | `production` | `deployment.environment` stamped on telemetry that arrives without one. |

**Anonymous access means anyone who can reach port 3000 can read every log line.** Do not publish
it to the internet as is: bind it to localhost (`GRAFANA_PORT=127.0.0.1:3000`), put it behind a
reverse proxy that authenticates, or both. The same goes for the OTLP ports.

## Files

```
otel-collector/config.yaml        receivers, processors, one pipeline per signal
prometheus/prometheus.yml         OTLP label promotion, self-scrape of the stack
prometheus/alerts.yml             17 alert rules (no Alertmanager: they show in Grafana only)
loki/loki.yaml                    single binary, filesystem, structured metadata on
tempo/tempo.yaml                  single binary, local storage, metrics generator
grafana/provisioning/             data sources (with trace/log/metric links) and the dashboard provider
grafana/dashboards/*.json         generated: do not edit by hand
grafana/generate_dashboards.py    the dashboard source: edit, then run it
```

Change a dashboard:

```bash
python3 deployment/observability/grafana/generate_dashboards.py   # Grafana reloads within 30 s
```

Change an alert rule, then reload Prometheus without a restart:

```bash
docker compose -f deployment/observability/docker-compose.yml exec prometheus promtool check rules /etc/prometheus/alerts.yml
docker compose -f deployment/observability/docker-compose.yml exec prometheus wget -qO- --post-data= http://localhost:9090/-/reload
```

## Troubleshooting

- **No data at all.** Check that the bot has an endpoint (`OpenTelemetry__Otlp__Endpoint` or
  `OTLP_ENDPOINT`), that it is on the `observability` network
  (`docker network inspect observability`), and the collector's log
  (`docker compose -f deployment/observability/docker-compose.yml logs otel-collector`).
- **`network observability declared as external, but could not be found`.** Start this stack
  before the bot, or create the network once: `docker network create observability`.
- **Discord panels are empty.** Expected with `Discord:OfflineMode=true`: the bot never connects.
- **Assistant panels are empty.** The assistant is only registered when `OpenRouter:ApiKey` is set.
- **Log queries hang or time out.** Lines with large exception details can exceed Loki's default
  4 MB gRPC message size; `loki.yaml` raises it to 64 MB. Keep that setting if you replace the file.
