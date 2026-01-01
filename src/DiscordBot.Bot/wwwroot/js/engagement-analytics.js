// engagement-analytics.js
// Engagement analytics dashboard with Chart.js visualizations

(function () {
    'use strict';

    function getDesignToken(name) {
        return getComputedStyle(document.documentElement)
            .getPropertyValue(`--color-${name}`).trim();
    }

    function initColors() {
        return {
            accentOrange: getDesignToken('accent-orange') || '#cb4e1b',
            accentBlue: getDesignToken('accent-blue') || '#098ecf',
            success: getDesignToken('success') || '#10b981',
            warning: getDesignToken('warning') || '#f59e0b',
            info: getDesignToken('info') || '#06b6d4',
            error: getDesignToken('error') || '#ef4444',
            bgPrimary: getDesignToken('bg-primary') || '#1d2022',
            bgSecondary: getDesignToken('bg-secondary') || '#262a2d',
            bgTertiary: getDesignToken('bg-tertiary') || '#2f3336',
            textPrimary: getDesignToken('text-primary') || '#d7d3d0',
            textSecondary: getDesignToken('text-secondary') || '#a8a5a3',
            textTertiary: getDesignToken('text-tertiary') || '#7a7876',
            borderPrimary: getDesignToken('border-primary') || '#3f4447',
        };
    }

    let colors = initColors();

    const CHART_COLORS = [
        colors.accentOrange,
        colors.accentBlue,
        colors.success,
        colors.warning,
        colors.info,
        colors.error,
        '#8b5cf6',
        '#ec4899',
        '#14b8a6',
        '#6366f1',
    ];

    const BG_COLORS = {
        primary: colors.bgPrimary,
        secondary: colors.bgSecondary,
        tertiary: colors.bgTertiary,
    };

    const TEXT_COLORS = {
        primary: colors.textPrimary,
        secondary: colors.textSecondary,
        tertiary: colors.textTertiary,
    };

    const BORDER_COLOR = colors.borderPrimary;

    let messageTrendsChart = null;
    let channelEngagementChart = null;

    const commonOptions = {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
            legend: {
                display: false,
            },
            tooltip: {
                backgroundColor: BG_COLORS.tertiary,
                titleColor: TEXT_COLORS.primary,
                bodyColor: TEXT_COLORS.secondary,
                borderColor: BORDER_COLOR,
                borderWidth: 1,
                padding: 12,
                cornerRadius: 6,
            },
        },
    };

    const gridConfig = {
        color: 'rgba(63, 68, 71, 0.5)',
        drawBorder: false,
    };

    const ticksConfig = {
        color: TEXT_COLORS.tertiary,
        font: {
            size: 12,
        },
    };

    function formatNumber(num) {
        return num.toString().replace(/\B(?=(\d{3})+(?!\d))/g, ',');
    }

    function formatDate(dateStr) {
        const date = new Date(dateStr);
        const monthNames = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
            'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
        return `${monthNames[date.getMonth()]} ${date.getDate()}`;
    }

    /**
     * Initializes the Message Trends dual-axis chart.
     * @param {Array} trendsData - Array of {date, messageCount, uniqueAuthors, avgLength} objects
     */
    function initMessageTrendsChart(trendsData) {
        const canvas = document.getElementById('messageTrendsChart');
        if (!canvas || !trendsData || trendsData.length === 0) {
            return;
        }

        const ctx = canvas.getContext('2d');
        const labels = trendsData.map(item => formatDate(item.date));
        const messagesData = trendsData.map(item => item.messageCount);
        const authorsData = trendsData.map(item => item.uniqueAuthors);

        const messagesGradient = ctx.createLinearGradient(0, 0, 0, 300);
        messagesGradient.addColorStop(0, 'rgba(9, 142, 207, 0.3)');
        messagesGradient.addColorStop(1, 'rgba(9, 142, 207, 0.0)');

        messageTrendsChart = new Chart(ctx, {
            type: 'line',
            data: {
                labels: labels,
                datasets: [
                    {
                        label: 'Messages',
                        data: messagesData,
                        borderColor: CHART_COLORS[1],
                        backgroundColor: messagesGradient,
                        borderWidth: 2,
                        fill: true,
                        tension: 0.3,
                        pointRadius: 4,
                        pointBackgroundColor: CHART_COLORS[1],
                        pointBorderColor: BG_COLORS.primary,
                        pointBorderWidth: 2,
                        pointHoverRadius: 6,
                        yAxisID: 'y',
                    },
                    {
                        label: 'Unique Authors',
                        data: authorsData,
                        borderColor: CHART_COLORS[2],
                        backgroundColor: 'transparent',
                        borderWidth: 2,
                        fill: false,
                        tension: 0.3,
                        pointRadius: 3,
                        pointBackgroundColor: CHART_COLORS[2],
                        pointBorderColor: BG_COLORS.primary,
                        pointBorderWidth: 2,
                        pointHoverRadius: 5,
                        yAxisID: 'y1',
                    }
                ]
            },
            options: {
                ...commonOptions,
                plugins: {
                    ...commonOptions.plugins,
                    legend: {
                        display: true,
                        position: 'bottom',
                        labels: {
                            color: TEXT_COLORS.primary,
                            padding: 16,
                            font: { size: 12 },
                            usePointStyle: true,
                        }
                    },
                    tooltip: {
                        ...commonOptions.plugins.tooltip,
                        callbacks: {
                            title: function (context) {
                                return trendsData[context[0].dataIndex].date;
                            },
                            label: function (context) {
                                return `${context.dataset.label}: ${formatNumber(context.parsed.y)}`;
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        grid: gridConfig,
                        ticks: ticksConfig,
                    },
                    y: {
                        type: 'linear',
                        display: true,
                        position: 'left',
                        beginAtZero: true,
                        grid: gridConfig,
                        ticks: {
                            ...ticksConfig,
                            callback: function (value) {
                                if (value >= 1000) return (value / 1000).toFixed(1) + 'k';
                                return value;
                            }
                        },
                        title: {
                            display: true,
                            text: 'Messages',
                            color: TEXT_COLORS.secondary,
                        }
                    },
                    y1: {
                        type: 'linear',
                        display: true,
                        position: 'right',
                        beginAtZero: true,
                        grid: {
                            drawOnChartArea: false,
                        },
                        ticks: ticksConfig,
                        title: {
                            display: true,
                            text: 'Unique Authors',
                            color: TEXT_COLORS.secondary,
                        }
                    }
                },
                interaction: {
                    intersect: false,
                    mode: 'index',
                }
            }
        });

        console.log('Message trends chart initialized');
    }

    /**
     * Initializes the Channel Engagement bar chart.
     * @param {Array} channelData - Array of {channelName, messageCount, uniqueUsers} objects
     */
    function initChannelEngagementChart(channelData) {
        const canvas = document.getElementById('channelEngagementChart');
        if (!canvas || !channelData || channelData.length === 0) {
            return;
        }

        const ctx = canvas.getContext('2d');
        const labels = channelData.map(item => '#' + item.channelName);
        const messagesData = channelData.map(item => item.messageCount);
        const usersData = channelData.map(item => item.uniqueUsers);

        channelEngagementChart = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [
                    {
                        label: 'Messages',
                        data: messagesData,
                        backgroundColor: CHART_COLORS[1],
                        borderColor: CHART_COLORS[1],
                        borderWidth: 1,
                        borderRadius: 4,
                    },
                    {
                        label: 'Unique Users',
                        data: usersData,
                        backgroundColor: CHART_COLORS[2],
                        borderColor: CHART_COLORS[2],
                        borderWidth: 1,
                        borderRadius: 4,
                    }
                ]
            },
            options: {
                ...commonOptions,
                plugins: {
                    ...commonOptions.plugins,
                    legend: {
                        display: true,
                        position: 'bottom',
                        labels: {
                            color: TEXT_COLORS.primary,
                            padding: 16,
                            font: { size: 12 },
                            usePointStyle: true,
                        }
                    },
                    tooltip: {
                        ...commonOptions.plugins.tooltip,
                        callbacks: {
                            label: function (context) {
                                return `${context.dataset.label}: ${formatNumber(context.parsed.y)}`;
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        grid: { display: false },
                        ticks: ticksConfig,
                    },
                    y: {
                        beginAtZero: true,
                        grid: gridConfig,
                        ticks: {
                            ...ticksConfig,
                            callback: function (value) {
                                if (value >= 1000) return (value / 1000).toFixed(1) + 'k';
                                return value;
                            }
                        },
                    }
                },
                animation: {
                    duration: 500,
                    easing: 'easeOutQuart',
                }
            }
        });

        console.log('Channel engagement chart initialized');
    }

    /**
     * Renders the retention funnel visualization.
     * @param {Array} retentionData - Array of {period, joined, activeDay1, activeDay7, activeDay30} objects
     */
    function renderRetentionFunnel(retentionData) {
        const container = document.getElementById('retentionFunnel');
        if (!container || !retentionData || retentionData.length === 0) {
            return;
        }

        // Aggregate data across all periods
        const totals = retentionData.reduce((acc, item) => {
            acc.joined += item.joined || 0;
            acc.activeDay1 += item.activeDay1 || 0;
            acc.activeDay7 += item.activeDay7 || 0;
            acc.activeDay30 += item.activeDay30 || 0;
            return acc;
        }, { joined: 0, activeDay1: 0, activeDay7: 0, activeDay30: 0 });

        const stages = [
            { label: 'Joined', value: totals.joined, color: CHART_COLORS[1] },
            { label: 'Active Day 1', value: totals.activeDay1, color: CHART_COLORS[2] },
            { label: 'Active Day 7', value: totals.activeDay7, color: CHART_COLORS[3] },
            { label: 'Active Day 30', value: totals.activeDay30, color: CHART_COLORS[0] },
        ];

        const maxValue = Math.max(...stages.map(s => s.value));

        let html = '<div class="space-y-4">';
        stages.forEach((stage, index) => {
            const percentage = totals.joined > 0 ? ((stage.value / totals.joined) * 100).toFixed(1) : 0;
            const barWidth = maxValue > 0 ? (stage.value / maxValue) * 100 : 0;
            const retentionFromPrev = index === 0 ? 100 : (stages[index - 1].value > 0 ? (stage.value / stages[index - 1].value) * 100 : 0);

            html += `
                <div class="flex items-center gap-4">
                    <div class="w-28 text-sm text-text-secondary font-medium">${stage.label}</div>
                    <div class="flex-1 relative">
                        <div class="h-8 bg-bg-tertiary rounded-lg overflow-hidden">
                            <div class="h-full rounded-lg transition-all duration-500"
                                 style="width: ${barWidth}%; background-color: ${stage.color};">
                            </div>
                        </div>
                    </div>
                    <div class="w-20 text-right">
                        <span class="text-lg font-bold text-text-primary">${formatNumber(stage.value)}</span>
                        <span class="text-xs text-text-tertiary ml-1">(${percentage}%)</span>
                    </div>
                </div>
            `;
        });
        html += '</div>';

        // Summary metrics
        const overallRetention = totals.joined > 0 ? ((totals.activeDay30 / totals.joined) * 100).toFixed(1) : 0;
        html += `
            <div class="mt-6 pt-4 border-t border-border-primary">
                <div class="flex items-center justify-between text-sm">
                    <span class="text-text-secondary">30-Day Retention Rate</span>
                    <span class="text-lg font-bold ${parseFloat(overallRetention) >= 20 ? 'text-success' : parseFloat(overallRetention) >= 10 ? 'text-warning' : 'text-error'}">${overallRetention}%</span>
                </div>
            </div>
        `;

        container.innerHTML = html;
        console.log('Retention funnel rendered');
    }

    /**
     * Initialize all engagement analytics charts.
     */
    function init() {
        const dataElement = document.getElementById('engagementAnalyticsChartData');
        if (!dataElement) {
            console.log('Engagement analytics chart data not found on this page');
            return;
        }

        try {
            const chartData = JSON.parse(dataElement.textContent);

            if (chartData.messageTrends && chartData.messageTrends.length > 0) {
                initMessageTrendsChart(chartData.messageTrends);
            }

            if (chartData.channelEngagement && chartData.channelEngagement.length > 0) {
                initChannelEngagementChart(chartData.channelEngagement);
            }

            if (chartData.retention && chartData.retention.length > 0) {
                renderRetentionFunnel(chartData.retention);
            }

            console.log('Engagement analytics charts initialized');

        } catch (error) {
            console.error('Failed to initialize engagement analytics charts:', error);
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})();
