// command-stats-chart.js
// Command usage statistics chart with Chart.js and dynamic time range filtering

(function () {
    'use strict';

    // Configuration
    const API_ENDPOINT = '/api/commandlogs/stats';

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

    // Chart instance reference
    let commandChart = null;

    /**
     * Formats a number with thousands separator.
     * @param {number} num - The number to format
     * @returns {string} Formatted number (e.g., "1,234")
     */
    function formatNumber(num) {
        return num.toString().replace(/\B(?=(\d{3})+(?!\d))/g, ',');
    }

    /**
     * Calculates the DateTime to pass to the API based on time range in hours.
     * @param {number|null} hours - The time range in hours, or null for all time
     * @returns {string|null} ISO date string or null for all time
     */
    function calculateSinceDate(hours) {
        if (!hours) {
            return null;
        }
        const date = new Date();
        date.setHours(date.getHours() - hours);
        return date.toISOString();
    }

    /**
     * Initializes the Chart.js horizontal bar chart.
     * @param {Object} initialData - Initial chart data from embedded JSON
     */
    function initChart(initialData) {
        const canvas = document.getElementById('commandUsageChart');
        if (!canvas) {
            console.warn('Command usage chart canvas not found');
            return;
        }

        const ctx = canvas.getContext('2d');
        const totalCount = initialData.totalCommands;

        commandChart = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: initialData.labels,
                datasets: [{
                    label: 'Usage Count',
                    data: initialData.counts,
                    backgroundColor: CHART_COLORS,
                    borderColor: CHART_COLORS,
                    borderWidth: 1,
                    borderRadius: 4,
                    barThickness: 24
                }]
            },
            options: {
                indexAxis: 'y',
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        display: false
                    },
                    tooltip: {
                        backgroundColor: '#2f3336',
                        titleColor: '#d7d3d0',
                        bodyColor: '#a8a5a3',
                        borderColor: '#3f4447',
                        borderWidth: 1,
                        padding: 12,
                        cornerRadius: 6,
                        displayColors: true,
                        callbacks: {
                            label: function (context) {
                                const value = context.parsed.x;
                                const percentage = totalCount > 0 ? ((value / totalCount) * 100).toFixed(1) : '0.0';
                                return `Count: ${formatNumber(value)} (${percentage}%)`;
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        beginAtZero: true,
                        grid: {
                            color: 'rgba(63, 68, 71, 0.5)',
                            drawBorder: false
                        },
                        ticks: {
                            color: '#7a7876',
                            font: {
                                size: 12
                            },
                            callback: function (value) {
                                if (value >= 1000) {
                                    return (value / 1000).toFixed(1) + 'k';
                                }
                                return value;
                            }
                        }
                    },
                    y: {
                        grid: {
                            display: false
                        },
                        ticks: {
                            color: '#d7d3d0',
                            font: {
                                size: 13,
                                family: 'ui-monospace, SFMono-Regular, "SF Mono", Menlo, Monaco, Consolas, "Liberation Mono", "Courier New", monospace'
                            },
                            padding: 8
                        }
                    }
                },
                animation: {
                    duration: 500,
                    easing: 'easeOutQuart'
                }
            }
        });

        console.log('Command usage chart initialized');
    }

    /**
     * Shows the chart container and hides the empty state.
     */
    function showChartContainer() {
        const chartContainer = document.querySelector('#commandUsageChart')?.parentElement;
        const emptyState = document.querySelector('[data-command-stats-card] [data-empty-state]');
        if (chartContainer) {
            chartContainer.style.display = '';
        }
        if (emptyState) {
            emptyState.style.display = 'none';
        }
    }

    /**
     * Hides the chart container and shows the empty state.
     */
    function showEmptyState() {
        const chartContainer = document.querySelector('#commandUsageChart')?.parentElement;
        const emptyState = document.querySelector('[data-command-stats-card] [data-empty-state]');
        if (chartContainer) {
            chartContainer.style.display = 'none';
        }
        if (emptyState) {
            emptyState.style.display = '';
        }
    }

    /**
     * Updates the chart with new data from the API.
     * @param {number|null} timeRangeHours - Time range in hours, or null for all time
     */
    async function updateChart(timeRangeHours) {
        const card = document.querySelector('[data-command-stats-card]');
        if (!card) {
            console.warn('Command stats card not found');
            return;
        }

        try {
            // Build API URL with query parameters
            const sinceDate = calculateSinceDate(timeRangeHours);
            const url = sinceDate ? `${API_ENDPOINT}?since=${encodeURIComponent(sinceDate)}` : API_ENDPOINT;

            const response = await fetch(url);
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }

            const stats = await response.json();

            // Transform API response into chart data
            const sortedStats = Object.entries(stats)
                .sort((a, b) => b[1] - a[1])
                .slice(0, 10);

            const labels = sortedStats.map(([name]) => name);
            const counts = sortedStats.map(([, count]) => count);
            const totalCommands = counts.reduce((sum, count) => sum + count, 0);

            // Handle chart visibility based on data availability
            if (labels.length > 0) {
                showChartContainer();

                // Initialize chart if it doesn't exist yet
                if (!commandChart) {
                    initChart({
                        labels: labels,
                        counts: counts,
                        totalCommands: totalCommands
                    });
                } else {
                    // Update existing chart
                    commandChart.data.labels = labels;
                    commandChart.data.datasets[0].data = counts;

                    // Update tooltip callback with new total
                    commandChart.options.plugins.tooltip.callbacks.label = function (context) {
                        const value = context.parsed.x;
                        const percentage = totalCommands > 0 ? ((value / totalCommands) * 100).toFixed(1) : '0.0';
                        return `Count: ${formatNumber(value)} (${percentage}%)`;
                    };

                    commandChart.update('active');
                }
            } else {
                // No data - show empty state
                showEmptyState();
                if (commandChart) {
                    commandChart.destroy();
                    commandChart = null;
                }
            }

            // Update total count display
            const totalCountElement = card.querySelector('[data-total-count]');
            if (totalCountElement) {
                totalCountElement.textContent = formatNumber(totalCommands);
            }

            // Update command count label
            const commandCountLabel = card.querySelector('[data-command-count-label]');
            if (commandCountLabel) {
                if (labels.length > 0) {
                    commandCountLabel.textContent = `Showing top ${labels.length} commands`;
                } else {
                    commandCountLabel.textContent = 'No commands to display';
                }
            }

            console.log(`Chart updated for time range: ${timeRangeHours ? timeRangeHours + 'h' : 'all time'}`);

        } catch (error) {
            console.error('Failed to update command stats chart:', error);
            // Optionally show error state in UI
            const totalCountElement = card.querySelector('[data-total-count]');
            if (totalCountElement) {
                totalCountElement.textContent = 'Error';
                totalCountElement.classList.add('text-error');
            }
        }
    }

    /**
     * Initialize the command stats chart functionality.
     */
    function init() {
        // Check if command stats card exists on the page
        const card = document.querySelector('[data-command-stats-card]');
        if (!card) {
            return;
        }

        // Load initial data from embedded JSON
        const initialDataElement = document.querySelector('[data-command-stats-initial]');
        if (!initialDataElement) {
            console.error('Initial chart data not found');
            return;
        }

        try {
            const initialData = JSON.parse(initialDataElement.textContent);

            // Only initialize chart if there's data
            if (initialData.labels && initialData.labels.length > 0) {
                initChart(initialData);
            } else {
                console.log('No command data available, chart not initialized');
            }

            // Set up time range selector event listener
            const timeRangeSelector = card.querySelector('[data-time-range-selector]');
            if (timeRangeSelector) {
                timeRangeSelector.addEventListener('change', function () {
                    const hours = this.value ? parseInt(this.value, 10) : null;
                    updateChart(hours);
                });
            }

            console.log('Command stats chart functionality initialized');

        } catch (error) {
            console.error('Failed to initialize command stats chart:', error);
        }
    }

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})();
