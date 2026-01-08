/**
 * Commands Performance Real-time Updates
 * Manages SignalR subscription for live command performance updates.
 * Part of issue #630 - Add real-time streaming updates to Command Performance page.
 */
const CommandsRealtime = (function() {
    'use strict';

    // Configuration
    const POLLING_FALLBACK_INTERVAL = 30000; // 30 seconds
    const MAX_CHART_POINTS = 100; // Sliding window size for chart data

    // State
    let isSubscribed = false;
    let pollingFallbackId = null;

    // Chart references (set by page initialization)
    let responseTimeChart = null;
    let throughputChart = null;
    let errorRateChart = null;

    /**
     * Initialize real-time updates for command performance.
     */
    async function initialize() {
        try {
            // Connect to SignalR hub
            const connected = await DashboardHub.connect();
            if (!connected) {
                console.warn('[CommandsRealtime] SignalR connection failed, falling back to polling');
                startPollingFallback();
                return;
            }

            // Register event handlers
            DashboardHub.on('CommandPerformanceUpdate', handleCommandPerformanceUpdate);
            DashboardHub.on('reconnected', handleReconnected);
            DashboardHub.on('disconnected', handleDisconnected);

            // Join the performance group
            await DashboardHub.joinPerformanceGroup();
            isSubscribed = true;

            // Get initial data via on-demand request
            const metrics = await DashboardHub.invoke('GetCurrentCommandPerformance', window.selectedHours || 24);
            if (metrics) {
                updateSummaryCards(metrics);
            }

            // Show live indicator
            showLiveIndicator();

            console.log('[CommandsRealtime] Initialized with SignalR');
        } catch (error) {
            console.error('[CommandsRealtime] Initialization error:', error);
            startPollingFallback();
        }
    }

    /**
     * Set chart references for streaming updates.
     * @param {Object} charts - Object with chart references
     */
    function setChartReferences(charts) {
        responseTimeChart = charts.responseTime || null;
        throughputChart = charts.throughput || null;
        errorRateChart = charts.errorRate || null;
    }

    /**
     * Handle incoming command performance update from SignalR.
     * @param {Object} data - CommandPerformanceUpdateDto
     */
    function handleCommandPerformanceUpdate(data) {
        updateSummaryCards(data);

        // Append data points to charts if available
        if (responseTimeChart) {
            appendToResponseTimeChart(data.avgResponseTimeMs, data.p95ResponseTimeMs, data.p99ResponseTimeMs, data.timestamp);
        }
        if (throughputChart) {
            appendToThroughputChart(data.commandsLastHour, data.timestamp);
        }
        if (errorRateChart) {
            appendToErrorRateChart(data.errorRate, data.timestamp);
        }

        flashLiveIndicator();
    }

    /**
     * Update summary metric cards with new data.
     * @param {Object} data - Command performance data object
     */
    function updateSummaryCards(data) {
        // Average response time
        animateValueChange('avgResponseTimeMs', formatMs(data.avgResponseTimeMs));
        updateLatencyClass('avgResponseTimeMs', data.avgResponseTimeMs);

        // P50 (median) - not in DTO, skip
        // P95 response time
        animateValueChange('p95ResponseTimeMs', formatMs(data.p95ResponseTimeMs));
        updateLatencyClass('p95ResponseTimeMs', data.p95ResponseTimeMs);

        // P99 response time
        animateValueChange('p99ResponseTimeMs', formatMs(data.p99ResponseTimeMs));
        updateLatencyClass('p99ResponseTimeMs', data.p99ResponseTimeMs);

        // Total commands 24h
        animateValueChange('totalCommands24h', data.totalCommands24h.toLocaleString());

        // Commands last hour
        animateValueChange('commandsLastHour', data.commandsLastHour.toLocaleString());

        // Error rate
        animateValueChange('errorRate', formatPercent(data.errorRate));

        // Update last refresh time
        updateLastRefreshTime();
    }

    /**
     * Format milliseconds for display.
     * @param {number} ms - Milliseconds value
     * @returns {string} Formatted string
     */
    function formatMs(ms) {
        return ms.toFixed(0);
    }

    /**
     * Format percentage for display.
     * @param {number} rate - Rate value (0-100)
     * @returns {string} Formatted string
     */
    function formatPercent(rate) {
        return rate.toFixed(2) + '%';
    }

    /**
     * Update latency CSS class based on value thresholds.
     * @param {string} elementId - Element ID
     * @param {number} ms - Latency in milliseconds
     */
    function updateLatencyClass(elementId, ms) {
        const element = document.getElementById(elementId);
        if (!element) return;

        const parent = element.closest('.metric-value');
        if (!parent) return;

        // Remove existing latency classes
        parent.classList.remove('latency-good', 'latency-warning', 'latency-critical');

        // Add appropriate class based on thresholds
        if (ms < 200) {
            parent.classList.add('latency-good');
        } else if (ms < 500) {
            parent.classList.add('latency-warning');
        } else {
            parent.classList.add('latency-critical');
        }
    }

    /**
     * Append new data point to response time chart (streaming update).
     * @param {number} avg - Average response time
     * @param {number} p95 - P95 response time
     * @param {number} p99 - P99 response time
     * @param {string} timestamp - ISO timestamp
     */
    function appendToResponseTimeChart(avg, p95, p99, timestamp) {
        if (!responseTimeChart) return;

        const formattedTime = formatChartTime(timestamp);

        responseTimeChart.data.labels.push(formattedTime);
        responseTimeChart.data.datasets[0].data.push(avg);
        responseTimeChart.data.datasets[1].data.push(p95);
        responseTimeChart.data.datasets[2].data.push(p99);

        // Implement sliding window to prevent memory growth
        if (responseTimeChart.data.labels.length > MAX_CHART_POINTS) {
            responseTimeChart.data.labels.shift();
            responseTimeChart.data.datasets.forEach(ds => ds.data.shift());
        }

        // Update without animation for smooth streaming
        responseTimeChart.update('none');
    }

    /**
     * Append new data point to throughput chart (streaming update).
     * @param {number} value - Commands per hour
     * @param {string} timestamp - ISO timestamp
     */
    function appendToThroughputChart(value, timestamp) {
        if (!throughputChart) return;

        const formattedTime = formatChartTime(timestamp);

        throughputChart.data.labels.push(formattedTime);
        throughputChart.data.datasets[0].data.push(value);

        // Implement sliding window
        if (throughputChart.data.labels.length > MAX_CHART_POINTS) {
            throughputChart.data.labels.shift();
            throughputChart.data.datasets[0].data.shift();
        }

        throughputChart.update('none');
    }

    /**
     * Append new data point to error rate chart (streaming update).
     * @param {number} errorRate - Error rate percentage
     * @param {string} timestamp - ISO timestamp
     */
    function appendToErrorRateChart(errorRate, timestamp) {
        if (!errorRateChart) return;

        const formattedTime = formatChartTime(timestamp);

        errorRateChart.data.labels.push(formattedTime);
        errorRateChart.data.datasets[0].data.push(errorRate);

        // Implement sliding window
        if (errorRateChart.data.labels.length > MAX_CHART_POINTS) {
            errorRateChart.data.labels.shift();
            errorRateChart.data.datasets[0].data.shift();
        }

        errorRateChart.update('none');
    }

    /**
     * Format timestamp for chart labels.
     * @param {string} isoString - ISO 8601 timestamp string
     * @returns {string} Formatted time string
     */
    function formatChartTime(isoString) {
        if (!isoString) return '';
        const date = new Date(isoString);
        return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    }

    /**
     * Animate a value change with a flash highlight effect.
     * @param {string} elementId - The ID of the element to animate
     * @param {*} newValue - The new value to display
     */
    function animateValueChange(elementId, newValue) {
        const element = document.getElementById(elementId);
        if (!element) return;

        const oldValue = element.textContent;
        const displayValue = String(newValue);

        if (oldValue !== displayValue) {
            element.textContent = displayValue;
            element.classList.add('value-changed');
            setTimeout(() => element.classList.remove('value-changed'), 500);
        }
    }

    /**
     * Update the last refresh timestamp.
     */
    function updateLastRefreshTime() {
        const element = document.getElementById('lastRefreshTime');
        if (element) {
            const now = new Date();
            element.textContent = now.toLocaleTimeString();
        }
    }

    /**
     * Show the live indicator.
     */
    function showLiveIndicator() {
        const indicator = document.getElementById('commandsLiveIndicator');
        if (indicator) {
            indicator.classList.remove('hidden');
            indicator.classList.remove('paused');
        }
    }

    /**
     * Hide the live indicator.
     */
    function hideLiveIndicator() {
        const indicator = document.getElementById('commandsLiveIndicator');
        if (indicator) {
            indicator.classList.add('paused');
        }
    }

    /**
     * Flash the live indicator to show data received.
     */
    function flashLiveIndicator() {
        const indicator = document.getElementById('commandsLiveIndicator');
        if (indicator) {
            indicator.classList.add('flash');
            setTimeout(() => indicator.classList.remove('flash'), 300);
        }
    }

    /**
     * Handle SignalR reconnection.
     */
    async function handleReconnected() {
        console.log('[CommandsRealtime] Reconnected, rejoining performance group');
        try {
            await DashboardHub.joinPerformanceGroup();
            isSubscribed = true;
            showLiveIndicator();

            // Refresh data
            const metrics = await DashboardHub.invoke('GetCurrentCommandPerformance', window.selectedHours || 24);
            if (metrics) {
                updateSummaryCards(metrics);
            }
        } catch (error) {
            console.error('[CommandsRealtime] Failed to rejoin after reconnection:', error);
        }
    }

    /**
     * Handle SignalR disconnection.
     */
    function handleDisconnected() {
        console.log('[CommandsRealtime] Disconnected from SignalR');
        isSubscribed = false;
        hideLiveIndicator();
    }

    /**
     * Start polling fallback if SignalR is unavailable.
     */
    function startPollingFallback() {
        console.warn('[CommandsRealtime] SignalR unavailable, falling back to polling');
        hideLiveIndicator();

        pollingFallbackId = setInterval(async function() {
            try {
                // Reinitialize the tab to refresh data
                if (typeof window.initCommandsTab === 'function') {
                    await window.initCommandsTab();
                }
            } catch (error) {
                console.error('[CommandsRealtime] Polling error:', error);
            }
        }, POLLING_FALLBACK_INTERVAL);
    }

    /**
     * Clean up subscriptions and event handlers.
     */
    async function cleanup() {
        // Stop polling fallback if running
        if (pollingFallbackId) {
            clearInterval(pollingFallbackId);
            pollingFallbackId = null;
        }

        // Unsubscribe from SignalR
        if (isSubscribed) {
            try {
                await DashboardHub.leavePerformanceGroup();
            } catch (error) {
                console.warn('[CommandsRealtime] Error leaving performance group:', error);
            }
            isSubscribed = false;
        }

        // Remove event handlers
        DashboardHub.off('CommandPerformanceUpdate', handleCommandPerformanceUpdate);
        DashboardHub.off('reconnected', handleReconnected);
        DashboardHub.off('disconnected', handleDisconnected);

        // Clear chart references
        responseTimeChart = null;
        throughputChart = null;
        errorRateChart = null;

        console.log('[CommandsRealtime] Cleaned up');
    }

    // Public API
    return {
        initialize,
        cleanup,
        setChartReferences,
        isSubscribed: () => isSubscribed
    };
})();

// Export for module systems
if (typeof module !== 'undefined' && module.exports) {
    module.exports = CommandsRealtime;
}
