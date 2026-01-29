/**
 * Settings Page Module
 * Handles tab switching, form submissions, and settings management
 */
(function() {
    'use strict';

    let currentCategory = window.initialActiveCategory || 'General';
    let isDirty = false;

    // Bot Control polling configuration
    const STATUS_POLL_INTERVAL_MS = 5000; // 5 seconds for control panel
    const API_ENDPOINT = '/api/bot/status';
    let statusPollInterval = null;

    // Icon SVG templates for button states
    const icons = {
        save: '<svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" /></svg>',
        loading: '<svg class="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24" stroke="currentColor"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path></svg>',
        success: '<svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>',
        error: '<svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" /></svg>',
        info: '<svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>'
    };

    // Store original button states for reset
    const buttonOriginalStates = new WeakMap();

    /**
     * Build form data with proper checkbox handling
     * Checkboxes need special handling because unchecked boxes don't submit values
     * @param {HTMLFormElement} form - The form element
     * @returns {FormData} - FormData with correct checkbox values
     */
    function buildFormData(form) {
        const formData = new FormData();

        // Add the anti-forgery token
        const token = form.querySelector('input[name="__RequestVerificationToken"]');
        if (token) {
            formData.append('__RequestVerificationToken', token.value);
        }

        // Process all toggle checkboxes - add their current state (true/false)
        const toggles = form.querySelectorAll('input[data-setting-toggle]');
        toggles.forEach(toggle => {
            formData.append(toggle.name, toggle.checked ? 'true' : 'false');
        });

        // Process all other form inputs (text, number, select, etc.)
        const inputs = form.querySelectorAll('input:not([type="checkbox"]):not([type="hidden"]), select, textarea');
        inputs.forEach(input => {
            if (input.name && !input.name.startsWith('__')) {
                formData.append(input.name, input.value);
            }
        });

        return formData;
    }

    /**
     * Store the original state of a button for later reset
     * @param {HTMLButtonElement} button - The button element
     */
    function storeButtonState(button) {
        if (!button || buttonOriginalStates.has(button)) return;
        buttonOriginalStates.set(button, {
            innerHTML: button.innerHTML,
            disabled: button.disabled,
            classList: [...button.classList]
        });
    }

    /**
     * Reset a button to its original state
     * @param {HTMLButtonElement} button - The button element
     */
    function resetButtonState(button) {
        if (!button) return;
        const original = buttonOriginalStates.get(button);
        if (original) {
            button.innerHTML = original.innerHTML;
            button.disabled = original.disabled;
            // Reset classes
            button.classList.remove('btn-save-success', 'btn-save-error', 'btn-save-info');
            button.classList.add('bg-accent-orange', 'hover:bg-orange-600', 'active:bg-orange-700');
        } else {
            // Fallback for category save buttons
            button.disabled = false;
            button.innerHTML = 'Save Changes';
            button.classList.remove('btn-save-success', 'btn-save-error', 'btn-save-info');
            button.classList.add('bg-accent-orange', 'hover:bg-orange-600', 'active:bg-orange-700');
        }
    }

    /**
     * Set button to loading state
     * @param {HTMLButtonElement} button - The button element
     */
    function setButtonLoading(button) {
        if (!button) return;
        storeButtonState(button);
        button.disabled = true;
        button.innerHTML = `${icons.loading} Saving...`;
    }

    /**
     * Set button to success state
     * @param {HTMLButtonElement} button - The button element
     * @param {boolean} autoReset - Whether to auto-reset after 2 seconds
     */
    function setButtonSuccess(button, autoReset = true) {
        if (!button) return;
        button.disabled = true;
        button.innerHTML = `${icons.success} Saved!`;
        button.classList.remove('bg-accent-orange', 'hover:bg-orange-600', 'active:bg-orange-700', 'btn-save-error', 'btn-save-info');
        button.classList.add('btn-save-success');

        if (autoReset) {
            setTimeout(() => resetButtonState(button), 2000);
        }
    }

    /**
     * Set button to error state (allows retry)
     * @param {HTMLButtonElement} button - The button element
     */
    function setButtonError(button) {
        if (!button) return;
        button.disabled = false; // Allow retry
        button.innerHTML = `${icons.error} Save Failed - Retry`;
        button.classList.remove('bg-accent-orange', 'hover:bg-orange-600', 'active:bg-orange-700', 'btn-save-success', 'btn-save-info');
        button.classList.add('btn-save-error');
    }

    /**
     * Set button to info state (no changes detected)
     * @param {HTMLButtonElement} button - The button element
     */
    function setButtonInfo(button) {
        if (!button) return;
        button.disabled = true;
        button.innerHTML = `${icons.info} No Changes`;
        button.classList.remove('bg-accent-orange', 'hover:bg-orange-600', 'active:bg-orange-700', 'btn-save-success', 'btn-save-error');
        button.classList.add('btn-save-info');

        setTimeout(() => resetButtonState(button), 2000);
    }

    /**
     * Show inline success alert
     * @param {string} message - The success message
     * @param {string} category - Optional category for category-specific alerts
     */
    function showInlineSuccess(message, category = null) {
        const alertId = category ? `saveSuccessAlert-${category}` : 'saveSuccessAlert';
        const alert = document.getElementById(alertId);
        if (!alert) return;

        const messageEl = alert.querySelector('.inline-alert-message');
        if (messageEl) {
            messageEl.textContent = message;
        }

        alert.classList.remove('hidden');

        // Announce to screen readers
        announceToScreenReader('success', message);
    }

    /**
     * Show inline error alert
     * @param {string} message - The error message
     * @param {string} category - Optional category for category-specific alerts
     */
    function showInlineError(message, category = null) {
        const alertId = category ? `saveErrorAlert-${category}` : 'saveErrorAlert';
        const alert = document.getElementById(alertId);
        if (!alert) return;

        const messageEl = alert.querySelector('.inline-alert-message');
        if (messageEl) {
            messageEl.textContent = message;
        }

        alert.classList.remove('hidden');

        // Announce to screen readers
        announceToScreenReader('error', message);
    }

    /**
     * Hide inline alerts
     * @param {string} category - Optional category for category-specific alerts
     */
    function hideInlineAlerts(category = null) {
        if (category) {
            const successAlert = document.getElementById(`saveSuccessAlert-${category}`);
            const errorAlert = document.getElementById(`saveErrorAlert-${category}`);
            if (successAlert) successAlert.classList.add('hidden');
            if (errorAlert) errorAlert.classList.add('hidden');
        } else {
            const successAlert = document.getElementById('saveSuccessAlert');
            const errorAlert = document.getElementById('saveErrorAlert');
            if (successAlert) successAlert.classList.add('hidden');
            if (errorAlert) errorAlert.classList.add('hidden');
        }
    }

    /**
     * Announce message to screen readers via ARIA live region
     * @param {string} type - Message type (success, error, info)
     * @param {string} message - The message to announce
     */
    function announceToScreenReader(type, message) {
        // Use the toast live region if available
        const liveRegion = document.getElementById('toastLiveRegion');
        if (liveRegion) {
            const typeLabels = {
                success: 'Success',
                error: 'Error',
                info: 'Information'
            };
            liveRegion.textContent = `${typeLabels[type] || type}: ${message}`;

            // Clear after a short delay to allow for repeated announcements
            setTimeout(() => {
                liveRegion.textContent = '';
            }, 1000);
        }
    }

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
        if (!input) return;

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
            updateStatusElement('[data-last-updated]', new Date().toLocaleTimeString());
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
     * Switch between settings category tabs
     * @param {string} category - The category name (General, Logging, Features, Advanced)
     */
    function switchTab(category) {
        if (isDirty && !confirm('You have unsaved changes. Are you sure you want to switch tabs?')) {
            return;
        }

        currentCategory = category;

        // Update tab buttons
        const tabs = document.querySelectorAll('.settings-tab');
        tabs.forEach(tab => {
            if (tab.dataset.tab === category) {
                tab.classList.add('active');
                tab.setAttribute('aria-selected', 'true');
            } else {
                tab.classList.remove('active');
                tab.setAttribute('aria-selected', 'false');
            }
        });

        // Update section visibility
        const sections = document.querySelectorAll('.settings-section');
        sections.forEach(section => {
            if (section.id === `${category.replace(/([a-z])([A-Z])/g, '$1-$2').toLowerCase()}-settings`) {
                section.classList.add('active');
            } else {
                section.classList.remove('active');
            }
        });

        // Manage Bot Control status polling based on tab visibility
        if (category === 'BotControl') {
            startPolling();
        } else {
            stopPolling();
        }

        // Reset dirty flag when switching tabs
        isDirty = false;
    }

    /**
     * Save settings for a specific category
     * @param {string} category - The category to save
     */
    async function saveCategory(category) {
        const form = document.getElementById('settingsForm');
        if (!form) return;

        const formData = buildFormData(form);
        const token = document.querySelector('input[name="__RequestVerificationToken"]').value;

        // Get the save button from the event
        const saveButton = event?.target;

        // Hide any existing inline alerts for this category
        hideInlineAlerts(category);

        // Show loading state
        setButtonLoading(saveButton);

        try {
            const response = await fetch(`?handler=SaveCategory&category=${category}`, {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': token
                },
                body: formData
            });

            const data = await response.json();

            if (response.ok && data.success) {
                // Show success button state
                const willReload = data.restartRequired;
                setButtonSuccess(saveButton, !willReload);

                // Show inline success alert
                showInlineSuccess(data.message, category);

                // Show toast
                window.quickActions?.showToast(data.message, 'success');
                isDirty = false;

                // If restart required, reload page to show banner
                if (willReload) {
                    setTimeout(() => window.location.reload(), 1500);
                }
            } else {
                const errorMsg = data.errors ? data.errors.join(', ') : data.message || 'Failed to save settings.';

                // Show error button state (allows retry)
                setButtonError(saveButton);

                // Show inline error alert
                showInlineError(errorMsg, category);

                // Show toast with longer duration for errors
                window.quickActions?.showToast(errorMsg, 'error');
            }
        } catch (error) {
            console.error('Save category error:', error);
            const errorMsg = 'An error occurred while saving settings.';

            // Show error button state
            setButtonError(saveButton);

            // Show inline error alert
            showInlineError(errorMsg, category);

            // Show toast
            window.quickActions?.showToast(errorMsg, 'error');
        }
    }

    /**
     * Build form data for command module toggles
     * @returns {FormData} - FormData with command module states
     */
    function buildCommandModulesFormData() {
        const formData = new FormData();

        // Add the anti-forgery token
        const form = document.getElementById('settingsForm');
        const token = form?.querySelector('input[name="__RequestVerificationToken"]');
        if (token) {
            formData.append('__RequestVerificationToken', token.value);
        }

        // Process all command module toggles - add their current state (true/false)
        const toggles = document.querySelectorAll('input[data-command-module-toggle]');
        toggles.forEach(toggle => {
            if (!toggle.disabled) {
                formData.append(toggle.name, toggle.checked ? 'true' : 'false');
            }
        });

        return formData;
    }

    /**
     * Save command module configurations
     */
    async function saveCommandModules() {
        const formData = buildCommandModulesFormData();
        const token = document.querySelector('input[name="__RequestVerificationToken"]').value;

        // Get the save button from the event
        const saveButton = event?.target;

        // Hide any existing inline alerts for Commands
        hideInlineAlerts('Commands');

        // Show loading state
        setButtonLoading(saveButton);

        try {
            const response = await fetch('?handler=SaveCommandModules', {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': token
                },
                body: formData
            });

            const data = await response.json();

            if (response.ok && data.success) {
                // Show success button state
                const willReload = data.restartRequired;
                setButtonSuccess(saveButton, !willReload);

                // Show inline success alert
                showInlineSuccess(data.message, 'Commands');

                // Show toast
                window.quickActions?.showToast(data.message, 'success');
                isDirty = false;

                // If restart required, reload page to show banner
                if (willReload) {
                    setTimeout(() => window.location.reload(), 1500);
                }
            } else {
                const errorMsg = data.errors ? data.errors.join(', ') : data.message || 'Failed to save command module settings.';

                // Show error button state (allows retry)
                setButtonError(saveButton);

                // Show inline error alert
                showInlineError(errorMsg, 'Commands');

                // Show toast with longer duration for errors
                window.quickActions?.showToast(errorMsg, 'error');
            }
        } catch (error) {
            console.error('Save command modules error:', error);
            const errorMsg = 'An error occurred while saving command module settings.';

            // Show error button state
            setButtonError(saveButton);

            // Show inline error alert
            showInlineError(errorMsg, 'Commands');

            // Show toast
            window.quickActions?.showToast(errorMsg, 'error');
        }
    }

    /**
     * Save all settings across all categories
     */
    async function saveAllSettings() {
        const form = document.getElementById('settingsForm');
        if (!form) return;

        const formData = buildFormData(form);
        const token = document.querySelector('input[name="__RequestVerificationToken"]').value;

        // Get the save button from the event
        const saveButton = event?.target;

        // Hide any existing inline alerts (global)
        hideInlineAlerts();

        // Show loading state
        setButtonLoading(saveButton);

        try {
            const response = await fetch('?handler=SaveAll', {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': token
                },
                body: formData
            });

            const data = await response.json();

            if (response.ok && data.success) {
                // Show success button state
                const willReload = data.restartRequired;
                setButtonSuccess(saveButton, !willReload);

                // Show inline success alert
                showInlineSuccess(data.message);

                // Show toast
                window.quickActions?.showToast(data.message, 'success');
                isDirty = false;

                // If restart required, reload page to show banner
                if (willReload) {
                    setTimeout(() => window.location.reload(), 1500);
                }
            } else {
                const errorMsg = data.errors ? data.errors.join(', ') : data.message || 'Failed to save all settings.';

                // Show error button state (allows retry)
                setButtonError(saveButton);

                // Show inline error alert
                showInlineError(errorMsg);

                // Show toast with longer duration for errors
                window.quickActions?.showToast(errorMsg, 'error');
            }
        } catch (error) {
            console.error('Save all error:', error);
            const errorMsg = 'An error occurred while saving settings.';

            // Show error button state
            setButtonError(saveButton);

            // Show inline error alert
            showInlineError(errorMsg);

            // Show toast
            window.quickActions?.showToast(errorMsg, 'error');
        }
    }

    /**
     * Show reset category confirmation modal
     * @param {string} category - The category to reset
     */
    function showResetCategoryModal(category) {
        currentCategory = category;
        window.quickActions?.showConfirmationModal('resetCategoryModal');

        // Attach handler to the modal form
        const modal = document.getElementById('resetCategoryModal');
        if (modal) {
            const form = modal.querySelector('form');
            if (form && !form.dataset.categoryHandler) {
                form.dataset.categoryHandler = 'true';
                form.addEventListener('submit', async (e) => {
                    e.preventDefault();
                    await resetCategory(category);
                });
            }
        }
    }

    /**
     * Reset a category to default values
     * @param {string} category - The category to reset
     */
    async function resetCategory(category) {
        const token = document.querySelector('input[name="__RequestVerificationToken"]').value;

        try {
            const response = await fetch(`?handler=ResetCategory&category=${category}`, {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': token,
                    'Content-Type': 'application/x-www-form-urlencoded'
                }
            });

            const data = await response.json();

            if (response.ok && data.success) {
                window.quickActions?.showToast(data.message, 'success');
                window.quickActions?.hideConfirmationModal('resetCategoryModal');

                // Reload page to show updated values
                setTimeout(() => window.location.reload(), 1000);
            } else {
                const errorMsg = data.errors ? data.errors.join(', ') : data.message;
                window.quickActions?.showToast(errorMsg || 'Failed to reset category.', 'error');
            }
        } catch (error) {
            console.error('Reset category error:', error);
            window.quickActions?.showToast('An error occurred while resetting settings.', 'error');
        }
    }

    /**
     * Show reset all settings confirmation modal
     */
    function showResetAllModal() {
        window.quickActions?.showConfirmationModal('resetAllModal');

        // Attach handler to the modal form
        const modal = document.getElementById('resetAllModal');
        if (modal) {
            const form = modal.querySelector('form');
            if (form && !form.dataset.resetAllHandler) {
                form.dataset.resetAllHandler = 'true';
                form.addEventListener('submit', async (e) => {
                    e.preventDefault();
                    await resetAll();
                });
            }
        }
    }

    /**
     * Reset all settings to defaults
     */
    async function resetAll() {
        const token = document.querySelector('input[name="__RequestVerificationToken"]').value;

        try {
            const response = await fetch('?handler=ResetAll', {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': token,
                    'Content-Type': 'application/x-www-form-urlencoded'
                }
            });

            const data = await response.json();

            if (response.ok && data.success) {
                window.quickActions?.showToast(data.message, 'success');
                window.quickActions?.hideConfirmationModal('resetAllModal');

                // Reload page to show updated values
                setTimeout(() => window.location.reload(), 1000);
            } else {
                const errorMsg = data.errors ? data.errors.join(', ') : data.message;
                window.quickActions?.showToast(errorMsg || 'Failed to reset all settings.', 'error');
            }
        } catch (error) {
            console.error('Reset all error:', error);
            window.quickActions?.showToast('An error occurred while resetting settings.', 'error');
        }
    }

    /**
     * Save appearance settings (default theme)
     */
    async function saveAppearance() {
        const formData = new FormData();

        // Add the anti-forgery token
        const form = document.getElementById('settingsForm');
        const token = form?.querySelector('input[name="__RequestVerificationToken"]');
        if (token) {
            formData.append('__RequestVerificationToken', token.value);
        }

        // Get the selected theme ID
        const themeSelect = document.getElementById('SelectedThemeId');
        if (themeSelect) {
            formData.append('SelectedThemeId', themeSelect.value);
        }

        // Get the save button from the event
        const saveButton = event?.target;

        // Hide any existing inline alerts for Appearance
        hideInlineAlerts('Appearance');

        // Show loading state
        setButtonLoading(saveButton);

        try {
            const response = await fetch('?handler=SaveAppearance', {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': token?.value || ''
                },
                body: formData
            });

            const data = await response.json();

            if (response.ok && data.success) {
                // Show success button state
                setButtonSuccess(saveButton);

                // Show inline success alert
                showInlineSuccess(data.message, 'Appearance');

                // Show toast
                window.quickActions?.showToast(data.message, 'success');
                isDirty = false;
            } else {
                const errorMsg = data.errors ? data.errors.join(', ') : data.message || 'Failed to save appearance settings.';

                // Show error button state (allows retry)
                setButtonError(saveButton);

                // Show inline error alert
                showInlineError(errorMsg, 'Appearance');

                // Show toast with longer duration for errors
                window.quickActions?.showToast(errorMsg, 'error');
            }
        } catch (error) {
            console.error('Save appearance error:', error);
            const errorMsg = 'An error occurred while saving appearance settings.';

            // Show error button state
            setButtonError(saveButton);

            // Show inline error alert
            showInlineError(errorMsg, 'Appearance');

            // Show toast
            window.quickActions?.showToast(errorMsg, 'error');
        }
    }

    /**
     * Reset appearance settings to default
     */
    async function resetAppearance() {
        const token = document.querySelector('input[name="__RequestVerificationToken"]').value;

        // Get the reset button from the event
        const resetButton = event?.target;

        // Hide any existing inline alerts for Appearance
        hideInlineAlerts('Appearance');

        try {
            const response = await fetch('?handler=ResetAppearance', {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': token,
                    'Content-Type': 'application/x-www-form-urlencoded'
                }
            });

            const data = await response.json();

            if (response.ok && data.success) {
                // Show inline success alert
                showInlineSuccess(data.message, 'Appearance');

                // Show toast
                window.quickActions?.showToast(data.message, 'success');

                // Reload page to show updated default theme selection
                setTimeout(() => window.location.reload(), 1000);
            } else {
                const errorMsg = data.errors ? data.errors.join(', ') : data.message || 'Failed to reset appearance settings.';

                // Show inline error alert
                showInlineError(errorMsg, 'Appearance');

                // Show toast
                window.quickActions?.showToast(errorMsg, 'error');
            }
        } catch (error) {
            console.error('Reset appearance error:', error);
            const errorMsg = 'An error occurred while resetting appearance settings.';

            // Show inline error alert
            showInlineError(errorMsg, 'Appearance');

            // Show toast
            window.quickActions?.showToast(errorMsg, 'error');
        }
    }

    /**
     * Track form changes to set dirty flag
     */
    function trackFormChanges() {
        const form = document.getElementById('settingsForm');
        if (!form) return;

        form.addEventListener('input', () => {
            isDirty = true;
        });

        form.addEventListener('change', () => {
            isDirty = true;
        });
    }

    /**
     * Warn user about unsaved changes before leaving
     */
    function setupUnloadWarning() {
        window.addEventListener('beforeunload', (e) => {
            if (isDirty) {
                e.preventDefault();
                e.returnValue = 'You have unsaved changes. Are you sure you want to leave?';
                return e.returnValue;
            }
        });
    }

    /**
     * Initialize the module
     */
    function init() {
        trackFormChanges();
        setupUnloadWarning();

        // Start polling if Bot Control tab is active on page load
        if (currentCategory === 'BotControl') {
            startPolling();
        }

        // Handle escape key to close modals
        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape') {
                const openModal = document.querySelector('[role="alertdialog"]:not(.hidden)');
                if (openModal) {
                    // Try typed modal first, then quick actions modal
                    if (openModal.id && typeof hideTypedModal === 'function') {
                        hideTypedModal(openModal.id);
                    } else if (window.quickActions) {
                        window.quickActions.hideConfirmationModal(openModal.id);
                    }
                }
            }
        });

        // Clean up polling on page unload
        window.addEventListener('beforeunload', stopPolling);
    }

    // Expose public API
    window.settingsManager = {
        switchTab,
        saveCategory,
        saveAllSettings,
        saveCommandModules,
        saveAppearance,
        resetAppearance,
        showResetCategoryModal,
        showResetAllModal,
        // Bot Control functions
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
