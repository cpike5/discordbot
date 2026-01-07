/**
 * Health Metrics Real-time Updates
 * Manages SignalR subscription for live health metrics updates.
 * Part of issue #627 - Replace polling with SignalR on Health Metrics page.
 */
const HealthMetricsRealtime = (function() {
    'use strict';

    // Configuration
    const POLLING_FALLBACK_INTERVAL = 30000; // 30 seconds
    const SPARKLINE_MAX_POINTS = 20;

    // State
    let isSubscribed = false;
    let pollingFallbackId = null;
    let sparklineData = [];

    // External dependencies (set during initialization)
    let gaugeCharts = {
        latency: null,
        memory: null,
        cpu: null
    };
    let sparklineChart = null;

    /**
     * Initialize real-time updates for health metrics.
     * @param {Object} charts - Object containing gauge chart instances { latency, memory, cpu, sparkline }
     * @param {number[]} initialSparklineData - Initial sparkline data points
     */
    async function initialize(charts, initialSparklineData = []) {
        gaugeCharts = {
            latency: charts.latency,
            memory: charts.memory,
            cpu: charts.cpu
        };
        sparklineChart = charts.sparkline;
        sparklineData = [...initialSparklineData];

        try {
            // Connect to SignalR hub
            const connected = await DashboardHub.connect();
            if (!connected) {
                console.warn('[HealthMetricsRealtime] SignalR connection failed, falling back to polling');
                startPollingFallback();
                return;
            }

            // Register event handlers
            DashboardHub.on('HealthMetricsUpdate', handleHealthUpdate);
            DashboardHub.on('reconnected', handleReconnected);
            DashboardHub.on('disconnected', handleDisconnected);

            // Join the performance group
            await DashboardHub.joinPerformanceGroup();
            isSubscribed = true;

            // Get initial data
            const metrics = await DashboardHub.getCurrentPerformanceMetrics();
            if (metrics) {
                updateAllMetrics(metrics);
            }

            // Show live indicator
            showLiveIndicator();

            console.log('[HealthMetricsRealtime] Initialized with SignalR');
        } catch (error) {
            console.error('[HealthMetricsRealtime] Initialization error:', error);
            startPollingFallback();
        }
    }

    /**
     * Handle incoming health metrics update from SignalR.
     * @param {Object} data - HealthMetricsUpdateDto
     */
    function handleHealthUpdate(data) {
        updateAllMetrics(data);
        flashLiveIndicator();
    }

    /**
     * Update all metrics displays with new data.
     * @param {Object} data - Metrics data object
     */
    function updateAllMetrics(data) {
        // Update latency gauge and text
        updateGauge('latency', data.latencyMs, 500);
        if (typeof animateValueChange === 'function') {
            animateValueChange('currentLatency', data.latencyMs);
        } else {
            document.getElementById('currentLatency').textContent = data.latencyMs;
        }

        // Update memory gauge and text
        updateGauge('memory', data.workingSetMB, 1024);
        if (typeof animateValueChange === 'function') {
            animateValueChange('memoryUsage', data.workingSetMB);
        } else {
            document.getElementById('memoryUsage').textContent = data.workingSetMB;
        }

        // Update CPU gauge and text
        if (data.cpuUsagePercent !== undefined) {
            updateGauge('cpu', data.cpuUsagePercent, 100);
            const cpuValue = Math.round(data.cpuUsagePercent);
            if (typeof animateValueChange === 'function') {
                animateValueChange('cpuUsage', cpuValue);
            } else {
                document.getElementById('cpuUsage').textContent = cpuValue;
            }
        }

        // Append to sparkline
        appendToSparkline(data.latencyMs);

        // Update connection state if changed
        updateConnectionState(data.connectionState);
    }

    /**
     * Update a gauge chart with a new value.
     * @param {string} gaugeType - 'latency', 'memory', or 'cpu'
     * @param {number} value - New value
     * @param {number} maxValue - Maximum value for the gauge
     */
    function updateGauge(gaugeType, value, maxValue) {
        const chart = gaugeCharts[gaugeType];
        if (!chart) return;

        // Get thresholds and colors based on gauge type
        const { thresholds, colors } = getGaugeConfig(gaugeType);

        const percentage = Math.min((value / maxValue) * 100, 100);
        const remaining = 100 - percentage;
        const gaugeColor = getGaugeColor(value, thresholds, colors);

        chart.data.datasets[0].data = [percentage, remaining];
        chart.data.datasets[0].backgroundColor[0] = gaugeColor;
        chart.update('none');
    }

    /**
     * Get gauge configuration based on type.
     */
    function getGaugeConfig(gaugeType) {
        switch (gaugeType) {
            case 'latency':
                return {
                    thresholds: [0, 100, 200],
                    colors: ['rgb(16, 185, 129)', 'rgb(245, 158, 11)', 'rgb(239, 68, 68)']
                };
            case 'memory':
                return {
                    thresholds: [0, 512, 768],
                    colors: ['rgb(16, 185, 129)', 'rgb(245, 158, 11)', 'rgb(239, 68, 68)']
                };
            case 'cpu':
                return {
                    thresholds: [0, 50, 80],
                    colors: ['rgb(16, 185, 129)', 'rgb(245, 158, 11)', 'rgb(239, 68, 68)']
                };
            default:
                return { thresholds: [0], colors: ['rgb(16, 185, 129)'] };
        }
    }

    /**
     * Get gauge color based on value and thresholds.
     */
    function getGaugeColor(value, thresholds, colors) {
        for (let i = thresholds.length - 1; i >= 0; i--) {
            if (value >= thresholds[i]) {
                return colors[i];
            }
        }
        return colors[0];
    }

    /**
     * Append a new value to the sparkline chart.
     * @param {number} latencyMs - New latency value
     */
    function appendToSparkline(latencyMs) {
        if (!sparklineChart) return;

        sparklineData.push(latencyMs);

        // Keep sliding window
        if (sparklineData.length > SPARKLINE_MAX_POINTS) {
            sparklineData.shift();
        }

        // Update chart data
        sparklineChart.data.labels = sparklineData.map((_, i) => i + 1);
        sparklineChart.data.datasets[0].data = sparklineData;
        sparklineChart.data.datasets[0].backgroundColor = sparklineData.map(v => {
            if (v < 100) return 'rgba(16, 185, 129, 0.8)';
            if (v < 200) return 'rgba(245, 158, 11, 0.8)';
            return 'rgba(239, 68, 68, 0.8)';
        });

        sparklineChart.update('none');
    }

    /**
     * Update connection state display.
     * @param {string} state - Connection state string
     */
    function updateConnectionState(state) {
        // Update the connection state badge if the state has changed
        const stateElement = document.querySelector('.health-status-label');
        if (stateElement && stateElement.textContent !== state) {
            stateElement.textContent = state;
        }
    }

    /**
     * Show the live indicator.
     */
    function showLiveIndicator() {
        const indicator = document.getElementById('liveIndicator');
        if (indicator) {
            indicator.classList.remove('hidden');
            indicator.classList.remove('paused');
        }
    }

    /**
     * Hide the live indicator.
     */
    function hideLiveIndicator() {
        const indicator = document.getElementById('liveIndicator');
        if (indicator) {
            indicator.classList.add('paused');
        }
    }

    /**
     * Flash the live indicator to show data received.
     */
    function flashLiveIndicator() {
        const indicator = document.getElementById('liveIndicator');
        if (indicator) {
            indicator.classList.add('flash');
            setTimeout(() => indicator.classList.remove('flash'), 300);
        }
    }

    /**
     * Handle SignalR reconnection.
     */
    async function handleReconnected() {
        console.log('[HealthMetricsRealtime] Reconnected, rejoining performance group');
        try {
            await DashboardHub.joinPerformanceGroup();
            isSubscribed = true;
            showLiveIndicator();

            // Refresh data
            const metrics = await DashboardHub.getCurrentPerformanceMetrics();
            if (metrics) {
                updateAllMetrics(metrics);
            }
        } catch (error) {
            console.error('[HealthMetricsRealtime] Failed to rejoin after reconnection:', error);
        }
    }

    /**
     * Handle SignalR disconnection.
     */
    function handleDisconnected() {
        console.log('[HealthMetricsRealtime] Disconnected from SignalR');
        isSubscribed = false;
        hideLiveIndicator();
    }

    /**
     * Start polling fallback if SignalR is unavailable.
     */
    function startPollingFallback() {
        console.warn('[HealthMetricsRealtime] SignalR unavailable, falling back to polling');
        hideLiveIndicator();

        pollingFallbackId = setInterval(async function() {
            try {
                const response = await fetch('/api/metrics/health');
                const health = await response.json();
                updateAllMetrics({
                    latencyMs: health.latencyMs,
                    workingSetMB: health.workingSetMB || health.memoryMB,
                    cpuUsagePercent: health.cpuUsagePercent,
                    connectionState: health.connectionState
                });
            } catch (error) {
                console.error('[HealthMetricsRealtime] Polling error:', error);
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
                console.warn('[HealthMetricsRealtime] Error leaving performance group:', error);
            }
            isSubscribed = false;
        }

        // Remove event handlers
        DashboardHub.off('HealthMetricsUpdate', handleHealthUpdate);
        DashboardHub.off('reconnected', handleReconnected);
        DashboardHub.off('disconnected', handleDisconnected);

        console.log('[HealthMetricsRealtime] Cleaned up');
    }


    // Public API
    return {
        initialize,
        cleanup,
        isSubscribed: () => isSubscribed
    };
})();

// Export for module systems
if (typeof module !== 'undefined' && module.exports) {
    module.exports = HealthMetricsRealtime;
}
