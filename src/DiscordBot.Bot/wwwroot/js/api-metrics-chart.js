// api-metrics-chart.js
// API latency chart with Chart.js for the API Metrics performance page

(function () {
    'use strict';

    // Configuration
    const API_LATENCY_ENDPOINT = '/api/metrics/api/latency';

    // Chart instance reference
    let latencyChart = null;

    /**
     * Gets the number of hours from the global window variable set by the page.
     * @returns {number} Number of hours for the time range
     */
    function getHoursFromPage() {
        return window.apiMetricsHours || 24;
    }

    /**
     * Formats a timestamp for chart labels based on the time range.
     * @param {Date} date - The date to format
     * @param {number} hours - The time range in hours
     * @returns {string} Formatted label
     */
    function formatChartLabel(date, hours) {
        if (hours >= 168) {
            // For 7+ days, show "Mon 15" format
            return date.toLocaleDateString('en-US', { weekday: 'short', day: 'numeric' });
        } else {
            // For shorter ranges, show "14:30" format
            return date.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit', hour12: false });
        }
    }

    /**
     * Initializes the API latency chart with Chart.js.
     * @param {Array} samples - Array of latency samples from the API
     * @param {Object} statistics - Aggregate statistics for the period
     */
    function initChart(samples, statistics) {
        const canvas = document.getElementById('apiLatencyChart');
        if (!canvas) {
            console.warn('API latency chart canvas not found');
            return;
        }

        const ctx = canvas.getContext('2d');
        const hours = getHoursFromPage();

        // Extract data from samples
        const labels = samples.map(s => formatChartLabel(new Date(s.timestamp), hours));
        const avgData = samples.map(s => s.avgLatencyMs);
        const p95Data = samples.map(s => s.p95LatencyMs);

        // Destroy existing chart if it exists
        if (latencyChart) {
            latencyChart.destroy();
        }

        latencyChart = new Chart(ctx, {
            type: 'line',
            data: {
                labels: labels,
                datasets: [
                    {
                        label: 'Average Latency',
                        data: avgData,
                        borderColor: '#098ecf',
                        backgroundColor: 'rgba(9, 142, 207, 0.1)',
                        fill: true,
                        tension: 0.4,
                        pointRadius: 2,
                        pointHoverRadius: 5
                    },
                    {
                        label: 'P95 Latency',
                        data: p95Data,
                        borderColor: '#f59e0b',
                        backgroundColor: 'transparent',
                        fill: false,
                        tension: 0.4,
                        pointRadius: 2,
                        pointHoverRadius: 5,
                        borderDash: [5, 5]
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: {
                    mode: 'index',
                    intersect: false
                },
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: {
                            boxWidth: 12,
                            padding: 20,
                            color: '#a8a5a3'
                        }
                    },
                    tooltip: {
                        backgroundColor: '#2f3336',
                        titleColor: '#d7d3d0',
                        bodyColor: '#a8a5a3',
                        borderColor: '#3f4447',
                        borderWidth: 1,
                        padding: 12,
                        callbacks: {
                            label: function (context) {
                                return context.dataset.label + ': ' + context.parsed.y.toFixed(1) + ' ms';
                            }
                        }
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        grid: {
                            color: '#2f3336'
                        },
                        ticks: {
                            color: '#a8a5a3',
                            callback: function (value) {
                                return value + ' ms';
                            }
                        }
                    },
                    x: {
                        grid: {
                            display: false
                        },
                        ticks: {
                            color: '#a8a5a3',
                            maxRotation: 45,
                            minRotation: 0
                        }
                    }
                }
            }
        });

        console.log('API latency chart initialized with', samples.length, 'samples');
    }

    /**
     * Loads latency data from the API and initializes/updates the chart.
     */
    async function loadChartData() {
        try {
            const hours = getHoursFromPage();
            const url = `${API_LATENCY_ENDPOINT}?hours=${hours}`;

            const response = await fetch(url);
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }

            const data = await response.json();

            // Check if we have data
            if (!data.samples || data.samples.length === 0) {
                console.warn('No latency samples available');
                return;
            }

            initChart(data.samples, data.statistics);

        } catch (error) {
            console.error('Failed to load API latency chart data:', error);
        }
    }

    /**
     * Initialize the chart on page load.
     */
    function init() {
        // Check if we're on the API metrics page
        const canvas = document.getElementById('apiLatencyChart');
        if (!canvas) {
            return;
        }

        // Set Chart.js defaults for dark theme
        Chart.defaults.color = '#a8a5a3';
        Chart.defaults.borderColor = '#3f4447';

        // Load chart data
        loadChartData();

        // Auto-refresh every 30 seconds
        setInterval(loadChartData, 30000);

        console.log('API metrics chart initialized');
    }

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})();
