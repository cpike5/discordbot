/**
 * Performance Dashboard - Chart Utilities
 * Shared Chart.js configuration and helper functions.
 *
 * Public API (additive: existing members keep their signatures)
 *   defaultOptions, colors, formatLabel, getGranularity, mergeOptions, destroyChart(s)
 *   createLineChart / createBarChart / createGaugeChart / createChart(ctx, config)
 *   setGaugeColors / updateGauge(chart, value, max, thresholds)   live gauge updates
 *   showChartError(chartId, message, onRetry)      plain-language error with an optional Retry
 *   showChartEmpty(canvasOrId, { title, description })   empty state in place of the canvas
 *   clearChartState(canvasOrId)                    removes either state and shows the canvas again
 *   describeChart(canvas, { caption, labels, datasets, unit })   text alternative: summary + data table
 *   describeGauge(canvas, { label, value, unit, max })
 *   applyThemeColors(dataset, colors)              fills a dataset's colours from its `themeColors`
 *                                                  spec: { borderColor: c => c.secondary, ... }
 *   summarize(labels, datasets, unit), tableModel(labels, datasets, maxRows)   pure helpers
 *
 * Datasets that carry a `themeColors` spec are recoloured when the theme changes
 * (ChartTheme.onChange), so series colours follow Graphite / Purple Dusk without a reload.
 */
(function() {
    'use strict';

    const root = typeof window !== 'undefined' ? window : globalThis;
    root.Performance = root.Performance || {};
    const MAX_TABLE_ROWS = 60;
    let tableCounter = 0;

    const ChartUtils = {
        // Layout defaults. Colours come from the design tokens through chart-theme.js
        // (Chart.defaults, and a repaint on theme change), so none are set here.
        get defaultOptions() {
            return {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        labels: {
                            boxWidth: 12,
                            padding: 20
                        }
                    },
                    tooltip: {
                        borderWidth: 1,
                        padding: 12
                    }
                },
                scales: {
                    y: {
                        grid: {}
                    },
                    x: {
                        grid: { display: false }
                    }
                }
            };
        },

        // Series and status colours for the active theme
        get colors() {
            const c = window.ChartTheme ? window.ChartTheme.colors() : null;
            return {
                primary: c ? c.secondary : '#3d9ad6',
                secondary: c ? c.primary : '#e6602b',
                success: c ? c.success : '#2fbf7f',
                warning: c ? c.warning : '#f0a323',
                error: c ? c.error : '#ef4f4f',
                info: c ? c.info : '#2fb3cc',
                muted: c ? c.track : 'rgba(47, 51, 54, 0.8)'
            };
        },

        /**
         * Format timestamp for chart labels based on time range
         */
        formatLabel: function(isoString, hours) {
            const date = new Date(isoString);
            if (hours <= 24) {
                const month = date.toLocaleDateString('en-US', { month: 'short' });
                const day = date.getDate();
                const hour = date.toLocaleTimeString('en-US', { hour: 'numeric', hour12: true });
                return `${month} ${day}, ${hour}`;
            } else {
                return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
            }
        },

        /**
         * Get granularity for API calls based on time range
         */
        getGranularity: function(hours) {
            return hours <= 24 ? 'hour' : 'day';
        },

        /**
         * Create a line chart with standard configuration
         */
        createLineChart: function(ctx, labels, datasets, options) {
            if (window.ChartTheme) window.ChartTheme.ensureRegistered();
            const mergedOptions = this.mergeOptions(this.defaultOptions, options || {});
            return this.createChart(ctx, {
                type: 'line',
                data: { labels, datasets },
                options: mergedOptions
            });
        },

        /**
         * Create a bar chart with standard configuration
         */
        createBarChart: function(ctx, labels, datasets, options) {
            if (window.ChartTheme) window.ChartTheme.ensureRegistered();
            const mergedOptions = this.mergeOptions(this.defaultOptions, options || {});
            return this.createChart(ctx, {
                type: 'bar',
                data: { labels, datasets },
                options: mergedOptions
            });
        },

        /**
         * Create a semi-circular gauge chart
         */
        createGaugeChart: function(ctx, value, maxValue, thresholds, colorScheme) {
            if (!ctx) return null;
            const percentage = Math.min((value / maxValue) * 100, 100);
            const remaining = 100 - percentage;
            const dataset = {
                data: [percentage, remaining],
                borderWidth: 0,
                circumference: 180,
                rotation: 270
            };
            this.setGaugeColors(dataset, value, thresholds, colorScheme);

            if (root.ChartTheme) root.ChartTheme.ensureRegistered();
            return new Chart(ctx, {
                type: 'doughnut',
                data: {
                    datasets: [dataset]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    cutout: '75%',
                    plugins: {
                        legend: { display: false },
                        tooltip: { enabled: false }
                    }
                }
            });
        },

        /**
         * Colours a gauge dataset from the theme: the arc by threshold, the track neutral. The
         * dataset keeps a `themeColors` spec, so it is recoloured when the theme changes.
         * `colorScheme` (optional) replaces the success/warning/error ramp.
         */
        setGaugeColors: function(dataset, value, thresholds, colorScheme) {
            dataset.themeColors = {
                backgroundColor: c => [
                    this.getThresholdColor(value, thresholds, colorScheme || [c.success, c.warning, c.error]),
                    c.track
                ]
            };
            this.applyThemeColors(dataset);
            return dataset;
        },

        /**
         * Moves a gauge to a new value (live updates) without losing its theme spec.
         */
        updateGauge: function(chart, value, maxValue, thresholds, colorScheme) {
            if (!chart) return;
            const percentage = Math.min((value / maxValue) * 100, 100);
            const dataset = chart.data.datasets[0];
            dataset.data = [percentage, 100 - percentage];
            this.setGaugeColors(dataset, value, thresholds, colorScheme);
            chart.update('none');
        },

        /**
         * Get color based on value and thresholds
         */
        getThresholdColor: function(value, thresholds, colors) {
            for (let i = thresholds.length - 1; i >= 0; i--) {
                if (value >= thresholds[i]) {
                    return colors[i];
                }
            }
            return colors[0];
        },

        /**
         * Deep merge options objects
         */
        mergeOptions: function(base, override) {
            const result = { ...base };
            for (const key of Object.keys(override)) {
                if (typeof override[key] === 'object' && !Array.isArray(override[key]) && override[key] !== null) {
                    result[key] = this.mergeOptions(base[key] || {}, override[key]);
                } else {
                    result[key] = override[key];
                }
            }
            return result;
        },

        /**
         * Error state in place of a chart: plain language (never an exception's text) with an
         * optional Retry. `message` is accepted for older callers and ignored.
         */
        showChartError: function(chartId, message, onRetry) {
            const canvas = this._resolveCanvas(chartId);
            if (!canvas || !canvas.parentElement) return;
            const options = {
                type: 'error',
                size: 'compact',
                headingLevel: 3,
                title: 'This chart could not be loaded',
                description: 'The data did not load. Check your connection and try again.',
                action: typeof onRetry === 'function' ? { text: 'Retry', iconPath: '', onClick: onRetry } : null
            };
            const node = root.EmptyState
                ? root.EmptyState.create(options)
                : this._textNode(options.title, options.description);
            this._setChartState(canvas, node);
        },

        /**
         * Escape HTML for safe insertion
         */
        escapeHtml: function(text) {
            const div = document.createElement('div');
            div.textContent = text || '';
            return div.innerHTML.replace(/"/g, '&quot;').replace(/'/g, '&#39;');
        },

        /**
         * Generic creator: registers the theme plugin, fills token-based dataset colours and builds
         * the chart. Use it instead of `new Chart` so series follow the theme.
         */
        createChart: function(ctx, config) {
            if (root.ChartTheme) root.ChartTheme.ensureRegistered();
            const colors = root.ChartTheme ? root.ChartTheme.colors() : null;
            if (config && config.data && Array.isArray(config.data.datasets)) {
                config.data.datasets.forEach(ds => this.applyThemeColors(ds, colors));
            }
            return new Chart(ctx, config);
        },

        /**
         * Fills the colour properties named in `dataset.themeColors` from the active theme.
         * Each value is a function of ChartTheme.colors() returning a colour or an array of them.
         */
        applyThemeColors: function(dataset, colors) {
            if (!dataset || !dataset.themeColors) return dataset;
            const c = colors || (root.ChartTheme ? root.ChartTheme.colors() : null);
            if (!c) return dataset;
            Object.keys(dataset.themeColors).forEach(prop => {
                const spec = dataset.themeColors[prop];
                if (typeof spec === 'function') dataset[prop] = spec(c);
            });
            return dataset;
        },

        /**
         * One-line description of a series set, for the chart's accessible name:
         * "Commands: 24 values from 0 to 18, latest 4".
         */
        summarize: function(labels, datasets, unit) {
            const u = unit ? ' ' + unit : '';
            return (datasets || []).map(ds => {
                const values = (ds.data || []).filter(v => typeof v === 'number' && isFinite(v));
                if (values.length === 0) return `${ds.label || 'Series'}: no values`;
                const min = Math.min(...values);
                const max = Math.max(...values);
                const last = values[values.length - 1];
                const fmt = n => (Math.round(n * 10) / 10).toLocaleString();
                return `${ds.label || 'Series'}: ${values.length} ${values.length === 1 ? 'value' : 'values'} from ${fmt(min)}${u} to ${fmt(max)}${u}, latest ${fmt(last)}${u}`;
            }).join('. ');
        },

        /**
         * Rows for the hidden data table: [label, ...values per dataset]. Keeps the latest
         * `maxRows` points; `truncated` says how many earlier ones were dropped.
         */
        tableModel: function(labels, datasets, maxRows) {
            const limit = maxRows || MAX_TABLE_ROWS;
            const all = labels || [];
            const start = Math.max(0, all.length - limit);
            const rows = [];
            for (let i = start; i < all.length; i++) {
                rows.push([String(all[i])].concat((datasets || []).map(ds => {
                    const v = (ds.data || [])[i];
                    return v === undefined || v === null ? '' : String(Math.round(v * 100) / 100);
                })));
            }
            return {
                headers: ['Period'].concat((datasets || []).map(ds => ds.label || 'Value')),
                rows,
                truncated: start
            };
        },

        /**
         * Text alternative for a chart: the canvas becomes an image with a one-line summary as its
         * name, and a visually hidden table of the same numbers follows it (screen readers can
         * walk the table; sighted users are unaffected).
         * @param {HTMLCanvasElement|string} canvas Element or id.
         * @param {Object} opts caption (what the chart shows), labels, datasets, unit, maxRows,
         *        firstColumn (header of the label column, default "Period").
         */
        describeChart: function(canvas, opts) {
            const el = typeof canvas === 'string' ? document.getElementById(canvas) : canvas;
            if (!el) return;
            const o = opts || {};
            const summary = (o.caption ? o.caption + '. ' : '') + this.summarize(o.labels, o.datasets, o.unit);
            el.setAttribute('role', 'img');
            el.setAttribute('aria-label', summary);

            const existing = el.parentElement ? el.parentElement.querySelector('[data-chart-table]') : null;
            if (existing) existing.remove();
            const model = this.tableModel(o.labels, o.datasets, o.maxRows);
            if (model.rows.length === 0 || !el.parentElement) return;
            if (o.firstColumn) model.headers[0] = o.firstColumn;

            const id = 'chart-data-' + (++tableCounter);
            const table = document.createElement('table');
            table.className = 'sr-only';
            table.id = id;
            table.setAttribute('data-chart-table', '');
            const cap = document.createElement('caption');
            cap.textContent = (o.caption || 'Chart data') +
                (model.truncated > 0 ? ` (latest ${model.rows.length} of ${model.rows.length + model.truncated})` : '');
            table.appendChild(cap);
            const headRow = table.createTHead().insertRow();
            model.headers.forEach(h => {
                const th = document.createElement('th');
                th.scope = 'col';
                th.textContent = h;
                headRow.appendChild(th);
            });
            const tbody = table.createTBody();
            model.rows.forEach(r => {
                const tr = tbody.insertRow();
                r.forEach((cell, i) => {
                    const c = document.createElement(i === 0 ? 'th' : 'td');
                    if (i === 0) c.scope = 'row';
                    c.textContent = cell;
                    tr.appendChild(c);
                });
            });
            el.insertAdjacentElement('afterend', table);
            el.setAttribute('aria-describedby', id);
        },

        /** Text alternative for a gauge: its number is already on screen, so the name restates it. */
        describeGauge: function(canvas, opts) {
            const el = typeof canvas === 'string' ? document.getElementById(canvas) : canvas;
            if (!el) return;
            const o = opts || {};
            const unit = o.unit ? ' ' + o.unit : '';
            const of = o.max !== undefined ? ` of ${o.max}${unit}` : '';
            el.setAttribute('role', 'img');
            el.setAttribute('aria-label', `${o.label || 'Gauge'}: ${o.value}${unit}${of}`);
        },

        _resolveCanvas: function(canvasOrId) {
            return typeof canvasOrId === 'string' ? document.getElementById(canvasOrId) : canvasOrId;
        },

        _setChartState: function(canvasOrId, node) {
            const canvas = this._resolveCanvas(canvasOrId);
            const container = canvas ? canvas.parentElement : null;
            if (!canvas || !container) return null;
            this.clearChartState(canvas);
            canvas.style.display = 'none';
            node.setAttribute('data-chart-state', '');
            container.appendChild(node);
            return node;
        },

        /** Removes an empty or error state and shows the canvas again. */
        clearChartState: function(canvasOrId) {
            const canvas = this._resolveCanvas(canvasOrId);
            const container = canvas ? canvas.parentElement : null;
            if (!canvas || !container) return;
            container.querySelectorAll('[data-chart-state]').forEach(n => n.remove());
            canvas.style.display = '';
        },

        /** Empty state in place of a chart that has nothing to draw. */
        showChartEmpty: function(canvasOrId, opts) {
            const o = opts || {};
            const options = {
                type: 'noData',
                size: 'compact',
                headingLevel: 3,
                title: o.title || 'No data yet',
                description: o.description || 'Nothing has been recorded for this period.'
            };
            const node = root.EmptyState
                ? root.EmptyState.create(options)
                : this._textNode(options.title, options.description);
            return this._setChartState(canvasOrId, node);
        },

        _textNode: function(title, description) {
            const wrap = document.createElement('div');
            wrap.className = 'text-center p-4';
            const t = document.createElement('div');
            t.className = 'font-medium text-text-primary';
            t.textContent = title;
            const d = document.createElement('div');
            d.className = 'text-sm text-text-secondary mt-1';
            d.textContent = description;
            wrap.appendChild(t);
            wrap.appendChild(d);
            return wrap;
        },

        /**
         * Safely destroy a chart instance
         */
        destroyChart: function(chart) {
            if (chart && typeof chart.destroy === 'function') {
                chart.destroy();
            }
        },

        /**
         * Destroy multiple charts
         */
        destroyCharts: function(charts) {
            if (Array.isArray(charts)) {
                charts.forEach(c => this.destroyChart(c));
            }
        }
    };

    // Recolour themed datasets when the theme changes (chart-theme.js repaints the chrome itself).
    if (root.ChartTheme && typeof root.ChartTheme.onChange === 'function') {
        root.ChartTheme.onChange(function(chart, colors) {
            if (!chart || !chart.data || !Array.isArray(chart.data.datasets)) return;
            chart.data.datasets.forEach(ds => ChartUtils.applyThemeColors(ds, colors));
        });
    }

    root.Performance.ChartUtils = ChartUtils;
    if (typeof module === 'object' && module.exports) {
        module.exports = ChartUtils;
    }
})();
