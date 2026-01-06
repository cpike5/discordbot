/**
 * Performance Dashboard - Chart Utilities
 * Shared Chart.js configuration and helper functions
 */
(function() {
    'use strict';

    window.Performance = window.Performance || {};

    const ChartUtils = {
        // Default Chart.js theme configuration for dark mode
        defaultOptions: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    labels: {
                        boxWidth: 12,
                        padding: 20
                    }
                },
                tooltip: {
                    backgroundColor: '#2f3336',
                    titleColor: '#d7d3d0',
                    bodyColor: '#a8a5a3',
                    borderColor: '#3f4447',
                    borderWidth: 1,
                    padding: 12
                }
            },
            scales: {
                y: {
                    grid: { color: '#2f3336' }
                },
                x: {
                    grid: { display: false }
                }
            }
        },

        // Color palette
        colors: {
            primary: '#098ecf',
            secondary: '#cb4e1b',
            success: '#10b981',
            warning: '#f59e0b',
            error: '#ef4444',
            info: '#3b82f6',
            muted: 'rgba(47, 51, 54, 0.8)'
        },

        /**
         * Format timestamp for chart labels based on time range
         */
        formatLabel: function(isoString, hours) {
            const date = new Date(isoString);
            if (hours <= 24) {
                const month = date.toLocaleDateString('en-US', { month: 'short' });
                const day = date.getDate();
                const hour = date.toLocaleTimeString('en-US', { hour: 'numeric', hour12: true });
                return `${month} ${day}, ${hour}`;
            } else {
                return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
            }
        },

        /**
         * Get granularity for API calls based on time range
         */
        getGranularity: function(hours) {
            return hours <= 24 ? 'hour' : 'day';
        },

        /**
         * Create a line chart with standard configuration
         */
        createLineChart: function(ctx, labels, datasets, options) {
            const mergedOptions = this.mergeOptions(this.defaultOptions, options || {});
            return new Chart(ctx, {
                type: 'line',
                data: { labels, datasets },
                options: mergedOptions
            });
        },

        /**
         * Create a bar chart with standard configuration
         */
        createBarChart: function(ctx, labels, datasets, options) {
            const mergedOptions = this.mergeOptions(this.defaultOptions, options || {});
            return new Chart(ctx, {
                type: 'bar',
                data: { labels, datasets },
                options: mergedOptions
            });
        },

        /**
         * Create a semi-circular gauge chart
         */
        createGaugeChart: function(ctx, value, maxValue, thresholds, colorScheme) {
            const percentage = Math.min((value / maxValue) * 100, 100);
            const remaining = 100 - percentage;
            const colors = colorScheme || ['#10b981', '#f59e0b', '#ef4444'];
            const gaugeColor = this.getThresholdColor(value, thresholds, colors);

            return new Chart(ctx, {
                type: 'doughnut',
                data: {
                    datasets: [{
                        data: [percentage, remaining],
                        backgroundColor: [gaugeColor, this.colors.muted],
                        borderWidth: 0,
                        circumference: 180,
                        rotation: 270
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    cutout: '75%',
                    plugins: {
                        legend: { display: false },
                        tooltip: { enabled: false }
                    }
                }
            });
        },

        /**
         * Get color based on value and thresholds
         */
        getThresholdColor: function(value, thresholds, colors) {
            for (let i = thresholds.length - 1; i >= 0; i--) {
                if (value >= thresholds[i]) {
                    return colors[i];
                }
            }
            return colors[0];
        },

        /**
         * Deep merge options objects
         */
        mergeOptions: function(base, override) {
            const result = { ...base };
            for (const key of Object.keys(override)) {
                if (typeof override[key] === 'object' && !Array.isArray(override[key]) && override[key] !== null) {
                    result[key] = this.mergeOptions(base[key] || {}, override[key]);
                } else {
                    result[key] = override[key];
                }
            }
            return result;
        },

        /**
         * Show error state in chart container
         */
        showChartError: function(chartId, message) {
            const container = document.getElementById(chartId)?.parentElement;
            if (!container) return;

            container.innerHTML = `
                <div class="flex flex-col items-center justify-center h-full text-center p-4">
                    <svg class="w-8 h-8 text-error mb-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
                    </svg>
                    <div class="text-error font-medium">Failed to load chart</div>
                    <div class="text-text-secondary text-sm mt-1">${this.escapeHtml(message)}</div>
                </div>
            `;
        },

        /**
         * Escape HTML for safe insertion
         */
        escapeHtml: function(text) {
            const div = document.createElement('div');
            div.textContent = text || '';
            return div.innerHTML;
        },

        /**
         * Safely destroy a chart instance
         */
        destroyChart: function(chart) {
            if (chart && typeof chart.destroy === 'function') {
                chart.destroy();
            }
        },

        /**
         * Destroy multiple charts
         */
        destroyCharts: function(charts) {
            if (Array.isArray(charts)) {
                charts.forEach(c => this.destroyChart(c));
            }
        }
    };

    window.Performance.ChartUtils = ChartUtils;
})();
