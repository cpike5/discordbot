/**
 * Alerts Real-time Updates
 * Manages SignalR subscription for live alert notifications on the Alerts page.
 * Part of issue #628 - Add real-time alert updates to Alerts page via SignalR.
 */
const AlertsRealtime = (function() {
    'use strict';

    // Configuration
    const POLLING_FALLBACK_INTERVAL = 30000; // 30 seconds
    const TOAST_DURATION_MS = 10000; // 10 seconds for alert toasts

    // State
    let isSubscribed = false;
    let pollingFallbackId = null;

    /**
     * Initialize real-time updates for alerts.
     */
    async function initialize() {
        try {
            // Connect to SignalR hub
            const connected = await DashboardHub.connect();
            if (!connected) {
                console.warn('[AlertsRealtime] SignalR connection failed, falling back to polling');
                startPollingFallback();
                return;
            }

            // Register event handlers
            DashboardHub.on('OnAlertTriggered', handleAlertTriggered);
            DashboardHub.on('OnAlertResolved', handleAlertResolved);
            DashboardHub.on('OnAlertAcknowledged', handleAlertAcknowledged);
            DashboardHub.on('OnActiveAlertCountChanged', handleActiveAlertCountChanged);
            DashboardHub.on('reconnected', handleReconnected);
            DashboardHub.on('disconnected', handleDisconnected);

            // Join the alerts group
            await DashboardHub.joinAlertsGroup();
            isSubscribed = true;

            // Show live indicator
            showLiveIndicator();

            console.log('[AlertsRealtime] Initialized with SignalR');
        } catch (error) {
            console.error('[AlertsRealtime] Initialization error:', error);
            startPollingFallback();
        }
    }

    /**
     * Handle incoming alert triggered event from SignalR.
     * @param {Object} alert - PerformanceIncidentDto
     */
    function handleAlertTriggered(alert) {
        console.log('[AlertsRealtime] Alert triggered:', alert);

        // Add to active alerts list
        addAlertToActiveList(alert);

        // Update count badge
        updateActiveAlertBadge();

        // Add to incident history timeline
        addToIncidentTimeline(alert);

        // Show toast notification for Critical/Warning
        if (alert.severity === 'Critical' || alert.severity === 'Warning' ||
            alert.severity === 0 || alert.severity === 1) {
            showAlertToast(alert);
        }

        flashLiveIndicator();
    }

    /**
     * Handle incoming alert resolved event from SignalR.
     * @param {Object} alert - PerformanceIncidentDto
     */
    function handleAlertResolved(alert) {
        console.log('[AlertsRealtime] Alert resolved:', alert);

        // Remove from active alerts list with animation
        removeAlertFromActiveList(alert.id);

        // Update count badge
        updateActiveAlertBadge();

        // Update incident in history timeline
        updateIncidentInTimeline(alert);

        // Show resolution toast
        showResolvedToast(alert);

        flashLiveIndicator();
    }

    /**
     * Handle incoming alert acknowledged event from SignalR.
     * @param {Object} data - { incidentId, acknowledgedBy, acknowledgedAt }
     */
    function handleAlertAcknowledged(data) {
        console.log('[AlertsRealtime] Alert acknowledged:', data);

        // Update the alert row to show acknowledged state
        markAlertAsAcknowledged(data.incidentId, data.acknowledgedBy);

        flashLiveIndicator();
    }

    /**
     * Handle active alert count changed event.
     * @param {Object} summary - ActiveAlertSummaryDto
     */
    function handleActiveAlertCountChanged(summary) {
        console.log('[AlertsRealtime] Active alert count changed:', summary);
        updateHeaderBadge(summary);
    }

    /**
     * Add a new alert to the active alerts list.
     * @param {Object} alert - PerformanceIncidentDto
     */
    function addAlertToActiveList(alert) {
        const list = document.querySelector('.chart-card-body .divide-y');
        const emptyState = document.querySelector('.chart-card-body .empty-state');

        // Hide empty state if showing
        if (emptyState) {
            emptyState.style.display = 'none';
        }

        // Create container if it doesn't exist
        if (!list) {
            const cardBody = document.querySelector('.chart-card-body.p-0');
            if (cardBody) {
                const newList = document.createElement('div');
                newList.className = 'divide-y divide-border-primary';
                cardBody.insertBefore(newList, cardBody.firstChild);
                addAlertRow(newList, alert);
            }
            return;
        }

        addAlertRow(list, alert);
    }

    /**
     * Create and add an alert row element.
     * @param {HTMLElement} container - The container to add the row to
     * @param {Object} alert - PerformanceIncidentDto
     */
    function addAlertRow(container, alert) {
        const severityValue = typeof alert.severity === 'string' ? alert.severity : getSeverityString(alert.severity);
        const alertClass = severityValue === 'Critical' ? 'alert-card-critical' :
                          severityValue === 'Warning' ? 'alert-card-warning' : 'alert-card-info';
        const iconClass = severityValue === 'Critical' ? 'alert-icon-critical' :
                         severityValue === 'Warning' ? 'alert-icon-warning' : 'alert-icon-info';
        const severityBadgeClass = severityValue === 'Critical' ? 'severity-critical' :
                                   severityValue === 'Warning' ? 'severity-warning' : 'severity-info';
        const iconPath = severityValue === 'Critical'
            ? '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />'
            : '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />';

        const row = document.createElement('div');
        row.className = `alert-card border-0 rounded-none ${alertClass} alert-entering`;
        row.setAttribute('data-incident-id', alert.id);
        row.setAttribute('role', severityValue === 'Critical' ? 'alert' : 'status');
        row.setAttribute('aria-live', severityValue === 'Critical' ? 'assertive' : 'polite');

        const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        const token = tokenInput ? tokenInput.value : '';

        row.innerHTML = `
            <svg class="alert-icon ${iconClass} flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
                ${iconPath}
            </svg>
            <div class="alert-content flex-1">
                <div class="alert-title">${escapeHtml(alert.message)}</div>
                <div class="alert-description">Metric: ${escapeHtml(alert.metricName)} | Threshold: ${alert.thresholdValue} | Actual: ${alert.actualValue}</div>
                <div class="alert-meta">
                    <span class="severity-badge ${severityBadgeClass}">${severityValue}</span>
                    <span class="ack-info"></span>
                    <span data-utc-time="${new Date(alert.triggeredAt).toISOString()}" data-format="relative">Triggered just now</span>
                </div>
            </div>
            <div class="alert-actions flex items-center gap-2">
                <button type="button" onclick="acknowledgeIncident('${alert.id}')" class="acknowledge-btn btn btn-secondary btn-sm">Acknowledge</button>
            </div>
        `;

        container.insertBefore(row, container.firstChild);

        // Remove entering animation after completion
        setTimeout(() => row.classList.remove('alert-entering'), 500);
    }

    /**
     * Remove an alert from the active alerts list with animation.
     * @param {string} incidentId - The incident ID to remove
     */
    function removeAlertFromActiveList(incidentId) {
        const row = document.querySelector(`[data-incident-id="${incidentId}"]`);
        if (row) {
            row.classList.add('alert-exiting');
            setTimeout(() => {
                row.remove();
                // Show empty state if no more alerts
                checkEmptyState();
            }, 300);
        }
    }

    /**
     * Mark an alert as acknowledged in the UI.
     * @param {string} incidentId - The incident ID
     * @param {string} acknowledgedBy - The user who acknowledged
     */
    function markAlertAsAcknowledged(incidentId, acknowledgedBy) {
        const row = document.querySelector(`[data-incident-id="${incidentId}"]`);
        if (row) {
            // Update or add acknowledged badge
            const metaDiv = row.querySelector('.alert-meta');
            if (metaDiv) {
                // Check if acknowledged badge already exists
                let ackBadge = metaDiv.querySelector('.status-badge-secondary');
                if (!ackBadge) {
                    const severityBadge = metaDiv.querySelector('.severity-badge');
                    if (severityBadge) {
                        ackBadge = document.createElement('span');
                        ackBadge.className = 'status-badge status-badge-secondary';
                        ackBadge.textContent = 'Acknowledged';
                        severityBadge.insertAdjacentElement('afterend', ackBadge);
                    }
                }

                // Update ack info
                const ackInfo = metaDiv.querySelector('.ack-info');
                if (ackInfo) {
                    ackInfo.textContent = `by ${acknowledgedBy}`;
                    ackInfo.className = 'text-text-tertiary';
                } else if (ackBadge) {
                    const newAckInfo = document.createElement('span');
                    newAckInfo.className = 'text-text-tertiary';
                    newAckInfo.textContent = `by ${acknowledgedBy}`;
                    ackBadge.insertAdjacentElement('afterend', newAckInfo);
                }
            }

            // Disable acknowledge button
            const ackBtn = row.querySelector('.acknowledge-btn');
            if (ackBtn) {
                ackBtn.disabled = true;
            }

            // Remove the action form
            const actionsDiv = row.querySelector('.alert-actions');
            if (actionsDiv) {
                actionsDiv.remove();
            }
        }
    }

    /**
     * Add an incident to the incident history timeline.
     * @param {Object} alert - PerformanceIncidentDto
     */
    function addToIncidentTimeline(alert) {
        const timeline = document.querySelector('.timeline');
        if (!timeline) return;

        const severityValue = typeof alert.severity === 'string' ? alert.severity : getSeverityString(alert.severity);
        const dotClass = severityValue === 'Critical' ? 'timeline-dot-critical' :
                        severityValue === 'Warning' ? 'timeline-dot-warning' : 'timeline-dot-info';
        const severityBadgeClass = severityValue === 'Critical' ? 'severity-critical' :
                                   severityValue === 'Warning' ? 'severity-warning' : 'severity-info';

        const entry = document.createElement('div');
        entry.className = 'timeline-entry alert-entering';
        entry.setAttribute('data-timeline-incident-id', alert.id);

        entry.innerHTML = `
            <div class="timeline-dot ${dotClass}"></div>
            <div class="timeline-content">
                <div class="timeline-time" data-utc-time="${new Date(alert.triggeredAt).toISOString()}">
                    ${formatDateTime(new Date(alert.triggeredAt))}
                </div>
                <div class="timeline-title">${escapeHtml(alert.message)}</div>
                <div class="timeline-description">
                    <span class="severity-badge ${severityBadgeClass} mr-2">${severityValue}</span>
                    <span class="timeline-duration">In progress</span>
                </div>
            </div>
        `;

        timeline.insertBefore(entry, timeline.firstChild);
        setTimeout(() => entry.classList.remove('alert-entering'), 500);
    }

    /**
     * Update an incident in the timeline when resolved.
     * @param {Object} alert - PerformanceIncidentDto
     */
    function updateIncidentInTimeline(alert) {
        const entry = document.querySelector(`[data-timeline-incident-id="${alert.id}"]`);
        if (entry) {
            const durationSpan = entry.querySelector('.timeline-duration');
            if (durationSpan) {
                const duration = alert.durationSeconds || 0;
                durationSpan.textContent = `Duration: ${formatDuration(duration)} - Auto-resolved`;
            }
        }
    }

    /**
     * Check if we should show the empty state.
     */
    function checkEmptyState() {
        const list = document.querySelector('.chart-card-body .divide-y');
        const cardBody = document.querySelector('.chart-card-body.p-0');

        if (list && list.children.length === 0) {
            list.remove();
        }

        // Re-check after list removal
        const remainingList = document.querySelector('.chart-card-body .divide-y');
        if (!remainingList && cardBody) {
            let emptyState = cardBody.querySelector('.empty-state');
            if (!emptyState) {
                emptyState = document.createElement('div');
                emptyState.className = 'empty-state py-12';
                emptyState.innerHTML = `
                    <svg class="empty-state-icon" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
                    </svg>
                    <div class="empty-state-title">No Active Alerts</div>
                    <div class="empty-state-description">All systems are operating within normal parameters.</div>
                `;
                cardBody.appendChild(emptyState);
            }
            emptyState.style.display = '';
        }
    }

    /**
     * Update the active alert badge in the page header.
     */
    function updateActiveAlertBadge() {
        // Count current active alerts in DOM
        const activeAlerts = document.querySelectorAll('[data-incident-id]');
        const count = activeAlerts.length;

        const badge = document.querySelector('.severity-badge[class*="severity-"]');
        if (badge) {
            if (count > 0) {
                badge.textContent = `${count} Active Alert${count !== 1 ? 's' : ''}`;
                badge.style.display = '';
            } else {
                badge.style.display = 'none';
            }
        }
    }

    /**
     * Update the header badge with summary data from server.
     * @param {Object} summary - ActiveAlertSummaryDto
     */
    function updateHeaderBadge(summary) {
        const badge = document.querySelector('.severity-badge[class*="severity-"]');
        if (badge) {
            if (summary.activeCount > 0) {
                // Update severity class based on highest priority
                badge.classList.remove('severity-critical', 'severity-warning', 'severity-info');
                if (summary.criticalCount > 0) {
                    badge.classList.add('severity-critical');
                } else if (summary.warningCount > 0) {
                    badge.classList.add('severity-warning');
                } else {
                    badge.classList.add('severity-info');
                }
                badge.textContent = `${summary.activeCount} Active Alert${summary.activeCount !== 1 ? 's' : ''}`;
                badge.style.display = '';
            } else {
                badge.style.display = 'none';
            }
        }
    }

    /**
     * Show a toast notification for new alerts.
     * @param {Object} alert - PerformanceIncidentDto
     */
    function showAlertToast(alert) {
        if (typeof ToastManager === 'undefined') return;

        const severityValue = typeof alert.severity === 'string' ? alert.severity : getSeverityString(alert.severity);
        const type = severityValue === 'Critical' ? 'error' : 'warning';
        const title = `Alert: ${alert.metricName}`;

        ToastManager.show(type, alert.message, {
            title: title,
            duration: TOAST_DURATION_MS,
            action: {
                label: 'View',
                onClick: () => {
                    const row = document.querySelector(`[data-incident-id="${alert.id}"]`);
                    if (row) {
                        row.scrollIntoView({ behavior: 'smooth', block: 'center' });
                        row.classList.add('alert-highlight');
                        setTimeout(() => row.classList.remove('alert-highlight'), 2000);
                    }
                }
            }
        });
    }

    /**
     * Show a toast notification for resolved alerts.
     * @param {Object} alert - PerformanceIncidentDto
     */
    function showResolvedToast(alert) {
        if (typeof ToastManager === 'undefined') return;

        ToastManager.show('success', 'The alert has been automatically resolved.', {
            title: `Resolved: ${alert.metricName}`,
            duration: 5000
        });
    }

    /**
     * Show the live indicator.
     */
    function showLiveIndicator() {
        const indicator = document.getElementById('alertsLiveIndicator');
        if (indicator) {
            indicator.classList.remove('hidden');
            indicator.classList.remove('paused');
        }
    }

    /**
     * Hide the live indicator.
     */
    function hideLiveIndicator() {
        const indicator = document.getElementById('alertsLiveIndicator');
        if (indicator) {
            indicator.classList.add('paused');
        }
    }

    /**
     * Flash the live indicator to show data received.
     */
    function flashLiveIndicator() {
        const indicator = document.getElementById('alertsLiveIndicator');
        if (indicator) {
            indicator.classList.add('flash');
            setTimeout(() => indicator.classList.remove('flash'), 300);
        }
    }

    /**
     * Handle SignalR reconnection.
     */
    async function handleReconnected() {
        console.log('[AlertsRealtime] Reconnected, rejoining alerts group');
        try {
            await DashboardHub.joinAlertsGroup();
            isSubscribed = true;
            showLiveIndicator();

            // Refresh data by reloading the page
            // This ensures all data is current after reconnection
            location.reload();
        } catch (error) {
            console.error('[AlertsRealtime] Failed to rejoin after reconnection:', error);
        }
    }

    /**
     * Handle SignalR disconnection.
     */
    function handleDisconnected() {
        console.log('[AlertsRealtime] Disconnected from SignalR');
        isSubscribed = false;
        hideLiveIndicator();
    }

    /**
     * Start polling fallback if SignalR is unavailable.
     */
    function startPollingFallback() {
        console.warn('[AlertsRealtime] SignalR unavailable, falling back to polling');
        hideLiveIndicator();

        pollingFallbackId = setInterval(async function() {
            try {
                const response = await fetch('/api/alerts/active');
                if (response.ok) {
                    const activeCount = await response.json();
                    // Basic count update only in fallback mode
                    console.log('[AlertsRealtime] Polling - active alerts:', activeCount);
                }
            } catch (error) {
                console.error('[AlertsRealtime] Polling error:', error);
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
                await DashboardHub.leaveAlertsGroup();
            } catch (error) {
                console.warn('[AlertsRealtime] Error leaving alerts group:', error);
            }
            isSubscribed = false;
        }

        // Remove event handlers
        DashboardHub.off('OnAlertTriggered', handleAlertTriggered);
        DashboardHub.off('OnAlertResolved', handleAlertResolved);
        DashboardHub.off('OnAlertAcknowledged', handleAlertAcknowledged);
        DashboardHub.off('OnActiveAlertCountChanged', handleActiveAlertCountChanged);
        DashboardHub.off('reconnected', handleReconnected);
        DashboardHub.off('disconnected', handleDisconnected);

        console.log('[AlertsRealtime] Cleaned up');
    }

    // Helper functions
    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    function getSeverityString(severityValue) {
        switch (severityValue) {
            case 0: return 'Critical';
            case 1: return 'Warning';
            case 2: return 'Info';
            default: return 'Info';
        }
    }

    function formatDateTime(date) {
        return date.toLocaleDateString('en-US', {
            month: 'short',
            day: 'numeric',
            year: 'numeric'
        }) + ' at ' + date.toLocaleTimeString('en-US', {
            hour: '2-digit',
            minute: '2-digit',
            hour12: false
        });
    }

    function formatDuration(seconds) {
        if (seconds < 60) return `${Math.round(seconds)}s`;
        if (seconds < 3600) return `${Math.round(seconds / 60)}m`;
        return `${Math.round(seconds / 3600)}h ${Math.round((seconds % 3600) / 60)}m`;
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
    module.exports = AlertsRealtime;
}

/**
 * Acknowledge a single incident via AJAX.
 * @param {string} incidentId - The incident ID to acknowledge
 */
async function acknowledgeIncident(incidentId) {
    const btn = document.querySelector(`[data-incident-id="${incidentId}"] .acknowledge-btn`);
    if (btn) {
        btn.disabled = true;
        btn.textContent = 'Acknowledging...';
    }

    try {
        const response = await fetch(`/api/alerts/incidents/${incidentId}/acknowledge`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ notes: '' })
        });

        if (!response.ok) {
            const errorData = await response.json().catch(() => ({}));
            throw new Error(errorData.message || `Failed to acknowledge: ${response.status}`);
        }

        // UI will be updated via SignalR broadcast, but update immediately for responsiveness
        const row = document.querySelector(`[data-incident-id="${incidentId}"]`);
        if (row) {
            // Remove the action button
            const actionsDiv = row.querySelector('.alert-actions');
            if (actionsDiv) {
                actionsDiv.remove();
            }

            // Add acknowledged badge
            const metaDiv = row.querySelector('.alert-meta');
            if (metaDiv) {
                const severityBadge = metaDiv.querySelector('.severity-badge');
                if (severityBadge) {
                    const ackBadge = document.createElement('span');
                    ackBadge.className = 'status-badge status-badge-secondary';
                    ackBadge.textContent = 'Acknowledged';
                    severityBadge.insertAdjacentElement('afterend', ackBadge);
                }
            }
        }

        // Show success toast
        if (typeof Toast !== 'undefined' && Toast.success) {
            Toast.success('Incident acknowledged successfully');
        }
    } catch (error) {
        console.error('[Alerts] Failed to acknowledge incident:', error);

        // Re-enable button on error
        if (btn) {
            btn.disabled = false;
            btn.textContent = 'Acknowledge';
        }

        // Show error toast
        if (typeof Toast !== 'undefined' && Toast.error) {
            Toast.error(error.message || 'Failed to acknowledge incident');
        } else {
            alert(error.message || 'Failed to acknowledge incident');
        }
    }
}

/**
 * Acknowledge all active incidents via AJAX.
 */
async function acknowledgeAllIncidents() {
    const btn = document.getElementById('acknowledgeAllBtn');
    if (btn) {
        btn.disabled = true;
        btn.textContent = 'Acknowledging...';
    }

    try {
        const response = await fetch('/api/alerts/incidents/acknowledge-all', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            }
        });

        if (!response.ok) {
            const errorData = await response.json().catch(() => ({}));
            throw new Error(errorData.message || `Failed to acknowledge all: ${response.status}`);
        }

        const result = await response.json();

        // UI will be updated via SignalR broadcast, but update immediately for responsiveness
        document.querySelectorAll('[data-incident-id]').forEach(row => {
            const actionsDiv = row.querySelector('.alert-actions');
            if (actionsDiv) {
                actionsDiv.remove();
            }

            const metaDiv = row.querySelector('.alert-meta');
            if (metaDiv && !metaDiv.querySelector('.status-badge-secondary')) {
                const severityBadge = metaDiv.querySelector('.severity-badge');
                if (severityBadge) {
                    const ackBadge = document.createElement('span');
                    ackBadge.className = 'status-badge status-badge-secondary';
                    ackBadge.textContent = 'Acknowledged';
                    severityBadge.insertAdjacentElement('afterend', ackBadge);
                }
            }
        });

        // Hide the "Acknowledge All" button
        if (btn) {
            btn.style.display = 'none';
        }

        // Show success toast
        const count = result.acknowledgedCount || 0;
        if (typeof Toast !== 'undefined' && Toast.success) {
            Toast.success(`Acknowledged ${count} incident${count !== 1 ? 's' : ''} successfully`);
        }
    } catch (error) {
        console.error('[Alerts] Failed to acknowledge all incidents:', error);

        // Re-enable button on error
        if (btn) {
            btn.disabled = false;
            btn.textContent = 'Acknowledge All';
        }

        // Show error toast
        if (typeof Toast !== 'undefined' && Toast.error) {
            Toast.error(error.message || 'Failed to acknowledge incidents');
        } else {
            alert(error.message || 'Failed to acknowledge incidents');
        }
    }
}
