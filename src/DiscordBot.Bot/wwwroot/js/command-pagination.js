/**
 * Command Pages - Pagination Module
 * Handles pagination with AJAX for unified command pages
 */
(function() {
    'use strict';

    // Namespace
    window.CommandPages = window.CommandPages || {};

    // Private state
    const state = {
        currentPage: 1,
        totalPages: 1,
        abortController: null
    };

    let cachedElements = {};

    /**
     * Initialize cached DOM elements
     */
    function cacheElements() {
        cachedElements.paginationContainer = document.querySelector('[data-command-pagination]');
        cachedElements.contentArea = document.getElementById('commandContentArea');
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
     * Parse pagination state from DOM
     */
    function parsePaginationState() {
        if (!cachedElements.paginationContainer) return;

        const currentPageEl = cachedElements.paginationContainer.querySelector('[data-current-page]');
        const totalPagesEl = cachedElements.paginationContainer.querySelector('[data-total-pages]');

        if (currentPageEl) {
            state.currentPage = parseInt(currentPageEl.dataset.currentPage, 10) || 1;
        }
        if (totalPagesEl) {
            state.totalPages = parseInt(totalPagesEl.dataset.totalPages, 10) || 1;
        }
    }

    /**
     * Validate page number
     * @param {number} pageNumber - The page number to validate
     * @returns {boolean} True if valid
     */
    function isValidPage(pageNumber) {
        return Number.isInteger(pageNumber) && pageNumber >= 1 && pageNumber <= state.totalPages;
    }

    /**
     * Navigate to a specific page
     * @param {number} pageNumber - The page number to navigate to
     */
    async function goToPage(pageNumber) {
        // Validate page number
        if (!isValidPage(pageNumber)) {
            console.warn('Invalid page number:', pageNumber);
            return;
        }

        // Already on this page
        if (pageNumber === state.currentPage) {
            return;
        }

        // Update state
        state.currentPage = pageNumber;

        // Get current tab if Tabs module is available
        let currentTab = 'list';
        if (window.CommandPages.Tabs && typeof window.CommandPages.Tabs.getCurrentTab === 'function') {
            currentTab = window.CommandPages.Tabs.getCurrentTab();
        }

        // Build URL with current filters and page number
        const endpoint = getTabEndpoint(currentTab);
        const url = new URL(endpoint, window.location.origin);

        // Add page parameter
        if (pageNumber > 1) {
            url.searchParams.append('page', pageNumber);
        }

        // Add current filter state if available
        if (window.CommandPages.Filters && typeof window.CommandPages.Filters.getState === 'function') {
            const filterState = window.CommandPages.Filters.getState();
            Object.keys(filterState).forEach(key => {
                const value = filterState[key];
                if (Array.isArray(value)) {
                    value.forEach(v => url.searchParams.append(key, v));
                } else if (value) {
                    url.searchParams.append(key, value);
                }
            });
        }

        try {
            // Cancel any in-flight request
            if (state.abortController) {
                state.abortController.abort();
            }

            // Create new abort controller for this request
            state.abortController = new AbortController();

            // Show loading state
            if (cachedElements.contentArea) {
                cachedElements.contentArea.classList.add('opacity-50', 'pointer-events-none');
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
            if (cachedElements.contentArea) {
                cachedElements.contentArea.innerHTML = html;
                cachedElements.contentArea.classList.remove('opacity-50', 'pointer-events-none');
            }

            // Re-initialize pagination for new content
            init();

            // Scroll to top of content area
            if (cachedElements.contentArea) {
                const rect = cachedElements.contentArea.getBoundingClientRect();
                const scrollTop = window.pageYOffset || document.documentElement.scrollTop;
                const targetY = rect.top + scrollTop - 100; // 100px offset for header

                window.scrollTo({
                    top: targetY,
                    behavior: 'smooth'
                });
            }

            // Update URL state if available
            if (window.CommandPages.URLState && typeof window.CommandPages.URLState.updatePage === 'function') {
                window.CommandPages.URLState.updatePage(pageNumber);
            }

        } catch (error) {
            // Silently ignore abort errors (user-initiated cancellation)
            if (error.name === 'AbortError') {
                return;
            }

            console.error('Failed to navigate to page:', error);

            // Remove loading state
            if (cachedElements.contentArea) {
                cachedElements.contentArea.classList.remove('opacity-50', 'pointer-events-none');
            }

            // Show error message
            if (typeof ToastManager !== 'undefined') {
                ToastManager.show('error', 'Failed to load page. Please try again.');
            }
        }
    }

    /**
     * Reset pagination to page 1
     * Called by filter module when filters change
     */
    function resetToPage1() {
        state.currentPage = 1;
        // Don't trigger navigation here - let the calling module handle it
    }

    /**
     * Set up event listeners for pagination links
     */
    function setupEventListeners() {
        if (!cachedElements.paginationContainer) return;

        // Use event delegation for pagination links
        cachedElements.paginationContainer.addEventListener('click', (e) => {
            // Find closest pagination link
            const link = e.target.closest('[data-page]');
            if (!link) return;

            e.preventDefault();

            const pageNumber = parseInt(link.dataset.page, 10);
            if (!isNaN(pageNumber)) {
                goToPage(pageNumber);
            }
        });
    }

    /**
     * Initialize the pagination module
     */
    function init() {
        // Cache DOM elements
        cacheElements();

        if (!cachedElements.paginationContainer) {
            // No pagination present, gracefully skip
            return;
        }

        // Parse current pagination state from DOM
        parsePaginationState();

        // Set up event listeners
        setupEventListeners();
    }

    /**
     * Destroy the module and clean up
     */
    function destroy() {
        // Abort any in-flight request
        if (state.abortController) {
            state.abortController.abort();
            state.abortController = null;
        }

        // Reset state
        state.currentPage = 1;
        state.totalPages = 1;

        // Clear cached elements
        cachedElements = {};
    }

    /**
     * Get current page number
     * @returns {number} Current page number
     */
    function getCurrentPage() {
        return state.currentPage;
    }

    /**
     * Get total pages
     * @returns {number} Total number of pages
     */
    function getTotalPages() {
        return state.totalPages;
    }

    // Public API
    const Pagination = {
        init: init,
        configure: configure,
        goToPage: goToPage,
        resetToPage1: resetToPage1,
        getCurrentPage: getCurrentPage,
        getTotalPages: getTotalPages,
        destroy: destroy
    };

    // Export to namespace
    window.CommandPages.Pagination = Pagination;

    // Convenience function for inline usage
    window.initCommandPagination = function() { Pagination.init(); };
})();
