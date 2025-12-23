// command-analytics.js
// Command analytics dashboard with multiple Chart.js visualizations

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
    let usageOverTimeChart = null;
    let topCommandsChart = null;
    let successRateChart = null;
    let responseTimeChart = null;

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
     * Initializes the Usage Over Time line chart.
     * @param {Array} usageData - Array of {date, count} objects
     */
    function initUsageOverTimeChart(usageData) {
        const canvas = document.getElementById('usageOverTimeChart');
        if (!canvas || !usageData || usageData.length === 0) {
            return;
        }

        const ctx = canvas.getContext('2d');
        const labels = usageData.map(item => formatDate(item.date));
        const data = usageData.map(item => item.count);

        // Create gradient for area fill
        const gradient = ctx.createLinearGradient(0, 0, 0, 300);
        gradient.addColorStop(0, 'rgba(203, 78, 27, 0.3)');
        gradient.addColorStop(1, 'rgba(203, 78, 27, 0.0)');

        usageOverTimeChart = new Chart(ctx, {
            type: 'line',
            data: {
                labels: labels,
                datasets: [{
                    label: 'Commands',
                    data: data,
                    borderColor: CHART_COLORS[0],
                    backgroundColor: gradient,
                    borderWidth: 2,
                    fill: true,
                    tension: 0.3,
                    pointRadius: 4,
                    pointBackgroundColor: CHART_COLORS[0],
                    pointBorderColor: BG_COLORS.primary,
                    pointBorderWidth: 2,
                    pointHoverRadius: 6,
                }]
            },
            options: {
                ...commonOptions,
                plugins: {
                    ...commonOptions.plugins,
                    tooltip: {
                        ...commonOptions.plugins.tooltip,
                        callbacks: {
                            title: function (context) {
                                // Return full date from original data
                                return usageData[context[0].dataIndex].date;
                            },
                            label: function (context) {
                                return `Commands: ${formatNumber(context.parsed.y)}`;
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

        console.log('Usage over time chart initialized');
    }

    /**
     * Initializes the Top Commands horizontal bar chart.
     * @param {Array} topCommandsData - Array of {commandName, count, percentage} objects
     */
    function initTopCommandsChart(topCommandsData) {
        const canvas = document.getElementById('topCommandsChart');
        if (!canvas || !topCommandsData || topCommandsData.length === 0) {
            return;
        }

        const ctx = canvas.getContext('2d');
        const labels = topCommandsData.map(item => item.commandName);
        const data = topCommandsData.map(item => item.count);
        const percentages = topCommandsData.map(item => item.percentage);

        // Assign colors cycling through CHART_COLORS
        const colors = labels.map((_, index) => CHART_COLORS[index % CHART_COLORS.length]);

        topCommandsChart = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [{
                    label: 'Usage Count',
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
                                const percentage = percentages[context.dataIndex];
                                return `Count: ${formatNumber(value)} (${percentage.toFixed(1)}%)`;
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
                                if (value >= 1000) {
                                    return (value / 1000).toFixed(1) + 'k';
                                }
                                return value;
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
                                size: 13,
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

        console.log('Top commands chart initialized');
    }

    /**
     * Initializes the Success Rate doughnut chart.
     * @param {Object} successRateData - Object with {successCount, failureCount, successRate}
     */
    function initSuccessRateChart(successRateData) {
        const canvas = document.getElementById('successRateChart');
        if (!canvas || !successRateData) {
            return;
        }

        const ctx = canvas.getContext('2d');
        const successPercentage = successRateData.successRate.toFixed(1);

        successRateChart = new Chart(ctx, {
            type: 'doughnut',
            data: {
                labels: ['Success', 'Failure'],
                datasets: [{
                    data: [successRateData.successCount, successRateData.failureCount],
                    backgroundColor: [CHART_COLORS[2], CHART_COLORS[5]], // success green, error red
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
                                const total = successRateData.successCount + successRateData.failureCount;
                                const percentage = total > 0 ? ((value / total) * 100).toFixed(1) : '0.0';
                                return `${label}: ${formatNumber(value)} (${percentage}%)`;
                            }
                        }
                    },
                    centerText: {
                        text: `${successPercentage}%`
                    }
                }
            },
            plugins: [centerTextPlugin]
        });

        console.log('Success rate chart initialized');
    }

    /**
     * Initializes the Response Time horizontal bar chart.
     * @param {Array} responseTimeData - Array of {commandName, avgResponseTimeMs} objects
     */
    function initResponseTimeChart(responseTimeData) {
        const canvas = document.getElementById('responseTimeChart');
        if (!canvas || !responseTimeData || responseTimeData.length === 0) {
            return;
        }

        const ctx = canvas.getContext('2d');
        const labels = responseTimeData.map(item => item.commandName);
        const data = responseTimeData.map(item => item.avgResponseTimeMs);

        // Assign colors cycling through CHART_COLORS
        const colors = labels.map((_, index) => CHART_COLORS[index % CHART_COLORS.length]);

        responseTimeChart = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [{
                    label: 'Avg Response Time',
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
                                return `Response Time: ${value.toFixed(2)} ms`;
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
                                return value.toFixed(0) + ' ms';
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
                                size: 13,
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

        console.log('Response time chart initialized');
    }

    /**
     * Initialize all command analytics charts.
     */
    function init() {
        // Load chart data from embedded JSON
        const dataElement = document.getElementById('analyticsChartData');
        if (!dataElement) {
            console.log('Analytics chart data not found on this page');
            return;
        }

        try {
            const chartData = JSON.parse(dataElement.textContent);

            // Initialize each chart if data is available
            if (chartData.usageOverTime && chartData.usageOverTime.length > 0) {
                initUsageOverTimeChart(chartData.usageOverTime);
            }

            if (chartData.topCommands && chartData.topCommands.length > 0) {
                initTopCommandsChart(chartData.topCommands);
            }

            if (chartData.successRate) {
                initSuccessRateChart(chartData.successRate);
            }

            if (chartData.responseTime && chartData.responseTime.length > 0) {
                initResponseTimeChart(chartData.responseTime);
            }

            console.log('Command analytics charts initialized');

        } catch (error) {
            console.error('Failed to initialize command analytics charts:', error);
        }
    }

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})();
