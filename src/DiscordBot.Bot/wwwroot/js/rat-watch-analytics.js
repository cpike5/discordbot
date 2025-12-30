// rat-watch-analytics.js
// Rat Watch analytics dashboard with Chart.js visualizations and activity heatmap

(function () {
    'use strict';

    // Design system chart colors
    const CHART_COLORS = [
        '#cb4e1b',  // accent-orange
        '#098ecf',  // accent-blue
        '#10b981',  // success
        '#f59e0b',  // warning
        '#06b6d4',  // info
        '#ef4444',  // error
        '#8b5cf6',  // purple
        '#ec4899',  // pink
        '#14b8a6',  // teal
        '#6366f1',  // indigo
    ];

    const BG_COLORS = {
        primary: '#1d2022',
        secondary: '#262a2d',
        tertiary: '#2f3336',
    };

    const TEXT_COLORS = {
        primary: '#d7d3d0',
        secondary: '#a8a5a3',
        tertiary: '#7a7876',
    };

    const BORDER_COLOR = '#3f4447';

    // Chart instance references
    let watchesOverTimeChart = null;
    let outcomeDistributionChart = null;
    let topUsersChart = null;

    /**
     * Common Chart.js options for all charts.
     */
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

    /**
     * Common grid styling for chart axes.
     */
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

    /**
     * Formats a number with thousands separator.
     * @param {number} num - The number to format
     * @returns {string} Formatted number (e.g., "1,234")
     */
    function formatNumber(num) {
        return num.toString().replace(/\B(?=(\d{3})+(?!\d))/g, ',');
    }

    /**
     * Formats a date string to short format (e.g., "Jan 15").
     * @param {string} dateStr - ISO date string
     * @returns {string} Formatted date
     */
    function formatDate(dateStr) {
        const date = new Date(dateStr);
        const monthNames = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
            'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
        return `${monthNames[date.getMonth()]} ${date.getDate()}`;
    }

    /**
     * Plugin to display center text in doughnut chart.
     */
    const centerTextPlugin = {
        id: 'centerText',
        afterDatasetsDraw: function (chart) {
            if (!chart.config.options.plugins.centerText) {
                return;
            }

            const { ctx, chartArea: { width, height } } = chart;
            const centerX = width / 2;
            const centerY = height / 2;
            const text = chart.config.options.plugins.centerText.text;

            ctx.save();
            ctx.font = 'bold 32px ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto';
            ctx.fillStyle = TEXT_COLORS.primary;
            ctx.textAlign = 'center';
            ctx.textBaseline = 'middle';
            ctx.fillText(text, centerX, centerY);
            ctx.restore();
        }
    };

    /**
     * Initializes the Watches Over Time line chart.
     * @param {Array} timeSeriesData - Array of {date, totalCount, guiltyCount, clearedCount} objects
     */
    function initWatchesOverTimeChart(timeSeriesData) {
        const canvas = document.getElementById('watchesOverTimeChart');
        if (!canvas || !timeSeriesData || timeSeriesData.length === 0) {
            return;
        }

        const ctx = canvas.getContext('2d');
        const labels = timeSeriesData.map(item => formatDate(item.date));
        const totalData = timeSeriesData.map(item => item.totalCount);
        const guiltyData = timeSeriesData.map(item => item.guiltyCount);
        const clearedData = timeSeriesData.map(item => item.clearedCount);

        // Create gradients
        const totalGradient = ctx.createLinearGradient(0, 0, 0, 300);
        totalGradient.addColorStop(0, 'rgba(203, 78, 27, 0.3)');
        totalGradient.addColorStop(1, 'rgba(203, 78, 27, 0.0)');

        watchesOverTimeChart = new Chart(ctx, {
            type: 'line',
            data: {
                labels: labels,
                datasets: [
                    {
                        label: 'Total Watches',
                        data: totalData,
                        borderColor: CHART_COLORS[0],
                        backgroundColor: totalGradient,
                        borderWidth: 2,
                        fill: true,
                        tension: 0.3,
                        pointRadius: 4,
                        pointBackgroundColor: CHART_COLORS[0],
                        pointBorderColor: BG_COLORS.primary,
                        pointBorderWidth: 2,
                        pointHoverRadius: 6,
                    },
                    {
                        label: 'Guilty',
                        data: guiltyData,
                        borderColor: CHART_COLORS[5],
                        backgroundColor: 'transparent',
                        borderWidth: 2,
                        fill: false,
                        tension: 0.3,
                        pointRadius: 3,
                        pointBackgroundColor: CHART_COLORS[5],
                        pointBorderColor: BG_COLORS.primary,
                        pointBorderWidth: 2,
                        pointHoverRadius: 5,
                        borderDash: [5, 5],
                    },
                    {
                        label: 'Cleared Early',
                        data: clearedData,
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
                        borderDash: [2, 2],
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
                            font: {
                                size: 12,
                            },
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
                        beginAtZero: true,
                        grid: gridConfig,
                        ticks: {
                            ...ticksConfig,
                            callback: function (value) {
                                if (value >= 1000) {
                                    return (value / 1000).toFixed(1) + 'k';
                                }
                                return value;
                            }
                        },
                    }
                },
                interaction: {
                    intersect: false,
                    mode: 'index',
                }
            }
        });

        console.log('Watches over time chart initialized');
    }

    /**
     * Initializes the Outcome Distribution doughnut chart.
     * @param {Object} outcomeData - Object with {guiltyCount, clearedEarlyCount, activeCount, otherCount}
     */
    function initOutcomeDistributionChart(outcomeData) {
        const canvas = document.getElementById('outcomeDistributionChart');
        if (!canvas || !outcomeData) {
            return;
        }

        const ctx = canvas.getContext('2d');
        const total = outcomeData.guiltyCount + outcomeData.clearedEarlyCount +
                     outcomeData.activeCount + outcomeData.otherCount;
        const guiltyPercentage = total > 0 ? ((outcomeData.guiltyCount / total) * 100).toFixed(1) : '0.0';

        outcomeDistributionChart = new Chart(ctx, {
            type: 'doughnut',
            data: {
                labels: ['Guilty', 'Cleared Early', 'Active', 'Other'],
                datasets: [{
                    data: [
                        outcomeData.guiltyCount,
                        outcomeData.clearedEarlyCount,
                        outcomeData.activeCount,
                        outcomeData.otherCount
                    ],
                    backgroundColor: [
                        CHART_COLORS[5], // error red for guilty
                        CHART_COLORS[2], // success green for cleared
                        CHART_COLORS[1], // blue for active
                        CHART_COLORS[3]  // warning orange for other
                    ],
                    borderColor: BG_COLORS.primary,
                    borderWidth: 3,
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                cutout: '70%',
                plugins: {
                    legend: {
                        display: true,
                        position: 'bottom',
                        labels: {
                            color: TEXT_COLORS.primary,
                            padding: 16,
                            font: {
                                size: 13,
                            },
                            usePointStyle: true,
                            pointStyle: 'circle',
                        }
                    },
                    tooltip: {
                        ...commonOptions.plugins.tooltip,
                        callbacks: {
                            label: function (context) {
                                const label = context.label || '';
                                const value = context.parsed;
                                const percentage = total > 0 ? ((value / total) * 100).toFixed(1) : '0.0';
                                return `${label}: ${formatNumber(value)} (${percentage}%)`;
                            }
                        }
                    },
                    centerText: {
                        text: `${guiltyPercentage}%`
                    }
                }
            },
            plugins: [centerTextPlugin]
        });

        console.log('Outcome distribution chart initialized');
    }

    /**
     * Initializes the Top Watched Users horizontal bar chart.
     * @param {Array} topUsersData - Array of {userId, watchesAgainst, guiltyCount} objects
     */
    function initTopUsersChart(topUsersData) {
        const canvas = document.getElementById('topUsersChart');
        if (!canvas || !topUsersData || topUsersData.length === 0) {
            return;
        }

        const ctx = canvas.getContext('2d');
        const labels = topUsersData.map(item => `User ${item.userId.toString().substring(0, 8)}`);
        const data = topUsersData.map(item => item.watchesAgainst);
        const guiltyData = topUsersData.map(item => item.guiltyCount);

        // Assign colors cycling through CHART_COLORS
        const colors = labels.map((_, index) => CHART_COLORS[index % CHART_COLORS.length]);

        topUsersChart = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [{
                    label: 'Total Watches',
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
                        displayColors: true,
                        callbacks: {
                            label: function (context) {
                                const value = context.parsed.x;
                                const guilty = guiltyData[context.dataIndex];
                                return `Watches: ${formatNumber(value)} (${guilty} guilty)`;
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
                                return Math.floor(value);
                            }
                        },
                    },
                    y: {
                        grid: {
                            display: false,
                        },
                        ticks: {
                            color: TEXT_COLORS.primary,
                            font: {
                                size: 12,
                                family: 'ui-monospace, SFMono-Regular, "SF Mono", Menlo, Monaco, Consolas, "Liberation Mono", "Courier New", monospace',
                            },
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

        console.log('Top users chart initialized');
    }

    /**
     * Renders the activity heatmap.
     * @param {Array} heatmapData - Array of {dayOfWeek, hour, count} objects
     */
    function renderActivityHeatmap(heatmapData) {
        const container = document.getElementById('activityHeatmap');
        if (!container || !heatmapData || heatmapData.length === 0) {
            return;
        }

        const days = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
        const hours = Array.from({ length: 24 }, (_, i) => i);

        // Create a map for quick lookup
        const dataMap = new Map();
        let maxCount = 0;
        heatmapData.forEach(item => {
            const key = `${item.dayOfWeek}-${item.hour}`;
            dataMap.set(key, item.count);
            maxCount = Math.max(maxCount, item.count);
        });

        // Build heatmap HTML
        let html = '<div class="text-xs text-text-tertiary mb-2">Activity by day and hour (UTC)</div>';
        html += '<div class="grid grid-cols-[auto_repeat(24,minmax(0,1fr))] gap-1 text-xs">';

        // Header row (hours)
        html += '<div></div>'; // Empty corner
        hours.forEach(hour => {
            html += `<div class="text-center text-text-tertiary">${hour % 6 === 0 ? hour : ''}</div>`;
        });

        // Data rows
        days.forEach((day, dayIndex) => {
            html += `<div class="text-text-secondary font-medium pr-2">${day}</div>`;
            hours.forEach(hour => {
                const key = `${dayIndex}-${hour}`;
                const count = dataMap.get(key) || 0;
                const intensity = maxCount > 0 ? count / maxCount : 0;
                const bgColor = getHeatmapColor(intensity);
                const title = `${day} ${hour}:00 - ${count} watches`;
                html += `<div class="aspect-square rounded-sm" style="background-color: ${bgColor};" title="${title}"></div>`;
            });
        });

        html += '</div>';

        // Add legend
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

    /**
     * Gets a color based on heatmap intensity.
     * @param {number} intensity - Value between 0 and 1
     * @returns {string} Hex color
     */
    function getHeatmapColor(intensity) {
        if (intensity === 0) return '#2f3336'; // bg-tertiary
        if (intensity < 0.25) return 'rgba(203, 78, 27, 0.2)'; // light orange
        if (intensity < 0.5) return 'rgba(203, 78, 27, 0.4)';
        if (intensity < 0.75) return 'rgba(203, 78, 27, 0.6)';
        return 'rgba(203, 78, 27, 0.8)'; // full orange
    }

    /**
     * Initialize all Rat Watch analytics charts.
     */
    function init() {
        // Load chart data from embedded JSON
        const dataElement = document.getElementById('ratWatchChartData');
        if (!dataElement) {
            console.log('Rat Watch chart data not found on this page');
            return;
        }

        try {
            const chartData = JSON.parse(dataElement.textContent);

            // Initialize each chart if data is available
            if (chartData.timeSeries && chartData.timeSeries.length > 0) {
                initWatchesOverTimeChart(chartData.timeSeries);
            }

            if (chartData.outcomeDistribution) {
                initOutcomeDistributionChart(chartData.outcomeDistribution);
            }

            if (chartData.heatmap && chartData.heatmap.length > 0) {
                renderActivityHeatmap(chartData.heatmap);
            }

            if (chartData.topUsers && chartData.topUsers.length > 0) {
                initTopUsersChart(chartData.topUsers);
            }

            console.log('Rat Watch analytics charts initialized');

        } catch (error) {
            console.error('Failed to initialize Rat Watch analytics charts:', error);
        }
    }

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})();
