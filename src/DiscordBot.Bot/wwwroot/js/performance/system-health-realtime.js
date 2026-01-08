/**
 * System Health Real-time Updates
 * Manages SignalR subscription for live system health updates.
 * Part of issue #629 - Replace full page reload with SignalR on System Health page.
 */
const SystemHealthRealtime = (function() {
    'use strict';

    // Configuration
    const POLLING_FALLBACK_INTERVAL = 30000; // 30 seconds

    // State
    let isSubscribed = false;
    let pollingFallbackId = null;

    /**
     * Initialize real-time updates for system health.
     */
    async function initialize() {
        try {
            // Connect to SignalR hub
            const connected = await DashboardHub.connect();
            if (!connected) {
                console.warn('[SystemHealthRealtime] SignalR connection failed, falling back to polling');
                startPollingFallback();
                return;
            }

            // Register event handlers
            DashboardHub.on('SystemMetricsUpdate', handleSystemMetricsUpdate);
            DashboardHub.on('reconnected', handleReconnected);
            DashboardHub.on('disconnected', handleDisconnected);

            // Join the system health group
            await DashboardHub.joinSystemHealthGroup();
            isSubscribed = true;

            // Get initial data
            const metrics = await DashboardHub.getCurrentSystemHealth();
            if (metrics) {
                updateAllSystemMetrics(metrics);
            }

            // Show live indicator
            showLiveIndicator();

            console.log('[SystemHealthRealtime] Initialized with SignalR');
        } catch (error) {
            console.error('[SystemHealthRealtime] Initialization error:', error);
            startPollingFallback();
        }
    }

    /**
     * Handle incoming system metrics update from SignalR.
     * @param {Object} data - SystemMetricsUpdateDto
     */
    function handleSystemMetricsUpdate(data) {
        updateAllSystemMetrics(data);
        flashLiveIndicator();
    }

    /**
     * Update all system metrics displays with new data.
     * @param {Object} data - System metrics data object
     */
    function updateAllSystemMetrics(data) {
        // Update database metrics
        updateDatabaseMetrics(data);

        // Update background service status list
        updateBackgroundServices(data.backgroundServices);

        // Update cache statistics
        updateCacheStats(data.cacheStats);

        // Update last updated timestamp
        updateLastRefreshTime();
    }

    /**
     * Update database performance metrics.
     * @param {Object} data - Database metrics data
     */
    function updateDatabaseMetrics(data) {
        animateValueChange('avgQueryTime', data.avgQueryTimeMs.toFixed(0), 'ms');
        animateValueChange('totalQueries', data.totalQueries.toLocaleString());
        animateValueChange('queriesPerSecond', data.queriesPerSecond.toFixed(1));
        animateValueChange('slowQueryCount', data.slowQueryCount);

        // Update slow query count color based on value
        const slowQueryElement = document.getElementById('slowQueryCount');
        if (slowQueryElement) {
            slowQueryElement.parentElement.classList.remove('text-error', 'text-warning', 'text-success');
            if (data.slowQueryCount > 10) {
                slowQueryElement.parentElement.classList.add('text-error');
            } else if (data.slowQueryCount > 0) {
                slowQueryElement.parentElement.classList.add('text-warning');
            } else {
                slowQueryElement.parentElement.classList.add('text-success');
            }
        }
    }

    /**
     * Update background services status display.
     * @param {Array} services - Array of BackgroundServiceStatusDto
     */
    function updateBackgroundServices(services) {
        if (!services || services.length === 0) return;

        services.forEach(service => {
            const row = document.querySelector(`[data-service-name="${service.serviceName}"]`);
            if (row) {
                updateServiceRow(row, service);
            }
        });
    }

    /**
     * Update a single service row.
     * @param {HTMLElement} row - The service row element
     * @param {Object} service - Service status data
     */
    function updateServiceRow(row, service) {
        // Update status indicator dot
        const statusDot = row.querySelector('.service-status-dot');
        if (statusDot) {
            statusDot.classList.remove('bg-success', 'bg-warning', 'bg-error', 'bg-gray-500', 'animate-pulse');
            const statusClass = getServiceStatusClass(service.status);
            statusDot.classList.add(statusClass);
            if (service.status.toUpperCase() === 'RUNNING') {
                statusDot.classList.add('animate-pulse');
            }
        }

        // Update status text
        const statusText = row.querySelector('.service-status-text');
        if (statusText) {
            const oldValue = statusText.textContent;
            statusText.textContent = service.status;
            statusText.classList.remove('text-success', 'text-warning', 'text-text-tertiary', 'text-error');
            const statusColor = getServiceStatusColor(service.status);
            statusText.classList.add(statusColor);

            if (oldValue !== service.status) {
                statusText.classList.add('value-changed');
                setTimeout(() => statusText.classList.remove('value-changed'), 500);
            }
        }

        // Update heartbeat timestamp
        const heartbeatSpan = row.querySelector('.service-heartbeat');
        if (heartbeatSpan && service.lastHeartbeat) {
            heartbeatSpan.textContent = formatRelativeTime(service.lastHeartbeat);
        }

        // Update error message
        const errorSpan = row.querySelector('.service-error');
        if (errorSpan) {
            if (service.lastError) {
                errorSpan.textContent = service.lastError;
                errorSpan.classList.remove('hidden');
            } else {
                errorSpan.classList.add('hidden');
            }
        }
    }

    /**
     * Get status class for service status dot.
     * @param {string} status - Service status string
     * @returns {string} CSS class for the status dot
     */
    function getServiceStatusClass(status) {
        switch (status.toUpperCase()) {
            case 'RUNNING': return 'bg-success';
            case 'STARTING': return 'bg-warning';
            case 'STOPPED': return 'bg-gray-500';
            default: return 'bg-error';
        }
    }

    /**
     * Get color class for service status text.
     * @param {string} status - Service status string
     * @returns {string} CSS class for the status text
     */
    function getServiceStatusColor(status) {
        switch (status.toUpperCase()) {
            case 'RUNNING': return 'text-success';
            case 'STARTING': return 'text-warning';
            case 'STOPPED': return 'text-text-tertiary';
            default: return 'text-error';
        }
    }

    /**
     * Update cache statistics display.
     * @param {Object} cacheStats - Dictionary of cache statistics by key prefix
     */
    function updateCacheStats(cacheStats) {
        if (!cacheStats) return;

        Object.entries(cacheStats).forEach(([keyPrefix, stats]) => {
            const row = document.querySelector(`[data-cache-name="${keyPrefix}"]`);
            if (row) {
                updateCacheRow(row, stats);
            }
        });

        // Update overall cache stats
        updateOverallCacheStats(cacheStats);
    }

    /**
     * Update a single cache row.
     * @param {HTMLElement} row - The cache row element
     * @param {Object} stats - Cache statistics data
     */
    function updateCacheRow(row, stats) {
        // Update hit rate text and progress bar
        const hitRateText = row.querySelector('.cache-hit-rate');
        if (hitRateText) {
            const oldValue = hitRateText.textContent;
            const newValue = stats.hitRate.toFixed(1) + '%';
            hitRateText.textContent = newValue;

            // Update color class based on hit rate
            hitRateText.classList.remove('text-success', 'text-warning', 'text-error');
            if (stats.hitRate >= 90) {
                hitRateText.classList.add('text-success');
            } else if (stats.hitRate >= 70) {
                hitRateText.classList.add('text-warning');
            } else {
                hitRateText.classList.add('text-error');
            }

            if (oldValue !== newValue) {
                hitRateText.classList.add('value-changed');
                setTimeout(() => hitRateText.classList.remove('value-changed'), 500);
            }
        }

        // Update progress bar
        const progressBar = row.querySelector('.progress-bar-fill');
        if (progressBar) {
            progressBar.style.width = stats.hitRate.toFixed(1) + '%';
            progressBar.classList.remove('progress-bar-success', 'progress-bar-warning', 'progress-bar-error');
            if (stats.hitRate >= 90) {
                progressBar.classList.add('progress-bar-success');
            } else if (stats.hitRate >= 70) {
                progressBar.classList.add('progress-bar-warning');
            } else {
                progressBar.classList.add('progress-bar-error');
            }
        }

        // Update hits/misses count
        const hitsText = row.querySelector('.cache-hits');
        if (hitsText) {
            hitsText.textContent = stats.hits.toLocaleString() + ' hits / ' + stats.misses.toLocaleString() + ' misses';
        }

        // Update size
        const sizeText = row.querySelector('.cache-size');
        if (sizeText) {
            sizeText.textContent = 'Size: ' + stats.size + ' items';
        }
    }

    /**
     * Update overall cache statistics.
     * @param {Object} cacheStats - Dictionary of all cache statistics
     */
    function updateOverallCacheStats(cacheStats) {
        let totalHits = 0;
        let totalMisses = 0;
        let totalSize = 0;

        Object.values(cacheStats).forEach(stats => {
            totalHits += stats.hits;
            totalMisses += stats.misses;
            totalSize += stats.size;
        });

        animateValueChange('totalCacheHits', totalHits.toLocaleString());
        animateValueChange('totalCacheMisses', totalMisses.toLocaleString());
        animateValueChange('totalCacheItems', totalSize.toLocaleString());
    }

    /**
     * Animate a value change with a flash highlight effect.
     * @param {string} elementId - The ID of the element to animate
     * @param {*} newValue - The new value to display
     * @param {string} suffix - Optional suffix (e.g., 'ms', '%')
     */
    function animateValueChange(elementId, newValue, suffix = '') {
        const element = document.getElementById(elementId);
        if (!element) return;

        const oldValue = element.textContent;
        const displayValue = suffix ? newValue + suffix : String(newValue);

        if (oldValue !== displayValue) {
            element.textContent = displayValue;
            element.classList.add('value-changed');
            setTimeout(() => element.classList.remove('value-changed'), 500);
        }
    }

    /**
     * Format a UTC timestamp as a relative time string.
     * @param {string} isoString - ISO 8601 timestamp string
     * @returns {string} Relative time string
     */
    function formatRelativeTime(isoString) {
        if (!isoString) return 'Never';

        const date = new Date(isoString);
        const now = new Date();
        const diffMs = now - date;
        const diffSecs = Math.floor(diffMs / 1000);
        const diffMins = Math.floor(diffSecs / 60);
        const diffHours = Math.floor(diffMins / 60);

        if (diffSecs < 60) return `${diffSecs}s ago`;
        if (diffMins < 60) return `${diffMins}m ago`;
        return `${diffHours}h ago`;
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
        const indicator = document.getElementById('systemHealthLiveIndicator');
        if (indicator) {
            indicator.classList.remove('hidden');
            indicator.classList.remove('paused');
        }
    }

    /**
     * Hide the live indicator.
     */
    function hideLiveIndicator() {
        const indicator = document.getElementById('systemHealthLiveIndicator');
        if (indicator) {
            indicator.classList.add('paused');
        }
    }

    /**
     * Flash the live indicator to show data received.
     */
    function flashLiveIndicator() {
        const indicator = document.getElementById('systemHealthLiveIndicator');
        if (indicator) {
            indicator.classList.add('flash');
            setTimeout(() => indicator.classList.remove('flash'), 300);
        }
    }

    /**
     * Handle SignalR reconnection.
     */
    async function handleReconnected() {
        console.log('[SystemHealthRealtime] Reconnected, rejoining system health group');
        try {
            await DashboardHub.joinSystemHealthGroup();
            isSubscribed = true;
            showLiveIndicator();

            // Refresh data
            const metrics = await DashboardHub.getCurrentSystemHealth();
            if (metrics) {
                updateAllSystemMetrics(metrics);
            }
        } catch (error) {
            console.error('[SystemHealthRealtime] Failed to rejoin after reconnection:', error);
        }
    }

    /**
     * Handle SignalR disconnection.
     */
    function handleDisconnected() {
        console.log('[SystemHealthRealtime] Disconnected from SignalR');
        isSubscribed = false;
        hideLiveIndicator();
    }

    /**
     * Start polling fallback if SignalR is unavailable.
     */
    function startPollingFallback() {
        console.warn('[SystemHealthRealtime] SignalR unavailable, falling back to polling');
        hideLiveIndicator();

        pollingFallbackId = setInterval(async function() {
            try {
                const response = await fetch('/api/metrics/system');
                const data = await response.json();
                updateAllSystemMetrics(data);
            } catch (error) {
                console.error('[SystemHealthRealtime] Polling error:', error);
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
                await DashboardHub.leaveSystemHealthGroup();
            } catch (error) {
                console.warn('[SystemHealthRealtime] Error leaving system health group:', error);
            }
            isSubscribed = false;
        }

        // Remove event handlers
        DashboardHub.off('SystemMetricsUpdate', handleSystemMetricsUpdate);
        DashboardHub.off('reconnected', handleReconnected);
        DashboardHub.off('disconnected', handleDisconnected);

        console.log('[SystemHealthRealtime] Cleaned up');
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
    module.exports = SystemHealthRealtime;
}
