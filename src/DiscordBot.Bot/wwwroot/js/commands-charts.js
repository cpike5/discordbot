/**
 * commands-charts.js - the four charts on the Commands Analytics tab.
 *
 * The partial (Pages/Commands/Tabs/_AnalyticsTab.cshtml) carries its data in a
 * <script type="application/json" data-commands-chart-data> island and one <canvas data-chart>
 * per chart; commands-page.js calls CommandsCharts.render(region) after each swap. Series
 * colours come from ChartTheme.colors() (chart-theme.js) and are re-applied when the theme
 * changes; the axis, grid, legend and tooltip chrome is chart-theme.js's job.
 *
 * Charts from an earlier render are destroyed first, so a reload never stacks canvases.
 */
(function () {
    'use strict';

    var charts = [];
    var listening = false;

    function dayLabel(iso) {
        var parts = String(iso).split('-');
        if (parts.length !== 3) return iso;
        var date = new Date(+parts[0], +parts[1] - 1, +parts[2]);
        try {
            return new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric' }).format(date);
        } catch (e) {
            return iso;
        }
    }

    /** A hex colour at ~20% opacity (the line's area fill); other formats pass through. */
    function translucent(color) {
        return /^#[0-9a-f]{6}$/i.test(color) ? color + '33' : color;
    }

    /** Dataset colours for one chart kind, from the active theme's tokens. */
    function paintSeries(chart, c) {
        var kind = chart.$commandsKind;
        var set = chart.data.datasets[0];
        if (!set) return;
        if (kind === 'usage') {
            set.borderColor = c.secondary;
            set.backgroundColor = translucent(c.secondary);
            set.pointBackgroundColor = c.secondary;
        } else if (kind === 'top') {
            set.backgroundColor = c.fills.primary;
        } else if (kind === 'response') {
            set.backgroundColor = c.fills.info;
        } else if (kind === 'success') {
            set.backgroundColor = [c.fills.success, c.fills.error];
            set.borderColor = c.canvas;
        }
    }

    function build(canvas, kind, config) {
        var chart = new window.Chart(canvas, config);
        chart.$commandsKind = kind;
        paintSeries(chart, window.ChartTheme.colors());
        chart.update('none');
        charts.push(chart);
    }

    function destroyAll() {
        charts.forEach(function (chart) { chart.destroy(); });
        charts = [];
    }

    function render(root) {
        destroyAll();
        if (!window.Chart || !window.ChartTheme) return;
        var island = root.querySelector('[data-commands-chart-data]');
        if (!island) return;

        var data;
        try {
            data = JSON.parse(island.textContent);
        } catch (e) {
            return;
        }

        if (!listening) {
            listening = true;
            // chart-theme.js repaints the chrome; the series are ours
            window.ChartTheme.onChange(function (chart, colors) {
                if (chart.$commandsKind) paintSeries(chart, colors);
            });
        }

        var usage = root.querySelector('[data-chart="usage"]');
        if (usage && data.usageOverTime.length) {
            build(usage, 'usage', {
                type: 'line',
                data: {
                    labels: data.usageOverTime.map(function (d) { return dayLabel(d.date); }),
                    datasets: [{ label: 'Commands executed', data: data.usageOverTime.map(function (d) { return d.count; }), tension: 0.3, fill: true, borderWidth: 2 }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: { legend: { display: false } },
                    scales: { y: { beginAtZero: true, ticks: { precision: 0 } } }
                }
            });
        }

        var top = root.querySelector('[data-chart="top"]');
        if (top && data.topCommands.length) {
            build(top, 'top', {
                type: 'bar',
                data: {
                    labels: data.topCommands.map(function (d) { return '/' + d.name; }),
                    datasets: [{ label: 'Executions', data: data.topCommands.map(function (d) { return d.count; }), borderWidth: 0, borderRadius: 3 }]
                },
                options: {
                    indexAxis: 'y',
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: { legend: { display: false } },
                    scales: { x: { beginAtZero: true, ticks: { precision: 0 } } }
                }
            });
        }

        var success = root.querySelector('[data-chart="success"]');
        if (success && data.successCount + data.failureCount > 0) {
            build(success, 'success', {
                type: 'doughnut',
                data: {
                    labels: ['Succeeded', 'Failed'],
                    datasets: [{ data: [data.successCount, data.failureCount], borderWidth: 2 }]
                },
                options: { responsive: true, maintainAspectRatio: false }
            });
        }

        var response = root.querySelector('[data-chart="response"]');
        if (response && data.performance.length) {
            build(response, 'response', {
                type: 'bar',
                data: {
                    labels: data.performance.map(function (d) { return '/' + d.name; }),
                    datasets: [{ label: 'Average response (ms)', data: data.performance.map(function (d) { return Math.round(d.avgMs); }), borderWidth: 0, borderRadius: 3 }]
                },
                options: {
                    indexAxis: 'y',
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: { legend: { display: false } },
                    scales: { x: { beginAtZero: true, title: { display: true, text: 'milliseconds' } } }
                }
            });
        }
    }

    window.CommandsCharts = { render: render, destroy: destroyAll };
})();
