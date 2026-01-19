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
     * @param {string} filterId - The ID of the filter panel
     * @param {string} preset - The preset to apply ('today', '7days', '30days')
     */
    function setPreset(filterId, preset) {
        const form = document.querySelector(`#${filterId}-content`).closest('form');
        if (!form) {
            console.error('Filter form not found');
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
     * @param {HTMLFormElement} form - The form to submit
     */
    function preserveHashAndSubmit(form) {
        if (!form) {
            console.error('Form is null or undefined');
            return;
        }

        // Get current hash
        const currentHash = window.location.hash;

        // If there's a hash, add it to the form action
        if (currentHash) {
            const formAction = form.action || window.location.pathname;
            const separator = formAction.includes('?') ? '&' : '?';

            // Build URL with query params
            const formData = new FormData(form);
            const params = new URLSearchParams();

            for (const [key, value] of formData.entries()) {
                if (value) {
                    params.append(key, value);
                }
            }

            // Set new action with hash
            const newUrl = `${formAction}${params.toString() ? separator + params.toString() : ''}${currentHash}`;
            window.location.href = newUrl;
        } else {
            // No hash, submit normally
            form.submit();
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

    // Expose public API
    window.DateRangeFilter = {
        togglePanel,
        setPreset,
        preserveHashAndSubmit
    };
})();
