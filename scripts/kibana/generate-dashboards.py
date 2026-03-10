#!/usr/bin/env python3
"""
Kibana Dashboard Generator for DiscordBot Production Monitoring.

Generates NDJSON files containing 11 dashboards with ~90 inline Lens panels.
Run: python3 generate-dashboards.py
Output: objects/dashboards.ndjson, objects/saved-searches.ndjson
"""

import json
import os
import uuid

# ─────────────────────────────────────────────────────────────────────────────
# Constants
# ─────────────────────────────────────────────────────────────────────────────

DV_LOGS = "discordbot-logs-prod"
DV_APM = "apm_static_data_view_id_default"

NAMESPACE = uuid.UUID("a1b2c3d4-e5f6-7890-abcd-ef1234567890")

TIMESTAMP = "2026-01-06T00:00:00.000Z"

OPTIONS_JSON = json.dumps({
    "useMargins": True,
    "syncColors": True,
    "syncCursor": True,
    "syncTooltips": True,
    "hidePanelTitles": False,
})

# Color conventions
COLOR_INFO = "#6DCCB1"
COLOR_WARN = "#E8C44A"
COLOR_ERROR = "#E7664C"
COLOR_FATAL = "#920000"
COLOR_SUCCESS = "#54B399"
COLOR_FAILURE = "#E7664C"
COLOR_SOUNDBOARD = "#54B399"
COLOR_TTS = "#6092C0"
COLOR_VOX = "#D36086"

# Tag definitions: (id, name, description, color)
TAGS = {
    "discordbot":  ("discordbot-tag",       "DiscordBot",           "DiscordBot application dashboards", "#6092C0"),
    "logs":        ("discordbot-tag-logs",   "DiscordBot: Logs",     "Log-based dashboards",              "#D6BF57"),
    "apm":         ("discordbot-tag-apm",    "DiscordBot: APM",      "APM trace dashboards",              "#6DCCB1"),
    "audio":       ("discordbot-tag-audio",  "DiscordBot: Audio",    "Audio & voice dashboards",          "#D36086"),
    "ai":          ("discordbot-tag-ai",     "DiscordBot: AI",       "AI assistant dashboards",           "#9170B8"),
    "moderation":  ("discordbot-tag-mod",    "DiscordBot: Mod",      "Moderation dashboards",             "#E7664C"),
    "scheduling":  ("discordbot-tag-sched",  "DiscordBot: Schedule", "Scheduling dashboards",             "#E8C44A"),
    "portal":      ("discordbot-tag-portal", "DiscordBot: Portal",   "Web portal dashboards",             "#54B399"),
    "infra":       ("discordbot-tag-infra",  "DiscordBot: Infra",    "Infrastructure dashboards",         "#AA6556"),
}

# Map dashboard IDs to their tag keys (all get "discordbot" automatically)
DASHBOARD_TAGS = {
    "discordbot-dashboard-operations-overview":    ["logs", "apm"],
    "discordbot-dashboard-error-analysis":         ["logs", "apm"],
    "discordbot-dashboard-web-portal":             ["apm", "portal"],
    "discordbot-dashboard-background-services":    ["logs", "infra"],
    "discordbot-dashboard-external-dependencies":  ["apm", "infra"],
    "discordbot-dashboard-log-deep-dive":          ["logs"],
    "discordbot-dashboard-audio-voice":            ["logs", "audio"],
    "discordbot-dashboard-ai-assistant":           ["logs", "ai"],
    "discordbot-dashboard-moderation":             ["logs", "moderation"],
    "discordbot-dashboard-scheduling":             ["logs", "scheduling"],
    "discordbot-dashboard-data-retention":         ["logs", "infra"],
}


# ─────────────────────────────────────────────────────────────────────────────
# Builder Helpers
# ─────────────────────────────────────────────────────────────────────────────

def make_id(*parts):
    """Deterministic UUID5 from string parts."""
    return str(uuid.uuid5(NAMESPACE, ":".join(str(p) for p in parts)))


def make_column(op_type, source_field, label, params=None, is_bucketed=False, data_type=None):
    """Create a Lens column dict."""
    col = {
        "label": label,
        "dataType": data_type or _infer_data_type(op_type, source_field),
        "operationType": op_type,
        "sourceField": source_field,
        "isBucketed": is_bucketed,
        "scale": _infer_scale(op_type, is_bucketed),
    }
    if params:
        col["params"] = params
    elif op_type == "date_histogram":
        col["params"] = {"interval": "auto", "includeEmptyRows": True}
    elif op_type == "count":
        col["params"] = {"emptyAsNull": False}
    elif op_type in ("average", "sum", "min", "max", "median"):
        col["params"] = {"emptyAsNull": False}
    elif op_type == "percentile":
        col["params"] = {"percentile": 95, "emptyAsNull": False}
    elif op_type == "terms":
        col["params"] = {
            "size": 10,
            "orderDirection": "desc",
            "otherBucket": True,
            "missingBucket": False,
        }
    elif op_type == "unique_count":
        col["params"] = {"emptyAsNull": False}
    elif op_type == "last_value":
        col["params"] = {"sortField": "@timestamp"}
    return col


def _infer_data_type(op_type, source_field):
    if op_type == "date_histogram":
        return "date"
    if op_type == "terms":
        return "string"
    if op_type in ("count", "sum", "average", "min", "max", "median",
                    "percentile", "unique_count", "cumulative_sum",
                    "counter_rate", "moving_average", "differences"):
        return "number"
    if op_type == "last_value":
        if source_field and "timestamp" in source_field.lower():
            return "date"
        return "string"
    return "number"


def _infer_scale(op_type, is_bucketed):
    if is_bucketed:
        if op_type == "date_histogram":
            return "interval"
        return "ordinal"
    return "ratio"


def make_layer(data_view_id, columns, column_order):
    """Create a layer dict with required fields."""
    return {
        "columns": columns,
        "columnOrder": column_order,
        "incompleteColumns": {},
        "indexPatternId": data_view_id,
        "ignoreGlobalFilters": False,
        "sampling": 1,
    }


def make_datasource_states(layers_dict):
    """Wrap layers in formBased + required siblings."""
    return {
        "formBased": {"layers": layers_dict},
        "indexpattern": {"layers": {}},
        "textBased": {"layers": {}},
    }


def make_xy_viz(layers, preferred_series_type, y_title=None, legend_position="right"):
    """Create lnsXY visualization state."""
    viz = {
        "legend": {"isVisible": True, "position": legend_position},
        "preferredSeriesType": preferred_series_type,
        "layers": layers,
        "axisTitlesVisibilitySettings": {"x": True, "yLeft": True, "yRight": True},
        "yLeftExtent": {"mode": "full"},
    }
    if y_title:
        viz["yTitle"] = y_title
    return viz


def make_metric_viz(layer_id, metric_col, color=None, secondary_metric_col=None):
    """Create lnsMetric visualization state."""
    viz = {
        "layerId": layer_id,
        "layerType": "data",
        "metricAccessor": metric_col,
    }
    if secondary_metric_col:
        viz["secondaryMetricAccessor"] = secondary_metric_col
    if color:
        viz["color"] = color
    return viz


def make_pie_viz(layer_id, metric_col, slice_col, shape="donut"):
    """Create lnsPie visualization state."""
    return {
        "shape": shape,
        "layers": [{
            "layerId": layer_id,
            "layerType": "data",
            "primaryGroups": [slice_col],
            "metrics": [metric_col],
            "numberDisplay": "percent",
            "categoryDisplay": "default",
            "legendDisplay": "default",
        }],
    }


def make_datatable_viz(layer_id, column_ids):
    """Create lnsDatatable visualization state."""
    cols = []
    for cid in column_ids:
        cols.append({
            "columnId": cid,
            "isTransposed": False,
        })
    return {
        "layerId": layer_id,
        "layerType": "data",
        "columns": cols,
    }


def make_panel(panel_id, title, viz_type, datasource_states, visualization,
               references, grid, query=None, filters=None, viz_type_name=None):
    """Create a full inline Lens panel dict."""
    state = {
        "datasourceStates": datasource_states,
        "visualization": visualization,
        "query": query if query else {"query": "", "language": "kuery"},
        "filters": filters if filters is not None else [],
    }

    panel = {
        "type": "lens",
        "gridData": grid,
        "panelIndex": panel_id,
        "embeddableConfig": {
            "title": title,
            "hidePanelTitles": False,
            "enhancements": {},
            "attributes": {
                "title": title,
                "visualizationType": viz_type_name or viz_type,
                "state": state,
                "references": references,
            },
        },
    }
    return panel


def make_search_panel(panel_id, search_id, title, grid):
    """Create a panel that references a saved search."""
    return {
        "type": "search",
        "gridData": grid,
        "panelIndex": panel_id,
        "embeddableConfig": {
            "title": title,
            "hidePanelTitles": False,
            "enhancements": {},
        },
        "panelRefName": f"panel_{panel_id}",
    }


def make_dashboard(dashboard_id, title, description, panels, time_from, time_to,
                   refresh_ms=30000, references=None):
    """Create a dashboard saved object."""
    all_refs = list(references or [])

    # Add dashboard-level index-pattern refs for each inline Lens panel.
    # Kibana expects refs at dashboard level with name "{panelIndex}:{innerRefName}".
    for panel in panels:
        pid = panel.get("panelIndex", "")
        inner_refs = (panel.get("embeddableConfig", {})
                           .get("attributes", {})
                           .get("references", []))
        for ref in inner_refs:
            all_refs.append({
                "type": ref["type"],
                "id": ref["id"],
                "name": f"{pid}:{ref['name']}",
            })

    # Add tag references — every dashboard gets "discordbot" + its category tags
    tag_keys = ["discordbot"] + DASHBOARD_TAGS.get(dashboard_id, [])
    for key in tag_keys:
        tag_id = TAGS[key][0]
        all_refs.append({
            "type": "tag",
            "id": tag_id,
            "name": f"tag-ref-{tag_id}",
        })

    return {
        "attributes": {
            "title": title,
            "description": description,
            "panelsJSON": json.dumps(panels),
            "optionsJSON": OPTIONS_JSON,
            "kibanaSavedObjectMeta": {
                "searchSourceJSON": json.dumps({
                    "query": {"query": "", "language": "kuery"},
                    "filter": [],
                }),
            },
            "timeRestore": True,
            "timeFrom": time_from,
            "timeTo": time_to,
            "refreshInterval": {"pause": False, "value": refresh_ms},
            "version": 1,
        },
        "id": dashboard_id,
        "type": "dashboard",
        "references": all_refs,
        "coreMigrationVersion": "8.8.0",
        "typeMigrationVersion": "8.9.0",
        "managed": False,
        "updated_at": TIMESTAMP,
        "created_at": TIMESTAMP,
    }


# ─────────────────────────────────────────────────────────────────────────────
# Layout Helpers
# ─────────────────────────────────────────────────────────────────────────────

class GridLayout:
    """Auto-positions panels in a 48-unit grid."""

    def __init__(self):
        self.current_y = 0
        self.current_x = 0
        self.row_height = 0

    def place(self, w, h, panel_id):
        """Place a panel and return gridData dict."""
        if self.current_x + w > 48:
            self.current_y += self.row_height
            self.current_x = 0
            self.row_height = 0

        grid = {
            "x": self.current_x,
            "y": self.current_y,
            "w": w,
            "h": h,
            "i": panel_id,
        }
        self.current_x += w
        self.row_height = max(self.row_height, h)
        return grid

    def new_row(self):
        """Force a new row."""
        if self.current_x > 0:
            self.current_y += self.row_height
            self.current_x = 0
            self.row_height = 0

    def metric(self, panel_id):
        return self.place(12, 8, panel_id)

    def chart_half(self, panel_id):
        return self.place(24, 15, panel_id)

    def chart_full(self, panel_id):
        return self.place(48, 15, panel_id)

    def chart_third(self, panel_id):
        return self.place(16, 15, panel_id)

    def table(self, panel_id):
        return self.place(48, 12, panel_id)


# ─────────────────────────────────────────────────────────────────────────────
# Panel Builder Shortcuts
# ─────────────────────────────────────────────────────────────────────────────

def _layer_ref(layer_id, data_view_id):
    return {"type": "index-pattern", "id": data_view_id,
            "name": f"indexpattern-datasource-layer-{layer_id}"}


def _kql(query_str):
    return {"query": query_str, "language": "kuery"}


def _apm_query():
    return _kql("service.environment: production AND service.name: discordbot")


def _log_query(q=""):
    return _kql(q)


def build_count_over_time_panel(panel_id, title, data_view, query, grid,
                                series_type="line", split_field=None, split_size=10):
    """Build a common count-over-time panel with optional split."""
    lid = make_id(panel_id, "layer")
    ts_col = make_id(panel_id, "ts")
    cnt_col = make_id(panel_id, "cnt")

    columns = {
        ts_col: make_column("date_histogram", "@timestamp", "@timestamp", is_bucketed=True),
        cnt_col: make_column("count", "___records___", "Count"),
    }
    col_order = [ts_col, cnt_col]

    split_col = None
    if split_field:
        split_col = make_id(panel_id, "split")
        columns[split_col] = make_column("terms", split_field, split_field,
                                         params={"size": split_size, "orderDirection": "desc",
                                                 "orderBy": {"type": "column", "columnId": cnt_col},
                                                 "otherBucket": True, "missingBucket": False},
                                         is_bucketed=True)
        col_order = [ts_col, split_col, cnt_col]

    layer = make_layer(data_view, columns, col_order)
    ds = make_datasource_states({lid: layer})

    viz_layers = [{
        "layerId": lid, "layerType": "data", "seriesType": series_type,
        "xAccessor": ts_col, "accessors": [cnt_col],
    }]
    if split_col:
        viz_layers[0]["splitAccessor"] = split_col

    viz = make_xy_viz(viz_layers, series_type)
    refs = [_layer_ref(lid, data_view)]

    return make_panel(panel_id, title, "lnsXY", ds, viz, refs, grid,
                      query=query if query else None)


def build_metric_panel(panel_id, title, data_view, query, grid,
                       op_type="count", source_field="___records___",
                       color=None, label=None):
    """Build a metric panel."""
    lid = make_id(panel_id, "layer")
    met_col = make_id(panel_id, "metric")

    col = make_column(op_type, source_field, label or title)
    columns = {met_col: col}

    layer = make_layer(data_view, columns, [met_col])
    ds = make_datasource_states({lid: layer})
    viz = make_metric_viz(lid, met_col, color=color)
    refs = [_layer_ref(lid, data_view)]

    return make_panel(panel_id, title, "lnsMetric", ds, viz, refs, grid,
                      query=query if query else None)


def build_terms_bar_panel(panel_id, title, data_view, query, grid,
                          terms_field, metric_op="count", metric_field="___records___",
                          series_type="bar_horizontal", size=10, metric_label="Count"):
    """Build a bar chart with terms on one axis."""
    lid = make_id(panel_id, "layer")
    terms_col = make_id(panel_id, "terms")
    met_col = make_id(panel_id, "metric")

    met = make_column(metric_op, metric_field, metric_label)
    terms = make_column("terms", terms_field, terms_field,
                        params={"size": size, "orderDirection": "desc",
                                "orderBy": {"type": "column", "columnId": met_col},
                                "otherBucket": True, "missingBucket": False},
                        is_bucketed=True)

    columns = {terms_col: terms, met_col: met}
    layer = make_layer(data_view, columns, [terms_col, met_col])
    ds = make_datasource_states({lid: layer})

    viz = make_xy_viz([{
        "layerId": lid, "layerType": "data", "seriesType": series_type,
        "xAccessor": terms_col, "accessors": [met_col],
    }], series_type)
    refs = [_layer_ref(lid, data_view)]

    return make_panel(panel_id, title, "lnsXY", ds, viz, refs, grid,
                      query=query if query else None)


def build_donut_panel(panel_id, title, data_view, query, grid,
                      terms_field, size=10, shape="donut"):
    """Build a donut/pie panel."""
    lid = make_id(panel_id, "layer")
    terms_col = make_id(panel_id, "terms")
    cnt_col = make_id(panel_id, "cnt")

    cnt = make_column("count", "___records___", "Count")
    terms = make_column("terms", terms_field, terms_field,
                        params={"size": size, "orderDirection": "desc",
                                "orderBy": {"type": "column", "columnId": cnt_col},
                                "otherBucket": True, "missingBucket": False},
                        is_bucketed=True)

    columns = {terms_col: terms, cnt_col: cnt}
    layer = make_layer(data_view, columns, [terms_col, cnt_col])
    ds = make_datasource_states({lid: layer})
    viz = make_pie_viz(lid, cnt_col, terms_col, shape=shape)
    refs = [_layer_ref(lid, data_view)]

    return make_panel(panel_id, title, "lnsPie", ds, viz, refs, grid,
                      query=query if query else None)


def build_table_panel(panel_id, title, data_view, query, grid,
                      field_columns, sort_field="@timestamp"):
    """Build a data table panel.

    field_columns: list of (field_name, label, op_type) tuples.
    """
    lid = make_id(panel_id, "layer")
    columns = {}
    col_order = []

    for i, (field, label, op) in enumerate(field_columns):
        cid = make_id(panel_id, f"col{i}")
        if op == "terms":
            col = make_column("terms", field, label,
                              params={"size": 20, "orderDirection": "desc",
                                      "orderBy": {"type": "column",
                                                   "columnId": make_id(panel_id, "tbl_cnt")},
                                      "otherBucket": False, "missingBucket": True},
                              is_bucketed=True)
        elif op == "last_value":
            col = make_column("last_value", field, label,
                              params={"sortField": sort_field},
                              data_type=_infer_data_type("last_value", field))
        elif op == "count":
            col = make_column("count", "___records___", label)
        else:
            col = make_column(op, field, label)
        columns[cid] = col
        col_order.append(cid)

    # Ensure a count column exists for ordering terms
    cnt_id = make_id(panel_id, "tbl_cnt")
    if cnt_id not in columns:
        columns[cnt_id] = make_column("count", "___records___", "Count")
        col_order.append(cnt_id)

    layer = make_layer(data_view, columns, col_order)
    ds = make_datasource_states({lid: layer})
    viz = make_datatable_viz(lid, col_order)
    refs = [_layer_ref(lid, data_view)]

    return make_panel(panel_id, title, "lnsDatatable", ds, viz, refs, grid,
                      query=query if query else None)


def build_percentile_line_panel(panel_id, title, data_view, query, grid,
                                metric_field, percentiles=None, y_title=None):
    """Build a line chart with percentile metrics over time."""
    if percentiles is None:
        percentiles = [50, 95]

    lid = make_id(panel_id, "layer")
    ts_col = make_id(panel_id, "ts")

    columns = {
        ts_col: make_column("date_histogram", "@timestamp", "@timestamp", is_bucketed=True),
    }
    col_order = [ts_col]
    accessors = []

    for p in percentiles:
        pcol = make_id(panel_id, f"p{p}")
        columns[pcol] = make_column("percentile", metric_field, f"p{p} {metric_field}",
                                    params={"percentile": p, "emptyAsNull": False})
        col_order.append(pcol)
        accessors.append(pcol)

    layer = make_layer(data_view, columns, col_order)
    ds = make_datasource_states({lid: layer})

    viz = make_xy_viz([{
        "layerId": lid, "layerType": "data", "seriesType": "line",
        "xAccessor": ts_col, "accessors": accessors,
    }], "line")
    if y_title:
        viz["yTitle"] = y_title
    refs = [_layer_ref(lid, data_view)]

    return make_panel(panel_id, title, "lnsXY", ds, viz, refs, grid,
                      query=query if query else None)


def build_avg_line_panel(panel_id, title, data_view, query, grid,
                         metric_field, label=None):
    """Build a line chart with avg metric over time."""
    lid = make_id(panel_id, "layer")
    ts_col = make_id(panel_id, "ts")
    avg_col = make_id(panel_id, "avg")

    columns = {
        ts_col: make_column("date_histogram", "@timestamp", "@timestamp", is_bucketed=True),
        avg_col: make_column("average", metric_field, label or f"Avg {metric_field}"),
    }
    layer = make_layer(data_view, columns, [ts_col, avg_col])
    ds = make_datasource_states({lid: layer})

    viz = make_xy_viz([{
        "layerId": lid, "layerType": "data", "seriesType": "line",
        "xAccessor": ts_col, "accessors": [avg_col],
    }], "line")
    refs = [_layer_ref(lid, data_view)]

    return make_panel(panel_id, title, "lnsXY", ds, viz, refs, grid,
                      query=query if query else None)


def build_treemap_panel(panel_id, title, data_view, query, grid,
                        terms_field, size=10):
    """Build a treemap panel."""
    lid = make_id(panel_id, "layer")
    terms_col = make_id(panel_id, "terms")
    cnt_col = make_id(panel_id, "cnt")

    cnt = make_column("count", "___records___", "Count")
    terms = make_column("terms", terms_field, terms_field,
                        params={"size": size, "orderDirection": "desc",
                                "orderBy": {"type": "column", "columnId": cnt_col},
                                "otherBucket": True, "missingBucket": False},
                        is_bucketed=True)

    columns = {terms_col: terms, cnt_col: cnt}
    layer = make_layer(data_view, columns, [terms_col, cnt_col])
    ds = make_datasource_states({lid: layer})
    viz = make_pie_viz(lid, cnt_col, terms_col, shape="treemap")
    refs = [_layer_ref(lid, data_view)]

    return make_panel(panel_id, title, "lnsPie", ds, viz, refs, grid,
                      query=query if query else None)


def build_multi_logger_count_panel(panel_id, title, data_view, loggers, grid,
                                   series_type="area_stacked", extra_query=""):
    """Build a count-over-time panel filtered to specific loggers, split by logger."""
    logger_filter = " OR ".join(f'log.logger: "{l}"' for l in loggers)
    q = f"({logger_filter})"
    if extra_query:
        q = f"{q} AND {extra_query}"
    return build_count_over_time_panel(
        panel_id, title, data_view, _log_query(q), grid,
        series_type=series_type, split_field="log.logger", split_size=len(loggers) + 2)


# ─────────────────────────────────────────────────────────────────────────────
# Dashboard 1: Operations Overview
# ─────────────────────────────────────────────────────────────────────────────

def dashboard_operations_overview():
    panels = []
    g = GridLayout()

    # Row 1: Three metrics (16+16+16 = 48)
    pid = "ops-restarts"
    panels.append(build_metric_panel(
        pid, "Service Restarts", DV_LOGS,
        _log_query('log.logger: "Microsoft.Hosting.Lifetime" AND message: "Application started"'),
        g.place(16, 8, pid), color=COLOR_WARN))

    pid = "ops-version"
    lid = make_id(pid, "layer")
    ver_col = make_id(pid, "ver")
    cnt_col = make_id(pid, "cnt")
    columns = {
        ver_col: make_column("terms", "service.version", "Version",
                             params={"size": 1, "orderDirection": "desc",
                                     "orderBy": {"type": "column", "columnId": cnt_col},
                                     "otherBucket": False, "missingBucket": False},
                             is_bucketed=True),
        cnt_col: make_column("count", "___records___", "Count"),
    }
    layer = make_layer(DV_LOGS, columns, [ver_col, cnt_col])
    ds = make_datasource_states({lid: layer})
    viz = make_metric_viz(lid, cnt_col)
    viz["breakdownByAccessor"] = ver_col
    refs = [_layer_ref(lid, DV_LOGS)]
    panels.append(make_panel(pid, "Current Version", "lnsMetric", ds, viz, refs, g.place(16, 8, pid)))

    pid = "ops-error-count"
    panels.append(build_metric_panel(
        pid, "Error Count", DV_LOGS,
        _log_query('log.level: "Error"'),
        g.place(16, 8, pid), color=COLOR_ERROR))

    # Row 2: Log Volume + Request Throughput (24+24 = 48)
    pid = "ops-log-volume"
    panels.append(build_count_over_time_panel(
        pid, "Log Volume by Level", DV_LOGS, None, g.chart_half(pid),
        series_type="area_stacked", split_field="log.level", split_size=5))

    pid = "ops-req-throughput"
    panels.append(build_count_over_time_panel(
        pid, "Request Throughput", DV_APM,
        _kql("service.environment: production AND service.name: discordbot AND processor.event: transaction AND transaction.type: request"),
        g.chart_half(pid), series_type="line"))

    # Row 3: Request Latency + Error Rate by Outcome (24+24 = 48)
    pid = "ops-req-latency"
    panels.append(build_percentile_line_panel(
        pid, "Request Latency (p50/p95)", DV_APM,
        _kql("service.environment: production AND service.name: discordbot AND processor.event: transaction AND transaction.type: request"),
        g.chart_half(pid),
        "transaction.duration.us", percentiles=[50, 95], y_title="Duration (μs)"))

    pid = "ops-error-rate"
    panels.append(build_donut_panel(
        pid, "Error Rate by Outcome", DV_APM,
        _kql("service.environment: production AND service.name: discordbot AND processor.event: transaction"),
        g.chart_half(pid),
        "event.outcome", size=5))

    # Row 4: Dependency Health (full width)
    pid = "ops-dep-health"
    panels.append(build_terms_bar_panel(
        pid, "Dependency Health", DV_APM,
        _kql("service.environment: production AND service.name: discordbot AND processor.event: span"),
        g.chart_full(pid),
        "service.target.name", metric_op="average", metric_field="span.duration.us",
        metric_label="Avg Duration (μs)"))

    # Row 5: Recent Errors table (full width)
    pid = "ops-recent-errors"
    panels.append(build_table_panel(
        pid, "Recent Errors", DV_LOGS,
        _log_query('log.level: "Error"'),
        g.table(pid),
        [("@timestamp", "Timestamp", "date_histogram"),
         ("log.logger", "Logger", "terms"),
         ("metadata.ExceptionDetail.Message", "Message", "terms"),
         ("trace.id", "Trace ID", "terms")]))

    return make_dashboard(
        "discordbot-dashboard-operations-overview",
        "[DiscordBot] Operations Overview",
        "At-a-glance daily monitoring — log volume, errors, request throughput, and dependency health",
        panels, "now-24h", "now", 30000)


# ─────────────────────────────────────────────────────────────────────────────
# Dashboard 2: Error & Exception Analysis
# ─────────────────────────────────────────────────────────────────────────────

def dashboard_error_analysis():
    panels = []
    g = GridLayout()

    # 1. Errors Over Time (stacked area by logger)
    pid = "err-over-time"
    panels.append(build_count_over_time_panel(
        pid, "Errors Over Time", DV_LOGS,
        _log_query('log.level: "Error"'),
        g.chart_half(pid),
        series_type="area_stacked", split_field="log.logger", split_size=10))

    # 2. Error Sources (treemap)
    pid = "err-sources"
    panels.append(build_treemap_panel(
        pid, "Error Sources", DV_LOGS,
        _log_query('log.level: "Error"'),
        g.chart_half(pid),
        "log.logger", size=15))

    # 3. Exception Types (horizontal bar)
    pid = "err-exception-types"
    panels.append(build_terms_bar_panel(
        pid, "Exception Types", DV_LOGS,
        _log_query('log.level: "Error"'),
        g.chart_half(pid),
        "metadata.ExceptionDetail.Type", size=10))

    # 4. Error Messages (table)
    pid = "err-messages"
    panels.append(build_table_panel(
        pid, "Error Messages", DV_LOGS,
        _log_query('log.level: "Error"'),
        g.chart_half(pid),
        [("metadata.ExceptionDetail.Message", "Message", "terms"),
         ("log.logger", "Logger", "terms")]))

    # 5. Warning Trend (line)
    pid = "err-warn-trend"
    panels.append(build_count_over_time_panel(
        pid, "Warning Trend", DV_LOGS,
        _log_query('log.level: "Warning"'),
        g.chart_half(pid), series_type="line"))

    # 6. Warning Sources (pie)
    pid = "err-warn-sources"
    panels.append(build_donut_panel(
        pid, "Warning Sources", DV_LOGS,
        _log_query('log.level: "Warning"'),
        g.chart_half(pid),
        "log.logger", size=10, shape="pie"))

    # 7. APM Error Events (table) — uses log data view since APM traces lack error.exception fields
    pid = "err-apm-events"
    panels.append(build_table_panel(
        pid, "APM Error Events", DV_LOGS,
        _log_query('log.level: "Error" AND metadata.ExceptionDetail.Type: *'),
        g.table(pid),
        [("metadata.ExceptionDetail.Type", "Exception Type", "terms"),
         ("metadata.ExceptionDetail.Message", "Message", "terms"),
         ("trace.id", "Trace ID", "terms")]))

    return make_dashboard(
        "discordbot-dashboard-error-analysis",
        "[DiscordBot] Error & Exception Analysis",
        "Deep dive into error spikes — exception types, error sources, and APM error events",
        panels, "now-7d", "now", 60000)


# ─────────────────────────────────────────────────────────────────────────────
# Dashboard 3: Web Portal Performance
# ─────────────────────────────────────────────────────────────────────────────

def dashboard_web_portal():
    panels = []
    g = GridLayout()
    apm_q = "service.environment: production AND service.name: discordbot"

    # 1. Top Endpoints by Throughput (horizontal bar)
    pid = "web-top-endpoints"
    panels.append(build_terms_bar_panel(
        pid, "Top Endpoints by Throughput", DV_APM,
        _kql(f"{apm_q} AND transaction.type: request"),
        g.chart_half(pid),
        "transaction.name", size=15))

    # 2. Slowest Endpoints p95 (horizontal bar)
    pid = "web-slowest"
    panels.append(build_terms_bar_panel(
        pid, "Slowest Endpoints (p95)", DV_APM,
        _kql(f"{apm_q} AND transaction.type: request"),
        g.chart_half(pid),
        "transaction.name", metric_op="percentile", metric_field="transaction.duration.us",
        size=15, metric_label="p95 Duration (μs)"))

    # 3. Login Activity (line)
    pid = "web-login"
    panels.append(build_count_over_time_panel(
        pid, "Login Activity", DV_APM,
        _kql(f'{apm_q} AND (transaction.name: "GET /Account/Login" OR transaction.name: "POST /Account/Login")'),
        g.chart_half(pid), series_type="line"))

    # 4. TTS Endpoint Latency (line)
    pid = "web-tts-latency"
    panels.append(build_percentile_line_panel(
        pid, "TTS Endpoint Latency", DV_APM,
        _kql(f'{apm_q} AND (transaction.name: *PortalTts*)'),
        g.chart_half(pid),
        "transaction.duration.us", percentiles=[50, 95]))

    # 5. Soundboard Endpoint Latency (line)
    pid = "web-sb-latency"
    panels.append(build_percentile_line_panel(
        pid, "Soundboard Endpoint Latency", DV_APM,
        _kql(f'{apm_q} AND (transaction.name: *PlaySound* OR transaction.name: *Soundboard*)'),
        g.chart_half(pid),
        "transaction.duration.us", percentiles=[50, 95]))

    # 6. SignalR Hub Activity (line)
    pid = "web-signalr"
    panels.append(build_count_over_time_panel(
        pid, "SignalR Hub Activity", DV_APM,
        _kql(f'{apm_q} AND (transaction.name: */hubs/dashboard*)'),
        g.chart_half(pid), series_type="line"))

    # 7. SQLite Query Latency (line)
    pid = "web-sqlite"
    panels.append(build_percentile_line_panel(
        pid, "SQLite Query Latency", DV_APM,
        _kql(f"{apm_q} AND span.type: db AND span.subtype: sqlite"),
        g.chart_half(pid),
        "span.duration.us", percentiles=[50, 95]))

    # 8. HTTP Error Responses (line)
    pid = "web-http-errors"
    panels.append(build_count_over_time_panel(
        pid, "HTTP Error Responses", DV_APM,
        _kql(f"{apm_q} AND http.response.status_code >= 400"),
        g.chart_half(pid), series_type="line"))

    # 9. Suspicious Traffic (table)
    pid = "web-suspicious"
    panels.append(build_table_panel(
        pid, "Suspicious Traffic", DV_APM,
        _kql(f'{apm_q} AND (transaction.name: *wp-login* OR transaction.name: *.env* OR transaction.name: *wp-admin* OR transaction.name: *xmlrpc*)'),
        g.table(pid),
        [("transaction.name", "Endpoint", "terms"),
         ("@timestamp", "Time", "date_histogram")]))

    return make_dashboard(
        "discordbot-dashboard-web-portal",
        "[DiscordBot] Web Portal Performance",
        "ASP.NET Core web portal monitoring — endpoint throughput, latency, and error tracking",
        panels, "now-24h", "now", 30000)


# ─────────────────────────────────────────────────────────────────────────────
# Dashboard 4: Bot Background Services
# ─────────────────────────────────────────────────────────────────────────────

def dashboard_background_services():
    panels = []
    g = GridLayout()

    # 1. Background Service Log Volume (stacked area)
    pid = "bg-service-volume"
    panels.append(build_multi_logger_count_panel(
        pid, "Background Service Log Volume", DV_LOGS,
        ["DiscordBot.Bot.Services.BotHostedService",
         "DiscordBot.Bot.Services.BackgroundServiceHealthRegistry",
         "DiscordBot.Bot.Services.ChannelActivityAggregationService",
         "DiscordBot.Bot.Services.MemberActivityAggregationService",
         "DiscordBot.Bot.Services.AlertMonitoringService"],
        g.chart_full(pid)))

    # 2. Command Performance Metrics (line)
    pid = "bg-cmd-perf"
    panels.append(build_avg_line_panel(
        pid, "Command Success Rate", DV_LOGS,
        _log_query('log.logger: "DiscordBot.Infrastructure.Repositories.CommandLogRepository"'),
        g.chart_half(pid),
        "metadata.SuccessRate", label="Avg Success Rate"))

    # 3. Command Success/Failure (metric — two metrics side by side)
    pid = "bg-cmd-success"
    panels.append(build_metric_panel(
        pid, "Command Success Count", DV_LOGS,
        _log_query('log.logger: "DiscordBot.Infrastructure.Repositories.CommandLogRepository"'),
        g.place(12, 15, pid), op_type="sum", source_field="metadata.SuccessCount",
        color=COLOR_SUCCESS, label="Success Count"))

    pid = "bg-cmd-failure"
    panels.append(build_metric_panel(
        pid, "Command Failure Count", DV_LOGS,
        _log_query('log.logger: "DiscordBot.Infrastructure.Repositories.CommandLogRepository"'),
        g.place(12, 15, pid), op_type="sum", source_field="metadata.FailureCount",
        color=COLOR_FAILURE, label="Failure Count"))

    # 4. Member Sync Activity (line)
    pid = "bg-member-sync"
    panels.append(build_count_over_time_panel(
        pid, "Member Sync Activity", DV_LOGS,
        _log_query('log.logger: "DiscordBot.Bot.Services.MemberSyncService"'),
        g.chart_half(pid), series_type="line"))

    # 5. Audit Log Processing (line)
    pid = "bg-audit-log"
    panels.append(build_multi_logger_count_panel(
        pid, "Audit Log Processing", DV_LOGS,
        ["DiscordBot.Bot.Services.AuditLogQueueProcessor",
         "DiscordBot.Infrastructure.Repositories.AuditLogRepository"],
        g.chart_half(pid), series_type="line"))

    # 6. Bot Lifecycle Events (table) — APM
    pid = "bg-lifecycle"
    panels.append(build_table_panel(
        pid, "Bot Lifecycle Events", DV_APM,
        _kql("service.environment: production AND service.name: discordbot AND transaction.name: bot.lifecycle.*"),
        g.table(pid),
        [("transaction.name", "Event", "terms"),
         ("@timestamp", "Time", "date_histogram"),
         ("transaction.duration.us", "Duration (μs)", "average")]))

    # 7. Connection State (line)
    pid = "bg-connection"
    panels.append(build_count_over_time_panel(
        pid, "Connection State", DV_LOGS,
        _log_query('log.logger: "DiscordBot.Bot.Services.ConnectionStateService"'),
        g.chart_half(pid), series_type="line", split_field="log.level", split_size=5))

    # 8. Discord API Calls (line) — APM spans
    pid = "bg-discord-api"
    panels.append(build_count_over_time_panel(
        pid, "Discord API Calls", DV_APM,
        _kql('service.environment: production AND service.name: discordbot AND processor.event: span AND (service.target.name: "discord.com:443" OR service.target.name: gateway*.discord.gg*)'),
        g.chart_half(pid), series_type="line"))

    return make_dashboard(
        "discordbot-dashboard-background-services",
        "[DiscordBot] Bot Background Services",
        "Discord bot worker health — background service volume, command metrics, sync activity",
        panels, "now-24h", "now", 30000)


# ─────────────────────────────────────────────────────────────────────────────
# Dashboard 5: External Dependencies
# ─────────────────────────────────────────────────────────────────────────────

def dashboard_external_dependencies():
    panels = []
    g = GridLayout()
    apm_q = "service.environment: production AND service.name: discordbot"

    # 1. Dependency Map (donut)
    pid = "dep-map"
    panels.append(build_donut_panel(
        pid, "Dependency Map", DV_APM,
        _kql(f"{apm_q} AND processor.event: span"),
        g.chart_half(pid),
        "service.target.name", size=15))

    # 2. SQLite Performance (line — count + avg)
    pid = "dep-sqlite"
    panels.append(build_percentile_line_panel(
        pid, "SQLite Performance", DV_APM,
        _kql(f"{apm_q} AND span.subtype: sqlite"),
        g.chart_half(pid),
        "span.duration.us", percentiles=[50, 95], y_title="Duration (μs)"))

    # 3. ES Self-Writes (line)
    pid = "dep-es-writes"
    panels.append(build_count_over_time_panel(
        pid, "Elasticsearch Self-Writes", DV_APM,
        _kql(f'{apm_q} AND service.target.name: "localhost:9200"'),
        g.chart_half(pid), series_type="line"))

    # 4. Discord API Latency (line)
    pid = "dep-discord-api"
    panels.append(build_percentile_line_panel(
        pid, "Discord API Latency", DV_APM,
        _kql(f'{apm_q} AND service.target.name: "discord.com:443"'),
        g.chart_half(pid),
        "span.duration.us", percentiles=[50, 95]))

    # 5. Discord Gateway (line)
    pid = "dep-gateway"
    panels.append(build_count_over_time_panel(
        pid, "Discord Gateway", DV_APM,
        _kql(f"{apm_q} AND service.target.name: gateway*.discord.gg*"),
        g.chart_half(pid), series_type="line"))

    # 6. Discord Voice (table)
    pid = "dep-voice"
    panels.append(build_table_panel(
        pid, "Discord Voice Connections", DV_APM,
        _kql(f"{apm_q} AND service.target.name: *.discord.media*"),
        g.chart_half(pid),
        [("service.target.name", "Voice Server", "terms"),
         ("@timestamp", "Time", "date_histogram")]))

    # 7. Failed External Calls (table)
    pid = "dep-failed"
    panels.append(build_table_panel(
        pid, "Failed External Calls", DV_APM,
        _kql(f"{apm_q} AND event.outcome: failure AND span.type: external"),
        g.table(pid),
        [("service.target.name", "Target", "terms"),
         ("span.subtype", "Type", "terms"),
         ("@timestamp", "Time", "date_histogram")]))

    # 8. ES Bulk Write Errors (line)
    pid = "dep-es-errors"
    panels.append(build_count_over_time_panel(
        pid, "ES Bulk Write Errors", DV_APM,
        _kql(f"{apm_q} AND processor.event: error AND transaction.name: *_bulk*"),
        g.chart_full(pid), series_type="line"))

    return make_dashboard(
        "discordbot-dashboard-external-dependencies",
        "[DiscordBot] External Dependencies",
        "Health of all outbound connections — SQLite, Elasticsearch, Discord API/Gateway/Voice",
        panels, "now-24h", "now", 30000)


# ─────────────────────────────────────────────────────────────────────────────
# Dashboard 6: Log Deep Dive
# ─────────────────────────────────────────────────────────────────────────────

def dashboard_log_deep_dive():
    """Dashboard 6 embeds a saved search panel."""
    g = GridLayout()
    pid = "log-search"
    search_panel = make_search_panel(
        pid, "search-log-deep-dive",
        "Log Deep Dive — Warnings, Errors & Fatal",
        g.place(48, 30, pid))

    refs = [{
        "id": "search-log-deep-dive",
        "name": f"panel_{pid}",
        "type": "search",
    }]

    return make_dashboard(
        "discordbot-dashboard-log-deep-dive",
        "[DiscordBot] Log Deep Dive",
        "Ad-hoc log investigation with preset columns — Warnings, Errors, and Fatal events",
        [search_panel], "now-24h", "now", 30000, references=refs)


# ─────────────────────────────────────────────────────────────────────────────
# Dashboard 7: Audio & Voice Overview
# ─────────────────────────────────────────────────────────────────────────────

def dashboard_audio_voice():
    panels = []
    g = GridLayout()
    apm_q = "service.environment: production AND service.name: discordbot"

    # 1. Audio Plays Over Time (stacked area)
    pid = "audio-plays"
    panels.append(build_multi_logger_count_panel(
        pid, "Audio Plays Over Time", DV_LOGS,
        ["DiscordBot.Bot.Services.PlaybackService",
         "DiscordBot.Bot.Services.TtsPlaybackService",
         "DiscordBot.Bot.Services.VoxService"],
        g.chart_full(pid),
        extra_query='message: "Starting playback" OR log.logger: "DiscordBot.Bot.Services.TtsPlaybackService" OR log.logger: "DiscordBot.Bot.Services.VoxService"'))

    # 2. Soundboard Plays by Sound (horizontal bar)
    pid = "audio-by-sound"
    panels.append(build_terms_bar_panel(
        pid, "Soundboard Plays by Sound", DV_LOGS,
        _log_query('log.logger: "DiscordBot.Bot.Services.PlaybackService" AND message: "Starting playback"'),
        g.chart_half(pid),
        "metadata.SoundId", size=15))

    # 3. Playback Success vs Error (donut)
    pid = "audio-success"
    panels.append(build_donut_panel(
        pid, "Playback Success vs Error", DV_LOGS,
        _log_query('log.logger: "DiscordBot.Bot.Services.PlaybackService"'),
        g.chart_half(pid),
        "log.level", size=5))

    # 4. FFmpeg Failures (table)
    pid = "audio-ffmpeg"
    panels.append(build_table_panel(
        pid, "FFmpeg Failures", DV_LOGS,
        _log_query('log.logger: "DiscordBot.Bot.Services.PlaybackService" AND log.level: "Error"'),
        g.table(pid),
        [("@timestamp", "Time", "date_histogram"),
         ("metadata.ExceptionDetail.Message", "Message", "terms"),
         ("metadata.SoundId", "Sound ID", "terms"),
         ("metadata.GuildId", "Guild ID", "terms")]))

    # 5. TTS Synthesis Volume (line)
    pid = "audio-tts-vol"
    panels.append(build_count_over_time_panel(
        pid, "TTS Synthesis Volume", DV_LOGS,
        _log_query('log.logger: "DiscordBot.Bot.Services.AzureTtsService" AND message: "Speech synthesis completed"'),
        g.chart_half(pid), series_type="line"))

    # 6. TTS Audio Size (line avg)
    pid = "audio-tts-size"
    panels.append(build_avg_line_panel(
        pid, "TTS Audio Size", DV_LOGS,
        _log_query('log.logger: "DiscordBot.Bot.Services.AzureTtsService"'),
        g.chart_half(pid),
        "metadata.SizeBytes", label="Avg Size (bytes)"))

    # 7. TTS Voice Usage (pie)
    pid = "audio-tts-voice"
    panels.append(build_donut_panel(
        pid, "TTS Voice Usage", DV_LOGS,
        _log_query('log.logger: "DiscordBot.Bot.Services.AzureTtsService" AND message: "Built SSML"'),
        g.chart_half(pid),
        "labels.Voice", size=10, shape="pie"))

    # 8. VOX Concatenation Performance (line p50/p95)
    pid = "audio-vox-perf"
    panels.append(build_percentile_line_panel(
        pid, "VOX Concatenation Performance", DV_LOGS,
        _log_query('log.logger: "DiscordBot.Bot.Services.VoxConcatenationService"'),
        g.chart_half(pid),
        "metadata.ConcatenationMs", percentiles=[50, 95]))

    # 9. VOX Clip Count Distribution (bar)
    pid = "audio-vox-clips"
    panels.append(build_terms_bar_panel(
        pid, "VOX Clip Count Distribution", DV_LOGS,
        _log_query('log.logger: "DiscordBot.Bot.Services.VoxConcatenationService"'),
        g.chart_half(pid),
        "metadata.ClipCount", series_type="bar", size=15, metric_label="Count"))

    # 10. VOX Audio Output Size (metric avg)
    pid = "audio-vox-size"
    panels.append(build_metric_panel(
        pid, "VOX Audio Output Size", DV_LOGS,
        _log_query('log.logger: "DiscordBot.Bot.Services.VoxConcatenationService"'),
        g.chart_half(pid),
        op_type="average", source_field="metadata.AudioBytes", label="Avg Bytes"))

    # 11. Soundboard Orchestration Activity (line)
    pid = "audio-orch"
    panels.append(build_count_over_time_panel(
        pid, "Soundboard Orchestration Activity", DV_LOGS,
        _log_query('log.logger: "DiscordBot.Bot.Services.SoundboardOrchestrationService"'),
        g.chart_half(pid), series_type="line"))

    # 12. Portal Controller Activity (stacked area)
    pid = "audio-portal"
    panels.append(build_multi_logger_count_panel(
        pid, "Portal Controller Activity", DV_LOGS,
        ["DiscordBot.Bot.Controllers.PortalSoundboardController",
         "DiscordBot.Bot.Controllers.PortalTtsController",
         "DiscordBot.Bot.Controllers.PortalVoxController"],
        g.chart_half(pid)))

    # 13. Voice Channel Auto-Leave (metric)
    pid = "audio-autoleave"
    panels.append(build_metric_panel(
        pid, "Voice Channel Auto-Leave", DV_LOGS,
        _log_query('log.logger: "DiscordBot.Bot.Services.VoiceAutoLeaveService"'),
        g.chart_half(pid)))

    # 14. Audio Cache Health (line)
    pid = "audio-cache"
    panels.append(build_multi_logger_count_panel(
        pid, "Audio Cache Health", DV_LOGS,
        ["DiscordBot.Bot.Services.SoundCacheService",
         "DiscordBot.Bot.Services.AudioCacheCleanupService"],
        g.chart_half(pid), series_type="line"))

    # 15. Audio Errors by Source (treemap)
    pid = "audio-errors"
    audio_loggers = [
        "DiscordBot.Bot.Services.PlaybackService",
        "DiscordBot.Bot.Services.SoundFileService",
        "DiscordBot.Bot.Services.AudioService",
        "DiscordBot.Bot.Services.AzureTtsService",
        "DiscordBot.Bot.Services.VoxConcatenationService",
        "DiscordBot.Bot.Services.SoundboardOrchestrationService",
    ]
    logger_q = " OR ".join(f'log.logger: "{l}"' for l in audio_loggers)
    panels.append(build_treemap_panel(
        pid, "Audio Errors by Source", DV_LOGS,
        _log_query(f'log.level: "Error" AND ({logger_q})'),
        g.chart_half(pid),
        "log.logger", size=10))

    # 16. TTS Endpoint Latency (line) — APM
    pid = "audio-tts-ep"
    panels.append(build_percentile_line_panel(
        pid, "TTS Endpoint Latency", DV_APM,
        _kql(f"{apm_q} AND transaction.name: *PortalTts*"),
        g.chart_half(pid),
        "transaction.duration.us", percentiles=[50, 95]))

    # 17. Soundboard Endpoint Latency (line) — APM
    pid = "audio-sb-ep"
    panels.append(build_percentile_line_panel(
        pid, "Soundboard Endpoint Latency", DV_APM,
        _kql(f"{apm_q} AND (transaction.name: *PlaySound* OR transaction.name: *Soundboard*)"),
        g.chart_full(pid),
        "transaction.duration.us", percentiles=[50, 95]))

    # 18. Discord Voice Server Connections (table) — APM
    pid = "audio-voice-conn"
    panels.append(build_table_panel(
        pid, "Discord Voice Server Connections", DV_APM,
        _kql(f"{apm_q} AND service.target.name: *.discord.media*"),
        g.table(pid),
        [("service.target.name", "Voice Server", "terms"),
         ("@timestamp", "Time", "date_histogram"),
         ("span.duration.us", "Duration (μs)", "average")]))

    return make_dashboard(
        "discordbot-dashboard-audio-voice",
        "[DiscordBot] Audio & Voice Overview",
        "Unified Soundboard, TTS, and VOX monitoring — playback, synthesis, cache, and endpoints",
        panels, "now-7d", "now", 60000)


# ─────────────────────────────────────────────────────────────────────────────
# Dashboard 8: AI Assistant Overview
# ─────────────────────────────────────────────────────────────────────────────

def dashboard_ai_assistant():
    panels = []
    g = GridLayout()

    dm_logger = "DiscordBot.Bot.Services.DmAssistantService"
    guild_logger = "DiscordBot.Bot.Services.AssistantService"
    llm_logger = "DiscordBot.Bot.Services.AnthropicLlmClient"
    agent_logger = "DiscordBot.Bot.Services.AgentRunner"

    # 1. Conversations Over Time (line)
    pid = "ai-convos"
    panels.append(build_multi_logger_count_panel(
        pid, "Conversations Over Time", DV_LOGS,
        [dm_logger, guild_logger],
        g.chart_half(pid), series_type="line"))

    # 2. Total Cost (metric sum)
    pid = "ai-total-cost"
    panels.append(build_metric_panel(
        pid, "Total Cost (Period)", DV_LOGS,
        _log_query(f'log.logger: "{dm_logger}" OR log.logger: "{guild_logger}"'),
        g.chart_half(pid),
        op_type="sum", source_field="metadata.Cost", label="Total Cost ($)"))

    # 3. Cost Over Time (stacked area)
    pid = "ai-cost-time"
    panels.append(build_multi_logger_count_panel(
        pid, "Cost Over Time", DV_LOGS,
        [dm_logger, guild_logger],
        g.chart_full(pid), series_type="area_stacked"))
    # Override to use sum of Cost instead of count
    _replace_with_sum_metric(panels[-1], pid, "metadata.Cost", "Cost ($)",
                             [dm_logger, guild_logger])

    # 4. Token Usage Breakdown (stacked bar)
    pid = "ai-tokens"
    lid = make_id(pid, "layer")
    ts_col = make_id(pid, "ts")
    input_col = make_id(pid, "input")
    output_col = make_id(pid, "output")
    cached_col = make_id(pid, "cached")

    columns = {
        ts_col: make_column("date_histogram", "@timestamp", "@timestamp", is_bucketed=True),
        input_col: make_column("sum", "metadata.InputTokens", "Input Tokens"),
        output_col: make_column("sum", "metadata.OutputTokens", "Output Tokens"),
        cached_col: make_column("sum", "metadata.CachedTokens", "Cached Tokens"),
    }
    layer = make_layer(DV_LOGS, columns, [ts_col, input_col, output_col, cached_col])
    ds = make_datasource_states({lid: layer})
    viz = make_xy_viz([{
        "layerId": lid, "layerType": "data", "seriesType": "bar_stacked",
        "xAccessor": ts_col, "accessors": [input_col, output_col, cached_col],
    }], "bar_stacked")
    refs = [_layer_ref(lid, DV_LOGS)]
    panels.append(make_panel(pid, "Token Usage Breakdown", "lnsXY", ds, viz, refs,
                             g.chart_half(pid),
                             query=_log_query(f'log.logger: "{llm_logger}"')))

    # 5. Cache vs Input Tokens (stacked area — instead of formula)
    pid = "ai-cache-eff"
    lid = make_id(pid, "layer")
    ts_col = make_id(pid, "ts")
    cached_col = make_id(pid, "cached")
    input_col = make_id(pid, "input")

    columns = {
        ts_col: make_column("date_histogram", "@timestamp", "@timestamp", is_bucketed=True),
        cached_col: make_column("sum", "metadata.CachedTokens", "Cached Tokens"),
        input_col: make_column("sum", "metadata.InputTokens", "Input Tokens"),
    }
    layer = make_layer(DV_LOGS, columns, [ts_col, cached_col, input_col])
    ds = make_datasource_states({lid: layer})
    viz = make_xy_viz([{
        "layerId": lid, "layerType": "data", "seriesType": "area_stacked",
        "xAccessor": ts_col, "accessors": [cached_col, input_col],
    }], "area_stacked")
    refs = [_layer_ref(lid, DV_LOGS)]
    panels.append(make_panel(pid, "Cache vs Input Tokens", "lnsXY", ds, viz, refs,
                             g.chart_half(pid),
                             query=_log_query(f'log.logger: "{llm_logger}"')))

    # 6. Response Latency p50/p95 (line)
    pid = "ai-latency"
    panels.append(build_percentile_line_panel(
        pid, "Response Latency (p50/p95)", DV_LOGS,
        _log_query(f'log.logger: "{dm_logger}" OR log.logger: "{guild_logger}"'),
        g.chart_half(pid),
        "metadata.LatencyMs", percentiles=[50, 95], y_title="Latency (ms)"))

    # 7. Agentic Loops per Request (bar)
    pid = "ai-loops"
    panels.append(build_terms_bar_panel(
        pid, "Agentic Loops per Request", DV_LOGS,
        _log_query(f'log.logger: "{agent_logger}"'),
        g.chart_half(pid),
        "metadata.Iterations", series_type="bar", size=10, metric_label="Requests"))

    # 8. Tool Calls per Request (bar)
    pid = "ai-tool-calls"
    panels.append(build_terms_bar_panel(
        pid, "Tool Calls per Request", DV_LOGS,
        _log_query(f'log.logger: "{agent_logger}"'),
        g.chart_half(pid),
        "metadata.ToolCalls", series_type="bar", size=10, metric_label="Requests"))

    # 9. Tool Providers (table)
    pid = "ai-tool-providers"
    panels.append(build_table_panel(
        pid, "Tool Providers", DV_LOGS,
        _log_query('log.logger: "DiscordBot.Infrastructure.Services.LLM.ToolRegistry"'),
        g.chart_half(pid),
        [("labels.ProviderName", "Provider", "terms"),
         ("metadata.ToolCount", "Tool Count", "terms")]))

    # 10. DM vs Guild Split (donut)
    pid = "ai-dm-guild"
    panels.append(build_donut_panel(
        pid, "DM vs Guild Split", DV_LOGS,
        _log_query(f'log.logger: "{dm_logger}" OR log.logger: "{guild_logger}"'),
        g.chart_half(pid),
        "log.logger", size=5))

    # 11. Active Users (metric cardinality)
    pid = "ai-active-users"
    panels.append(build_metric_panel(
        pid, "Active Users", DV_LOGS,
        _log_query('log.logger: "DiscordBot.Bot.Services.DmAssistantMessageHandler"'),
        g.chart_half(pid),
        op_type="unique_count", source_field="metadata.UserId", label="Unique Users"))

    # 12. Per-User Usage (horizontal bar)
    pid = "ai-per-user"
    panels.append(build_terms_bar_panel(
        pid, "Per-User Usage", DV_LOGS,
        _log_query(f'log.logger: "{dm_logger}"'),
        g.chart_full(pid),
        "metadata.UserId", size=15))

    # 13. Cost per Conversation avg (line)
    pid = "ai-cost-avg"
    panels.append(build_avg_line_panel(
        pid, "Cost per Conversation (Avg)", DV_LOGS,
        _log_query(f'log.logger: "{dm_logger}" OR log.logger: "{guild_logger}"'),
        g.chart_full(pid),
        "metadata.Cost", label="Avg Cost ($)"))

    # 14. Errors (table)
    pid = "ai-errors"
    ai_loggers = [dm_logger, guild_logger, llm_logger, agent_logger,
                  "DiscordBot.Bot.Services.DmAssistantMessageHandler",
                  "DiscordBot.Bot.Services.AssistantMessageHandler"]
    logger_q = " OR ".join(f'log.logger: "{l}"' for l in ai_loggers)
    panels.append(build_table_panel(
        pid, "AI Assistant Errors", DV_LOGS,
        _log_query(f'(log.level: "Error" OR log.level: "Warning") AND ({logger_q})'),
        g.table(pid),
        [("@timestamp", "Time", "date_histogram"),
         ("log.logger", "Logger", "terms"),
         ("metadata.ExceptionDetail.Message", "Message", "terms"),
         ("log.level", "Level", "terms")]))

    # 15. Guild Settings Activity (metric)
    pid = "ai-guild-settings"
    panels.append(build_metric_panel(
        pid, "Guild Settings Activity", DV_LOGS,
        _log_query('log.logger: "DiscordBot.Bot.Services.AssistantGuildSettingsService" OR log.logger: "DiscordBot.Bot.Pages.AssistantSettingsModel"'),
        g.chart_full(pid), label="Settings Events"))

    return make_dashboard(
        "discordbot-dashboard-ai-assistant",
        "[DiscordBot] AI Assistant Overview",
        "Cost tracking, usage analytics, and agent performance for the Anthropic-powered assistant",
        panels, "now-7d", "now", 60000)


def _replace_with_sum_metric(panel, pid, field, label, loggers):
    """Replace the count metric in a multi-logger panel with a sum metric."""
    lid = make_id(pid, "layer")
    ts_col = make_id(pid, "ts")
    sum_col = make_id(pid, "sum")
    split_col = make_id(pid, "split")

    logger_filter = " OR ".join(f'log.logger: "{l}"' for l in loggers)

    columns = {
        ts_col: make_column("date_histogram", "@timestamp", "@timestamp", is_bucketed=True),
        sum_col: make_column("sum", field, label),
        split_col: make_column("terms", "log.logger", "Logger",
                               params={"size": len(loggers) + 2, "orderDirection": "desc",
                                       "orderBy": {"type": "column", "columnId": sum_col},
                                       "otherBucket": True, "missingBucket": False},
                               is_bucketed=True),
    }
    layer = make_layer(DV_LOGS, columns, [ts_col, split_col, sum_col])
    ds = make_datasource_states({lid: layer})
    viz = make_xy_viz([{
        "layerId": lid, "layerType": "data", "seriesType": "area_stacked",
        "xAccessor": ts_col, "accessors": [sum_col], "splitAccessor": split_col,
    }], "area_stacked")
    refs = [_layer_ref(lid, DV_LOGS)]

    # Replace the panel's embeddableConfig
    panel["embeddableConfig"]["attributes"]["state"]["datasourceStates"] = ds
    panel["embeddableConfig"]["attributes"]["state"]["visualization"] = viz
    panel["embeddableConfig"]["attributes"]["state"]["query"] = _log_query(f"({logger_filter})")
    panel["embeddableConfig"]["attributes"]["references"] = refs


# ─────────────────────────────────────────────────────────────────────────────
# Dashboard 9: Moderation & Safety
# ─────────────────────────────────────────────────────────────────────────────

def dashboard_moderation():
    panels = []
    g = GridLayout()

    # 1. Raid Detection Initializations (line)
    pid = "mod-raid"
    panels.append(build_count_over_time_panel(
        pid, "Raid Detection Initializations", DV_LOGS,
        _log_query('log.logger: "DiscordBot.Bot.Services.RaidDetectionService"'),
        g.chart_half(pid), series_type="line"))

    # 2. Rat Watch Events (line)
    pid = "mod-ratwatch"
    panels.append(build_avg_line_panel(
        pid, "Rat Watch Events", DV_LOGS,
        _log_query('log.logger: "RatWatch.RatWatchService"'),
        g.chart_half(pid),
        "metadata.Count", label="Event Count"))

    # 3. Moderation Config Changes (table)
    pid = "mod-config"
    panels.append(build_table_panel(
        pid, "Moderation Config Changes", DV_LOGS,
        _log_query('log.logger: "DiscordBot.Bot.Services.GuildModerationConfigService"'),
        g.table(pid),
        [("@timestamp", "Time", "date_histogram"),
         ("event.action", "Action", "terms"),
         ("metadata.GuildId", "Guild ID", "terms")]))

    # 4. Moderation Page Activity (line)
    pid = "mod-pages"
    panels.append(build_multi_logger_count_panel(
        pid, "Moderation Page Activity", DV_LOGS,
        ["DiscordBot.Bot.Pages.Guilds.Members.ModerationModel",
         "DiscordBot.Bot.Pages.ModerationSettings.IndexModel"],
        g.chart_full(pid), series_type="line"))

    return make_dashboard(
        "discordbot-dashboard-moderation",
        "[DiscordBot] Moderation & Safety",
        "Raid detection, moderation configuration, and safety feature monitoring",
        panels, "now-7d", "now", 60000)


# ─────────────────────────────────────────────────────────────────────────────
# Dashboard 10: Scheduling & Notifications
# ─────────────────────────────────────────────────────────────────────────────

def dashboard_scheduling():
    panels = []
    g = GridLayout()

    # 1. Notification Volume (line)
    pid = "sched-notif-vol"
    panels.append(build_multi_logger_count_panel(
        pid, "Notification Volume", DV_LOGS,
        ["DiscordBot.Bot.Services.NotificationService",
         "DiscordBot.Infrastructure.Repositories.NotificationRepository"],
        g.chart_half(pid), series_type="line"))

    # 2. Reminder Service Health (line, colored by level)
    pid = "sched-reminder"
    panels.append(build_count_over_time_panel(
        pid, "Reminder Service Health", DV_LOGS,
        _log_query('log.logger: "DiscordBot.Bot.Services.ReminderExecutionService"'),
        g.chart_half(pid), series_type="line", split_field="log.level", split_size=5))

    # 3. Scheduled Messages (line)
    pid = "sched-msgs"
    panels.append(build_multi_logger_count_panel(
        pid, "Scheduled Messages", DV_LOGS,
        ["DiscordBot.Bot.Services.ScheduledMessageService",
         "DiscordBot.Bot.Services.ScheduledMessageExecutionService"],
        g.chart_half(pid), series_type="line"))

    # 4. Scheduled Message Count (metric)
    pid = "sched-msg-count"
    panels.append(build_metric_panel(
        pid, "Scheduled Message Count", DV_LOGS,
        _log_query('log.logger: "DiscordBot.Bot.Services.ScheduledMessageService"'),
        g.place(12, 15, pid),
        op_type="max", source_field="metadata.Total", label="Total Scheduled"))

    # 5. Notification Retention (metric)
    pid = "sched-retention"
    panels.append(build_metric_panel(
        pid, "Notification Retention", DV_LOGS,
        _log_query('log.logger: "DiscordBot.Bot.Services.NotificationRetentionService"'),
        g.place(12, 15, pid), label="Retention Events"))

    # 6. Errors (table)
    pid = "sched-errors"
    sched_loggers = [
        "DiscordBot.Bot.Services.NotificationService",
        "DiscordBot.Infrastructure.Repositories.NotificationRepository",
        "DiscordBot.Bot.Services.ReminderExecutionService",
        "DiscordBot.Bot.Services.NotificationRetentionService",
        "DiscordBot.Bot.Services.ScheduledMessageService",
        "DiscordBot.Bot.Services.ScheduledMessageExecutionService",
    ]
    logger_q = " OR ".join(f'log.logger: "{l}"' for l in sched_loggers)
    panels.append(build_table_panel(
        pid, "Scheduling Errors", DV_LOGS,
        _log_query(f'log.level: "Error" AND ({logger_q})'),
        g.table(pid),
        [("@timestamp", "Time", "date_histogram"),
         ("log.logger", "Logger", "terms"),
         ("metadata.ExceptionDetail.Message", "Message", "terms")]))

    return make_dashboard(
        "discordbot-dashboard-scheduling",
        "[DiscordBot] Scheduling & Notifications",
        "Scheduled messages, reminders, and notification delivery reliability",
        panels, "now-7d", "now", 60000)


# ─────────────────────────────────────────────────────────────────────────────
# Dashboard 11: Data & Retention Health
# ─────────────────────────────────────────────────────────────────────────────

def dashboard_data_retention():
    panels = []
    g = GridLayout()

    # 1. Retention Services (stacked area)
    pid = "data-retention"
    panels.append(build_multi_logger_count_panel(
        pid, "Retention Services", DV_LOGS,
        ["DiscordBot.Bot.Services.AnalyticsRetentionService",
         "DiscordBot.Bot.Services.AuditLogRetentionService",
         "DiscordBot.Bot.Services.MessageLogCleanupService",
         "DiscordBot.Bot.Services.SoundPlayLogRetentionService",
         "DiscordBot.Bot.Services.NotificationRetentionService"],
        g.chart_full(pid)))

    # 2. Audit Log Pipeline (line dual)
    pid = "data-audit"
    panels.append(build_multi_logger_count_panel(
        pid, "Audit Log Pipeline", DV_LOGS,
        ["DiscordBot.Bot.Services.AuditLogQueueProcessor",
         "DiscordBot.Infrastructure.Repositories.AuditLogRepository"],
        g.chart_half(pid), series_type="line"))

    # 3. Member Sync Pipeline (line)
    pid = "data-member-sync"
    panels.append(build_multi_logger_count_panel(
        pid, "Member Sync Pipeline", DV_LOGS,
        ["DiscordBot.Bot.Services.MemberSyncService",
         "DiscordBot.Infrastructure.Repositories.GuildMemberRepository"],
        g.chart_half(pid), series_type="line"))

    # 4. Metrics Collection (line)
    pid = "data-metrics"
    panels.append(build_multi_logger_count_panel(
        pid, "Metrics Collection", DV_LOGS,
        ["DiscordBot.Bot.Services.MetricsCollectionService",
         "DiscordBot.Infrastructure.Repositories.MetricSnapshotRepository",
         "DiscordBot.Bot.Services.GuildMetricsAggregationService"],
        g.chart_half(pid), series_type="line"))

    # 5. Activity Tracking (line)
    pid = "data-activity"
    panels.append(build_multi_logger_count_panel(
        pid, "Activity Tracking", DV_LOGS,
        ["DiscordBot.Infrastructure.Repositories.ChannelActivityRepository",
         "DiscordBot.Infrastructure.Repositories.MemberActivityRepository"],
        g.chart_half(pid), series_type="line"))

    # 6. Cleanup Services (stacked bar)
    pid = "data-cleanup"
    panels.append(build_multi_logger_count_panel(
        pid, "Cleanup Services", DV_LOGS,
        ["DiscordBot.Bot.Services.VerificationCleanupService",
         "DiscordBot.Bot.Services.InteractionStateCleanupService",
         "DiscordBot.Bot.Services.BusinessMetricsUpdateService"],
        g.chart_full(pid), series_type="bar_stacked"))

    # 7. Errors in Data Pipeline (table)
    pid = "data-errors"
    data_loggers = [
        "DiscordBot.Bot.Services.AuditLogQueueProcessor",
        "DiscordBot.Infrastructure.Repositories.AuditLogRepository",
        "DiscordBot.Bot.Services.MemberSyncService",
        "DiscordBot.Infrastructure.Repositories.GuildMemberRepository",
        "DiscordBot.Bot.Services.AnalyticsRetentionService",
        "DiscordBot.Bot.Services.AuditLogRetentionService",
        "DiscordBot.Bot.Services.MessageLogCleanupService",
        "DiscordBot.Bot.Services.SoundPlayLogRetentionService",
        "DiscordBot.Bot.Services.MetricsCollectionService",
        "DiscordBot.Bot.Services.NotificationRetentionService",
    ]
    logger_q = " OR ".join(f'log.logger: "{l}"' for l in data_loggers)
    panels.append(build_table_panel(
        pid, "Errors in Data Pipeline", DV_LOGS,
        _log_query(f'log.level: "Error" AND ({logger_q})'),
        g.table(pid),
        [("@timestamp", "Time", "date_histogram"),
         ("log.logger", "Logger", "terms"),
         ("metadata.ExceptionDetail.Message", "Message", "terms")]))

    return make_dashboard(
        "discordbot-dashboard-data-retention",
        "[DiscordBot] Data & Retention Health",
        "Background housekeeping and data pipeline health — retention, sync, metrics, cleanup",
        panels, "now-7d", "now", 60000)


# ─────────────────────────────────────────────────────────────────────────────
# Saved Searches
# ─────────────────────────────────────────────────────────────────────────────

def generate_saved_searches():
    """Generate updated saved searches with correct ECS field names."""
    searches = []

    # Log Deep Dive saved search (Dashboard 6)
    searches.append({
        "attributes": {
            "title": "[DiscordBot] Log Deep Dive",
            "description": "Ad-hoc log investigation — Warnings, Errors, and Fatal events",
            "columns": ["@timestamp", "log.level", "log.logger", "message",
                        "trace.id", "process.thread.name", "service.version"],
            "sort": [["@timestamp", "desc"]],
            "grid": {},
            "hideChart": False,
            "isTextBasedQuery": False,
            "usesAdHocDataView": False,
            "kibanaSavedObjectMeta": {
                "searchSourceJSON": json.dumps({
                    "query": {"query": 'log.level: "Warning" OR log.level: "Error" OR log.level: "Fatal"',
                              "language": "kuery"},
                    "filter": [],
                    "indexRefName": "kibanaSavedObjectMeta.searchSourceJSON.index",
                }),
            },
        },
        "id": "search-log-deep-dive",
        "type": "search",
        "references": [{
            "id": DV_LOGS,
            "name": "kibanaSavedObjectMeta.searchSourceJSON.index",
            "type": "index-pattern",
        }],
        "coreMigrationVersion": "8.8.0",
        "typeMigrationVersion": "8.0.0",
        "managed": False,
        "updated_at": TIMESTAMP,
        "created_at": TIMESTAMP,
    })

    # Production Errors (updated ECS fields)
    searches.append({
        "attributes": {
            "title": "[DiscordBot] Production Errors",
            "description": "All error-level log events for quick troubleshooting",
            "columns": ["@timestamp", "log.level", "log.logger", "message",
                        "trace.id", "service.version"],
            "sort": [["@timestamp", "desc"]],
            "grid": {},
            "hideChart": False,
            "isTextBasedQuery": False,
            "usesAdHocDataView": False,
            "kibanaSavedObjectMeta": {
                "searchSourceJSON": json.dumps({
                    "query": {"query": 'log.level: "Error"', "language": "kuery"},
                    "filter": [],
                    "indexRefName": "kibanaSavedObjectMeta.searchSourceJSON.index",
                }),
            },
        },
        "id": "search-production-errors",
        "type": "search",
        "references": [{
            "id": DV_LOGS,
            "name": "kibanaSavedObjectMeta.searchSourceJSON.index",
            "type": "index-pattern",
        }],
        "coreMigrationVersion": "8.8.0",
        "typeMigrationVersion": "8.0.0",
        "managed": False,
        "updated_at": TIMESTAMP,
        "created_at": TIMESTAMP,
    })

    # Slow Queries (updated ECS fields)
    searches.append({
        "attributes": {
            "title": "[DiscordBot] Slow Queries",
            "description": "Queries and operations taking longer than expected",
            "columns": ["@timestamp", "log.logger", "message",
                        "metadata.ExecutionTimeMs", "trace.id"],
            "sort": [["@timestamp", "desc"]],
            "grid": {},
            "hideChart": False,
            "isTextBasedQuery": False,
            "usesAdHocDataView": False,
            "kibanaSavedObjectMeta": {
                "searchSourceJSON": json.dumps({
                    "query": {"query": "metadata.ExecutionTimeMs > 500", "language": "kuery"},
                    "filter": [],
                    "indexRefName": "kibanaSavedObjectMeta.searchSourceJSON.index",
                }),
            },
        },
        "id": "search-slow-queries",
        "type": "search",
        "references": [{
            "id": DV_LOGS,
            "name": "kibanaSavedObjectMeta.searchSourceJSON.index",
            "type": "index-pattern",
        }],
        "coreMigrationVersion": "8.8.0",
        "typeMigrationVersion": "8.0.0",
        "managed": False,
        "updated_at": TIMESTAMP,
        "created_at": TIMESTAMP,
    })

    # Discord Command Executions (updated ECS fields)
    searches.append({
        "attributes": {
            "title": "[DiscordBot] Discord Command Executions",
            "description": "Discord slash command executions with correlation tracking",
            "columns": ["@timestamp", "log.logger", "message",
                        "metadata.GuildId", "metadata.UserId", "trace.id"],
            "sort": [["@timestamp", "desc"]],
            "grid": {},
            "hideChart": False,
            "isTextBasedQuery": False,
            "usesAdHocDataView": False,
            "kibanaSavedObjectMeta": {
                "searchSourceJSON": json.dumps({
                    "query": {"query": 'log.logger: *InteractionHandler* AND trace.id: *',
                              "language": "kuery"},
                    "filter": [],
                    "indexRefName": "kibanaSavedObjectMeta.searchSourceJSON.index",
                }),
            },
        },
        "id": "search-discord-commands",
        "type": "search",
        "references": [{
            "id": DV_LOGS,
            "name": "kibanaSavedObjectMeta.searchSourceJSON.index",
            "type": "index-pattern",
        }],
        "coreMigrationVersion": "8.8.0",
        "typeMigrationVersion": "8.0.0",
        "managed": False,
        "updated_at": TIMESTAMP,
        "created_at": TIMESTAMP,
    })

    # Guild Activity (updated ECS fields)
    searches.append({
        "attributes": {
            "title": "[DiscordBot] Guild Activity (Template)",
            "description": "Template search — filter by GuildId to see all activity for a specific guild",
            "columns": ["@timestamp", "log.level", "log.logger", "message",
                        "metadata.UserId", "metadata.GuildId"],
            "sort": [["@timestamp", "desc"]],
            "grid": {},
            "hideChart": False,
            "isTextBasedQuery": False,
            "usesAdHocDataView": False,
            "kibanaSavedObjectMeta": {
                "searchSourceJSON": json.dumps({
                    "query": {"query": "metadata.GuildId: *", "language": "kuery"},
                    "filter": [],
                    "indexRefName": "kibanaSavedObjectMeta.searchSourceJSON.index",
                }),
            },
        },
        "id": "search-guild-activity",
        "type": "search",
        "references": [{
            "id": DV_LOGS,
            "name": "kibanaSavedObjectMeta.searchSourceJSON.index",
            "type": "index-pattern",
        }],
        "coreMigrationVersion": "8.8.0",
        "typeMigrationVersion": "8.0.0",
        "managed": False,
        "updated_at": TIMESTAMP,
        "created_at": TIMESTAMP,
    })

    # User Activity (updated ECS fields)
    searches.append({
        "attributes": {
            "title": "[DiscordBot] User Activity (Template)",
            "description": "Template search — filter by UserId to see all activity for a specific user",
            "columns": ["@timestamp", "log.level", "log.logger", "message",
                        "metadata.GuildId", "metadata.UserId"],
            "sort": [["@timestamp", "desc"]],
            "grid": {},
            "hideChart": False,
            "isTextBasedQuery": False,
            "usesAdHocDataView": False,
            "kibanaSavedObjectMeta": {
                "searchSourceJSON": json.dumps({
                    "query": {"query": "metadata.UserId: *", "language": "kuery"},
                    "filter": [],
                    "indexRefName": "kibanaSavedObjectMeta.searchSourceJSON.index",
                }),
            },
        },
        "id": "search-user-activity",
        "type": "search",
        "references": [{
            "id": DV_LOGS,
            "name": "kibanaSavedObjectMeta.searchSourceJSON.index",
            "type": "index-pattern",
        }],
        "coreMigrationVersion": "8.8.0",
        "typeMigrationVersion": "8.0.0",
        "managed": False,
        "updated_at": TIMESTAMP,
        "created_at": TIMESTAMP,
    })

    return searches


# ─────────────────────────────────────────────────────────────────────────────
# Main
# ─────────────────────────────────────────────────────────────────────────────

def main():
    # Generate all dashboards
    dashboard_funcs = [
        dashboard_operations_overview,
        dashboard_error_analysis,
        dashboard_web_portal,
        dashboard_background_services,
        dashboard_external_dependencies,
        dashboard_log_deep_dive,
        dashboard_audio_voice,
        dashboard_ai_assistant,
        dashboard_moderation,
        dashboard_scheduling,
        dashboard_data_retention,
    ]

    dashboards = []
    total_panels = 0
    for func in dashboard_funcs:
        d = func()
        panels = json.loads(d["attributes"]["panelsJSON"])
        total_panels += len(panels)
        dashboards.append(d)

    # Validate all JSON
    for d in dashboards:
        line = json.dumps(d)
        json.loads(line)  # Validate round-trip

    # Generate tag objects
    tags = []
    for key, (tag_id, name, description, color) in TAGS.items():
        tags.append({
            "attributes": {
                "name": name,
                "description": description,
                "color": color,
            },
            "id": tag_id,
            "type": "tag",
            "references": [],
            "coreMigrationVersion": "8.8.0",
            "typeMigrationVersion": "8.0.0",
            "managed": False,
            "updated_at": TIMESTAMP,
            "created_at": TIMESTAMP,
        })

    # Write output files
    script_dir = os.path.dirname(os.path.abspath(__file__))
    objects_dir = os.path.join(script_dir, "objects")
    os.makedirs(objects_dir, exist_ok=True)

    # Tags NDJSON
    tags_path = os.path.join(objects_dir, "tags.ndjson")
    with open(tags_path, "w") as f:
        for t in tags:
            f.write(json.dumps(t, separators=(",", ":")) + "\n")

    # Dashboards NDJSON
    dashboards_path = os.path.join(objects_dir, "dashboards.ndjson")
    with open(dashboards_path, "w") as f:
        for d in dashboards:
            f.write(json.dumps(d, separators=(",", ":")) + "\n")

    # Generate and write saved searches
    searches = generate_saved_searches()
    for s in searches:
        line = json.dumps(s)
        json.loads(line)  # Validate round-trip

    searches_path = os.path.join(objects_dir, "saved-searches.ndjson")
    with open(searches_path, "w") as f:
        for s in searches:
            f.write(json.dumps(s, separators=(",", ":")) + "\n")

    print(f"Generated {len(tags)} tags")
    print(f"  -> {tags_path}")
    print(f"Generated {len(dashboards)} dashboards with {total_panels} total panels")
    print(f"  -> {dashboards_path}")
    print(f"Generated {len(searches)} saved searches")
    print(f"  -> {searches_path}")

    # Print dashboard summary
    for d in dashboards:
        panels = json.loads(d["attributes"]["panelsJSON"])
        print(f"  {d['attributes']['title']}: {len(panels)} panels")


if __name__ == "__main__":
    main()
