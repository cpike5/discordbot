/**
 * Engagement analytics page (Pages/Guilds/Analytics/Engagement.cshtml): message trends line chart
 * and channel engagement bars. Data comes from the JSON island #engagementChartData. Series
 * colours come from ChartTheme and follow the theme (see analytics-charts.js).
 */
(function () {
    'use strict';

    const A = window.AnalyticsCharts;
    if (!A) return;

    function buildTrends(trends, c) {
        return {
            type: 'line',
            data: {
                labels: trends.map(t => A.dayLabel(t.date)),
                datasets: [
                    {
                        label: 'Messages',
                        data: trends.map(t => t.messageCount),
                        borderColor: c.secondary,
                        backgroundColor: c.alpha('accent-blue', 0.12),
                        fill: true,
                        tension: 0.4,
                        yAxisID: 'y'
                    },
                    {
                        label: 'Unique Authors',
                        data: trends.map(t => t.uniqueAuthors),
                        borderColor: c.success,
                        backgroundColor: 'transparent',
                        borderDash: [5, 5],
                        tension: 0.4,
                        yAxisID: 'y1'
                    }
                ]
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
                        grid: { drawOnChartArea: false },
                        title: { display: true, text: 'Unique Authors' }
                    },
                    x: { grid: { display: false } }
                }
            }
        };
    }

    function recolorTrends(chart, c) {
        const [messages, authors] = chart.data.datasets;
        messages.borderColor = c.secondary;
        messages.backgroundColor = c.alpha('accent-blue', 0.12);
        authors.borderColor = c.success;
    }

    function buildChannels(channels, c) {
        return {
            type: 'bar',
            data: {
                labels: channels.map(ch => ch.channelName),
                datasets: [
                    {
                        label: 'Messages',
                        data: channels.map(ch => ch.messageCount),
                        backgroundColor: c.fills.secondary,
                        borderRadius: 4
                    },
                    {
                        label: 'Engagement Rate (%)',
                        data: channels.map(ch => ch.engagementRate),
                        backgroundColor: c.fills.primary,
                        borderRadius: 4
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                indexAxis: 'y',
                plugins: {
                    legend: { position: 'bottom', labels: { boxWidth: 12, padding: 20 } },
                    tooltip: {
                        padding: 12,
                        callbacks: {
                            label(ctx) {
                                return ctx.datasetIndex === 0
                                    ? A.number(ctx.parsed.x) + (ctx.parsed.x === 1 ? ' message' : ' messages')
                                    : ctx.parsed.x.toFixed(1) + '% engagement';
                            }
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
        const [messages, rate] = chart.data.datasets;
        messages.backgroundColor = c.fills.secondary;
        rate.backgroundColor = c.fills.primary;
    }

    function init() {
        const data = A.readData('engagementChartData');
        if (!data) return;

        if (data.messageTrends && data.messageTrends.length > 0) {
            A.create('messageTrendsChart', c => buildTrends(data.messageTrends, c), recolorTrends);
        }
        if (data.channelEngagement && data.channelEngagement.length > 0) {
            A.create('channelEngagementChart', c => buildChannels(data.channelEngagement, c), recolorChannels);
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
