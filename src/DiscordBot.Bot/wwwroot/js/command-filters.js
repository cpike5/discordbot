/**
 * Command Pages - Filter Module
 * Handles filter form submissions with AJAX and debouncing
 */
(function() {
    'use strict';

    // Namespace
    window.CommandPages = window.CommandPages || {};

    // Private state
    const state = {
        activeFilters: {},
        debouncedSubmit: null,
        debounceTimeout: null,
        abortController: null
    };

    let cachedElements = {};

    /**
     * Initialize cached DOM elements
     */
    function cacheElements() {
        cachedElements.filterForm = document.getElementById('commandFilterForm');
        cachedElements.searchInput = cachedElements.filterForm?.querySelector('input[type="search"], input[name="search"]');
        cachedElements.dateInputs = cachedElements.filterForm?.querySelectorAll('input[type="date"]');
        cachedElements.dropdowns = cachedElements.filterForm?.querySelectorAll('select');
        cachedElements.checkboxes = cachedElements.filterForm?.querySelectorAll('input[type="checkbox"]');
        cachedElements.clearButton = cachedElements.filterForm?.querySelector('[data-clear-filters]');
        cachedElements.quickDatePresets = cachedElements.filterForm?.querySelectorAll('[data-date-preset]');
    }

    /**
     * Debounce utility function
     * @param {Function} func - Function to debounce
     * @param {number} wait - Milliseconds to wait
     * @returns {Function} Debounced function
     */
    function debounce(func, wait) {
        return function executedFunction(...args) {
            const later = () => {
                clearTimeout(state.debounceTimeout);
                func(...args);
            };
            clearTimeout(state.debounceTimeout);
            state.debounceTimeout = setTimeout(later, wait);
        };
    }

    /**
     * Collect current filter values from form
     * @returns {Object} Key-value pairs of filter data
     */
    function collectFilters() {
        if (!cachedElements.filterForm) return {};

        const formData = new FormData(cachedElements.filterForm);
        const filters = {};

        // Convert FormData to plain object
        for (const [key, value] of formData.entries()) {
            // Skip anti-forgery token
            if (key === '__RequestVerificationToken') continue;

            // Handle multiple values (e.g., checkboxes with same name)
            if (filters[key]) {
                if (Array.isArray(filters[key])) {
                    filters[key].push(value);
                } else {
                    filters[key] = [filters[key], value];
                }
            } else {
                filters[key] = value;
            }
        }

        return filters;
    }

    /**
     * Submit filters via AJAX
     * @param {boolean} resetPage - Whether to reset to page 1
     */
    async function submitFilters(resetPage = true) {
        if (!cachedElements.filterForm) return;

        // Collect current filter state
        state.activeFilters = collectFilters();

        // Reset pagination to page 1 when filters change
        if (resetPage && window.CommandPages.Pagination && typeof window.CommandPages.Pagination.resetToPage1 === 'function') {
            window.CommandPages.Pagination.resetToPage1();
        }

        // Get current tab if Tabs module is available
        let currentTab = 'list';
        if (window.CommandPages.Tabs && typeof window.CommandPages.Tabs.getCurrentTab === 'function') {
            currentTab = window.CommandPages.Tabs.getCurrentTab();
        }

        // Build URL with filters
        const endpoint = getTabEndpoint(currentTab);
        const url = new URL(endpoint, window.location.origin);

        Object.keys(state.activeFilters).forEach(key => {
            const value = state.activeFilters[key];
            if (Array.isArray(value)) {
                value.forEach(v => url.searchParams.append(key, v));
            } else if (value) {
                url.searchParams.append(key, value);
            }
        });

        // Add current page if not page 1
        if (window.CommandPages.Pagination && typeof window.CommandPages.Pagination.getCurrentPage === 'function') {
            const currentPage = window.CommandPages.Pagination.getCurrentPage();
            if (currentPage > 1) {
                url.searchParams.append('page', currentPage);
            }
        }

        try {
            // Cancel any in-flight request
            if (state.abortController) {
                state.abortController.abort();
            }

            // Create new abort controller for this request
            state.abortController = new AbortController();

            // Show loading state in content area
            const contentArea = document.getElementById('commandContentArea');
            if (contentArea) {
                contentArea.classList.add('opacity-50', 'pointer-events-none');
            }

            const response = await fetch(url.toString(), {
                method: 'GET',
                headers: {
                    'Accept': 'text/html'
                },
                signal: state.abortController.signal
            });

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }

            const html = await response.text();

            // Update content area
            if (contentArea) {
                contentArea.innerHTML = html;
                contentArea.classList.remove('opacity-50', 'pointer-events-none');
            }

            // Re-initialize pagination module for new content
            if (window.CommandPages.Pagination && typeof window.CommandPages.Pagination.init === 'function') {
                window.CommandPages.Pagination.init();
            }

            // Update URL state if available
            if (window.CommandPages.URLState && typeof window.CommandPages.URLState.updateFilters === 'function') {
                window.CommandPages.URLState.updateFilters(state.activeFilters);
            }

        } catch (error) {
            // Silently ignore abort errors (user-initiated cancellation)
            if (error.name === 'AbortError') {
                return;
            }

            console.error('Failed to submit filters:', error);

            // Remove loading state
            const contentArea = document.getElementById('commandContentArea');
            if (contentArea) {
                contentArea.classList.remove('opacity-50', 'pointer-events-none');
            }

            // Show error message
            if (typeof ToastManager !== 'undefined') {
                ToastManager.show('error', 'Failed to apply filters. Please try again.');
            }
        }
    }

    // Configuration
    let config = {
        endpoints: {
            'list': '/Admin/Performance/Commands/ListContent',
            'analytics': '/Admin/Performance/Commands/AnalyticsContent',
            'logs': '/Admin/Performance/Commands/LogsContent'
        }
    };

    /**
     * Get API endpoint for current tab
     * @param {string} tabName - The tab name
     * @returns {string} The API endpoint URL
     */
    function getTabEndpoint(tabName) {
        return config.endpoints[tabName] || config.endpoints.list;
    }

    /**
     * Configure module with custom options
     * @param {Object} options - Configuration options
     * @param {Object} options.endpoints - Custom endpoints for tabs
     */
    function configure(options) {
        if (options && options.endpoints) {
            config.endpoints = { ...config.endpoints, ...options.endpoints };
        }
    }

    /**
     * Clear all filters and reload
     */
    function clearFilters() {
        if (!cachedElements.filterForm) return;

        // Reset form
        cachedElements.filterForm.reset();

        // Clear state
        state.activeFilters = {};

        // Submit to reload content
        submitFilters(true);
    }

    /**
     * Apply quick date preset
     * @param {string} preset - The preset name (today, last7days, last30days)
     */
    function applyDatePreset(preset) {
        if (!cachedElements.filterForm) return;

        const now = new Date();
        const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
        let startDate, endDate;

        switch (preset) {
            case 'today':
                startDate = today;
                endDate = today;
                break;
            case 'last7days':
                startDate = new Date(today);
                startDate.setDate(startDate.getDate() - 7);
                endDate = today;
                break;
            case 'last30days':
                startDate = new Date(today);
                startDate.setDate(startDate.getDate() - 30);
                endDate = today;
                break;
            default:
                return;
        }

        // Format dates as YYYY-MM-DD
        const formatDate = (date) => {
            const year = date.getFullYear();
            const month = String(date.getMonth() + 1).padStart(2, '0');
            const day = String(date.getDate()).padStart(2, '0');
            return `${year}-${month}-${day}`;
        };

        // Set date inputs
        const startDateInput = cachedElements.filterForm.querySelector('input[name="startDate"], input[name="StartDate"]');
        const endDateInput = cachedElements.filterForm.querySelector('input[name="endDate"], input[name="EndDate"]');

        if (startDateInput) {
            startDateInput.value = formatDate(startDate);
        }
        if (endDateInput) {
            endDateInput.value = formatDate(endDate);
        }

        // Submit filters immediately
        submitFilters(true);
    }

    /**
     * Set up event listeners for filter form
     */
    function setupEventListeners() {
        if (!cachedElements.filterForm) return;

        // Create debounced submit function for search inputs
        state.debouncedSubmit = debounce(() => submitFilters(true), 300);

        // Search input: debounced submission
        if (cachedElements.searchInput) {
            cachedElements.searchInput.addEventListener('input', () => {
                state.debouncedSubmit();
            });

            // Also submit on Enter key
            cachedElements.searchInput.addEventListener('keydown', (e) => {
                if (e.key === 'Enter') {
                    e.preventDefault();
                    // Cancel debounce and submit immediately
                    clearTimeout(state.debounceTimeout);
                    submitFilters(true);
                }
            });
        }

        // Dropdowns: immediate submission
        if (cachedElements.dropdowns) {
            cachedElements.dropdowns.forEach(dropdown => {
                dropdown.addEventListener('change', () => {
                    submitFilters(true);
                });
            });
        }

        // Checkboxes: immediate submission
        if (cachedElements.checkboxes) {
            cachedElements.checkboxes.forEach(checkbox => {
                checkbox.addEventListener('change', () => {
                    submitFilters(true);
                });
            });
        }

        // Date inputs: immediate submission
        if (cachedElements.dateInputs) {
            cachedElements.dateInputs.forEach(dateInput => {
                dateInput.addEventListener('change', () => {
                    submitFilters(true);
                });
            });
        }

        // Clear button
        if (cachedElements.clearButton) {
            cachedElements.clearButton.addEventListener('click', (e) => {
                e.preventDefault();
                clearFilters();
            });
        }

        // Quick date presets
        if (cachedElements.quickDatePresets) {
            cachedElements.quickDatePresets.forEach(preset => {
                preset.addEventListener('click', (e) => {
                    e.preventDefault();
                    const presetValue = preset.dataset.datePreset;
                    applyDatePreset(presetValue);
                });
            });
        }

        // Prevent traditional form submission
        cachedElements.filterForm.addEventListener('submit', (e) => {
            e.preventDefault();
            // Cancel any pending debounce and submit immediately
            clearTimeout(state.debounceTimeout);
            submitFilters(true);
        });

        // Support Enter key anywhere in form
        cachedElements.filterForm.addEventListener('keydown', (e) => {
            if (e.key === 'Enter' && e.target.tagName !== 'TEXTAREA') {
                e.preventDefault();
                clearTimeout(state.debounceTimeout);
                submitFilters(true);
            }
        });
    }

    /**
     * Initialize the filter module
     */
    function init() {
        // Cache DOM elements
        cacheElements();

        if (!cachedElements.filterForm) {
            // No filter form present, gracefully skip
            return;
        }

        // Collect initial filter state
        state.activeFilters = collectFilters();

        // Set up event listeners
        setupEventListeners();
    }

    /**
     * Destroy the module and clean up
     */
    function destroy() {
        // Clear debounce timeout
        if (state.debounceTimeout) {
            clearTimeout(state.debounceTimeout);
            state.debounceTimeout = null;
        }

        // Abort any in-flight request
        if (state.abortController) {
            state.abortController.abort();
            state.abortController = null;
        }

        // Reset state
        state.activeFilters = {};
        state.debouncedSubmit = null;

        // Clear cached elements
        cachedElements = {};
    }

    /**
     * Get current filter state
     * @returns {Object} Current filter values
     */
    function getState() {
        return { ...state.activeFilters };
    }

    // Public API
    const Filters = {
        init: init,
        configure: configure,
        submitFilters: submitFilters,
        clearFilters: clearFilters,
        getState: getState,
        destroy: destroy
    };

    // Export to namespace
    window.CommandPages.Filters = Filters;

    // Convenience function for inline usage
    window.initCommandFilters = function() { Filters.init(); };
})();
