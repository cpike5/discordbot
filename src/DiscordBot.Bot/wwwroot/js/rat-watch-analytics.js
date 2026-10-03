/**
 * Rat Watch analytics (Pages/Guilds/RatWatch/Analytics.cshtml and Pages/Admin/RatWatchAnalytics.cshtml):
 * watches over time, outcome doughnut, top watched users and the activity heatmap. Data comes from
 * the JSON island #ratWatchChartData; a chart whose canvas is not on the page is skipped.
 * Series colours come from ChartTheme and follow the theme (see analytics-charts.js).
 */
(function () {
    'use strict';

    const A = window.AnalyticsCharts;
    if (!A) return;

    // Guilty, cleared early, active, other.
    function outcomeColors(c) {
        return [c.error, c.success, c.secondary, c.warning];
    }

    /** Draws the guilty share in the middle of the doughnut, in the active theme's text colour. */
    const centerText = {
        id: 'centerText',
        afterDatasetsDraw(chart) {
            const opts = chart.config.options.plugins.centerText;
            if (!opts) return;
            const { ctx, chartArea } = chart;
            ctx.save();
            ctx.font = 'bold 28px ' + (getComputedStyle(document.documentElement).getPropertyValue('--font-body').trim() || 'sans-serif');
            ctx.fillStyle = window.ChartTheme.colors().text;
            ctx.textAlign = 'center';
            ctx.textBaseline = 'middle';
            ctx.fillText(opts.text, (chartArea.left + chartArea.right) / 2, (chartArea.top + chartArea.bottom) / 2);
            ctx.restore();
        }
    };

    function buildTimeSeries(series, c) {
        return {
            type: 'line',
            data: {
                labels: series.map(d => A.dayLabel(d.date)),
                datasets: [
                    {
                        label: 'Total Watches',
                        data: series.map(d => d.totalCount),
                        borderColor: c.primary,
                        backgroundColor: c.alpha('accent-orange', 0.15),
                        borderWidth: 2,
                        fill: true,
                        tension: 0.3,
                        pointRadius: 4,
                        pointHoverRadius: 6
                    },
                    {
                        label: 'Guilty',
                        data: series.map(d => d.guiltyCount),
                        borderColor: c.error,
                        backgroundColor: 'transparent',
                        borderWidth: 2,
                        tension: 0.3,
                        pointRadius: 3,
                        pointHoverRadius: 5,
                        borderDash: [6, 4]
                    },
                    {
                        label: 'Cleared Early',
                        data: series.map(d => d.clearedCount),
                        borderColor: c.success,
                        backgroundColor: 'transparent',
                        borderWidth: 2,
                        tension: 0.3,
                        pointRadius: 3,
                        pointHoverRadius: 5,
                        borderDash: [2, 3]
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: { intersect: false, mode: 'index' },
                plugins: {
                    legend: { position: 'bottom', labels: { padding: 16, usePointStyle: true } },
                    tooltip: {
                        padding: 12,
                        callbacks: {
                            label: ctx => ctx.dataset.label + ': ' + A.number(ctx.parsed.y)
                        }
                    }
                },
                scales: {
                    x: { grid: { display: false } },
                    y: { beginAtZero: true, ticks: { precision: 0, callback: value => A.compact(value) } }
                }
            }
        };
    }

    function recolorTimeSeries(chart, c) {
        const [total, guilty, cleared] = chart.data.datasets;
        total.borderColor = c.primary;
        total.backgroundColor = c.alpha('accent-orange', 0.15);
        guilty.borderColor = c.error;
        cleared.borderColor = c.success;
    }

    function buildOutcome(o, c) {
        const counts = [o.guiltyCount, o.clearedEarlyCount, o.activeCount, o.otherCount];
        const total = counts.reduce((a, b) => a + b, 0);
        const guiltyShare = total > 0 ? ((o.guiltyCount / total) * 100).toFixed(1) : '0.0';
        return {
            type: 'doughnut',
            data: {
                labels: ['Guilty', 'Cleared Early', 'Active', 'Other'],
                datasets: [{ data: counts, backgroundColor: outcomeColors(c), borderColor: c.canvas, borderWidth: 3 }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                cutout: '70%',
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: {
                            padding: 16,
                            usePointStyle: true,
                            pointStyle: 'circle',
                            // The count is in the legend text, so the segments never rely on colour alone.
                            generateLabels(chart) {
                                const ds = chart.data.datasets[0];
                                return chart.data.labels.map((label, i) => ({
                                    text: label + ' (' + A.number(ds.data[i]) + ')',
                                    fillStyle: ds.backgroundColor[i],
                                    strokeStyle: ds.backgroundColor[i],
                                    fontColor: chart.options.plugins.legend.labels.color,
                                    pointStyle: 'circle',
                                    index: i
                                }));
                            }
                        }
                    },
                    tooltip: {
                        padding: 12,
                        callbacks: {
                            label(ctx) {
                                const share = total > 0 ? ((ctx.parsed / total) * 100).toFixed(1) : '0.0';
                                return ctx.label + ': ' + A.number(ctx.parsed) + ' (' + share + '%)';
                            }
                        }
                    },
                    centerText: { text: guiltyShare + '%' }
                }
            },
            plugins: [centerText]
        };
    }

    function recolorOutcome(chart, c) {
        const dataset = chart.data.datasets[0];
        dataset.backgroundColor = outcomeColors(c);
        dataset.borderColor = c.canvas;
    }

    function buildTopUsers(users, c) {
        return {
            type: 'bar',
            data: {
                labels: users.map(u => u.username || 'Unknown user'),
                datasets: [{
                    label: 'Total Watches',
                    data: users.map(u => u.watchesAgainst),
                    backgroundColor: A.each(c.fills.primary, users.length),
                    borderRadius: 4
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                indexAxis: 'y',
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        padding: 12,
                        callbacks: {
                            label(ctx) {
                                const guilty = users[ctx.dataIndex].guiltyCount;
                                return A.number(ctx.parsed.x) + (ctx.parsed.x === 1 ? ' watch' : ' watches') +
                                    ' (' + A.number(guilty) + ' guilty)';
                            }
                        }
                    }
                },
                scales: {
                    x: { beginAtZero: true, ticks: { precision: 0 } },
                    y: { grid: { display: false } }
                }
            }
        };
    }

    function recolorTopUsers(chart, c) {
        chart.data.datasets[0].backgroundColor = A.each(c.fills.primary, chart.data.labels.length);
    }

    function init() {
        const data = A.readData('ratWatchChartData');
        if (!data) return;

        if (data.timeSeries && data.timeSeries.length > 0) {
            A.create('watchesOverTimeChart', c => buildTimeSeries(data.timeSeries, c), recolorTimeSeries);
        }
        if (data.outcomeDistribution) {
            A.create('outcomeDistributionChart', c => buildOutcome(data.outcomeDistribution, c), recolorOutcome);
        }
        if (data.topUsers && data.topUsers.length > 0) {
            A.create('topUsersChart', c => buildTopUsers(data.topUsers, c), recolorTopUsers);
        }
        if (data.heatmap && data.heatmap.length > 0) {
            A.heatmap(document.getElementById('activityHeatmap'), data.heatmap, {
                countKey: 'count',
                unit: 'watch',
                unitPlural: 'watches',
                accent: 'orange',
                note: 'Watches by scheduled day and hour (UTC)'
            });
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
