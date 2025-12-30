/**
 * Settings Page Module
 * Handles tab switching, form submissions, and settings management
 */
(function() {
    'use strict';

    let currentCategory = 'General';
    let isDirty = false;

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
            if (section.id === `${category.toLowerCase()}-settings`) {
                section.classList.add('active');
            } else {
                section.classList.remove('active');
            }
        });

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

        // Show loading state
        const saveButton = event?.target;
        if (saveButton) {
            saveButton.disabled = true;
            saveButton.innerHTML = '<svg class="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24" stroke="currentColor"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path></svg> Saving...';
        }

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
                window.quickActions?.showToast(data.message, 'success');
                isDirty = false;

                // If restart required, reload page to show banner
                if (data.restartRequired) {
                    setTimeout(() => window.location.reload(), 1500);
                }
            } else {
                const errorMsg = data.errors ? data.errors.join(', ') : data.message;
                window.quickActions?.showToast(errorMsg || 'Failed to save settings.', 'error');
            }
        } catch (error) {
            console.error('Save category error:', error);
            window.quickActions?.showToast('An error occurred while saving settings.', 'error');
        } finally {
            // Reset button state
            if (saveButton) {
                saveButton.disabled = false;
                saveButton.innerHTML = 'Save Changes';
            }
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

        // Show loading state
        const saveButton = event?.target;
        if (saveButton) {
            saveButton.disabled = true;
            const originalText = saveButton.innerHTML;
            saveButton.innerHTML = '<svg class="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24" stroke="currentColor"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path></svg> Saving...';
        }

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
                window.quickActions?.showToast(data.message, 'success');
                isDirty = false;

                // If restart required, reload page to show banner
                if (data.restartRequired) {
                    setTimeout(() => window.location.reload(), 1500);
                }
            } else {
                const errorMsg = data.errors ? data.errors.join(', ') : data.message;
                window.quickActions?.showToast(errorMsg || 'Failed to save all settings.', 'error');
            }
        } catch (error) {
            console.error('Save all error:', error);
            window.quickActions?.showToast('An error occurred while saving settings.', 'error');
        } finally {
            // Reset button state
            if (saveButton) {
                saveButton.disabled = false;
                saveButton.innerHTML = '<svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" /></svg> Save All';
            }
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

        // Handle escape key to close modals
        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape') {
                const openModal = document.querySelector('[role="alertdialog"]:not(.hidden)');
                if (openModal && window.quickActions) {
                    window.quickActions.hideConfirmationModal(openModal.id);
                }
            }
        });
    }

    // Expose public API
    window.settingsManager = {
        switchTab,
        saveCategory,
        saveAllSettings,
        showResetCategoryModal,
        showResetAllModal
    };

    // Initialize on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
