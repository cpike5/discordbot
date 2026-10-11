#!/usr/bin/env python3
"""Generates the provisioned Grafana dashboards in ./dashboards.

The JSON files are build output: edit this script and re-run it rather than editing the JSON
(Grafana allows UI edits, but they are lost the next time the files are regenerated).

    python3 deployment/observability/grafana/generate_dashboards.py

Conventions:
- Every bot query is scoped by $env and $instance through SEL.
- Status colours (green/orange/red) appear only as thresholds, never as series colours.
- One unit per panel: two measures of different scale get two panels, never a second axis.
"""
import json
import os

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "dashboards")

PROM = {"type": "prometheus", "uid": "prometheus"}
LOKI = {"type": "loki", "uid": "loki"}
TEMPO = {"type": "tempo", "uid": "tempo"}

SEL = 'service_name="discordbot", deployment_environment=~"$env", service_instance_id=~"$instance"'
LOKI_SEL = '{service_name="discordbot", deployment_environment=~"$env", service_instance_id=~"$instance"}'

GREEN, ORANGE, RED, BLUE = "green", "orange", "red", "blue"


# ---------------------------------------------------------------------------
# Panel builders
# ---------------------------------------------------------------------------

def prom(expr, legend="", instant=False, fmt=None, ref=None):
    t = {"datasource": PROM, "expr": expr, "legendFormat": legend, "range": not instant, "instant": instant}
    if fmt:
        t["format"] = fmt
    if ref:
        t["refId"] = ref
    return t


def loki(expr, legend="", qtype="range"):
    return {"datasource": LOKI, "expr": expr, "legendFormat": legend, "queryType": qtype}


def thresholds(*steps):
    """steps: (color, value) pairs; the first value is ignored (base)."""
    out = []
    for i, (color, value) in enumerate(steps):
        out.append({"color": color, "value": None if i == 0 else value})
    return {"mode": "absolute", "steps": out}


def _targets(targets):
    for i, t in enumerate(targets):
        t.setdefault("refId", chr(ord("A") + i))
    return targets


def timeseries(title, targets, unit="short", desc="", stack=False, min0=True, draw="line",
               thresholds_=None, legend_calcs=None, max_=None, overrides=None, ds=PROM):
    custom = {
        "drawStyle": draw,
        "lineWidth": 2,
        "fillOpacity": 18 if stack else 6,
        "gradientMode": "opacity",
        "showPoints": "never",
        "spanNulls": True,
        "axisSoftMin": 0 if min0 else None,
        "stacking": {"mode": "normal" if stack else "none", "group": "A"},
        "barAlignment": 0,
    }
    if draw == "bars":
        custom["fillOpacity"] = 70
        custom["lineWidth"] = 1
    defaults = {"unit": unit, "custom": custom, "color": {"mode": "palette-classic"}}
    if max_ is not None:
        defaults["max"] = max_
    if thresholds_:
        defaults["thresholds"] = thresholds_
        custom["thresholdsStyle"] = {"mode": "dashed"}
    return {
        "type": "timeseries",
        "title": title,
        "description": desc,
        "datasource": ds,
        "targets": _targets(targets),
        "fieldConfig": {"defaults": defaults, "overrides": overrides or []},
        "options": {
            "legend": {"displayMode": "table" if legend_calcs else "list", "placement": "bottom",
                       "calcs": legend_calcs or [], "showLegend": True},
            "tooltip": {"mode": "multi", "sort": "desc"},
        },
    }


def stat(title, targets, unit="short", desc="", thr=None, mappings=None, color_mode="value",
         graph=True, decimals=None, text_size=None):
    defaults = {"unit": unit, "color": {"mode": "thresholds"}, "noValue": "–",
                "thresholds": thr or thresholds((BLUE, None)), "mappings": mappings or []}
    if decimals is not None:
        defaults["decimals"] = decimals
    opts = {
        "reduceOptions": {"calcs": ["lastNotNull"], "fields": "", "values": False},
        "colorMode": color_mode,
        "graphMode": "area" if graph else "none",
        "justifyMode": "center",
        "textMode": "value",
        "orientation": "auto",
    }
    if text_size:
        opts["text"] = {"valueSize": text_size}
    return {"type": "stat", "title": title, "description": desc, "datasource": PROM,
            "targets": _targets(targets), "fieldConfig": {"defaults": defaults, "overrides": []},
            "options": opts}


def gauge(title, targets, unit="percent", desc="", thr=None, min_=0, max_=100):
    return {"type": "gauge", "title": title, "description": desc, "datasource": PROM,
            "targets": _targets(targets),
            "fieldConfig": {"defaults": {"unit": unit, "min": min_, "max": max_, "noValue": "–",
                                         "color": {"mode": "thresholds"},
                                         "thresholds": thr or thresholds((RED, None), (ORANGE, 95), (GREEN, 99))},
                            "overrides": []},
            "options": {"reduceOptions": {"calcs": ["lastNotNull"], "fields": "", "values": False},
                        "showThresholdMarkers": True, "showThresholdLabels": False}}


def bargauge(title, targets, unit="short", desc="", thr=None):
    return {"type": "bargauge", "title": title, "description": desc, "datasource": PROM,
            "targets": _targets(targets),
            "fieldConfig": {"defaults": {"unit": unit, "color": {"mode": "thresholds"},
                                         "thresholds": thr or thresholds((BLUE, None))},
                            "overrides": []},
            "options": {"reduceOptions": {"calcs": ["lastNotNull"], "fields": "", "values": False},
                        "orientation": "horizontal", "displayMode": "basic", "showUnfilled": True,
                        "valueMode": "color", "namePlacement": "left", "sizing": "auto"}}


def table(title, targets, desc="", overrides=None, transformations=None, sort=None):
    opts = {"showHeader": True, "cellHeight": "sm", "footer": {"show": False}}
    if sort:
        opts["sortBy"] = [{"displayName": sort[0], "desc": sort[1]}]
    return {"type": "table", "title": title, "description": desc, "datasource": PROM,
            "targets": _targets(targets),
            "fieldConfig": {"defaults": {"custom": {"align": "auto", "filterable": False}},
                            "overrides": overrides or []},
            "transformations": transformations or [],
            "options": opts}


def state_timeline(title, targets, desc="", mappings=None, thr=None):
    return {"type": "state-timeline", "title": title, "description": desc, "datasource": PROM,
            "targets": _targets(targets),
            "fieldConfig": {"defaults": {"color": {"mode": "thresholds"},
                                         "thresholds": thr or thresholds((RED, None), (ORANGE, 0.5), (GREEN, 1)),
                                         "mappings": mappings or [],
                                         "custom": {"fillOpacity": 80, "lineWidth": 0}},
                            "overrides": []},
            "options": {"showValue": "never", "mergeValues": True, "rowHeight": 0.85,
                        "legend": {"showLegend": False}, "tooltip": {"mode": "single"}}}


def logs(title, expr, desc=""):
    return {"type": "logs", "title": title, "description": desc, "datasource": LOKI,
            "targets": _targets([loki(expr)]),
            "options": {"showTime": True, "wrapLogMessage": True, "prettifyLogMessage": False,
                        "enableLogDetails": True, "sortOrder": "Descending", "dedupStrategy": "none",
                        "showLabels": False, "showCommonLabels": False}}


def traces_table(title, query, desc="", limit=20):
    return {"type": "table", "title": title, "description": desc, "datasource": TEMPO,
            "targets": [{"datasource": TEMPO, "refId": "A", "queryType": "traceql", "query": query,
                         "limit": limit, "tableType": "traces"}],
            "fieldConfig": {"defaults": {"custom": {"align": "auto"}}, "overrides": []},
            "options": {"showHeader": True, "cellHeight": "sm"}}


def node_graph(title, desc=""):
    return {"type": "nodeGraph", "title": title, "description": desc, "datasource": TEMPO,
            "targets": [{"datasource": TEMPO, "refId": "A", "queryType": "serviceMap",
                         "serviceMapQuery": '{deployment_environment=~"$env"}'}],
            "options": {}}


def alert_list(title):
    return {"type": "alertlist", "title": title, "datasource": None,
            "options": {"alertInstanceLabelFilter": "", "alertName": "", "dashboardAlerts": False,
                        "groupBy": [], "groupMode": "default", "maxItems": 20, "sortOrder": 1,
                        "stateFilter": {"firing": True, "pending": True, "noData": False,
                                        "normal": False, "error": True},
                        "viewMode": "list", "datasource": "prometheus"}}


def text(title, content):
    return {"type": "text", "title": title, "options": {"mode": "markdown", "content": content}}


def row(title, collapsed=False):
    return {"type": "row", "title": title, "collapsed": collapsed, "panels": []}


# ---------------------------------------------------------------------------
# Layout: panels are placed left to right, wrapping at 24 columns.
# ---------------------------------------------------------------------------

class Layout:
    def __init__(self):
        self.panels = []
        self.x = 0
        self.y = 0
        self.row_h = 0

    def newline(self):
        if self.x:
            self.y += self.row_h
        self.x = 0
        self.row_h = 0

    def row(self, title):
        self.newline()
        p = row(title)
        p["id"] = len(self.panels) + 1
        p["gridPos"] = {"x": 0, "y": self.y, "w": 24, "h": 1}
        self.panels.append(p)
        self.y += 1

    def add(self, panel, w, h):
        if self.x + w > 24:
            self.newline()
        panel["id"] = len(self.panels) + 1
        panel["gridPos"] = {"x": self.x, "y": self.y, "w": w, "h": h}
        self.x += w
        self.row_h = max(self.row_h, h)
        self.panels.append(panel)


def variables(extra=None):
    def query_var(name, label, query):
        return {"name": name, "label": label, "type": "query", "datasource": PROM,
                "query": {"query": query, "refId": "PrometheusVariableQueryEditor-VariableQuery",
                          "qryType": 1},
                "definition": query, "refresh": 2, "includeAll": True, "multi": True,
                "allValue": ".*", "current": {"selected": True, "text": ["All"], "value": ["$__all"]},
                "sort": 1}

    out = [
        query_var("env", "Environment",
                  'label_values(discordbot_process_uptime_seconds{service_name="discordbot"}, deployment_environment)'),
        query_var("instance", "Instance",
                  'label_values(discordbot_process_uptime_seconds{service_name="discordbot", deployment_environment=~"$env"}, service_instance_id)'),
    ]
    return out + (extra or [])


DASHBOARD_LINKS = [
    {"title": "Discord Bot", "type": "dashboards", "tags": ["discordbot"], "asDropdown": True,
     "includeVars": True, "keepTime": True, "icon": "external link"},
]


def dashboard(uid, title, layout, desc, tags=None, refresh="30s", time_from="now-6h", extra_vars=None):
    return {
        "uid": uid,
        "title": title,
        "description": desc,
        "tags": ["discordbot"] + (tags or []),
        "timezone": "browser",
        "editable": True,
        "graphTooltip": 1,
        "refresh": refresh,
        "schemaVersion": 39,
        "version": 1,
        "time": {"from": time_from, "to": "now"},
        "timepicker": {},
        "templating": {"list": variables(extra_vars)},
        "annotations": {"list": [
            {"builtIn": 1, "datasource": {"type": "grafana", "uid": "-- Grafana --"}, "enable": True,
             "hide": True, "iconColor": "rgba(0, 211, 255, 1)", "name": "Annotations & Alerts",
             "type": "dashboard"},
            {"name": "Gateway disconnects", "datasource": PROM, "enable": True, "iconColor": "red",
             "expr": f'changes(discordbot_gateway_connection_events_total{{{SEL}, event="disconnected"}}[1m]) > 0',
             "step": "60s", "titleFormat": "Gateway disconnected", "textFormat": "{{reason}}"},
            {"name": "Restarts", "datasource": PROM, "enable": True, "iconColor": "purple",
             "expr": f'discordbot_process_uptime_seconds{{{SEL}}} < 90',
             "step": "60s", "titleFormat": "Process started", "textFormat": "{{service_instance_id}}"},
        ]},
        "links": DASHBOARD_LINKS,
        "panels": layout.panels,
    }


# Reusable expressions
def unmatched(expr):
    """Names the empty http_route label that ASP.NET Core reports for requests no endpoint matched."""
    return f'label_replace({expr}, "http_route", "(unmatched)", "http_route", "")'


def rate(metric, extra="", by=None, window="$__rate_interval"):
    labels = f"{SEL}, {extra}" if extra else SEL
    inner = f"rate({metric}{{{labels}}}[{window}])"
    return f"sum by ({by}) ({inner})" if by else f"sum({inner})"


def quantile(q, bucket_metric, extra="", by=None):
    labels = f"{SEL}, {extra}" if extra else SEL
    by_clause = f"le, {by}" if by else "le"
    return f"histogram_quantile({q}, sum by ({by_clause}) (rate({bucket_metric}{{{labels}}}[$__rate_interval])))"


CONNECTED_MAP = [{"type": "value", "options": {"0": {"text": "Disconnected", "color": RED},
                                                "1": {"text": "Connected", "color": GREEN}}}]
HEALTH_MAP = [{"type": "value", "options": {"0": {"text": "Unhealthy", "color": RED},
                                             "0.5": {"text": "Degraded", "color": ORANGE},
                                             "1": {"text": "Healthy", "color": GREEN}}}]


# ---------------------------------------------------------------------------
# 1. Overview (home)
# ---------------------------------------------------------------------------

def overview():
    L = Layout()
    L.row("Is it up?")
    L.add(stat("Gateway", [prom(f"max(discordbot_gateway_connected{{{SEL}}})", instant=True)],
               desc="Discord gateway connection. Offline-mode hosts never connect.",
               mappings=CONNECTED_MAP, thr=thresholds((RED, None), (GREEN, 1)), color_mode="background",
               graph=False), 4, 4)
    L.add(stat("Health checks", [prom(f'min(discordbot_health_status{{{SEL}, check="overall"}})', instant=True)],
               desc="Overall result of the /health checks (database, Discord gateway), published every 30s.",
               mappings=HEALTH_MAP, thr=thresholds((RED, None), (ORANGE, 0.5), (GREEN, 1)),
               color_mode="background", graph=False), 4, 4)
    L.add(stat("Gateway latency", [prom(f"max(discordbot_gateway_latency_milliseconds{{{SEL}}})")],
               unit="ms", thr=thresholds((GREEN, None), (ORANGE, 250), (RED, 500))), 4, 4)
    L.add(stat("Background services in error",
               [prom(f"count(discordbot_background_up{{{SEL}}} == 0) or vector(0)", instant=True)],
               desc="Monitored background services whose last recorded state is Error. See Runtime & Data.",
               thr=thresholds((GREEN, None), (RED, 1)), color_mode="background", graph=False), 4, 4)
    L.add(stat("Uptime", [prom(f"min(discordbot_process_uptime_seconds{{{SEL}}})", instant=True)],
               unit="s", graph=False, decimals=1), 4, 4)
    L.add(stat("Guilds", [prom(f"max(discordbot_guilds_active{{{SEL}}})")]), 4, 4)

    L.row("Traffic and errors")
    L.add(stat("Commands / min", [prom(f"{rate('discordbot_command_count_total')} * 60")], decimals=1), 4, 4)
    L.add(stat("Command failure rate",
               [prom(rate("discordbot_command_count_total", 'status="failure"') + " / " + rate("discordbot_command_count_total"))],
               unit="percentunit", decimals=2, thr=thresholds((GREEN, None), (ORANGE, 0.01), (RED, 0.05))), 4, 4)
    L.add(stat("Command p95", [prom(quantile(0.95, "discordbot_command_duration_milliseconds_bucket"))],
               unit="ms", thr=thresholds((GREEN, None), (ORANGE, 1000), (RED, 3000))), 4, 4)
    L.add(stat("HTTP req / s", [prom(rate("http_server_request_duration_seconds_count"))], unit="reqps", decimals=2), 4, 4)
    L.add(stat("HTTP 5xx rate",
               [prom("(" + rate("http_server_request_duration_seconds_count", 'http_response_status_code=~"5.."') + " or vector(0)) / " + rate("http_server_request_duration_seconds_count"))],
               unit="percentunit", decimals=2, thr=thresholds((GREEN, None), (ORANGE, 0.01), (RED, 0.05))), 4, 4)
    L.add(stat("Error logs / min",
               [loki(f'sum(count_over_time({LOKI_SEL} | severity_text=~"Error|Fatal|Critical" [1m])) or vector(0)')],
               thr=thresholds((GREEN, None), (ORANGE, 1), (RED, 10)), decimals=0), 4, 4)
    L.panels[-1]["datasource"] = LOKI

    L.add(timeseries("Commands by outcome",
                     [prom(rate("discordbot_command_count_total", by="status") + " * 60", "{{status}}")],
                     unit="short", stack=True, desc="Slash commands per minute."), 12, 8)
    L.add(timeseries("HTTP requests by status class",
                     [prom(f'sum by (class) (label_replace(rate(http_server_request_duration_seconds_count{{{SEL}}}[$__rate_interval]), "class", "${{1}}xx", "http_response_status_code", "(\\\\d).*"))', "{{class}}")],
                     unit="reqps", stack=True), 12, 8)
    L.add(timeseries("Latency p95",
                     [prom(quantile(0.95, "discordbot_command_duration_milliseconds_bucket"), "Commands"),
                      prom(quantile(0.95, "http_server_request_duration_seconds_bucket") + " * 1000", "HTTP"),
                      prom(quantile(0.95, "discordbot_component_duration_milliseconds_bucket"), "Components")],
                     unit="ms"), 12, 8)
    L.add(timeseries("Log volume by level",
                     [loki(f'sum by (severity_text) (count_over_time({LOKI_SEL} [$__auto]))', "{{severity_text}}")],
                     unit="short", stack=True, draw="bars", ds=LOKI), 12, 8)

    L.row("Resources")
    L.add(timeseries("CPU", [prom(f"max by (service_instance_id) (discordbot_process_cpu_usage_percent{{{SEL}}})", "{{service_instance_id}}")],
                     unit="percent"), 6, 7)
    L.add(timeseries("Working set", [prom(f"max by (service_instance_id) (discordbot_process_memory_working_set_bytes{{{SEL}}})", "{{service_instance_id}}")],
                     unit="bytes"), 6, 7)
    L.add(timeseries("Gateway latency", [prom(f"max by (service_instance_id) (discordbot_gateway_latency_milliseconds{{{SEL}}})", "{{service_instance_id}}")],
                     unit="ms", thresholds_=thresholds((GREEN, None), (ORANGE, 250), (RED, 500))), 6, 7)
    L.add(timeseries("Assistant spend / hour", [prom(rate("discordbot_llm_cost_usd_total", window="1h") + " * 3600", "USD / h")],
                     unit="currencyUSD"), 6, 7)

    L.row("Attention")
    L.add(alert_list("Firing alerts"), 8, 10)
    L.add(logs("Recent errors and warnings", f'{LOKI_SEL} | severity_text=~"Warning|Error|Fatal|Critical"'), 16, 10)
    return dashboard("discordbot-overview", "Discord Bot — Overview", L,
                     "Start here: is the bot up, is it serving, is anything on fire. Links at top right go deeper.",
                     tags=["overview"])


# ---------------------------------------------------------------------------
# 2. Discord: gateway, commands, features
# ---------------------------------------------------------------------------

def discord():
    L = Layout()
    L.row("Gateway")
    L.add(state_timeline("Gateway connection", [prom(f"max by (service_instance_id) (discordbot_gateway_connected{{{SEL}}})", "{{service_instance_id}}")],
                         mappings=CONNECTED_MAP, thr=thresholds((RED, None), (GREEN, 1))), 12, 5)
    L.add(stat("Gateway availability (range)",
               [prom(f"avg(avg_over_time(discordbot_gateway_connected{{{SEL}}}[$__range]))", instant=True)],
               unit="percentunit", decimals=3, graph=False, thr=thresholds((RED, None), (ORANGE, 0.99), (GREEN, 0.999)),
               desc="Share of samples in the selected range with the gateway connected. This is the real uptime figure; the SLO 'uptime_30d' gauge is a command-success proxy."), 6, 5)
    L.add(stat("Disconnects (range)",
               [prom(f'sum(increase(discordbot_gateway_connection_events_total{{{SEL}, event="disconnected"}}[$__range])) or vector(0)', instant=True)],
               thr=thresholds((GREEN, None), (ORANGE, 1), (RED, 5)), graph=False, decimals=0), 6, 5)
    L.add(timeseries("Heartbeat latency", [prom(f"max by (service_instance_id) (discordbot_gateway_latency_milliseconds{{{SEL}}})", "{{service_instance_id}}")],
                     unit="ms", thresholds_=thresholds((GREEN, None), (ORANGE, 250), (RED, 500)), legend_calcs=["mean", "max"]), 12, 8)
    L.add(timeseries("Gateway events / s", [prom(rate("discordbot_gateway_events_total", by="event"), "{{event}}")],
                     unit="ops", stack=True), 12, 8)
    L.add(timeseries("Discord.NET log events", [prom(rate("discordbot_discord_log_events_total", by="severity") + " * 60", "{{severity}}")],
                     unit="short", stack=True, draw="bars", desc="Per minute, by severity. Warnings and errors here are Discord.NET's own (reconnects, rate limits, gateway faults)."), 12, 8)
    L.add(timeseries("Disconnects by reason", [prom(f'sum by (reason) (increase(discordbot_gateway_connection_events_total{{{SEL}, event="disconnected"}}[$__interval]))', "{{reason}}")],
                     unit="short", draw="bars"), 12, 8)

    L.row("Discord REST")
    L.add(timeseries("REST responses by route (top 10)", [prom(f"topk(10, {rate('discordbot_discord_rest_responses_total', by='route')})", "{{route}}")],
                     unit="reqps", desc="From Discord.NET's rate-limit callback, one per REST response. Snowflakes and tokens are collapsed in the route."), 12, 8)
    L.add(timeseries("Rate limits", [prom(rate("discordbot_discord_rest_ratelimits_total", by="kind, scope") + " * 60", "{{kind}} ({{scope}})")],
                     unit="short", draw="bars", desc="Per minute. hit = a 429 from Discord; exhausted = a bucket reached 0 remaining and Discord.NET waited."), 12, 8)
    L.add(table("Rate-limited routes (range)",
                [prom(f"sort_desc(sum by (route, kind) (increase(discordbot_discord_rest_ratelimits_total{{{SEL}}}[$__range])) > 0)", instant=True, fmt="table")],
                transformations=[{"id": "organize", "options": {"excludeByName": {"Time": True},
                                                                "renameByName": {"Value": "Events"}}}]), 24, 6)

    L.row("Slash commands")
    L.add(timeseries("Commands / min by command (top 10)",
                     [prom(f"topk(10, {rate('discordbot_command_count_total', by='command')} * 60)", "{{command}}")], unit="short"), 12, 8)
    L.add(timeseries("Command latency", [prom(quantile(0.5, "discordbot_command_duration_milliseconds_bucket"), "p50"),
                                         prom(quantile(0.95, "discordbot_command_duration_milliseconds_bucket"), "p95"),
                                         prom(quantile(0.99, "discordbot_command_duration_milliseconds_bucket"), "p99")],
                     unit="ms", thresholds_=thresholds((GREEN, None), (ORANGE, 3000))), 12, 8)
    L.add(table("Commands (range)", [
        prom(f"sum by (command) (increase(discordbot_command_count_total{{{SEL}}}[$__range]))", instant=True, fmt="table", ref="A"),
        prom(f'sum by (command) (increase(discordbot_command_count_total{{{SEL}, status="failure"}}[$__range])) / sum by (command) (increase(discordbot_command_count_total{{{SEL}}}[$__range]))', instant=True, fmt="table", ref="B"),
        prom(f"histogram_quantile(0.95, sum by (le, command) (increase(discordbot_command_duration_milliseconds_bucket{{{SEL}}}[$__range])))", instant=True, fmt="table", ref="C"),
    ], transformations=[
        {"id": "merge", "options": {}},
        {"id": "organize", "options": {"excludeByName": {"Time": True},
                                       "renameByName": {"Value #A": "Executions", "Value #B": "Failure rate", "Value #C": "p95"}}},
    ], overrides=[
        {"matcher": {"id": "byName", "options": "Failure rate"},
         "properties": [{"id": "unit", "value": "percentunit"}, {"id": "decimals", "value": 1},
                        {"id": "custom.cellOptions", "value": {"type": "color-text"}},
                        {"id": "thresholds", "value": thresholds((GREEN, None), (ORANGE, 0.02), (RED, 0.1))}]},
        {"matcher": {"id": "byName", "options": "p95"}, "properties": [{"id": "unit", "value": "ms"}]},
    ], sort=("Executions", True)), 12, 9)
    L.add(timeseries("Failures / min by command", [prom(rate("discordbot_command_count_total", 'status="failure"', by="command") + " * 60", "{{command}}")],
                     unit="short", draw="bars"), 12, 9)
    L.add(timeseries("Executing now", [prom(f"sum(discordbot_command_active{{{SEL}}})", "commands")], unit="short"), 8, 7)
    L.add(timeseries("Component interactions / min", [prom(rate("discordbot_component_count_total", by="component_type, status") + " * 60", "{{component_type}} {{status}}")],
                     unit="short", stack=True), 8, 7)
    L.add(timeseries("Command rate-limit violations / min", [prom(rate("discordbot_ratelimit_violations_total", by="command") + " * 60", "{{command}}")],
                     unit="short", draw="bars", desc="Users hitting the bot's own per-command rate limits (not Discord's)."), 8, 7)

    L.row("Features")
    L.add(timeseries("Auto-mod detections / h", [prom(rate("discordbot_automod_detections_total", by="rule_type, severity", window="1h") + " * 3600", "{{rule_type}} {{severity}}")],
                     unit="short"), 8, 8)
    L.add(timeseries("Auto-mod actions", [prom(f"sum by (action, status) (increase(discordbot_automod_actions_total{{{SEL}}}[$__interval]))", "{{action}} {{status}}")],
                     unit="short", draw="bars"), 8, 8)
    L.add(timeseries("Voice connections", [prom(f"sum(discordbot_voice_connections{{{SEL}}})", "connections")], unit="short"), 8, 8)
    L.add(timeseries("Audio playbacks", [prom(f"sum by (source, status) (increase(discordbot_audio_playbacks_total{{{SEL}}}[$__interval]))", "{{source}} {{status}}"),
                                         prom(f"sum by (status) (increase(discordbot_vox_commands_total{{{SEL}}}[$__interval]))", "vox {{status}}")],
                     unit="short", draw="bars", stack=True), 8, 8)
    L.add(timeseries("Playback duration p95", [prom(quantile(0.95, "discordbot_audio_playback_duration_seconds_bucket", by="source"), "{{source}}")],
                     unit="s"), 8, 8)
    L.add(timeseries("VOX errors", [prom(f"sum by (error_type) (increase(discordbot_vox_errors_total{{{SEL}}}[$__interval]))", "{{error_type}}")],
                     unit="short", draw="bars"), 8, 8)
    L.add(timeseries("Reminder deliveries", [prom(f"sum by (status) (increase(discordbot_reminders_deliveries_total{{{SEL}}}[$__interval]))", "{{status}}")],
                     unit="short", draw="bars", stack=True), 12, 8)
    L.add(timeseries("Scheduled message executions", [prom(f"sum by (status) (increase(discordbot_scheduled_messages_executions_total{{{SEL}}}[$__interval]))", "{{status}}")],
                     unit="short", draw="bars", stack=True), 12, 8)
    return dashboard("discordbot-discord", "Discord Bot — Gateway, Commands & Features", L,
                     "Gateway health, Discord REST rate limits, slash commands, and the features that run outside commands.",
                     tags=["discord"])


# ---------------------------------------------------------------------------
# 3. Web portal and API
# ---------------------------------------------------------------------------

def web():
    L = Layout()
    route_var = {"name": "route", "label": "Route", "type": "query", "datasource": PROM,
                 "query": {"query": f'label_values(http_server_request_duration_seconds_count{{service_name="discordbot", deployment_environment=~"$env"}}, http_route)',
                           "refId": "PrometheusVariableQueryEditor-VariableQuery", "qryType": 1},
                 "definition": 'label_values(http_server_request_duration_seconds_count, http_route)',
                 "refresh": 2, "includeAll": True, "multi": True, "allValue": ".*",
                 "current": {"selected": True, "text": ["All"], "value": ["$__all"]}, "sort": 1}
    R = 'http_route=~"$route"'
    L.row("Inbound HTTP (ASP.NET Core)")
    L.add(stat("Requests / s", [prom(rate("http_server_request_duration_seconds_count", R))], unit="reqps", decimals=2), 6, 4)
    L.add(stat("5xx rate", [prom("(" + rate("http_server_request_duration_seconds_count", R + ', http_response_status_code=~"5.."') + " or vector(0)) / " + rate("http_server_request_duration_seconds_count", R))],
               unit="percentunit", decimals=2, thr=thresholds((GREEN, None), (ORANGE, 0.01), (RED, 0.05))), 6, 4)
    L.add(stat("p95 latency", [prom(quantile(0.95, "http_server_request_duration_seconds_bucket", R))], unit="s",
               thr=thresholds((GREEN, None), (ORANGE, 0.5), (RED, 2))), 6, 4)
    L.add(stat("Active requests", [prom(f"sum(http_server_active_requests{{{SEL}}})")]), 6, 4)
    L.add(timeseries("Requests by route (top 10)", [prom(unmatched(f"topk(10, {rate('http_server_request_duration_seconds_count', R, by='http_route')})"), "{{http_route}}")],
                     unit="reqps"), 12, 8)
    L.add(timeseries("Responses by status code", [prom(rate("http_server_request_duration_seconds_count", R, by="http_response_status_code"), "{{http_response_status_code}}")],
                     unit="reqps", stack=True), 12, 8)
    L.add(timeseries("Latency by route p95 (top 10)", [prom(unmatched(f"topk(10, {quantile(0.95, 'http_server_request_duration_seconds_bucket', R, by='http_route')})"), "{{http_route}}")],
                     unit="s"), 12, 8)
    L.add(timeseries("Unhandled exceptions", [prom(f"sum by (error_type) (increase(aspnetcore_diagnostics_exceptions_total{{{SEL}}}[$__interval]))", "{{error_type}}")],
                     unit="short", draw="bars", desc="Exceptions that reached the ASP.NET Core exception handler."), 12, 8)
    L.add(table("Routes (range)", [
        prom(unmatched(f"sum by (http_route) (increase(http_server_request_duration_seconds_count{{{SEL}, {R}}}[$__range])) > 0"), instant=True, fmt="table", ref="A"),
        prom(unmatched(f'sum by (http_route) (increase(http_server_request_duration_seconds_count{{{SEL}, {R}, http_response_status_code=~"5.."}}[$__range])) / sum by (http_route) (increase(http_server_request_duration_seconds_count{{{SEL}, {R}}}[$__range]))'), instant=True, fmt="table", ref="B"),
        prom(unmatched(f"histogram_quantile(0.95, sum by (le, http_route) (increase(http_server_request_duration_seconds_bucket{{{SEL}, {R}}}[$__range])))"), instant=True, fmt="table", ref="C"),
    ], transformations=[
        {"id": "merge", "options": {}},
        {"id": "organize", "options": {"excludeByName": {"Time": True},
                                       "renameByName": {"http_route": "Route", "Value #A": "Requests", "Value #B": "5xx rate", "Value #C": "p95"}}},
    ], overrides=[
        {"matcher": {"id": "byName", "options": "5xx rate"},
         "properties": [{"id": "unit", "value": "percentunit"}, {"id": "decimals", "value": 2},
                        {"id": "custom.cellOptions", "value": {"type": "color-text"}},
                        {"id": "thresholds", "value": thresholds((GREEN, None), (ORANGE, 0.01), (RED, 0.05))}]},
        {"matcher": {"id": "byName", "options": "p95"}, "properties": [{"id": "unit", "value": "s"}]},
    ], sort=("Requests", True)), 24, 9)

    L.row("Connections and SignalR")
    L.add(timeseries("Kestrel connections", [prom(f"sum(kestrel_active_connections{{{SEL}}})", "active"),
                                             prom(f"sum(kestrel_queued_connections{{{SEL}}})", "queued")], unit="short"), 8, 7)
    L.add(timeseries("SignalR connections", [prom(f"sum by (signalr_transport) (signalr_server_active_connections{{{SEL}}})", "{{signalr_transport}}")],
                     unit="short", desc="Dashboard hub (/hubs/dashboard) connections by transport."), 8, 7)
    L.add(timeseries("Live dashboard subscribers", [prom(f"sum by (group) (discordbot_signalr_subscribers{{{SEL}}})", "{{group}}")], unit="short"), 8, 7)

    L.row("Outbound HTTP (Discord OAuth, OpenRouter, Azure)")
    L.add(timeseries("Requests by host", [prom(rate("http_client_request_duration_seconds_count", by="server_address"), "{{server_address}}")], unit="reqps"), 8, 8)
    L.add(timeseries("p95 by host", [prom(quantile(0.95, "http_client_request_duration_seconds_bucket", by="server_address"), "{{server_address}}")], unit="s"), 8, 8)
    L.add(timeseries("Failures by host", [prom(f'sum by (server_address, http_response_status_code, error_type) (rate(http_client_request_duration_seconds_count{{{SEL}, http_response_status_code=~"4..|5..|"}}[$__rate_interval]))', "{{server_address}} {{http_response_status_code}}{{error_type}}")],
                     unit="reqps", desc="4xx, 5xx and transport errors (error_type set, no status)."), 8, 8)

    L.row("Portal API middleware (normalized endpoints)")
    L.add(timeseries("API requests by endpoint (top 10)", [prom(f"topk(10, {rate('discordbot_api_request_count_total', by='endpoint')})", "{{endpoint}}")], unit="reqps"), 12, 8)
    L.add(timeseries("API p95 by endpoint (top 10)", [prom(f"topk(10, {quantile(0.95, 'discordbot_api_request_duration_milliseconds_bucket', by='endpoint')})", "{{endpoint}}")], unit="ms"), 12, 8)
    return dashboard("discordbot-web", "Discord Bot — Web Portal & API", L,
                     "The admin portal: inbound HTTP, SignalR, Kestrel, and the HTTP calls the app makes.",
                     tags=["web"], extra_vars=[route_var])


# ---------------------------------------------------------------------------
# 4. AI assistant
# ---------------------------------------------------------------------------

def assistant():
    L = Layout()
    mode_var = {"name": "mode", "label": "Mode", "type": "query", "datasource": PROM,
                "query": {"query": 'label_values(discordbot_llm_request_count_total, mode)',
                          "refId": "PrometheusVariableQueryEditor-VariableQuery", "qryType": 1},
                "definition": 'label_values(discordbot_llm_request_count_total, mode)',
                "refresh": 2, "includeAll": True, "multi": True, "allValue": ".*",
                "current": {"selected": True, "text": ["All"], "value": ["$__all"]}, "sort": 1}
    M = 'mode=~"$mode"'
    L.row("Requests")
    L.add(stat("Requests (range)", [prom(f"sum(increase(discordbot_llm_request_count_total{{{SEL}, {M}}}[$__range]))", instant=True)], decimals=0, graph=False), 4, 4)
    L.add(stat("Success rate", [prom(f'sum(rate(discordbot_llm_request_count_total{{{SEL}, {M}, status="success"}}[$__range])) / sum(rate(discordbot_llm_request_count_total{{{SEL}, {M}}}[$__range]))', instant=True)],
               unit="percentunit", decimals=1, graph=False, thr=thresholds((RED, None), (ORANGE, 0.9), (GREEN, 0.98))), 4, 4)
    L.add(stat("Spend (range)", [prom(f"sum(increase(discordbot_llm_cost_usd_total{{{SEL}, {M}}}[$__range]))", instant=True)], unit="currencyUSD", decimals=2, graph=False), 4, 4)
    L.add(stat("Tokens (range)", [prom(f"sum(increase(discordbot_llm_tokens_total{{{SEL}, {M}}}[$__range]))", instant=True)], unit="short", graph=False), 4, 4)
    L.add(stat("p95 latency", [prom(quantile(0.95, "discordbot_llm_request_duration_milliseconds_bucket", M))], unit="ms",
               thr=thresholds((GREEN, None), (ORANGE, 15000), (RED, 30000))), 4, 4)
    L.add(stat("Usage rows dropped (range)", [prom(f"sum(increase(discordbot_llm_usage_records_dropped_total{{{SEL}}}[$__range])) or vector(0)", instant=True)],
               decimals=0, graph=False, thr=thresholds((GREEN, None), (RED, 1)),
               desc="Ledger rows lost because the write queue was full. Non-zero means the portal's usage numbers undercount."), 4, 4)
    L.add(timeseries("Requests / min by mode", [prom(rate("discordbot_llm_request_count_total", M, by="mode, status") + " * 60", "{{mode}} {{status}}")],
                     unit="short", stack=True), 12, 8)
    L.add(timeseries("End-to-end latency", [prom(quantile(0.5, "discordbot_llm_request_duration_milliseconds_bucket", M, by="mode"), "{{mode}} p50"),
                                            prom(quantile(0.95, "discordbot_llm_request_duration_milliseconds_bucket", M, by="mode"), "{{mode}} p95")],
                     unit="ms", desc="Whole request, including every completion and tool call."), 12, 8)

    L.row("Cost and tokens")
    L.add(timeseries("Spend / hour by model", [prom(rate("discordbot_llm_cost_usd_total", M, by="model", window="1h") + " * 3600", "{{model}}")],
                     unit="currencyUSD", stack=True), 12, 8)
    L.add(timeseries("Tokens / min by type", [prom(rate("discordbot_llm_tokens_total", M, by="type") + " * 60", "{{type}}")],
                     unit="short", stack=True, desc="cached = prompt tokens served from the provider's cache; cache_write = tokens written to it."), 12, 8)
    L.add(stat("Prompt cache hit ratio", [prom(f'sum(rate(discordbot_llm_tokens_total{{{SEL}, {M}, type="cached"}}[$__range])) / sum(rate(discordbot_llm_tokens_total{{{SEL}, {M}, type=~"input|cached"}}[$__range]))', instant=True)],
               unit="percentunit", decimals=1, graph=False, desc="Cached prompt tokens over all prompt tokens. Assumes input excludes cached; compare with provider docs."), 6, 5)
    L.add(bargauge("Spend by mode (range)", [prom(f"sum by (mode) (increase(discordbot_llm_cost_usd_total{{{SEL}, {M}}}[$__range]))", "{{mode}}", instant=True)], unit="currencyUSD"), 9, 5)
    L.add(bargauge("Spend by model (range)", [prom(f"sum by (model) (increase(discordbot_llm_cost_usd_total{{{SEL}, {M}}}[$__range]))", "{{model}}", instant=True)], unit="currencyUSD"), 9, 5)

    L.row("Agent loop: completions and tools")
    L.add(timeseries("Completions / min by model", [prom(rate("discordbot_agent_completion_count_total", by="model, status") + " * 60", "{{model}} {{status}}")],
                     unit="short", stack=True), 8, 8)
    L.add(timeseries("Completion latency p95 by model", [prom(quantile(0.95, "discordbot_agent_completion_duration_milliseconds_bucket", by="model"), "{{model}}")], unit="ms"), 8, 8)
    L.add(timeseries("Stop reasons", [prom(rate("discordbot_agent_completion_count_total", 'status="success"', by="stop_reason") + " * 60", "{{stop_reason}}")],
                     unit="short", stack=True, desc="MaxTokens means answers are being truncated."), 8, 8)
    L.add(timeseries("Tool calls / min by outcome", [prom(rate("discordbot_agent_tool_count_total", by="outcome") + " * 60", "{{outcome}}")],
                     unit="short", stack=True, desc="ok, failed_result (expected failure the model handles), timeout, repeated_call, unknown_tool, error."), 12, 8)
    L.add(timeseries("Tool latency p95 (top 10)", [prom(f"topk(10, {quantile(0.95, 'discordbot_agent_tool_duration_milliseconds_bucket', by='tool')})", "{{tool}}")], unit="ms"), 12, 8)
    L.add(table("Tools (range)", [
        prom(f"sum by (tool) (increase(discordbot_agent_tool_count_total{{{SEL}}}[$__range]))", instant=True, fmt="table", ref="A"),
        prom(f'sum by (tool) (increase(discordbot_agent_tool_count_total{{{SEL}, outcome!="ok"}}[$__range])) / sum by (tool) (increase(discordbot_agent_tool_count_total{{{SEL}}}[$__range]))', instant=True, fmt="table", ref="B"),
        prom(f"histogram_quantile(0.95, sum by (le, tool) (increase(discordbot_agent_tool_duration_milliseconds_bucket{{{SEL}}}[$__range])))", instant=True, fmt="table", ref="C"),
    ], transformations=[
        {"id": "merge", "options": {}},
        {"id": "organize", "options": {"excludeByName": {"Time": True},
                                       "renameByName": {"tool": "Tool", "Value #A": "Calls", "Value #B": "Not ok", "Value #C": "p95"}}},
    ], overrides=[
        {"matcher": {"id": "byName", "options": "Not ok"},
         "properties": [{"id": "unit", "value": "percentunit"}, {"id": "decimals", "value": 1},
                        {"id": "custom.cellOptions", "value": {"type": "color-text"}},
                        {"id": "thresholds", "value": thresholds((GREEN, None), (ORANGE, 0.1), (RED, 0.3))}]},
        {"matcher": {"id": "byName", "options": "p95"}, "properties": [{"id": "unit", "value": "ms"}]},
    ], sort=("Calls", True)), 24, 9)
    L.add(traces_table("Slowest assistant traces", '{resource.service.name="discordbot" && name=~"agent.tool.*"} | select(span.gen_ai.tool.name, span.bot.tool.outcome)',
                       desc="Traces containing tool spans, from Tempo. Click a trace ID to open the waterfall."), 24, 9)
    return dashboard("discordbot-assistant", "Discord Bot — AI Assistant", L,
                     "Assistant requests, spend, tokens, completion and tool behaviour. Data appears once OpenRouter:ApiKey is set and the assistant is used.",
                     tags=["llm"], extra_vars=[mode_var], time_from="now-24h")


# ---------------------------------------------------------------------------
# 5. Runtime and data
# ---------------------------------------------------------------------------

def runtime():
    L = Layout()
    L.row("Process and .NET runtime")
    L.add(timeseries("CPU", [prom(f"max by (service_instance_id) (discordbot_process_cpu_usage_percent{{{SEL}}})", "{{service_instance_id}}")], unit="percent"), 8, 7)
    L.add(timeseries("Working set", [prom(f"max by (service_instance_id) (discordbot_process_memory_working_set_bytes{{{SEL}}})", "{{service_instance_id}}")], unit="bytes"), 8, 7)
    L.add(timeseries("GC heap by generation", [prom(f"sum by (generation) (process_runtime_dotnet_gc_heap_size_bytes{{{SEL}}})", "{{generation}}")], unit="bytes", stack=True), 8, 7)
    L.add(timeseries("GC collections / min", [prom(rate("process_runtime_dotnet_gc_collections_count_total", by="generation") + " * 60", "{{generation}}")], unit="short"), 8, 7)
    L.add(timeseries("GC pause time", [prom(f"sum(rate(process_runtime_dotnet_gc_duration_nanoseconds_total{{{SEL}}}[$__rate_interval])) / 1e9", "share of wall time")],
                     unit="percentunit", desc="Fraction of time the process spent paused for GC."), 8, 7)
    L.add(timeseries("Allocation rate", [prom(rate("process_runtime_dotnet_gc_allocations_size_bytes_total"), "allocated")], unit="Bps"), 8, 7)
    L.add(timeseries("Exceptions / min", [prom(rate("process_runtime_dotnet_exceptions_count_total") + " * 60", "thrown")], unit="short",
                     desc="Every first-chance exception, handled or not. A step change usually means a new failure loop."), 8, 7)
    L.add(timeseries("Thread pool", [prom(f"sum(process_runtime_dotnet_thread_pool_threads_count{{{SEL}}})", "threads"),
                                     prom(f"sum(process_runtime_dotnet_thread_pool_queue_length{{{SEL}}})", "queued work items")], unit="short",
                     desc="A growing queue with flat threads is thread-pool starvation (sync-over-async)."), 8, 7)
    L.add(timeseries("Lock contention / s", [prom(rate("process_runtime_dotnet_monitor_lock_contention_count_total"), "contentions")], unit="ops"), 8, 7)

    L.row("Database")
    L.add(timeseries("Npgsql pool", [prom(f"sum by (state) (db_client_connections_usage{{{SEL}}})", "{{state}}"),
                                     prom(f"max(db_client_connections_max{{{SEL}}})", "max")], unit="short",
                     overrides=[{"matcher": {"id": "byName", "options": "max"},
                                 "properties": [{"id": "custom.lineStyle", "value": {"fill": "dash", "dash": [10, 10]}},
                                                {"id": "custom.fillOpacity", "value": 0}]}]), 8, 8)
    L.add(timeseries("Commands / s", [prom(rate("db_client_commands_duration_seconds_count"), "Npgsql commands"),
                                      prom(rate("discordbot_db_queries_total"), "EF interceptor")], unit="ops"), 8, 8)
    L.add(timeseries("Command latency", [prom(quantile(0.5, "db_client_commands_duration_seconds_bucket"), "p50"),
                                         prom(quantile(0.95, "db_client_commands_duration_seconds_bucket"), "p95"),
                                         prom(quantile(0.99, "db_client_commands_duration_seconds_bucket"), "p99")], unit="s"), 8, 8)
    L.add(timeseries("Slow queries / min", [prom(rate("discordbot_db_slow_queries_total") + " * 60", "slow")], unit="short", draw="bars",
                     desc="Queries over Database:SlowQueryThresholdMs. The SQL is in the logs (QueryPerformanceInterceptor) and in EF Core spans outside Production."), 8, 7)
    L.add(timeseries("Connection create time p95", [prom(quantile(0.95, "db_client_connections_create_time_seconds_bucket"), "p95")], unit="s"), 8, 7)
    L.add(timeseries("Commands executing", [prom(f"sum(db_client_commands_executing{{{SEL}}})", "executing")], unit="short"), 8, 7)

    L.row("Caches and queues")
    L.add(timeseries("Cache hit ratio by prefix", [prom(f'sum by (prefix) (rate(discordbot_cache_requests_total{{{SEL}, result="hit"}}[$__rate_interval])) / sum by (prefix) (rate(discordbot_cache_requests_total{{{SEL}}}[$__rate_interval]))', "{{prefix}}")],
                     unit="percentunit", max_=1), 8, 8)
    L.add(timeseries("Cache entries by prefix", [prom(f"sum by (prefix) (discordbot_cache_entries{{{SEL}}})", "{{prefix}}")], unit="short"), 8, 8)
    L.add(timeseries("Queue depth", [prom(f"sum by (queue) (discordbot_queue_depth{{{SEL}}})", "{{queue}}"),
                                     prom(f"sum(discordbot_llm_usage_queue_depth{{{SEL}}})", "llm_usage"),
                                     prom(f"sum(discordbot_interaction_state_active{{{SEL}}})", "interaction_states")], unit="short",
                     desc="A queue that only grows means its processor is stuck; check the background services below."), 8, 8)

    L.row("Background services and health checks")
    L.add(table("Background services", [
        prom(f"max by (service, status) (discordbot_background_up{{{SEL}}})", instant=True, fmt="table", ref="A"),
        prom(f"max by (service) (discordbot_background_heartbeat_age_seconds{{{SEL}}})", instant=True, fmt="table", ref="B"),
        prom(f"sum by (service) (increase(discordbot_background_errors_total{{{SEL}}}[$__range]))", instant=True, fmt="table", ref="C"),
    ], transformations=[
        {"id": "merge", "options": {}},
        {"id": "organize", "options": {"excludeByName": {"Time": True},
                                       "renameByName": {"service": "Service", "status": "Status", "Value #A": "Up",
                                                        "Value #B": "Since heartbeat", "Value #C": "Errors (range)"}}},
    ], overrides=[
        {"matcher": {"id": "byName", "options": "Up"},
         "properties": [{"id": "mappings", "value": [{"type": "value", "options": {"0": {"text": "ERROR", "color": RED}, "1": {"text": "OK", "color": GREEN}}}]},
                        {"id": "custom.cellOptions", "value": {"type": "color-background"}}]},
        {"matcher": {"id": "byName", "options": "Since heartbeat"}, "properties": [{"id": "unit", "value": "s"}]},
        {"matcher": {"id": "byName", "options": "Errors (range)"},
         "properties": [{"id": "custom.cellOptions", "value": {"type": "color-text"}},
                        {"id": "thresholds", "value": thresholds((GREEN, None), (RED, 1))}, {"id": "decimals", "value": 0}]},
    ], sort=("Up", False)), 24, 10)
    L.add(timeseries("Background errors by service", [prom(f"sum by (service) (increase(discordbot_background_errors_total{{{SEL}}}[$__interval]))", "{{service}}")],
                     unit="short", draw="bars"), 12, 7)
    L.add(state_timeline("Health checks", [prom(f"min by (check) (discordbot_health_status{{{SEL}}})", "{{check}}")], mappings=HEALTH_MAP), 12, 7)
    return dashboard("discordbot-runtime", "Discord Bot — Runtime & Data", L,
                     ".NET runtime, the database, caches and queues, background services and health checks.",
                     tags=["runtime"])


# ---------------------------------------------------------------------------
# 6. Logs and traces
# ---------------------------------------------------------------------------

def logs_traces():
    L = Layout()
    search_var = {"name": "search", "label": "Search logs", "type": "textbox", "query": "", "current": {"value": ""}}
    level_var = {"name": "level", "label": "Level", "type": "custom", "multi": True, "includeAll": True,
                 "query": "Verbose,Debug,Information,Warning,Error,Fatal,Critical",
                 "allValue": ".*", "current": {"selected": True, "text": ["Warning", "Error", "Fatal", "Critical"],
                                               "value": ["Warning", "Error", "Fatal", "Critical"]},
                 "options": []}
    filt = f'{LOKI_SEL} | severity_text=~"$level" |= "$search"'
    L.row("Logs (Loki)")
    L.add(timeseries("Log volume by level", [loki(f'sum by (severity_text) (count_over_time({LOKI_SEL} |= "$search" [$__auto]))', "{{severity_text}}")],
                     unit="short", stack=True, draw="bars", ds=LOKI), 12, 7)
    L.add(timeseries("Top sources of warnings and errors", [loki(f'topk(10, sum by (scope_name) (count_over_time({LOKI_SEL} | severity_text=~"Warning|Error|Fatal|Critical" [$__auto])))', "{{scope_name}}")],
                     unit="short", ds=LOKI, desc="scope_name is the Serilog SourceContext (the logging class)."), 12, 7)
    L.add(logs("Logs", filt, desc="Filtered by the Level and Search variables. Expand a line and use the TraceID link to open its trace."), 24, 14)

    L.row("Traces (Tempo)")
    L.add(traces_table("Failed traces", '{resource.service.name="discordbot" && status=error}', desc="Traces with at least one span in error."), 12, 10)
    L.add(traces_table("Slow traces (> 2s)", '{resource.service.name="discordbot" && duration > 2s}'), 12, 10)
    L.add(node_graph("Service map", "Built by Tempo's metrics generator from client/server and database spans."), 12, 10)
    L.add(timeseries("Span p95 by operation (top 10)",
                     [prom('topk(10, histogram_quantile(0.95, sum by (le, span_name) (rate(traces_spanmetrics_latency_bucket{service="discordbot", deployment_environment=~"$env"}[$__rate_interval]))))', "{{span_name}}")],
                     unit="s", desc="From sampled spans only (production samples 10% by default; errors and slow spans always)."), 12, 10)
    return dashboard("discordbot-logs-traces", "Discord Bot — Logs & Traces", L,
                     "Search logs, find failed and slow traces, and see the service map.",
                     tags=["logs", "traces"], extra_vars=[level_var, search_var], time_from="now-1h", refresh="1m")


# ---------------------------------------------------------------------------
# 7. Business and SLO
# ---------------------------------------------------------------------------

def business():
    L = Layout()
    L.row("Service levels (computed from raw counters over the selected range)")
    L.add(gauge("Command success", [prom(f'sum(increase(discordbot_command_count_total{{{SEL}, status="success"}}[$__range])) / sum(increase(discordbot_command_count_total{{{SEL}}}[$__range])) * 100', instant=True)],
                desc="Target 99%."), 6, 6)
    L.add(gauge("Gateway availability", [prom(f"avg(avg_over_time(discordbot_gateway_connected{{{SEL}}}[$__range])) * 100", instant=True)],
                thr=thresholds((RED, None), (ORANGE, 99), (GREEN, 99.9)), desc="Target 99.9%."), 6, 6)
    L.add(gauge("HTTP availability", [prom(f'(1 - (sum(increase(http_server_request_duration_seconds_count{{{SEL}, http_response_status_code=~"5.."}}[$__range])) or vector(0)) / sum(increase(http_server_request_duration_seconds_count{{{SEL}}}[$__range]))) * 100', instant=True)],
                thr=thresholds((RED, None), (ORANGE, 99), (GREEN, 99.9)), desc="Share of portal requests that were not 5xx. Target 99.9%."), 6, 6)
    L.add(stat("Command p99 (range)", [prom(f"histogram_quantile(0.99, sum by (le) (increase(discordbot_command_duration_milliseconds_bucket{{{SEL}}}[$__range])))", instant=True)],
               unit="ms", graph=False, thr=thresholds((GREEN, None), (ORANGE, 3000), (RED, 10000)),
               desc="A true p99 from the histogram. The app's SLO gauge 'p99_latency_1h' is actually the maximum."), 6, 6)

    L.row("SLO gauges reported by the app (BusinessMetricsUpdateService, every 5 min)", )
    L.add(timeseries("Success rates", [prom(f"max(discordbot_slo_command_success_rate_24h_percent{{{SEL}}})", "command 24h"),
                                       prom(f"max(discordbot_slo_api_success_rate_24h_percent{{{SEL}}})", "API 24h"),
                                       prom(f"max(discordbot_slo_uptime_percentage_30d_percent{{{SEL}}})", "'uptime' 30d (command success proxy)")],
                     unit="percent", min0=False), 12, 8)
    L.add(timeseries("Error budget remaining (99.9% target)", [prom(f"max(discordbot_slo_error_budget_remaining_percent{{{SEL}}})", "remaining")],
                     unit="percent", thresholds_=thresholds((RED, None), (ORANGE, 25), (GREEN, 50))), 12, 8)

    L.row("Growth and engagement")
    L.add(stat("Guilds", [prom(f"max(discordbot_guilds_active{{{SEL}}})")]), 4, 5)
    L.add(stat("Active guilds today", [prom(f"max(discordbot_business_guilds_active_daily{{{SEL}}})")]), 4, 5)
    L.add(stat("Active users (7d)", [prom(f"max(discordbot_business_users_active_7d{{{SEL}}})")]), 4, 5)
    L.add(stat("Commands today", [prom(f"max(discordbot_business_commands_today{{{SEL}}})")]), 4, 5)
    L.add(stat("Joined today", [prom(f"max(discordbot_business_guilds_joined_today{{{SEL}}})")], thr=thresholds((BLUE, None))), 4, 5)
    L.add(stat("Left today", [prom(f"max(discordbot_business_guilds_left_today{{{SEL}}})")], thr=thresholds((BLUE, None))), 4, 5)
    L.add(timeseries("Guild joins and leaves", [prom(f"sum(increase(discordbot_business_guild_join_total{{{SEL}}}[$__interval]))", "joined"),
                                                prom(f"sum(increase(discordbot_business_guild_leave_total{{{SEL}}}[$__interval]))", "left")],
                     unit="short", draw="bars"), 12, 8)
    L.add(timeseries("Guild members (sum)", [prom(f"max(discordbot_users_unique{{{SEL}}})", "members")], unit="short",
                     desc="Sum of member counts across guilds, so a user in two guilds counts twice."), 12, 8)
    L.add(timeseries("Feature usage", [prom(rate("discordbot_business_feature_usage_total", by="feature", window="1h") + " * 3600", "{{feature}}")],
                     unit="short", desc="Per hour. Only VOX records feature usage today; slash command usage is on the Commands dashboard."), 24, 8)
    return dashboard("discordbot-business", "Discord Bot — Business & SLOs", L,
                     "Service levels computed from raw counters, the app's own SLO gauges, and growth.",
                     tags=["business", "slo"], time_from="now-7d", refresh="5m")


def main():
    os.makedirs(OUT, exist_ok=True)
    for build in (overview, discord, web, assistant, runtime, logs_traces, business):
        d = build()
        path = os.path.join(OUT, d["uid"] + ".json")
        with open(path, "w") as f:
            json.dump(d, f, indent=2)
            f.write("\n")
        print("wrote", os.path.relpath(path))


if __name__ == "__main__":
    main()
