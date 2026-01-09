/**
 * Performance Dashboard - Alerts Tab Module
 * Displays performance alerts, configuration, and alert frequency chart
 */
(function() {
    'use strict';

    window.Performance = window.Performance || {};
    window.Performance.Tabs = window.Performance.Tabs || {};

    const state = {
        charts: [],
        configChanges: {},
        isInitialized: false
    };

    const ChartUtils = window.Performance.ChartUtils;
    const TimestampUtils = window.Performance.TimestampUtils;
    const TimeRange = window.Performance.TimeRange;

    function getServerData() {
        const container = document.querySelector('[data-tab="alerts"]');
        if (!container) return { alertFrequencyData: [] };

        return {
            alertFrequencyData: JSON.parse(container.dataset.alertFrequencyData || '[]')
        };
    }

    // Configuration change tracking
    function trackConfigChange(metricName, field, value) {
        if (!state.configChanges[metricName]) {
            state.configChanges[metricName] = {};
        }
        state.configChanges[metricName][field] = value;
        updateSaveButtonVisibility();
        updateCurrentValueColor(metricName);
    }

    function updateCurrentValueColor(metricName) {
        const row = document.querySelector(`tr[data-metric="${metricName}"]`);
        if (!row) return;

        const warningInput = row.querySelector('input[data-field="warning"]');
        const criticalInput = row.querySelector('input[data-field="critical"]');
        const currentValueSpan = row.querySelector('td:nth-child(4) span');

        if (!currentValueSpan) return;

        // Extract current value from the span text (e.g., "85.50%" -> 85.50)
        const currentText = currentValueSpan.textContent.trim();
        const currentValue = parseFloat(currentText);

        if (isNaN(currentValue)) return; // N/A case

        const warningThreshold = warningInput ? parseFloat(warningInput.value) : null;
        const criticalThreshold = criticalInput ? parseFloat(criticalInput.value) : null;

        // Remove existing color classes
        currentValueSpan.classList.remove('text-success', 'text-warning', 'text-error');

        // Apply new color based on thresholds
        if (criticalThreshold !== null && !isNaN(criticalThreshold) && currentValue >= criticalThreshold) {
            currentValueSpan.classList.add('text-error');
        } else if (warningThreshold !== null && !isNaN(warningThreshold) && currentValue >= warningThreshold) {
            currentValueSpan.classList.add('text-warning');
        } else {
            currentValueSpan.classList.add('text-success');
        }
    }

    function hasUnsavedChanges() {
        return Object.keys(state.configChanges).length > 0;
    }

    function updateSaveButtonVisibility() {
        const saveBtn = document.getElementById('alertsTabSaveConfigBtn');
        if (saveBtn) {
            if (hasUnsavedChanges()) {
                saveBtn.style.display = 'inline-flex';
                saveBtn.classList.remove('hidden');
            } else {
                saveBtn.style.display = 'none';
                saveBtn.classList.add('hidden');
            }
        }
    }

    // Expose save function globally for onclick handler
    window.alertsTabSaveConfig = async function() {
        if (!hasUnsavedChanges()) return;

        const saveBtn = document.getElementById('alertsTabSaveConfigBtn');

        // Disable button and show saving state
        if (saveBtn) {
            saveBtn.disabled = true;
            saveBtn.innerHTML = `
                <svg class="w-4 h-4 mr-2 animate-spin" fill="none" viewBox="0 0 24 24">
                    <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                    <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                </svg>
                Saving...
            `;
        }

        const tokenElement = document.querySelector('input[name="__RequestVerificationToken"]');
        const token = tokenElement ? tokenElement.value : '';

        // Map HTML data-field values to API property names
        const fieldNameMap = {
            'warning': 'warningThreshold',
            'critical': 'criticalThreshold',
            'enabled': 'isEnabled'
        };

        let hasError = false;
        for (const [metricName, changes] of Object.entries(state.configChanges)) {
            // Convert field names
            const apiChanges = {};
            for (const [key, value] of Object.entries(changes)) {
                const apiKey = fieldNameMap[key] || key;
                apiChanges[apiKey] = value;
            }

            try {
                const response = await fetch(`/api/alerts/config/${encodeURIComponent(metricName)}`, {
                    method: 'PUT',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': token
                    },
                    body: JSON.stringify(apiChanges)
                });

                if (!response.ok) {
                    const errorData = await response.json().catch(() => ({}));
                    throw new Error(errorData.message || `Failed to update ${metricName}`);
                }
            } catch (error) {
                console.error('Failed to save config changes:', error);
                hasError = true;
                if (typeof ToastManager !== 'undefined') {
                    ToastManager.show('error', `Failed to save configuration for ${metricName}. Please try again.`, { title: 'Error' });
                }
                break;
            }
        }

        if (!hasError) {
            state.configChanges = {};
            if (typeof ToastManager !== 'undefined') {
                ToastManager.show('success', 'Alert thresholds saved successfully.', { title: 'Saved' });
            }
            updateSaveButtonVisibility();
        }

        // Reset button state
        if (saveBtn) {
            saveBtn.disabled = false;
            saveBtn.innerHTML = `
                <svg class="w-4 h-4 mr-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
                </svg>
                Save Changes
            `;
        }
    };

    // Expose acknowledge incident function globally for onclick handler
    window.acknowledgeIncident = async function(incidentId) {
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
    };

    // Expose acknowledge all incidents function globally for onclick handler
    window.acknowledgeAllIncidents = async function() {
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
    };

    function initAlertFrequencyChart() {
        const ctx = document.getElementById('alertsFrequencyChart');
        if (!ctx) return;

        const serverData = getServerData();
        const data = serverData.alertFrequencyData;

        if (!data || data.length === 0) {
            console.warn('No alert frequency data available');
            return;
        }

        const labels = data.map(d => {
            const date = new Date(d.date);
            return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
        });

        const criticalData = data.map(d => d.criticalCount);
        const warningData = data.map(d => d.warningCount);
        const infoData = data.map(d => d.infoCount);

        const chart = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [
                    {
                        label: 'Critical',
                        data: criticalData,
                        backgroundColor: ChartUtils.colors.error,
                        borderRadius: 2
                    },
                    {
                        label: 'Warning',
                        data: warningData,
                        backgroundColor: ChartUtils.colors.warning,
                        borderRadius: 2
                    },
                    {
                        label: 'Info',
                        data: infoData,
                        backgroundColor: ChartUtils.colors.primary,
                        borderRadius: 2
                    }
                ]
            },
            options: ChartUtils.mergeOptions(ChartUtils.defaultOptions, {
                plugins: {
                    legend: {
                        display: false
                    }
                },
                scales: {
                    x: {
                        stacked: true,
                        grid: {
                            display: false
                        },
                        ticks: {
                            maxRotation: 45,
                            minRotation: 45,
                            autoSkip: true,
                            maxTicksLimit: 15
                        }
                    },
                    y: {
                        stacked: true,
                        beginAtZero: true,
                        ticks: {
                            stepSize: 1
                        }
                    }
                }
            })
        });

        state.charts.push(chart);
    }

    function initConfigChangeTracking() {
        const thresholdInputs = document.querySelectorAll('.alerts-threshold-input');
        const enabledToggles = document.querySelectorAll('.alerts-enabled-toggle');

        thresholdInputs.forEach(input => {
            // Use 'input' event for immediate feedback while typing
            input.addEventListener('input', function() {
                const row = this.closest('tr');
                const metricName = row.dataset.metric;
                const field = this.dataset.field;
                const value = parseFloat(this.value);
                trackConfigChange(metricName, field, value);
            });
        });

        enabledToggles.forEach(input => {
            input.addEventListener('change', function() {
                const row = this.closest('tr');
                const metricName = row.dataset.metric;
                const field = this.dataset.field;
                const value = this.checked;
                trackConfigChange(metricName, field, value);
            });
        });
    }

    const Alerts = {
        init: async function(hours) {
            this.destroy();
            hours = hours || TimeRange.get();

            // Use requestAnimationFrame to ensure DOM is fully rendered after AJAX injection
            await new Promise(resolve => requestAnimationFrame(resolve));

            TimestampUtils.convertTimestamps();
            initAlertFrequencyChart();
            initConfigChangeTracking();

            state.isInitialized = true;
        },

        destroy: function() {
            ChartUtils.destroyCharts(state.charts);
            state.charts = [];
            state.configChanges = {};
            state.isInitialized = false;
        }
    };

    // Expose hasUnsavedChanges for standalone page beforeunload check
    window.alertsTabHasUnsavedChanges = hasUnsavedChanges;

    window.Performance.Tabs.Alerts = Alerts;
    window.initAlertsTab = function(hours) { Alerts.init(hours); };
    window.destroyAlertsTab = function() { Alerts.destroy(); };
})();
