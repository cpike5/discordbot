/**
 * Moderation analytics page (Pages/Guilds/Analytics/Moderation.cshtml): stacked trend chart,
 * case-type doughnut and moderator workload bars. Data comes from the JSON island
 * #moderationChartData. Series colours come from ChartTheme and follow the theme
 * (see analytics-charts.js).
 */
(function () {
    'use strict';

    const A = window.AnalyticsCharts;
    if (!A) return;

    // Warn, mute, kick, ban: the same four colours in the trend and the doughnut.
    function caseColors(c) {
        return [c.warning, c.info, c.primary, c.error];
    }

    function caseFills(c) {
        return [c.alpha('warning', 0.3), c.alpha('info', 0.3), c.alpha('accent-orange', 0.3), c.alpha('error', 0.3)];
    }

    function buildTrends(trends, c) {
        const lines = caseColors(c);
        const fills = caseFills(c);
        const series = [
            ['Warns', t => t.warnCount, []],
            ['Mutes', t => t.muteCount, [6, 3]],
            ['Kicks', t => t.kickCount, [2, 3]],
            ['Bans', t => t.banCount, [10, 4]]
        ];
        return {
            type: 'line',
            data: {
                labels: trends.map(t => A.dayLabel(t.date)),
                datasets: series.map((s, i) => ({
                    label: s[0],
                    data: trends.map(s[1]),
                    borderColor: lines[i],
                    backgroundColor: fills[i],
                    borderDash: s[2],
                    fill: true,
                    tension: 0.4
                }))
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: { mode: 'index', intersect: false },
                plugins: {
                    legend: { position: 'bottom', labels: { boxWidth: 12, padding: 20 } },
                    tooltip: { padding: 12 }
                },
                scales: {
                    y: { beginAtZero: true, stacked: true, ticks: { precision: 0 } },
                    x: { grid: { display: false } }
                }
            }
        };
    }

    function recolorTrends(chart, c) {
        const lines = caseColors(c);
        const fills = caseFills(c);
        chart.data.datasets.forEach((dataset, i) => {
            dataset.borderColor = lines[i];
            dataset.backgroundColor = fills[i];
        });
    }

    function buildDistribution(d, c) {
        const counts = [d.warnCount, d.muteCount, d.kickCount, d.banCount];
        const total = counts.reduce((a, b) => a + b, 0);
        return {
            type: 'doughnut',
            data: {
                labels: ['Warns', 'Mutes', 'Kicks', 'Bans'],
                datasets: [{ data: counts, backgroundColor: caseColors(c), borderColor: c.canvas, borderWidth: 2 }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                cutout: '65%',
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: {
                            boxWidth: 12,
                            padding: 15,
                            // The count is in the legend text, so the segments never rely on colour alone.
                            generateLabels(chart) {
                                const ds = chart.data.datasets[0];
                                return chart.data.labels.map((label, i) => ({
                                    text: label + ' (' + A.number(ds.data[i]) + ')',
                                    fillStyle: ds.backgroundColor[i],
                                    strokeStyle: ds.backgroundColor[i],
                                    fontColor: chart.options.plugins.legend.labels.color,
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
                    }
                }
            }
        };
    }

    function recolorDistribution(chart, c) {
        const dataset = chart.data.datasets[0];
        dataset.backgroundColor = caseColors(c);
        dataset.borderColor = c.canvas;
    }

    function buildWorkload(workload, c) {
        const total = workload.reduce((sum, m) => sum + m.totalActions, 0);
        return {
            type: 'bar',
            data: {
                labels: workload.map(m => m.moderatorUsername),
                datasets: [{
                    label: 'Cases Handled',
                    data: workload.map(m => m.totalActions),
                    backgroundColor: c.fills.secondary,
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
                                const share = total > 0 ? ((ctx.parsed.x / total) * 100).toFixed(1) : '0.0';
                                return A.number(ctx.parsed.x) + (ctx.parsed.x === 1 ? ' case' : ' cases') + ' (' + share + '%)';
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

    function recolorWorkload(chart, c) {
        chart.data.datasets[0].backgroundColor = c.fills.secondary;
    }

    function init() {
        const data = A.readData('moderationChartData');
        if (!data) return;

        if (data.trends && data.trends.length > 0) {
            A.create('moderationTrendsChart', c => buildTrends(data.trends, c), recolorTrends);
        }
        if (data.distribution) {
            A.create('caseDistributionChart', c => buildDistribution(data.distribution, c), recolorDistribution);
        }
        if (data.moderatorWorkload && data.moderatorWorkload.length > 0) {
            A.create('moderatorWorkloadChart', c => buildWorkload(data.moderatorWorkload, c), recolorWorkload);
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
