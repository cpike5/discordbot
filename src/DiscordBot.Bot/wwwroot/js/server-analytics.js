// server-analytics.js
// Server analytics dashboard with Chart.js visualizations for guild activity metrics

(function () {
    'use strict';

    /**
     * Gets a CSS custom property value from the :root element
     * @param {string} name - The property name (without --color- prefix)
     * @returns {string} The computed color value
     */
    function getDesignToken(name) {
        return getComputedStyle(document.documentElement)
            .getPropertyValue(`--color-${name}`).trim();
    }

    /**
     * Initialize design system colors from CSS custom properties
     */
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

    // Chart instance references
    let activityTimeSeriesChart = null;
    let topChannelsChart = null;

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
     * Initializes the Activity Time Series chart.
     * @param {Array} timeSeriesData - Array of {date, messages, activeMembers, activeChannels} objects
     */
    function initActivityTimeSeriesChart(timeSeriesData) {
        const canvas = document.getElementById('activityTimeSeriesChart');
        if (!canvas || !timeSeriesData || timeSeriesData.length === 0) {
            return;
        }

        const ctx = canvas.getContext('2d');
        const labels = timeSeriesData.map(item => formatDate(item.date));
        const messagesData = timeSeriesData.map(item => item.messages);
        const activeMembersData = timeSeriesData.map(item => item.activeMembers);

        const messagesGradient = ctx.createLinearGradient(0, 0, 0, 300);
        messagesGradient.addColorStop(0, 'rgba(9, 142, 207, 0.3)');
        messagesGradient.addColorStop(1, 'rgba(9, 142, 207, 0.0)');

        activityTimeSeriesChart = new Chart(ctx, {
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
                        label: 'Active Members',
                        data: activeMembersData,
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
                                return timeSeriesData[context[0].dataIndex].date;
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
                            text: 'Active Members',
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

        console.log('Activity time series chart initialized');
    }

    /**
     * Initializes the Top Channels horizontal bar chart.
     * @param {Array} channelsData - Array of {channelName, messageCount} objects
     */
    function initTopChannelsChart(channelsData) {
        const canvas = document.getElementById('topChannelsChart');
        if (!canvas || !channelsData || channelsData.length === 0) {
            return;
        }

        const ctx = canvas.getContext('2d');
        const labels = channelsData.map(item => '#' + item.channelName);
        const data = channelsData.map(item => item.messageCount);

        const colors = labels.map((_, index) => CHART_COLORS[index % CHART_COLORS.length]);

        topChannelsChart = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [{
                    label: 'Messages',
                    data: data,
                    backgroundColor: colors,
                    borderColor: colors,
                    borderWidth: 1,
                    borderRadius: 4,
                    barThickness: 24,
                }]
            },
            options: {
                indexAxis: 'y',
                ...commonOptions,
                plugins: {
                    ...commonOptions.plugins,
                    tooltip: {
                        ...commonOptions.plugins.tooltip,
                        callbacks: {
                            label: function (context) {
                                return `Messages: ${formatNumber(context.parsed.x)}`;
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        beginAtZero: true,
                        grid: gridConfig,
                        ticks: {
                            ...ticksConfig,
                            callback: function (value) {
                                if (value >= 1000) return (value / 1000).toFixed(1) + 'k';
                                return value;
                            }
                        },
                    },
                    y: {
                        grid: { display: false },
                        ticks: {
                            color: TEXT_COLORS.primary,
                            font: { size: 12 },
                            padding: 8,
                        }
                    }
                },
                animation: {
                    duration: 500,
                    easing: 'easeOutQuart',
                }
            }
        });

        console.log('Top channels chart initialized');
    }

    /**
     * Renders the activity heatmap.
     * @param {Array} heatmapData - Array of {dayOfWeek, hour, messageCount} objects
     */
    function renderActivityHeatmap(heatmapData) {
        const container = document.getElementById('activityHeatmap');
        if (!container || !heatmapData || heatmapData.length === 0) {
            return;
        }

        const days = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
        const hours = Array.from({ length: 24 }, (_, i) => i);

        const dataMap = new Map();
        let maxCount = 0;
        heatmapData.forEach(item => {
            const key = `${item.dayOfWeek}-${item.hour}`;
            dataMap.set(key, item.messageCount);
            maxCount = Math.max(maxCount, item.messageCount);
        });

        let html = '<div class="text-xs text-text-tertiary mb-3">Message activity by day and hour (UTC)</div>';
        html += '<div style="display: grid; grid-template-columns: 40px repeat(24, 16px); gap: 2px; font-size: 11px;">';

        html += '<div></div>';
        hours.forEach(hour => {
            html += `<div style="text-align: center; color: var(--text-tertiary);">${hour % 6 === 0 ? hour : ''}</div>`;
        });

        days.forEach((day, dayIndex) => {
            html += `<div style="color: var(--text-secondary); font-weight: 500; line-height: 16px;">${day}</div>`;
            hours.forEach(hour => {
                const key = `${dayIndex}-${hour}`;
                const count = dataMap.get(key) || 0;
                const intensity = maxCount > 0 ? count / maxCount : 0;
                const bgColor = getHeatmapColor(intensity);
                const title = `${day} ${hour}:00 - ${formatNumber(count)} messages`;
                html += `<div style="width: 16px; height: 16px; border-radius: 2px; background-color: ${bgColor}; cursor: pointer;" title="${title}"></div>`;
            });
        });

        html += '</div>';

        html += '<div class="flex items-center gap-2 mt-4 text-xs text-text-tertiary">';
        html += '<span>Less</span>';
        for (let i = 0; i <= 4; i++) {
            const intensity = i / 4;
            const bgColor = getHeatmapColor(intensity);
            html += `<div class="w-4 h-4 rounded-sm" style="background-color: ${bgColor};"></div>`;
        }
        html += '<span>More</span>';
        html += '</div>';

        container.innerHTML = html;
        console.log('Activity heatmap rendered');
    }

    function getHeatmapColor(intensity) {
        if (intensity === 0) return '#2f3336';
        if (intensity < 0.25) return 'rgba(9, 142, 207, 0.2)';
        if (intensity < 0.5) return 'rgba(9, 142, 207, 0.4)';
        if (intensity < 0.75) return 'rgba(9, 142, 207, 0.6)';
        return 'rgba(9, 142, 207, 0.8)';
    }

    /**
     * Initialize all server analytics charts.
     */
    function init() {
        const dataElement = document.getElementById('serverAnalyticsChartData');
        if (!dataElement) {
            console.log('Server analytics chart data not found on this page');
            return;
        }

        try {
            const chartData = JSON.parse(dataElement.textContent);

            if (chartData.timeSeries && chartData.timeSeries.length > 0) {
                initActivityTimeSeriesChart(chartData.timeSeries);
            }

            if (chartData.heatmap && chartData.heatmap.length > 0) {
                renderActivityHeatmap(chartData.heatmap);
            }

            if (chartData.topChannels && chartData.topChannels.length > 0) {
                initTopChannelsChart(chartData.topChannels);
            }

            console.log('Server analytics charts initialized');

        } catch (error) {
            console.error('Failed to initialize server analytics charts:', error);
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})();
