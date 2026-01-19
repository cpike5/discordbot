/**
 * Command Pagination Module
 * Handles pagination link clicks via AJAX.
 * Coordinates with tab loader and filter modules.
 */
(function() {
    'use strict';

    /**
     * Module state
     */
    const state = {
        currentPage: 1,
        isLoading: false,
        scrollBehavior: 'top', // 'top' | 'maintain' | 'none'
        delegatedClickHandler: null,
        callbacks: {
            onPageChange: null,
            onLoadStart: null,
            onLoadComplete: null,
            onError: null
        }
    };

    /**
     * Initialize the command pagination module.
     * @param {Object} [options] - Configuration options
     * @param {string} [options.scrollBehavior='top'] - Scroll behavior after page load ('top', 'maintain', 'none')
     * @param {Function} [options.onPageChange] - Callback when page changes (pageNumber)
     * @param {Function} [options.onLoadStart] - Callback when load starts
     * @param {Function} [options.onLoadComplete] - Callback when load completes (pageNumber)
     * @param {Function} [options.onError] - Callback on error (error)
     */
    function init(options) {
        options = options || {};

        // Configure
        if (options.scrollBehavior) state.scrollBehavior = options.scrollBehavior;
        if (options.onPageChange) state.callbacks.onPageChange = options.onPageChange;
        if (options.onLoadStart) state.callbacks.onLoadStart = options.onLoadStart;
        if (options.onLoadComplete) state.callbacks.onLoadComplete = options.onLoadComplete;
        if (options.onError) state.callbacks.onError = options.onError;

        // Create and store the delegated click handler
        state.delegatedClickHandler = handlePaginationClick;

        // Set up event delegation on document
        // This allows pagination to work even after AJAX updates
        document.addEventListener('click', state.delegatedClickHandler);

        // Determine initial page from URL or data attribute
        detectCurrentPage();
    }

    /**
     * Detect the current page number from pagination elements.
     */
    function detectCurrentPage() {
        const activePage = document.querySelector('.pagination [aria-current="page"]');
        if (activePage) {
            const pageNum = parseInt(activePage.dataset.page || activePage.textContent, 10);
            if (!isNaN(pageNum)) {
                state.currentPage = pageNum;
            }
        }
    }

    /**
     * Handle pagination link clicks with event delegation.
     * This function delegates to the tab loader module to perform the actual AJAX load.
     * @param {MouseEvent} event - The click event
     */
    function handlePaginationClick(event) {
        // Find pagination link
        const link = event.target.closest('.pagination a[data-page], .pagination button[data-page]');
        if (!link || state.isLoading) return;

        // Ignore disabled links
        if (link.classList.contains('disabled') || link.disabled) return;

        event.preventDefault();

        // Get page number
        const pageNumber = parseInt(link.dataset.page, 10);
        if (isNaN(pageNumber) || pageNumber === state.currentPage) return;

        // Store scroll position if needed
        const scrollPosition = state.scrollBehavior === 'maintain' ? window.scrollY : null;

        // Update state
        state.currentPage = pageNumber;

        // Notify page change (this callback should trigger the tab loader to reload with page param)
        if (state.callbacks.onPageChange) {
            state.callbacks.onPageChange(pageNumber);
        }

        // Handle scroll behavior after content loads
        // Use requestAnimationFrame for smoother scroll timing
        if (state.scrollBehavior === 'top') {
            requestAnimationFrame(function() {
                // Find results container and scroll to it
                const resultsContainer = document.querySelector('.command-results, [data-results-container]');
                if (resultsContainer) {
                    resultsContainer.scrollIntoView({ behavior: 'smooth', block: 'start' });
                } else {
                    window.scrollTo({ top: 0, behavior: 'smooth' });
                }
            });
        } else if (state.scrollBehavior === 'maintain' && scrollPosition !== null) {
            // Restore scroll position after DOM update
            requestAnimationFrame(function() {
                window.scrollTo({ top: scrollPosition, behavior: 'auto' });
            });
        }
    }

    /**
     * Get the current page number.
     * @returns {number} The current page number
     */
    function getCurrentPage() {
        return state.currentPage;
    }

    /**
     * Set the current page number without triggering callbacks.
     * Used for restoring state from URL without causing reload loops.
     * @param {number} pageNumber - The page number to set
     */
    function setCurrentPage(pageNumber) {
        if (isNaN(pageNumber) || pageNumber < 1) {
            console.warn('CommandPagination: Invalid page number:', pageNumber);
            return;
        }
        state.currentPage = pageNumber;
    }

    /**
     * Programmatically navigate to a specific page.
     * @param {number} pageNumber - The page number to navigate to
     */
    function goToPage(pageNumber) {
        if (isNaN(pageNumber) || pageNumber < 1) {
            console.warn('CommandPagination: Invalid page number:', pageNumber);
            return;
        }

        // Find the pagination link
        const link = document.querySelector('.pagination a[data-page="' + pageNumber + '"], .pagination button[data-page="' + pageNumber + '"]');
        if (link) {
            link.click();
        } else {
            // If link not found, update state and notify
            state.currentPage = pageNumber;
            if (state.callbacks.onPageChange) {
                state.callbacks.onPageChange(pageNumber);
            }
        }
    }

    /**
     * Navigate to the first page.
     */
    function goToFirstPage() {
        const firstLink = document.querySelector('.pagination a[data-page="1"], .pagination button[data-page="1"]');
        if (firstLink) {
            firstLink.click();
        }
    }

    /**
     * Navigate to the last page.
     */
    function goToLastPage() {
        const paginationLinks = document.querySelectorAll('.pagination a[data-page], .pagination button[data-page]');
        if (paginationLinks.length === 0) return;

        // Find highest page number
        let maxPage = 1;
        for (let i = 0; i < paginationLinks.length; i++) {
            const pageNum = parseInt(paginationLinks[i].dataset.page, 10);
            if (!isNaN(pageNum) && pageNum > maxPage) {
                maxPage = pageNum;
            }
        }

        goToPage(maxPage);
    }

    /**
     * Navigate to the next page.
     */
    function goToNextPage() {
        const nextLink = document.querySelector('.pagination a[aria-label="Next"], .pagination button[aria-label="Next"]');
        if (nextLink && !nextLink.classList.contains('disabled')) {
            nextLink.click();
        }
    }

    /**
     * Navigate to the previous page.
     */
    function goToPreviousPage() {
        const prevLink = document.querySelector('.pagination a[aria-label="Previous"], .pagination button[aria-label="Previous"]');
        if (prevLink && !prevLink.classList.contains('disabled')) {
            prevLink.click();
        }
    }

    /**
     * Update pagination UI after content load.
     * Call this after AJAX content is loaded to refresh pagination state.
     */
    function updatePaginationState() {
        detectCurrentPage();
    }

    /**
     * Set scroll behavior for pagination navigation.
     * @param {string} behavior - 'top' | 'maintain' | 'none'
     */
    function setScrollBehavior(behavior) {
        if (behavior === 'top' || behavior === 'maintain' || behavior === 'none') {
            state.scrollBehavior = behavior;
        } else {
            console.warn('CommandPagination: Invalid scroll behavior:', behavior);
        }
    }

    /**
     * Get current scroll behavior setting.
     * @returns {string} The current scroll behavior
     */
    function getScrollBehavior() {
        return state.scrollBehavior;
    }

    /**
     * Check if pagination is currently loading.
     * Note: This tracks external loading state set via setLoading().
     * The module itself delegates actual AJAX loading to the tab loader.
     * @returns {boolean} True if loading
     */
    function isLoading() {
        return state.isLoading;
    }

    /**
     * Set loading state (for external coordination).
     * This should be called by the coordinating code (e.g., tab loader)
     * to indicate when pagination-triggered loads are in progress.
     * @param {boolean} loading - The loading state
     */
    function setLoading(loading) {
        state.isLoading = loading;
    }

    /**
     * Build URL with page parameter for API requests.
     * @param {string} baseUrl - The base API URL
     * @param {number} pageNumber - The page number
     * @param {Object} [additionalParams] - Additional query parameters
     * @returns {string} The complete URL with query string
     */
    function buildPageUrl(baseUrl, pageNumber, additionalParams) {
        additionalParams = additionalParams || {};

        const params = new URLSearchParams();

        // Add page parameter
        params.append('page', pageNumber);

        // Add additional parameters
        for (const key in additionalParams) {
            if (additionalParams.hasOwnProperty(key)) {
                const value = additionalParams[key];
                if (value !== null && value !== undefined && value !== '') {
                    if (Array.isArray(value)) {
                        for (let i = 0; i < value.length; i++) {
                            params.append(key, value[i]);
                        }
                    } else {
                        params.append(key, value);
                    }
                }
            }
        }

        const queryString = params.toString();
        return queryString ? baseUrl + '?' + queryString : baseUrl;
    }

    /**
     * Extract page number from URL or link href.
     * @param {string} url - The URL to parse
     * @returns {number|null} The page number or null if not found
     */
    function extractPageNumber(url) {
        try {
            const urlObj = new URL(url, window.location.origin);
            const pageParam = urlObj.searchParams.get('page');
            if (pageParam) {
                const pageNum = parseInt(pageParam, 10);
                return isNaN(pageNum) ? null : pageNum;
            }
        } catch (error) {
            console.error('CommandPagination: Failed to parse URL:', error);
        }
        return null;
    }

    /**
     * Destroy the module and clean up event listeners.
     * Call this when the page is being unloaded or the module is no longer needed.
     */
    function destroy() {
        // Remove delegated event listener
        if (state.delegatedClickHandler) {
            document.removeEventListener('click', state.delegatedClickHandler);
            state.delegatedClickHandler = null;
        }

        // Clear state
        state.currentPage = 1;
        state.isLoading = false;
        state.scrollBehavior = 'top';
        state.callbacks = {
            onPageChange: null,
            onLoadStart: null,
            onLoadComplete: null,
            onError: null
        };
    }

    // Public API
    window.CommandPagination = {
        init: init,
        getCurrentPage: getCurrentPage,
        setCurrentPage: setCurrentPage,
        goToPage: goToPage,
        goToFirstPage: goToFirstPage,
        goToLastPage: goToLastPage,
        goToNextPage: goToNextPage,
        goToPreviousPage: goToPreviousPage,
        updatePaginationState: updatePaginationState,
        setScrollBehavior: setScrollBehavior,
        getScrollBehavior: getScrollBehavior,
        isLoading: isLoading,
        setLoading: setLoading,
        buildPageUrl: buildPageUrl,
        extractPageNumber: extractPageNumber,
        destroy: destroy
    };
})();
