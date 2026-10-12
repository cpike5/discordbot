# Debugging with the Grafana Stack (for Agents)

**Last Updated:** 2026-10-12

How an agent (a Claude Code session, a sub-agent, a script) brings up the Grafana stack, points
the app at it, and reads logs, traces and metrics back over HTTP while testing or debugging a
change. Everything here is a `curl` against Grafana; no browser is needed, though screenshots of
dashboards are covered at the end.

For what the stack is and how it is built, read [Grafana Observability Stack](grafana-observability.md)
and [`deployment/observability/README.md`](../../deployment/observability/README.md) first. This
page is the "how do I use it while working" companion.

## When to reach for it

The stack is worth starting when the question is about runtime behaviour rather than code shape:

- A page or endpoint returns the wrong thing and you need the server-side logs for that one request.
- Something is slow and you need the span breakdown (EF Core queries, outbound HTTP, tool calls).
- You added or changed a metric, log line, span or dashboard and need to prove it arrives.
- A background service misbehaves and you want its heartbeats, errors and log lines over time.

It is not needed for unit tests, and `dotnet test` never exports anything. The bot's console
output (`/tmp/.../bot.log` below) already has every log line; Loki adds filtering by request,
trace, level and source, plus the traces and metrics the console does not show.

## 1. Start the stack

From the repository root:

```bash
docker compose -f deployment/observability/docker-compose.yml up -d
curl -s http://localhost:3000/api/health      # {"database":"ok",...} once Grafana is up
```

**In a cloud (web) session** the Docker CLI is installed but the daemon is not running. Start it
first; it comes up in a few seconds:

```bash
(dockerd > /tmp/dockerd.log 2>&1 &) ; sleep 5 ; docker info --format '{{.ServerVersion}}'
```

The five images are about 2.2 GB; the first `up -d` pulls them through the session proxy, which
takes a minute or two. Free disk first if the session is short on space.

Only two things are published on the host: Grafana on `3000` and the OTLP collector on `4317`
(gRPC) / `4318` (HTTP). Prometheus, Loki and Tempo are reachable only inside the Docker network,
so every query below goes **through Grafana's data source proxy**. Grafana runs with anonymous
`Editor` access, so no token or login is needed.

## 2. Run the app against it

Run the app on the host, in offline mode, on the local PostgreSQL cluster, exporting to the
published collector port. Turn Elastic APM off: without an APM server it retries
`localhost:8200` forever and floods the logs and the outbound-HTTP metrics with failures.

```bash
dotnet build src/DiscordBot.Bot
ConnectionStrings__DefaultConnection="Host=localhost;Database=discordbot;Username=postgres;Password=postgres" \
Discord__OfflineMode=true \
OpenTelemetry__Otlp__Endpoint=http://localhost:4317 \
ElasticApm__Enabled=false \
nohup dotnet run --no-build --project src/DiscordBot.Bot > /tmp/bot.log 2>&1 &
echo $! > /tmp/bot.pid
```

Wait for `curl -s http://localhost:5124/health` to answer. What to expect:

- `ASPNETCORE_ENVIRONMENT` is `Development` under `dotnet run`, so telemetry carries
  `deployment_environment="development"` and **every trace is sampled** (the 10% default applies
  only in Production). `service_name` is `discordbot` and `service_instance_id` is the machine name.
- Logs and traces arrive within about 5 seconds (collector batch). Metrics are pushed every
  15 seconds; allow 20–30 seconds before concluding a metric is missing.
- Offline mode never connects to Discord: gateway, command and most feature panels stay empty,
  and the `discord` health check is `Degraded`. That is expected, not a bug in your query.
- Assistant metrics and spans exist only when `OpenRouter:ApiKey` is set.

Running the bot in Docker instead (`docker compose up -d` at the root) also works: it joins the
`observability` network and exports to `otel-collector:4317` on its own.

## 3. Query helpers

Paste these into the shell once; the rest of the page uses them.

```bash
G=http://localhost:3000
prom() { curl -s "$G/api/datasources/proxy/uid/prometheus/api/v1/query" --data-urlencode "query=$1"; }
logs() { curl -s -G "$G/api/datasources/proxy/uid/loki/loki/api/v1/query_range" \
           --data-urlencode "query=$1" --data-urlencode "limit=${2:-50}" --data-urlencode "since=${3:-15m}"; }
traces() { curl -s -G "$G/api/datasources/proxy/uid/tempo/api/search" --data-urlencode "q=$1" \
           --data-urlencode "limit=${2:-20}" --data-urlencode "start=$(( $(date +%s) - ${3:-900} ))" \
           --data-urlencode "end=$(date +%s)"; }
trace() { curl -s "$G/api/datasources/proxy/uid/tempo/api/v2/traces/$1"; }
```

The data source UIDs are fixed (`prometheus`, `loki`, `tempo`) in
`deployment/observability/grafana/provisioning/datasources/datasources.yaml`. The proxy passes the
path through unchanged, so each store's own HTTP API documentation applies.

## 4. Follow one request end to end

The most useful move. ASP.NET Core honours an incoming W3C `traceparent` header, so choose the
trace ID yourself instead of hunting for it afterwards:

```bash
TID=$(openssl rand -hex 16)
curl -s -o /dev/null -H "traceparent: 00-$TID-$(openssl rand -hex 8)-01" \
     -H "X-Correlation-ID: agent-$TID" http://localhost:5124/Account/Login
sleep 10

# Every log line written while serving it, oldest last
logs "{service_name=\"discordbot\"} | trace_id=\"$TID\"" | jq -r '.data.result[].values[][1]'

# The span tree: scope, name, duration
trace "$TID" | jq -r '.trace.resourceSpans[].scopeSpans[] | .scope.name as $s | .spans[]
  | "\($s)  \(.name)  \(((.endTimeUnixNano|tonumber)-(.startTimeUnixNano|tonumber))/1e6)ms"'
```

`X-Correlation-ID` is optional; the app echoes it in the response and stamps it on every log line
as `CorrelationId`, so `| CorrelationId="agent-..."` works as a log filter too. When the request
came from a browser or from code you do not control, read the `X-Correlation-ID` response header
and filter on that instead.

For a request that needs a signed-in user, sign in through the login form first with the seeded
`Identity:DefaultAdmin` account (as `Integration/RouteSmokeTests` does) and reuse the cookie jar
(`curl -c jar -b jar`); the headers above work the same way.

## 5. Logs (Loki)

Stream labels are only `service_name`, `service_instance_id` and `deployment_environment`.
Everything else is structured metadata, filtered with `| key="value"` after the selector. The
fields you will use most:

| Field | Holds |
|---|---|
| `severity_text` / `severity_number` | Serilog level: `Debug` (5), `Information` (9), `Warning` (13), `Error` (17), `Fatal` (21) |
| `scope_name` | The logger category, i.e. the class: `DiscordBot.Bot.Services.ReminderExecutionService` |
| `message_template_text` | The un-rendered template: group identical messages regardless of arguments |
| `trace_id`, `span_id` | Set on lines written inside a span |
| `CorrelationId`, `RequestPath`, `GuildId`, ... | Serilog properties, one field each |

```bash
# Warnings and errors in the last 15 minutes: level, class, message
logs '{service_name="discordbot"} | severity_number >= 13' 50 15m \
  | jq -r '.data.result[] | .stream as $s | .values[] | "\($s.severity_text) \($s.scope_name) | \(.[1])"'

# One class's lines
logs '{service_name="discordbot"} | scope_name=~".*ReminderExecutionService"'

# Text search in the rendered message
logs '{service_name="discordbot"} |= "timed out"'

# Every field present right now (when you do not know a property's name)
curl -s -G "$G/api/datasources/proxy/uid/loki/loki/api/v1/detected_fields" \
  --data-urlencode 'query={service_name="discordbot"}' --data-urlencode since=15m | jq -r '.fields[].label'
```

Each entry in `.data.result` is one unique combination of metadata (so usually one line); the
fields are in `.stream` and the timestamp and rendered message are in `.values[]`. Add
`--data-urlencode direction=forward` to read oldest first. Exception details from
Serilog.Exceptions arrive as many `Exception*` fields on the line, not inside the message.

## 6. Traces (Tempo)

Search with TraceQL, then fetch by ID:

```bash
traces '{ resource.service.name = "discordbot" && span.http.route != "" }' \
  | jq -c '.traces[] | {traceID, rootTraceName, durationMs}'      # recent HTTP requests
traces '{ status = error }'                                        # failed spans
traces '{ span.http.response.status_code >= 500 }'                 # server errors
traces '{ duration > 500ms }'                                      # slow spans
traces '{ name =~ "agent.tool.*" }'                                # assistant tool calls (with an API key)
```

What generates spans: inbound ASP.NET Core requests (except `/health`, `/metrics`, `/swagger` and
static files), outbound `HttpClient` calls, every EF Core command (with SQL text outside
Production), and the activity sources `DiscordBot.Bot`, `DiscordBot.Infrastructure`,
`DiscordBot.Agents` and `DiscordBot.Vox`. Background services open a root span that lives as long
as the service, so their children show "root span not yet received" — search for the child span
instead. Search results drop leading zeros from trace IDs (31 hex characters instead of 32); pass
the ID to `trace` exactly as returned. The third argument to `traces` is the look-back in
seconds (default 900); widen it when a search comes back empty.

## 7. Metrics (Prometheus)

```bash
# Requests by route and status since start
prom 'sum by (http_route, http_response_status_code) (http_server_request_duration_seconds_count{service_name="discordbot"})' \
  | jq -c '.data.result[] | {m: .metric, v: .value[1]}'

# p95 latency per route over the last 5 minutes
prom 'histogram_quantile(0.95, sum by (le, http_route) (rate(http_server_request_duration_seconds_bucket{service_name="discordbot"}[5m])))'

# Background services and health checks
prom 'discordbot_background_up' ; prom 'discordbot_health_status'

# Every app metric name (check that a new instrument arrived)
curl -s "$G/api/datasources/proxy/uid/prometheus/api/v1/label/__name__/values" | jq -r '.data[]' | grep ^discordbot_

# Alerts currently pending or firing
curl -s "$G/api/datasources/proxy/uid/prometheus/api/v1/alerts" | jq -c '.data.alerts[] | {a: .labels.alertname, s: .state}'
```

Instrument names translate to Prometheus names as described in
[Grafana Observability Stack § Metric reference](grafana-observability.md#metric-reference): dots
become underscores, counters gain `_total`, a unit is appended (`_milliseconds`, `_seconds`,
`_bytes`), units in braces are dropped. Use `.../api/v1/query_range` with `start`, `end` and `step`
for a series over time. Counters are cumulative from process start, so a restarted bot resets
them; use `rate()` or `increase()` across a restart.

## 8. Checking a telemetry change

When the change is the telemetry itself:

1. **New metric.** Exercise the code path, wait 30 seconds, and look for the name in the
   `__name__` list. Missing entirely: check the meter is registered with `AddMeter` in
   `OpenTelemetryExtensions`, and for a class holding only observable gauges, that it is built
   with `AddInstrumentation` (see [the lessons-learned note](../lessons-learned/grafana-otlp-stack.md)).
   Present but the value looks wrong for a histogram: check the bucket boundaries against the unit.
2. **New log property.** Find it in the `detected_fields` list, then filter on it.
3. **New span.** Search `{ name = "your.span.name" }`; make sure its activity source is in the
   `AddSource` list.
4. **Dashboard change.** Edit `deployment/observability/grafana/generate_dashboards.py`, run it,
   wait 30 seconds for Grafana to reload, then run each changed panel's query through `prom` or
   `logs`. Fail on query errors, not on empty results: offline mode legitimately leaves most
   Discord panels empty.
5. **Alert rule change.** Validate with `promtool` and reload Prometheus, as in the
   [stack README](../../deployment/observability/README.md#files).

## 9. Screenshots of dashboards

Dashboards are at `http://localhost:3000/d/<uid>`; list them with
`curl -s "$G/api/search?type=dash-db" | jq -r '.[] | "\(.uid)  \(.title)"'`. Add `&kiosk` to hide
Grafana's chrome and `&from=now-30m&to=now` to fit a short test run.

Headless Chromium **must** be given `locale: 'en-US'`; the sandbox default `en-US@posix` crashes
Grafana's bootstrap in `Intl.NumberFormat` and you get a blank page. With the globally installed
Playwright (CommonJS, so `NODE_PATH` resolves it):

```js
// shot.cjs — run: NODE_PATH=$(npm root -g) node shot.cjs <url> <out.png>
const { chromium } = require('playwright');
(async () => {
  const [, , url, out] = process.argv;
  const browser = await chromium.launch();
  const page = await browser.newPage({ locale: 'en-US', viewport: { width: 1600, height: 1000 } });
  await page.goto(url, { waitUntil: 'networkidle' });
  await page.waitForTimeout(3000);   // panels render after their queries return
  await page.screenshot({ path: out, fullPage: true });
  await browser.close();
})();
```

Keep scripts like this in the scratchpad, not the repository.

## 10. Clean up

```bash
kill "$(cat /tmp/bot.pid)"                                            # the app (dotnet run stops its child)
docker compose -f deployment/observability/docker-compose.yml down   # keeps the data volumes
docker compose -f deployment/observability/docker-compose.yml down -v  # and deletes them
```

Stop the app by PID, not with `pkill -f DiscordBot.Bot`: that pattern also matches the shell
running the `pkill`, so the command kills itself (exit 144).

Data persists in named volumes between `up` runs, so old runs show up in queries until you `down
-v` or narrow the time range. Restarting the bot gives it a new process but the same
`service_instance_id`.

## Troubleshooting

| Symptom | Cause |
|---|---|
| `failed to connect to the docker API at unix:///var/run/docker.sock` | The daemon is not running; start `dockerd` (step 1). |
| `network observability declared as external, but could not be found` | Bot compose started before the stack; start the stack first. |
| Nothing in any store | `OpenTelemetry__Otlp__Endpoint` not set on the app, or set to `otel-collector:4317` for a host process (use `localhost:4317`). Check `docker compose -f deployment/observability/docker-compose.yml logs otel-collector`. |
| Logs arrive, traces do not | Production environment with 10% sampling, or the path is filtered (`/health`, static files). Send `traceparent` with flag `01`. |
| `Unable to find datasource` | Wrong data source UID; they are `prometheus`, `loki` and `tempo`. |
| `404 page not found` from Loki | The path lacks the `/loki` prefix: Loki's API is `/loki/api/v1/...`. |
| Loki query hangs then times out | A replaced `loki.yaml` lost the raised gRPC message size; see the lessons-learned note. |
| Outbound HTTP panels full of failures to `localhost:8200` | Elastic APM still enabled; set `ElasticApm__Enabled=false`. |

## Related

- [Grafana Observability Stack](grafana-observability.md): architecture, configuration, metric catalogue
- [`deployment/observability/README.md`](../../deployment/observability/README.md): running and changing the stack
- [Lessons learned: Grafana stack and OTLP export](../lessons-learned/grafana-otlp-stack.md)
- [Distributed Tracing](tracing.md), [Metrics](metrics.md)
