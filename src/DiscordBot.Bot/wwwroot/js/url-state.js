/**
 * URL State Management Module
 * Manages URL query parameters for filters and pagination.
 * Enables shareable links, browser back/forward support, and bookmark-able views.
 */
(function() {
    'use strict';

    /**
     * Module state
     */
    const state = {
        initialized: false,
        isRestoringFromUrl: false,
        lastUrlState: null,
        popstateHandler: null,
        config: {
            paramMapping: {
                // Map form field names to URL parameter names (lowercase for URL convention)
                'StartDate': 'dateFrom',
                'EndDate': 'dateTo',
                'GuildId': 'guildId',
                'CommandName': 'commandName',
                'SearchTerm': 'searchTerm', // Changed from 'search' to match form field better
                'StatusFilter': 'status',
                'page': 'pageNumber', // Backend expects 'pageNumber' not 'page'
                'pageSize': 'pageSize'
            },
            reverseMapping: {},
            updateMethod: 'replace' // 'replace' | 'push'
        }
    };

    /**
     * Initialize the URL state module.
     * @param {Object} [options] - Configuration options
     * @param {Object} [options.paramMapping] - Custom parameter name mappings
     * @param {string} [options.updateMethod='replace'] - History update method ('replace' or 'push')
     * @param {Function} [options.onStateRestore] - Callback when state is restored from URL
     */
    function init(options) {
        if (state.initialized) {
            console.warn('UrlState: Already initialized');
            return;
        }

        options = options || {};

        // Apply custom parameter mappings if provided
        if (options.paramMapping) {
            Object.assign(state.config.paramMapping, options.paramMapping);
        }

        // Build reverse mapping (URL param name -> form field name)
        state.config.reverseMapping = {};
        for (const formField in state.config.paramMapping) {
            if (state.config.paramMapping.hasOwnProperty(formField)) {
                const urlParam = state.config.paramMapping[formField];
                state.config.reverseMapping[urlParam] = formField;
            }
        }

        // Set update method
        if (options.updateMethod) {
            state.config.updateMethod = options.updateMethod;
        }

        // Set up popstate listener for browser back/forward
        state.popstateHandler = handlePopState;
        window.addEventListener('popstate', state.popstateHandler);

        state.initialized = true;

        // Restore state from URL on init if query params present
        if (window.location.search) {
            const urlState = getStateFromUrl();
            if (Object.keys(urlState).length > 0) {
                if (options.onStateRestore) {
                    options.onStateRestore(urlState);
                }
            }
        }
    }

    /**
     * Get state from current URL query parameters.
     * @returns {Object} State object with form field names as keys
     */
    function getStateFromUrl() {
        const params = new URLSearchParams(window.location.search);
        const urlState = {};

        // Convert URL params to form field names
        for (const entry of params.entries()) {
            const urlParam = entry[0];
            const value = entry[1];

            // Check if we have a mapping for this URL param
            const formField = state.config.reverseMapping[urlParam];
            if (formField) {
                urlState[formField] = value;
            } else {
                // Keep unmapped params as-is (e.g., 'page')
                urlState[urlParam] = value;
            }
        }

        return urlState;
    }

    /**
     * Get state from filter forms and pagination.
     * @param {Object} [filterData] - Filter data from CommandFilters
     * @param {number} [currentPage] - Current page from CommandPagination
     * @returns {Object} State object with URL param names as keys
     */
    function getStateForUrl(filterData, currentPage) {
        filterData = filterData || {};
        const urlState = {};

        // Convert form field names to URL param names
        for (const formField in filterData) {
            if (filterData.hasOwnProperty(formField)) {
                const value = filterData[formField];

                // Skip empty values
                if (value === null || value === undefined || value === '') {
                    continue;
                }

                // Map to URL param name
                const urlParam = state.config.paramMapping[formField] || formField.toLowerCase();
                urlState[urlParam] = value;
            }
        }

        // Add page if provided and not page 1
        if (currentPage && currentPage > 1) {
            // Use the mapped parameter name ('pageNumber' for backend compatibility)
            const pageParam = state.config.paramMapping['page'] || 'page';
            urlState[pageParam] = currentPage.toString();
        }

        return urlState;
    }

    /**
     * Update URL with current state without page reload.
     * @param {Object} urlState - State to persist in URL (URL param names as keys)
     * @param {Object} [options] - Update options
     * @param {boolean} [options.replaceHistory=true] - Use replaceState instead of pushState
     */
    function updateUrl(urlState, options) {
        options = options || {};
        const replaceHistory = options.replaceHistory !== false;

        // Build new URL with query params
        const params = new URLSearchParams();

        for (const key in urlState) {
            if (urlState.hasOwnProperty(key)) {
                const value = urlState[key];
                // Skip empty values
                if (value === null || value === undefined || value === '') {
                    continue;
                }
                // Skip page=1 (default page, no need to show in URL)
                if (key === 'pageNumber' && (value === 1 || value === '1')) {
                    continue;
                }
                params.append(key, value);
            }
        }

        // Build new URL (preserve hash for tabs)
        const queryString = params.toString();
        const newUrl = window.location.pathname +
                      (queryString ? '?' + queryString : '') +
                      window.location.hash;

        // Only update if URL actually changed
        if (newUrl !== window.location.pathname + window.location.search + window.location.hash) {
            if (replaceHistory) {
                history.replaceState(null, '', newUrl);
            } else {
                history.pushState(null, '', newUrl);
            }

            // Store last known state
            state.lastUrlState = urlState;
        }
    }

    /**
     * Handle browser back/forward button (popstate event).
     */
    function handlePopState() {
        if (state.isRestoringFromUrl) {
            // Prevent recursive restoration
            return;
        }

        console.log('UrlState: popstate event - restoring state from URL');

        // Read state from URL
        const urlState = getStateFromUrl();

        // Mark as restoring to prevent loops
        state.isRestoringFromUrl = true;

        try {
            // Restore filters
            if (window.CommandFilters) {
                const filterValues = {};
                for (const formField in urlState) {
                    if (formField !== 'page' && formField !== 'pageSize') {
                        filterValues[formField] = urlState[formField];
                    }
                }
                if (Object.keys(filterValues).length > 0) {
                    window.CommandFilters.setFilters(filterValues);
                } else {
                    console.warn('UrlState: CommandFilters module not available');
                }
            }

            // Restore pagination with validation
            if (urlState.page && window.CommandPagination) {
                const pageNum = parseInt(urlState.page, 10);
                if (!isNaN(pageNum) && pageNum > 0) {
                    window.CommandPagination.goToPage(pageNum);
                } else {
                    console.warn('UrlState: Invalid page number in URL:', urlState.page);
                }
            } else {
                // No page param means page 1
                if (window.CommandPagination) {
                    window.CommandPagination.goToPage(1);
                }
            }

            // Trigger reload with restored state
            if (window.CommandTabLoader) {
                window.CommandTabLoader.reloadActiveTab();
            }
        } catch (error) {
            console.error('UrlState: Error restoring state from popstate:', error);
        } finally {
            // Allow future restorations
            state.isRestoringFromUrl = false;
        }
    }

    /**
     * Restore state from URL to filter forms and pagination.
     * Call this on page load to initialize forms from URL params.
     * @returns {Object} The restored state
     */
    function restoreStateFromUrl() {
        const urlState = getStateFromUrl();

        if (Object.keys(urlState).length === 0) {
            return urlState;
        }

        console.log('UrlState: Restoring state from URL:', urlState);

        // Mark as restoring to prevent loops
        state.isRestoringFromUrl = true;

        try {
            // Restore filters (excluding pagination)
            if (window.CommandFilters) {
                const filterValues = {};
                for (const formField in urlState) {
                    if (formField !== 'page' && formField !== 'pageSize') {
                        filterValues[formField] = urlState[formField];
                    }
                }
                if (Object.keys(filterValues).length > 0) {
                    window.CommandFilters.setFilters(filterValues);
                }
            } else {
                console.warn('UrlState: CommandFilters module not available for restoration');
            }

            // Restore pagination (will be used by tab loader)
            if (urlState.page && window.CommandPagination) {
                const pageNum = parseInt(urlState.page, 10);
                if (!isNaN(pageNum) && pageNum > 0) {
                    // Update internal state without triggering reload
                    // Tab loader will handle the actual load with pagination
                    window.CommandPagination.setCurrentPage(pageNum);
                } else {
                    console.warn('UrlState: Invalid page number in URL:', urlState.page);
                }
            }
        } catch (error) {
            console.error('UrlState: Error restoring state from URL:', error);
        } finally {
            state.isRestoringFromUrl = false;
        }

        return urlState;
    }

    /**
     * Clear URL query parameters (keep hash for tabs).
     */
    function clearUrl() {
        const newUrl = window.location.pathname + window.location.hash;
        history.replaceState(null, '', newUrl);
        state.lastUrlState = null;
    }

    /**
     * Check if currently restoring from URL (prevents update loops).
     * @returns {boolean} True if restoring from URL
     */
    function isRestoringFromUrl() {
        return state.isRestoringFromUrl;
    }

    /**
     * Get the parameter mapping configuration.
     * @returns {Object} The parameter mapping
     */
    function getParamMapping() {
        return state.config.paramMapping;
    }

    /**
     * Get the reverse parameter mapping (URL -> form field).
     * @returns {Object} The reverse mapping
     */
    function getReverseMapping() {
        return state.config.reverseMapping;
    }

    /**
     * Destroy the module and clean up event listeners.
     */
    function destroy() {
        if (state.popstateHandler) {
            window.removeEventListener('popstate', state.popstateHandler);
            state.popstateHandler = null;
        }

        state.initialized = false;
        state.isRestoringFromUrl = false;
        state.lastUrlState = null;
    }

    // Public API
    window.UrlState = {
        init: init,
        getStateFromUrl: getStateFromUrl,
        getStateForUrl: getStateForUrl,
        updateUrl: updateUrl,
        restoreStateFromUrl: restoreStateFromUrl,
        clearUrl: clearUrl,
        isRestoringFromUrl: isRestoringFromUrl,
        getParamMapping: getParamMapping,
        getReverseMapping: getReverseMapping,
        destroy: destroy
    };
})();
