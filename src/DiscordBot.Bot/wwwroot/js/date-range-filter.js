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

        if (isExpanded) {
            // Collapse
            panelContent.style.maxHeight = '0';
            chevron.style.transform = 'rotate(-90deg)';
            button.setAttribute('aria-expanded', 'false');
        } else {
            // Expand
            panelContent.style.maxHeight = panelContent.scrollHeight + 'px';
            chevron.style.transform = 'rotate(0deg)';
            button.setAttribute('aria-expanded', 'true');
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

        // Submit the form while preserving hash
        preserveHashAndSubmit(form);
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
        // Auto-expand panels if filters are active
        document.querySelectorAll('[data-filter-panel]').forEach(panel => {
            const filterId = panel.getAttribute('data-filter-panel');
            const hasActiveFilters = panel.getAttribute('data-has-active-filters') === 'true';

            if (hasActiveFilters) {
                const content = document.getElementById(`${filterId}-content`);
                const button = document.getElementById(`${filterId}-toggle`);

                if (content && button) {
                    content.style.maxHeight = content.scrollHeight + 'px';
                    button.setAttribute('aria-expanded', 'true');
                }
            }
        });
    }

    // Initialize on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
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
        clearFiltersAndReload
    };
})();
