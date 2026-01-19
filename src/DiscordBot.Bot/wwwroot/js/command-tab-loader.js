/**
 * Command Tab Loader Module
 * Handles lazy loading of tab content via AJAX for the Commands page.
 * Integrates with tab-panel.js and coordinates with filter/pagination modules.
 */
(function() {
    'use strict';

    /**
     * Module state
     */
    const state = {
        activeTabId: null,
        loadedTabs: new Set(),
        isLoading: false,
        abortController: null,
        tabContainer: null,
        callbacks: {
            onTabChange: null,
            onLoadStart: null,
            onLoadComplete: null,
            onError: null
        }
    };

    /**
     * Map tab IDs to API routes
     */
    const apiRouteMap = {
        'command-list': 'list',
        'execution-logs': 'logs',
        'analytics': 'analytics'
    };

    /**
     * Initialize the tab loader module.
     * @param {Object} [options] - Configuration options
     * @param {Function} [options.onTabChange] - Callback when tab changes (tabId)
     * @param {Function} [options.onLoadStart] - Callback when load starts
     * @param {Function} [options.onLoadComplete] - Callback when load completes (tabId, content)
     * @param {Function} [options.onError] - Callback on error (error, tabId)
     */
    function init(options) {
        options = options || {};

        // Register callbacks
        if (options.onTabChange) state.callbacks.onTabChange = options.onTabChange;
        if (options.onLoadStart) state.callbacks.onLoadStart = options.onLoadStart;
        if (options.onLoadComplete) state.callbacks.onLoadComplete = options.onLoadComplete;
        if (options.onError) state.callbacks.onError = options.onError;

        // Listen for tab change events from TabPanel module
        state.tabContainer = document.querySelector('[data-panel-id="commandTabs"]');
        if (!state.tabContainer) {
            console.warn('CommandTabLoader: Tab panel container not found');
            return;
        }

        // Listen for custom tabchange event
        state.tabContainer.addEventListener('tabchange', handleTabChange);

        // Determine initial active tab
        const activeTab = state.tabContainer.querySelector('[role="tab"][aria-selected="true"]');
        if (activeTab) {
            const tabId = activeTab.dataset.tabId;
            state.activeTabId = tabId;

            // Check if tab is already loaded (server-rendered)
            const panel = document.getElementById(activeTab.getAttribute('aria-controls'));
            if (panel && panel.dataset.loaded === 'true') {
                state.loadedTabs.add(tabId);
            }
        }
    }

    /**
     * Handle tab change events from TabPanel.
     * @param {CustomEvent} event - The tabchange event
     */
    async function handleTabChange(event) {
        if (state.isLoading) {
            // Cancel previous load
            if (state.abortController) {
                state.abortController.abort();
            }
        }

        const tabId = event.detail.tabId;
        if (!tabId) return;

        // Update state
        const previousTabId = state.activeTabId;
        state.activeTabId = tabId;

        // Notify tab change
        if (state.callbacks.onTabChange) {
            state.callbacks.onTabChange(tabId, previousTabId);
        }

        // Load content if not already loaded
        if (!state.loadedTabs.has(tabId)) {
            await loadTabContent(tabId);
        }
    }

    /**
     * Load tab content from the API.
     * @param {string} tabId - The tab identifier
     * @param {Object} [filters] - Optional filter parameters
     */
    async function loadTabContent(tabId, filters) {
        filters = filters || null;

        const tab = document.querySelector('[role="tab"][data-tab-id="' + tabId + '"]');
        if (!tab) {
            console.warn('CommandTabLoader: Tab not found: ' + tabId);
            return;
        }

        const panelId = tab.getAttribute('aria-controls');
        const panel = document.getElementById(panelId);
        if (!panel) {
            console.warn('CommandTabLoader: Panel not found: ' + panelId);
            return;
        }

        // Create abort controller for this request
        state.abortController = new AbortController();
        state.isLoading = true;

        // Notify load start
        if (state.callbacks.onLoadStart) {
            state.callbacks.onLoadStart(tabId);
        }

        try {
            // Build API URL
            const apiUrl = buildApiUrl(tabId, filters);

            const response = await fetch(apiUrl, {
                method: 'GET',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                },
                signal: state.abortController.signal
            });

            if (!response.ok) {
                throw new Error('HTTP ' + response.status + ': ' + response.statusText);
            }

            const html = await response.text();

            // Find content container or use panel
            var contentContainer = panel.querySelector('[data-tab-content]');
            var target = contentContainer || panel;

            // Update content
            target.innerHTML = html;
            panel.dataset.loaded = 'true';
            state.loadedTabs.add(tabId);

            // Notify load complete
            if (state.callbacks.onLoadComplete) {
                state.callbacks.onLoadComplete(tabId, html);
            }

        } catch (error) {
            // Ignore abort errors
            if (error.name === 'AbortError') {
                console.log('CommandTabLoader: Request aborted for tab: ' + tabId);
                return;
            }

            console.error('CommandTabLoader: Failed to load tab content:', error);

            // Show error in panel (CSP-compliant)
            panel.innerHTML =
                '<div class="text-center py-12">' +
                    '<div class="text-error mb-4">' +
                        '<svg class="w-12 h-12 mx-auto" fill="none" stroke="currentColor" viewBox="0 0 24 24">' +
                            '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" ' +
                                  'd="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"></path>' +
                        '</svg>' +
                    '</div>' +
                    '<p class="text-lg font-semibold text-default mb-2">Failed to load content</p>' +
                    '<p class="text-muted mb-4">An error occurred while loading the tab content.</p>' +
                    '<button data-action="reload-page" class="btn btn-primary">Reload Page</button>' +
                '</div>';

            // Add event listener for reload button (CSP-compliant)
            const reloadButton = panel.querySelector('[data-action="reload-page"]');
            if (reloadButton) {
                reloadButton.addEventListener('click', function() {
                    location.reload();
                });
            }

            // Notify error
            if (state.callbacks.onError) {
                state.callbacks.onError(error, tabId);
            }
        } finally {
            state.isLoading = false;
            state.abortController = null;
        }
    }

    /**
     * Build API URL for tab content with filters.
     * Maps tab IDs to actual API routes.
     * @param {string} tabId - The tab identifier (e.g., 'command-list', 'execution-logs', 'analytics')
     * @param {Object|null} filters - Optional filter parameters
     * @returns {string} The API URL with query string
     */
    function buildApiUrl(tabId, filters) {
        filters = filters || null;

        // Map tab ID to API route
        const apiRoute = apiRouteMap[tabId] || tabId;
        const baseUrl = '/api/commands/' + apiRoute;

        // If filters provided, use them
        if (filters) {
            const params = new URLSearchParams();
            for (const key in filters) {
                if (filters.hasOwnProperty(key)) {
                    const value = filters[key];
                    if (value !== null && value !== undefined && value !== '') {
                        params.append(key, value);
                    }
                }
            }
            const queryString = params.toString();
            return queryString ? baseUrl + '?' + queryString : baseUrl;
        }

        // Otherwise, try to get filters from form
        const filterForm = document.getElementById('commandFilterForm');
        if (!filterForm) {
            // Log warning if form was expected but not found
            if (tabId === 'execution-logs') {
                console.warn('CommandTabLoader: Filter form not found for logs tab');
            }
            return baseUrl;
        }

        const formData = new FormData(filterForm);
        const params = new URLSearchParams();

        for (const entry of formData.entries()) {
            const key = entry[0];
            const value = entry[1];
            if (value && value.trim && value.trim()) {
                params.append(key, value);
            }
        }

        const queryString = params.toString();
        return queryString ? baseUrl + '?' + queryString : baseUrl;
    }

    /**
     * Reload the content of the currently active tab.
     * Forces a refresh even if already loaded.
     * @param {Object} [filters] - Optional filter parameters
     */
    async function reloadActiveTab(filters) {
        filters = filters || null;

        if (!state.activeTabId) {
            console.warn('CommandTabLoader: No active tab to reload');
            return;
        }

        // Remove from loaded set to force reload
        state.loadedTabs.delete(state.activeTabId);

        await loadTabContent(state.activeTabId, filters);
    }

    /**
     * Reload a specific tab's content.
     * @param {string} tabId - The tab identifier
     * @param {Object} [filters] - Optional filter parameters
     */
    async function reloadTab(tabId, filters) {
        filters = filters || null;

        state.loadedTabs.delete(tabId);
        await loadTabContent(tabId, filters);
    }

    /**
     * Get the currently active tab ID.
     * @returns {string|null} The active tab ID
     */
    function getActiveTab() {
        return state.activeTabId;
    }

    /**
     * Check if a tab has been loaded.
     * @param {string} tabId - The tab identifier
     * @returns {boolean} True if loaded
     */
    function isTabLoaded(tabId) {
        return state.loadedTabs.has(tabId);
    }

    /**
     * Clear the loaded state for a specific tab or all tabs.
     * @param {string} [tabId] - Optional tab ID to clear, or omit to clear all
     */
    function clearLoadedState(tabId) {
        tabId = tabId || null;

        if (tabId) {
            state.loadedTabs.delete(tabId);
            const tab = document.querySelector('[role="tab"][data-tab-id="' + tabId + '"]');
            if (tab) {
                const panelId = tab.getAttribute('aria-controls');
                const panel = document.getElementById(panelId);
                if (panel) {
                    panel.dataset.loaded = 'false';
                }
            }
        } else {
            state.loadedTabs.clear();
            const panels = document.querySelectorAll('[role="tabpanel"]');
            for (let i = 0; i < panels.length; i++) {
                panels[i].dataset.loaded = 'false';
            }
        }
    }

    /**
     * Check if a load is currently in progress.
     * @returns {boolean} True if loading
     */
    function isLoading() {
        return state.isLoading;
    }

    /**
     * Destroy the module and clean up event listeners.
     * Call this when the page is being unloaded or the module is no longer needed.
     */
    function destroy() {
        // Remove event listener
        if (state.tabContainer) {
            state.tabContainer.removeEventListener('tabchange', handleTabChange);
        }

        // Abort any in-flight requests
        if (state.abortController) {
            state.abortController.abort();
        }

        // Clear state
        state.activeTabId = null;
        state.loadedTabs.clear();
        state.isLoading = false;
        state.abortController = null;
        state.tabContainer = null;
        state.callbacks = {
            onTabChange: null,
            onLoadStart: null,
            onLoadComplete: null,
            onError: null
        };
    }

    // Public API
    window.CommandTabLoader = {
        init: init,
        reloadActiveTab: reloadActiveTab,
        reloadTab: reloadTab,
        getActiveTab: getActiveTab,
        isTabLoaded: isTabLoaded,
        clearLoadedState: clearLoadedState,
        isLoading: isLoading,
        destroy: destroy
    };
})();
