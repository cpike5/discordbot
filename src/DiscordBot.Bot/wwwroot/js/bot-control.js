/**
 * Bot Control Panel Module
 * Handles typed confirmation, status polling, and action submissions
 */
(function() {
    'use strict';

    // Configuration
    const STATUS_POLL_INTERVAL_MS = 5000; // 5 seconds for control panel
    const API_ENDPOINT = '/api/bot/status';

    let statusPollInterval = null;

    /**
     * Show a typed confirmation modal
     * @param {string} modalId - The modal element ID
     */
    function showTypedModal(modalId) {
        const modal = document.getElementById(modalId);
        if (!modal) return;

        // Reset input and button state
        const input = modal.querySelector('input[type="text"]');
        const confirmBtn = modal.querySelector('button[type="submit"]');

        if (input) {
            input.value = '';
        }
        if (confirmBtn) {
            confirmBtn.disabled = true;
        }

        modal.classList.remove('hidden');
        document.body.classList.add('overflow-hidden');

        // Focus the input
        if (input) {
            setTimeout(() => input.focus(), 100);
        }

        // Setup form submission handler
        const form = modal.querySelector('form');
        if (form && !form.dataset.handlerAttached) {
            form.dataset.handlerAttached = 'true';
            form.addEventListener('submit', handleTypedFormSubmit);
        }
    }

    /**
     * Hide a typed confirmation modal
     * @param {string} modalId - The modal element ID
     */
    function hideTypedModal(modalId) {
        const modal = document.getElementById(modalId);
        if (!modal) return;

        modal.classList.add('hidden');
        document.body.classList.remove('overflow-hidden');

        // Reset input
        const input = modal.querySelector('input[type="text"]');
        if (input) {
            input.value = '';
        }

        // Reset button
        const confirmBtn = modal.querySelector('button[type="submit"]');
        if (confirmBtn) {
            confirmBtn.disabled = true;
            const btnText = confirmBtn.querySelector('.confirm-btn-text');
            const spinner = confirmBtn.querySelector('.confirm-btn-spinner');
            if (btnText) btnText.classList.remove('hidden');
            if (spinner) spinner.classList.add('hidden');
        }
    }

    /**
     * Validate typed input and enable/disable confirm button
     * @param {HTMLInputElement} input - The input element
     */
    function validateTypedInput(input) {
        const requiredText = input.dataset.requiredText;
        const confirmBtnId = input.dataset.confirmBtn;
        const confirmBtn = document.getElementById(confirmBtnId);

        if (confirmBtn) {
            confirmBtn.disabled = input.value !== requiredText;
        }
    }

    /**
     * Handle typed confirmation form submission via AJAX
     * @param {Event} e - The submit event
     */
    async function handleTypedFormSubmit(e) {
        e.preventDefault();

        const form = e.target;
        const modal = form.closest('[role="alertdialog"]');
        const confirmBtn = form.querySelector('button[type="submit"]');
        const btnText = confirmBtn?.querySelector('.confirm-btn-text');
        const spinner = confirmBtn?.querySelector('.confirm-btn-spinner');

        // Show loading state
        if (confirmBtn) confirmBtn.disabled = true;
        if (btnText) btnText.classList.add('hidden');
        if (spinner) spinner.classList.remove('hidden');

        try {
            const formData = new FormData(form);
            const handler = formData.get('handler');
            const token = formData.get('__RequestVerificationToken');

            const response = await fetch(`?handler=${handler}`, {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': token
                },
                body: formData
            });

            const data = await response.json();

            if (response.ok && data.success) {
                window.quickActions?.showToast(data.message, 'success');
                if (modal) {
                    hideTypedModal(modal.id);
                }
            } else {
                window.quickActions?.showToast(data.message || 'Action failed.', 'error');
                // Reset button state
                if (confirmBtn) confirmBtn.disabled = false;
                if (btnText) btnText.classList.remove('hidden');
                if (spinner) spinner.classList.add('hidden');
            }
        } catch (error) {
            console.error('Form submission error:', error);
            window.quickActions?.showToast('An error occurred. Please try again.', 'error');
            // Reset button state
            if (confirmBtn) confirmBtn.disabled = false;
            if (btnText) btnText.classList.remove('hidden');
            if (spinner) spinner.classList.add('hidden');
        }
    }

    /**
     * Refresh bot status via API
     */
    async function refreshStatus() {
        const statusContainer = document.querySelector('[data-bot-control-status]');
        if (!statusContainer) return;

        try {
            const response = await fetch(API_ENDPOINT);
            if (!response.ok) throw new Error('Status fetch failed');

            const data = await response.json();

            // Update status elements
            updateStatusElement('[data-connection-state]', data.connectionState);
            updateStatusElement('[data-latency]', data.latencyMs + ' ms');
            updateStatusElement('[data-guild-count]', data.guildCount);
            updateStatusElement('[data-uptime]', formatUptime(data.uptime));
            updateStatusElement('[data-last-updated]', new Date().toISOString().substr(11, 8) + ' UTC');
            updateStatusIndicator(data.connectionState);

        } catch (error) {
            console.error('Status refresh failed:', error);
        }
    }

    /**
     * Update a status element with a new value
     * @param {string} selector - CSS selector
     * @param {string} value - New value
     */
    function updateStatusElement(selector, value) {
        const el = document.querySelector(selector);
        if (el) el.textContent = value;
    }

    /**
     * Update the status indicator color
     * @param {string} state - Connection state
     */
    function updateStatusIndicator(state) {
        const indicator = document.querySelector('[data-status-indicator]');
        if (!indicator) return;

        const isOnline = state.toUpperCase() === 'CONNECTED';

        // Remove existing classes
        indicator.classList.remove('bg-success', 'bg-error', 'animate-pulse');

        // Add appropriate classes
        if (isOnline) {
            indicator.classList.add('bg-success', 'animate-pulse');
        } else {
            indicator.classList.add('bg-error');
        }
    }

    /**
     * Format a TimeSpan string to human-readable format
     * @param {string} timeSpanString - TimeSpan in format "d.hh:mm:ss" or "hh:mm:ss"
     * @returns {string} Formatted uptime string
     */
    function formatUptime(timeSpanString) {
        if (!timeSpanString) return '0s';

        // Parse the TimeSpan string
        const parts = timeSpanString.split(':');
        let days = 0, hours = 0, minutes = 0, seconds = 0;

        if (parts.length === 3) {
            // Format: "hh:mm:ss" or "d.hh:mm:ss"
            const hourPart = parts[0];
            if (hourPart.includes('.')) {
                const dayHour = hourPart.split('.');
                days = parseInt(dayHour[0], 10);
                hours = parseInt(dayHour[1], 10);
            } else {
                hours = parseInt(hourPart, 10);
            }
            minutes = parseInt(parts[1], 10);
            seconds = parseInt(parts[2].split('.')[0], 10); // Remove fractional seconds
        }

        // Build human-readable string
        if (days > 0) {
            return `${days}d ${hours}h ${minutes}m`;
        } else if (hours > 0) {
            return `${hours}h ${minutes}m ${seconds}s`;
        } else if (minutes > 0) {
            return `${minutes}m ${seconds}s`;
        } else {
            return `${seconds}s`;
        }
    }

    /**
     * Start status polling
     */
    function startPolling() {
        const statusContainer = document.querySelector('[data-bot-control-status]');
        if (!statusContainer) return;

        // Initial refresh
        refreshStatus();

        // Start interval polling
        statusPollInterval = setInterval(refreshStatus, STATUS_POLL_INTERVAL_MS);
    }

    /**
     * Stop status polling
     */
    function stopPolling() {
        if (statusPollInterval) {
            clearInterval(statusPollInterval);
            statusPollInterval = null;
        }
    }

    /**
     * Initialize the module
     */
    function init() {
        // Start status polling
        startPolling();

        // Handle escape key to close modals
        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape') {
                const openModal = document.querySelector('[role="alertdialog"]:not(.hidden)');
                if (openModal) {
                    hideTypedModal(openModal.id);
                }
            }
        });

        // Clean up on page unload
        window.addEventListener('beforeunload', stopPolling);
    }

    // Expose public API
    window.botControl = {
        showTypedModal,
        hideTypedModal,
        validateTypedInput,
        refreshStatus,
        startPolling,
        stopPolling
    };

    // Initialize on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
