/**
 * Command Filters Module
 * Handles filter form submission with debounced search input.
 * Coordinates with tab loader and pagination modules.
 */
(function() {
    'use strict';

    /**
     * Module state
     */
    const state = {
        searchDebounceTimer: null,
        debounceDelay: 300,
        lastSearchValue: '',
        formId: null,
        formSelector: null,
        eventHandlers: [], // Track delegated event handlers for cleanup
        callbacks: {
            onFiltersApplied: null,
            onFiltersClear: null,
            onSearchChange: null
        }
    };

    /**
     * Initialize the command filters module.
     * Supports two modes:
     * 1. Direct mode: Targets a specific form by ID (good for single-form pages)
     * 2. Delegation mode: Uses event delegation to handle multiple forms or forms replaced by AJAX
     *
     * @param {Object} [options] - Configuration options
     * @param {string} [options.formId] - ID of a specific filter form to handle (default: 'commandFilterForm')
     * @param {string} [options.formSelector] - CSS selector for forms to handle via event delegation (e.g., '[data-filter-form]')
     * @param {number} [options.debounceDelay=300] - Search debounce delay in ms
     * @param {Function} [options.onFiltersApplied] - Callback when filters are applied (filterData)
     * @param {Function} [options.onFiltersClear] - Callback when filters are cleared
     * @param {Function} [options.onSearchChange] - Callback when search value changes (searchValue)
     *
     * @example
     * // For a specific form (direct mode):
     * CommandFilters.init({ formId: 'executionLogsFilterForm' });
     *
     * @example
     * // For event delegation (handles all forms with attribute):
     * CommandFilters.init({ formSelector: '[data-filter-form]' });
     *
     * @example
     * // Default (looks for 'commandFilterForm'):
     * CommandFilters.init();
     */
    function init(options) {
        options = options || {};

        // Configure
        if (options.debounceDelay) state.debounceDelay = options.debounceDelay;
        if (options.onFiltersApplied) state.callbacks.onFiltersApplied = options.onFiltersApplied;
        if (options.onFiltersClear) state.callbacks.onFiltersClear = options.onFiltersClear;
        if (options.onSearchChange) state.callbacks.onSearchChange = options.onSearchChange;

        // Determine mode: formSelector takes precedence for event delegation
        if (options.formSelector) {
            state.formSelector = options.formSelector;
            state.formId = null;
            setupEventDelegation();
        } else {
            state.formId = options.formId || 'commandFilterForm';
            state.formSelector = null;

            // Verify form exists
            const filterForm = document.getElementById(state.formId);
            if (!filterForm) {
                console.warn('CommandFilters: Filter form not found with ID:', state.formId);
                return;
            }

            setupEventDelegation();
        }
    }

    /**
     * Set up event delegation for filter forms.
     * This allows the module to work with forms that are replaced by AJAX.
     */
    function setupEventDelegation() {
        // Form submit handler
        const submitHandler = function(e) {
            const form = getTargetForm(e.target);
            if (form) {
                handleFormSubmit(e, form);
            }
        };
        document.addEventListener('submit', submitHandler);
        state.eventHandlers.push({ type: 'submit', handler: submitHandler });

        // Search input handler
        const inputHandler = function(e) {
            const form = getTargetForm(e.target);
            if (form && e.target.matches('input[name="search"]')) {
                handleSearchInput(e, form);
            }
        };
        document.addEventListener('input', inputHandler);
        state.eventHandlers.push({ type: 'input', handler: inputHandler });

        // Keypress handler for Enter key in search
        const keypressHandler = function(e) {
            const form = getTargetForm(e.target);
            if (form && e.target.matches('input[name="search"]') && e.key === 'Enter') {
                e.preventDefault();
                // Clear debounce and submit immediately
                clearTimeout(state.searchDebounceTimer);
                state.searchDebounceTimer = null;
                submitFilters(form);
            }
        };
        document.addEventListener('keypress', keypressHandler);
        state.eventHandlers.push({ type: 'keypress', handler: keypressHandler });

        // Date input change handler
        const changeHandler = function(e) {
            const form = getTargetForm(e.target);
            if (form && (e.target.matches('input[name="dateFrom"]') ||
                         e.target.matches('input[name="dateTo"]') ||
                         e.target.matches('input[name="status"]'))) {
                submitFilters(form);
            }
        };
        document.addEventListener('change', changeHandler);
        state.eventHandlers.push({ type: 'change', handler: changeHandler });

        // Click handler for clear buttons and date presets
        const clickHandler = function(e) {
            const form = getTargetForm(e.target);
            if (!form) return;

            if (e.target.matches('[data-action="clear-filters"]')) {
                handleClearFilters(e, form);
            } else if (e.target.matches('[data-date-preset]')) {
                e.preventDefault();
                const preset = e.target.dataset.datePreset;
                const dateFromInput = form.querySelector('input[name="dateFrom"]');
                const dateToInput = form.querySelector('input[name="dateTo"]');
                if (dateFromInput && dateToInput) {
                    applyDatePreset(preset, dateFromInput, dateToInput);
                    submitFilters(form);
                }
            }
        };
        document.addEventListener('click', clickHandler);
        state.eventHandlers.push({ type: 'click', handler: clickHandler });

        // Initialize search state for existing form
        const existingForm = getCurrentForm();
        if (existingForm) {
            const searchInput = existingForm.querySelector('input[name="search"]');
            if (searchInput) {
                state.lastSearchValue = searchInput.value || '';
            }
        }
    }

    /**
     * Get the target form for an event.
     * Checks if the event target is within a valid filter form.
     * @param {HTMLElement} target - The event target
     * @returns {HTMLFormElement|null} The filter form or null
     */
    function getTargetForm(target) {
        // Check if target is or is within a form
        const form = target.closest('form');
        if (!form) return null;

        // Check if form matches our criteria
        if (state.formSelector) {
            return form.matches(state.formSelector) ? form : null;
        } else if (state.formId) {
            return form.id === state.formId ? form : null;
        }
        return null;
    }

    /**
     * Get the current filter form.
     * @returns {HTMLFormElement|null} The filter form or null
     */
    function getCurrentForm() {
        if (state.formSelector) {
            return document.querySelector(state.formSelector);
        } else if (state.formId) {
            return document.getElementById(state.formId);
        }
        return null;
    }

    /**
     * Handle form submission.
     * @param {Event} event - The submit event
     * @param {HTMLFormElement} form - The filter form
     */
    function handleFormSubmit(event, form) {
        event.preventDefault();
        submitFilters(form);
    }

    /**
     * Handle search input with debouncing.
     * @param {Event} event - The input event
     * @param {HTMLFormElement} form - The filter form
     */
    function handleSearchInput(event, form) {
        const searchValue = event.target.value || '';

        // Notify search change immediately
        if (state.callbacks.onSearchChange) {
            state.callbacks.onSearchChange(searchValue);
        }

        // Skip if value hasn't changed
        if (searchValue === state.lastSearchValue) {
            return;
        }

        state.lastSearchValue = searchValue;

        // Clear existing timer
        if (state.searchDebounceTimer) {
            clearTimeout(state.searchDebounceTimer);
        }

        // Set new timer
        state.searchDebounceTimer = setTimeout(function() {
            submitFilters(form);
        }, state.debounceDelay);
    }

    /**
     * Submit the filter form via AJAX.
     * @param {HTMLFormElement} [form] - The filter form (optional, will auto-detect if not provided)
     */
    function submitFilters(form) {
        if (!form) {
            form = getCurrentForm();
        }
        if (!form) return;

        // Collect filter data
        const filterData = getFilterData(form);

        // Notify filters applied
        if (state.callbacks.onFiltersApplied) {
            state.callbacks.onFiltersApplied(filterData);
        }
    }

    /**
     * Get filter data from the form.
     * @param {HTMLFormElement} form - The filter form
     * @returns {Object} Filter data as key-value pairs
     */
    function getFilterData(form) {
        const formData = new FormData(form);
        const filterData = {};

        for (const entry of formData.entries()) {
            const key = entry[0];
            const value = entry[1];

            // Handle multiple values (like checkboxes)
            if (filterData[key]) {
                // Convert to array if not already
                if (!Array.isArray(filterData[key])) {
                    filterData[key] = [filterData[key]];
                }
                filterData[key].push(value);
            } else {
                filterData[key] = value;
            }
        }

        // Clean up empty values
        for (const key in filterData) {
            if (filterData.hasOwnProperty(key)) {
                const value = filterData[key];
                if (value === '' || value === null || value === undefined) {
                    delete filterData[key];
                }
            }
        }

        return filterData;
    }

    /**
     * Handle clear filters action.
     * @param {Event} event - The click event
     * @param {HTMLFormElement} form - The filter form
     */
    function handleClearFilters(event, form) {
        event.preventDefault();

        if (!form) {
            form = getCurrentForm();
        }
        if (!form) return;

        // Clear form inputs
        form.reset();

        // Clear search state
        state.lastSearchValue = '';

        // Clear any pending debounce
        if (state.searchDebounceTimer) {
            clearTimeout(state.searchDebounceTimer);
            state.searchDebounceTimer = null;
        }

        // Notify filters cleared
        if (state.callbacks.onFiltersClear) {
            state.callbacks.onFiltersClear();
        }

        // Submit with cleared filters
        submitFilters(form);
    }

    /**
     * Apply a date preset to the date inputs.
     * @param {string} preset - The preset name (today, week, month)
     * @param {HTMLInputElement} dateFromInput - The date from input
     * @param {HTMLInputElement} dateToInput - The date to input
     */
    function applyDatePreset(preset, dateFromInput, dateToInput) {
        const now = new Date();
        const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
        let fromDate = null;

        switch (preset) {
            case 'today':
                fromDate = today;
                break;
            case 'week':
                fromDate = new Date(today);
                fromDate.setDate(today.getDate() - 7);
                break;
            case 'month':
                fromDate = new Date(today);
                fromDate.setDate(today.getDate() - 30);
                break;
            default:
                return;
        }

        // Format as YYYY-MM-DD
        dateFromInput.value = formatDateForInput(fromDate);
        dateToInput.value = formatDateForInput(today);
    }

    /**
     * Format a date for input[type="date"].
     * @param {Date} date - The date to format
     * @returns {string} Formatted date string (YYYY-MM-DD)
     */
    function formatDateForInput(date) {
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const day = String(date.getDate()).padStart(2, '0');
        return year + '-' + month + '-' + day;
    }

    /**
     * Programmatically set filter values.
     * @param {Object} filters - Filter values as key-value pairs
     */
    function setFilters(filters) {
        const filterForm = getCurrentForm();
        if (!filterForm) return;

        for (const key in filters) {
            if (filters.hasOwnProperty(key)) {
                const value = filters[key];
                const input = filterForm.querySelector('[name="' + key + '"]');
                if (!input) continue;

                if (input.type === 'checkbox' || input.type === 'radio') {
                    input.checked = !!value;
                } else {
                    input.value = value || '';
                }
            }
        }

        // Update search state
        const searchInput = filterForm.querySelector('input[name="search"]');
        if (searchInput) {
            state.lastSearchValue = searchInput.value || '';
        }
    }

    /**
     * Get current filter values.
     * @returns {Object} Current filter data
     */
    function getFilters() {
        const filterForm = getCurrentForm();
        if (!filterForm) return {};

        return getFilterData(filterForm);
    }

    /**
     * Clear all filters and reset the form.
     */
    function clearFilters() {
        const filterForm = getCurrentForm();
        if (!filterForm) return;

        const clearButton = filterForm.querySelector('[data-action="clear-filters"]');
        if (clearButton) {
            clearButton.click();
        } else {
            handleClearFilters(new Event('click'), filterForm);
        }
    }

    /**
     * Trigger a filter submission programmatically.
     */
    function applyFilters() {
        submitFilters();
    }

    /**
     * Destroy the module and clean up event listeners.
     * Call this when the page is being unloaded or the module is no longer needed.
     */
    function destroy() {
        // Clear debounce timer
        if (state.searchDebounceTimer) {
            clearTimeout(state.searchDebounceTimer);
            state.searchDebounceTimer = null;
        }

        // Remove all delegated event handlers
        for (let i = 0; i < state.eventHandlers.length; i++) {
            const handler = state.eventHandlers[i];
            document.removeEventListener(handler.type, handler.handler);
        }

        // Clear state
        state.eventHandlers = [];
        state.formId = null;
        state.formSelector = null;
        state.lastSearchValue = '';
        state.callbacks = {
            onFiltersApplied: null,
            onFiltersClear: null,
            onSearchChange: null
        };
    }

    // Public API
    window.CommandFilters = {
        init: init,
        setFilters: setFilters,
        getFilters: getFilters,
        clearFilters: clearFilters,
        applyFilters: applyFilters,
        destroy: destroy
    };
})();
