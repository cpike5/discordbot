/**
 * Server analytics page (Pages/Guilds/Analytics/Index.cshtml): activity line chart, top channels
 * bar chart and the activity heatmap. Data comes from the JSON island #serverAnalyticsChartData.
 * Series colours come from ChartTheme and follow the theme (see analytics-charts.js).
 */
(function () {
    'use strict';

    const A = window.AnalyticsCharts;
    if (!A) return;

    function buildActivity(series, c) {
        return {
            type: 'line',
            data: {
                labels: series.map(d => A.dayLabel(d.date)),
                datasets: [
                    {
                        label: 'Messages',
                        data: series.map(d => d.messageCount),
                        borderColor: c.secondary,
                        backgroundColor: c.alpha('accent-blue', 0.12),
                        fill: true,
                        tension: 0.4,
                        yAxisID: 'y',
                        pointRadius: 4,
                        pointHoverRadius: 6
                    },
                    {
                        label: 'Active Members',
                        data: series.map(d => d.activeMembers),
                        borderColor: c.success,
                        backgroundColor: c.alpha('success', 0.12),
                        borderDash: [6, 4],
                        pointStyle: 'rectRot',
                        fill: true,
                        tension: 0.4,
                        yAxisID: 'y1',
                        pointRadius: 4,
                        pointHoverRadius: 6
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: { mode: 'index', intersect: false },
                plugins: {
                    legend: { position: 'bottom', labels: { boxWidth: 12, padding: 20, usePointStyle: true } },
                    tooltip: { padding: 12 }
                },
                scales: {
                    y: {
                        type: 'linear',
                        position: 'left',
                        beginAtZero: true,
                        title: { display: true, text: 'Messages' }
                    },
                    y1: {
                        type: 'linear',
                        position: 'right',
                        beginAtZero: true,
                        title: { display: true, text: 'Active Members' },
                        grid: { drawOnChartArea: false }
                    },
                    x: { grid: { display: false } }
                }
            }
        };
    }

    function recolorActivity(chart, c) {
        const [messages, members] = chart.data.datasets;
        messages.borderColor = c.secondary;
        messages.backgroundColor = c.alpha('accent-blue', 0.12);
        members.borderColor = c.success;
        members.backgroundColor = c.alpha('success', 0.12);
    }

    function buildChannels(channels, c) {
        return {
            type: 'bar',
            data: {
                labels: channels.map(ch => ch.channelName),
                datasets: [{
                    label: 'Messages',
                    data: channels.map(ch => ch.messageCount),
                    backgroundColor: A.each(c.fills.secondary, channels.length),
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
                            label: ctx => A.number(ctx.parsed.x) + (ctx.parsed.x === 1 ? ' message' : ' messages')
                        }
                    }
                },
                scales: {
                    x: { beginAtZero: true, ticks: { callback: value => A.number(value) } },
                    y: { grid: { display: false } }
                }
            }
        };
    }

    function recolorChannels(chart, c) {
        chart.data.datasets[0].backgroundColor = A.each(c.fills.secondary, chart.data.labels.length);
    }

    function init() {
        const data = A.readData('serverAnalyticsChartData');
        if (!data) return;

        if (data.activityTimeSeries && data.activityTimeSeries.length > 0) {
            A.create('activityOverTimeChart', c => buildActivity(data.activityTimeSeries, c), recolorActivity);
        }
        if (data.topChannels && data.topChannels.length > 0) {
            A.create('topChannelsChart', c => buildChannels(data.topChannels, c), recolorChannels);
        }
        if (data.heatmap && data.heatmap.length > 0) {
            A.heatmap(document.getElementById('activityHeatmap'), data.heatmap, {
                countKey: 'messageCount',
                unit: 'message',
                unitPlural: 'messages',
                accent: 'blue',
                note: 'Message activity by day and hour (UTC)'
            });
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
