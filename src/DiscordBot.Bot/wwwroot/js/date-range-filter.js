// date-range-filter.js
// Provides functionality for the date range filter component

(function () {
    'use strict';

    /**
     * Toggles the visibility of a filter panel
     * @param {string} filterId - The ID of the filter panel to toggle
     */
    function togglePanel(filterId) {
        const panelContent = document.getElementById(`${filterId}-content`);
        const chevron = document.getElementById(`${filterId}-chevron`);
        const button = document.getElementById(`${filterId}-toggle`);

        if (!panelContent || !chevron || !button) {
            console.error(`Filter panel elements not found for ID: ${filterId}`);
            return;
        }

        const isExpanded = button.getAttribute('aria-expanded') === 'true';
        const newExpandedState = !isExpanded;

        if (newExpandedState) {
            // Expand
            panelContent.style.maxHeight = panelContent.scrollHeight + 'px';
            chevron.style.transform = 'rotate(0deg)';
            button.setAttribute('aria-expanded', 'true');
        } else {
            // Collapse
            panelContent.style.maxHeight = '0';
            chevron.style.transform = 'rotate(-90deg)';
            button.setAttribute('aria-expanded', 'false');
        }

        // Persist state in localStorage (shared across all filter panels on Commands page)
        try {
            localStorage.setItem('commandsPage-filterPanel-expanded', newExpandedState.toString());
        } catch (e) {
            console.warn('Failed to save filter panel state:', e);
        }
    }

    /**
     * Sets a date preset and submits the form
     * @param {string} filterId - The ID of the filter panel (e.g., 'analyticsFilter', 'executionLogsFilter')
     * @param {string} preset - The preset to apply ('today', '7days', '30days')
     */
    function setPreset(filterId, preset) {
        // Map filter panel IDs to form IDs
        const formIdMap = {
            'analyticsFilter': 'analyticsFilterForm',
            'executionLogsFilter': 'executionLogsFilterForm'
        };

        const formId = formIdMap[filterId];
        if (!formId) {
            console.error('Unknown filter ID:', filterId);
            return;
        }

        const form = document.getElementById(formId);
        if (!form) {
            console.error('Filter form not found:', formId);
            return;
        }

        const startDateInput = form.querySelector('[name="StartDate"]');
        const endDateInput = form.querySelector('[name="EndDate"]');

        if (!startDateInput || !endDateInput) {
            console.error('Date inputs not found');
            return;
        }

        const today = new Date();
        today.setHours(0, 0, 0, 0);

        let startDate;
        switch (preset) {
            case 'today':
                startDate = new Date(today);
                break;
            case '7days':
                startDate = new Date(today);
                startDate.setDate(startDate.getDate() - 7);
                break;
            case '30days':
                startDate = new Date(today);
                startDate.setDate(startDate.getDate() - 30);
                break;
            default:
                console.error(`Unknown preset: ${preset}`);
                return;
        }

        // Format dates as YYYY-MM-DD for date inputs
        startDateInput.value = formatDateForInput(startDate);
        endDateInput.value = formatDateForInput(today);

        // Update button styling to show active preset
        updatePresetButtonStyles(filterId, preset);

        // Submit the form while preserving hash
        preserveHashAndSubmit(form);
    }

    /**
     * Updates the styling of preset buttons to highlight the active one
     * @param {string} filterId - The filter panel ID
     * @param {string|null} activePreset - The active preset ('today', '7days', '30days', or null for none)
     */
    function updatePresetButtonStyles(filterId, activePreset) {
        const filterPanel = document.querySelector(`[data-filter-panel="${filterId}"]`);
        if (!filterPanel) return;

        const presetButtons = filterPanel.querySelectorAll('[onclick*="setPreset"]');

        const activeClasses = ['bg-accent-blue', 'text-white', 'border-accent-blue'];
        const inactiveClasses = ['bg-bg-tertiary', 'text-text-secondary', 'border-border-primary', 'hover:bg-bg-hover'];

        presetButtons.forEach(button => {
            const onclick = button.getAttribute('onclick');
            const match = onclick.match(/setPreset\([^,]+,\s*'([^']+)'/);
            const buttonPreset = match ? match[1] : null;

            if (buttonPreset === activePreset) {
                // Make active
                button.classList.remove(...inactiveClasses);
                button.classList.add(...activeClasses);
            } else {
                // Make inactive
                button.classList.remove(...activeClasses);
                button.classList.add(...inactiveClasses);
            }
        });
    }

    /**
     * Formats a Date object as YYYY-MM-DD string
     * @param {Date} date - The date to format
     * @returns {string} The formatted date string
     */
    function formatDateForInput(date) {
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const day = String(date.getDate()).padStart(2, '0');
        return `${year}-${month}-${day}`;
    }

    /**
     * Detects which preset (if any) matches the current date range
     * @param {string} startDateStr - Start date in YYYY-MM-DD format
     * @param {string} endDateStr - End date in YYYY-MM-DD format
     * @returns {string|null} The matching preset ('today', '7days', '30days') or null
     */
    function detectActivePreset(startDateStr, endDateStr) {
        if (!startDateStr || !endDateStr) return null;

        const today = new Date();
        today.setHours(0, 0, 0, 0);

        const startDate = new Date(startDateStr + 'T00:00:00');
        const endDate = new Date(endDateStr + 'T00:00:00');

        // Check if it matches today
        if (startDate.getTime() === today.getTime() && endDate.getTime() === today.getTime()) {
            return 'today';
        }

        // Check if it matches 7 days
        const sevenDaysAgo = new Date(today);
        sevenDaysAgo.setDate(sevenDaysAgo.getDate() - 7);
        if (startDate.getTime() === sevenDaysAgo.getTime() && endDate.getTime() === today.getTime()) {
            return '7days';
        }

        // Check if it matches 30 days
        const thirtyDaysAgo = new Date(today);
        thirtyDaysAgo.setDate(thirtyDaysAgo.getDate() - 30);
        if (startDate.getTime() === thirtyDaysAgo.getTime() && endDate.getTime() === today.getTime()) {
            return '30days';
        }

        return null;
    }

    /**
     * Submits a form while preserving the URL hash
     * Triggers AJAX reload of the active tab instead of full page navigation
     * @param {HTMLFormElement} form - The form to submit
     */
    function preserveHashAndSubmit(form) {
        if (!form) {
            console.error('Form is null or undefined');
            return;
        }

        // Build filter object from form data
        const formData = new FormData(form);
        const filters = {};

        for (const [key, value] of formData.entries()) {
            // Skip ActiveTab - it's for routing, not filtering
            if (key !== 'ActiveTab' && value && value.trim && value.trim()) {
                filters[key] = value;
            }
        }

        console.log('Applying filters:', filters);

        // Detect if the date range matches a preset and update button styling
        const formToFilterMap = {
            'analyticsFilterForm': 'analyticsFilter',
            'executionLogsFilterForm': 'executionLogsFilter'
        };
        const filterId = formToFilterMap[form.id];
        if (filterId) {
            const activePreset = detectActivePreset(filters.StartDate, filters.EndDate);
            updatePresetButtonStyles(filterId, activePreset);
        }

        // Trigger AJAX reload of active tab with filters
        if (window.CommandTabLoader && typeof window.CommandTabLoader.reloadActiveTab === 'function') {
            window.CommandTabLoader.reloadActiveTab(filters);
        } else {
            console.error('CommandTabLoader not available - falling back to full page reload');
            // Fallback: update URL and reload page
            const currentHash = window.location.hash;
            const params = new URLSearchParams();

            // Add ActiveTab from form
            const activeTab = formData.get('ActiveTab');
            if (activeTab) {
                params.append('ActiveTab', activeTab);
            }

            // Add other filters
            for (const key in filters) {
                params.append(key, filters[key]);
            }

            const newUrl = window.location.pathname + '?' + params.toString() + currentHash;
            window.location.href = newUrl;
        }
    }

    /**
     * Initializes the date range filter on page load
     */
    function init() {
        // Initialize all filter panels
        document.querySelectorAll('[data-filter-panel]').forEach(panel => {
            const filterId = panel.getAttribute('data-filter-panel');
            const hasActiveFilters = panel.getAttribute('data-has-active-filters') === 'true';
            const content = document.getElementById(`${filterId}-content`);
            const chevron = document.getElementById(`${filterId}-chevron`);
            const button = document.getElementById(`${filterId}-toggle`);

            if (!content || !chevron || !button) return;

            // Check localStorage for saved state (shared across Analytics and Execution Logs tabs)
            let shouldExpand = false;
            try {
                const savedState = localStorage.getItem('commandsPage-filterPanel-expanded');
                if (savedState !== null) {
                    // Use saved state (shared across both tabs)
                    shouldExpand = savedState === 'true';
                } else {
                    // No saved state - expand if filters are active
                    shouldExpand = hasActiveFilters;
                }
            } catch (e) {
                // localStorage not available - just use active filters check
                shouldExpand = hasActiveFilters;
            }

            // Apply the expansion state (ALWAYS set all properties to ensure consistency)
            if (shouldExpand) {
                content.style.maxHeight = content.scrollHeight + 'px';
                chevron.style.transform = 'rotate(0deg)';
                button.setAttribute('aria-expanded', 'true');
            } else {
                content.style.maxHeight = '0';
                chevron.style.transform = 'rotate(-90deg)';
                button.setAttribute('aria-expanded', 'false');
            }
        });
    }

    // Initialize on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    // Re-initialize when switching tabs (to sync chevron state with localStorage)
    document.addEventListener('tabchange', function(e) {
        // Only re-init for Commands page tabs
        if (e.detail && (e.detail.tabId === 'execution-logs' || e.detail.tabId === 'analytics')) {
            // Small delay to ensure tab panel visibility is updated
            requestAnimationFrame(init);
        }
    });

    /**
     * Applies default filter (7 days) if no date range is currently set
     * Does NOT submit the form - caller is responsible for that
     * @param {string} filterId - The ID of the filter panel (e.g., 'analyticsFilter', 'executionLogsFilter')
     */
    function applyDefaultFilterIfNeeded(filterId) {
        // Map filter panel IDs to form IDs
        const formIdMap = {
            'analyticsFilter': 'analyticsFilterForm',
            'executionLogsFilter': 'executionLogsFilterForm'
        };

        const formId = formIdMap[filterId];
        if (!formId) {
            console.error('Unknown filter ID:', filterId);
            return;
        }

        const form = document.getElementById(formId);
        if (!form) {
            console.error('Filter form not found:', formId);
            return;
        }

        const startDateInput = form.querySelector('[name="StartDate"]');
        const endDateInput = form.querySelector('[name="EndDate"]');

        if (!startDateInput || !endDateInput) {
            console.error('Date inputs not found');
            return;
        }

        // Check if both date inputs are empty
        if (!startDateInput.value && !endDateInput.value) {
            console.log('No date filters set, applying default 7-day filter');

            // Calculate 7-day date range
            const today = new Date();
            today.setHours(0, 0, 0, 0);
            const sevenDaysAgo = new Date(today);
            sevenDaysAgo.setDate(sevenDaysAgo.getDate() - 7);

            // Populate the input fields
            startDateInput.value = formatDateForInput(sevenDaysAgo);
            endDateInput.value = formatDateForInput(today);

            // Update button styling to show 7-day preset as active
            updatePresetButtonStyles(filterId, '7days');
        }
    }

    /**
     * Clears all filter inputs in a form and reloads the tab
     * @param {string} formId - The ID of the filter form
     */
    function clearFiltersAndReload(formId) {
        const form = document.getElementById(formId);
        if (!form) {
            console.error('Filter form not found:', formId);
            return;
        }

        // Clear all inputs except ActiveTab
        const inputs = form.querySelectorAll('input[type="date"], input[type="text"], select');
        inputs.forEach(input => {
            if (input.name !== 'ActiveTab') {
                if (input.tagName === 'SELECT') {
                    input.selectedIndex = 0;
                } else {
                    input.value = '';
                }
            }
        });

        // Find the filter panel ID from the form ID
        const formToFilterMap = {
            'analyticsFilterForm': 'analyticsFilter',
            'executionLogsFilterForm': 'executionLogsFilter'
        };
        const filterId = formToFilterMap[formId];

        // Clear preset button styling
        if (filterId) {
            updatePresetButtonStyles(filterId, null);
        }

        // Reload tab with no filters
        if (window.CommandTabLoader && typeof window.CommandTabLoader.reloadActiveTab === 'function') {
            window.CommandTabLoader.reloadActiveTab({});
        } else {
            // Fallback: navigate to page without query params
            const activeTab = form.querySelector('[name="ActiveTab"]');
            const tabId = activeTab ? activeTab.value : '';
            const newUrl = window.location.pathname + (tabId ? '?ActiveTab=' + tabId : '') + window.location.hash;
            window.location.href = newUrl;
        }
    }

    // Expose public API
    window.DateRangeFilter = {
        togglePanel,
        setPreset,
        preserveHashAndSubmit,
        clearFiltersAndReload,
        applyDefaultFilterIfNeeded,
        initFilterPanels: init  // Expose init for re-initialization after AJAX loads
    };
})();
