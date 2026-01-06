/**
 * Performance Tabs Module
 * Provides AJAX loading infrastructure for Performance Dashboard tabs.
 * Handles caching, loading states, error handling, and tab lifecycle.
 */
(function() {
    'use strict';

    const PerformanceTabs = {
        // Configuration
        config: {
            endpoints: {
                overview: '/api/performance/tabs/overview',
                health: '/api/performance/tabs/health',
                commands: '/api/performance/tabs/commands',
                api: '/api/performance/tabs/api',
                system: '/api/performance/tabs/system',
                alerts: '/api/performance/tabs/alerts'
            },
            cacheTimeout: 300000, // 5 minutes in ms
            requestTimeout: 10000, // 10 seconds in ms
            defaultTab: 'overview',
            tabPanelSelector: '.tab-panel',
            tabLinkSelector: '.performance-tab',
            loadingDelay: 150 // Delay before showing loading spinner (prevents flash)
        },

        // State management
        state: {
            loadedTabs: new Map(), // tabId -> { timestamp, content, abortController }
            activeTab: null,
            currentRequest: null,
            currentHours: 24,
            isInitialized: false
        },

        /**
         * Initialize the tab system
         * @param {Object} options - Optional configuration overrides
         */
        init: function(options = {}) {
            if (this.state.isInitialized) {
                console.warn('PerformanceTabs already initialized');
                return;
            }

            // Merge options
            Object.assign(this.config, options);

            // Bind event listeners
            this.bindTabListeners();
            this.bindTimeRangeListeners();
            this.bindInternalLinkListeners();

            // Load initial tab (overview by default)
            const activeTabLink = document.querySelector(this.config.tabLinkSelector + '.active');
            const initialTab = activeTabLink ? this.getTabIdFromLink(activeTabLink) : this.config.defaultTab;

            this.state.activeTab = initialTab;
            this.loadTab(initialTab, this.state.currentHours, false);

            this.state.isInitialized = true;
            console.log('PerformanceTabs initialized, loading tab:', initialTab);
        },

        /**
         * Bind click events to tab navigation links
         */
        bindTabListeners: function() {
            const self = this;
            document.querySelectorAll(this.config.tabLinkSelector).forEach(link => {
                link.addEventListener('click', function(e) {
                    e.preventDefault();
                    const tabId = self.getTabIdFromLink(this);
                    if (tabId && tabId !== self.state.activeTab) {
                        self.switchTab(tabId);
                    }
                });
            });
        },

        /**
         * Bind click events to time range selector buttons
         */
        bindTimeRangeListeners: function() {
            const self = this;
            document.querySelectorAll('.time-range-btn, [data-hours]').forEach(btn => {
                btn.addEventListener('click', function(e) {
                    const hours = parseInt(this.dataset.hours, 10);
                    if (!isNaN(hours) && hours !== self.state.currentHours) {
                        self.changeTimeRange(hours);

                        // Update active button state
                        document.querySelectorAll('.time-range-btn').forEach(b => {
                            b.classList.toggle('active', parseInt(b.dataset.hours, 10) === hours);
                        });
                    }
                });
            });
        },

        /**
         * Bind click events to internal tab links (e.g., "View Details" links)
         */
        bindInternalLinkListeners: function() {
            const self = this;
            // Use event delegation for dynamically loaded content
            document.addEventListener('click', function(e) {
                const link = e.target.closest('[data-tab-link]');
                if (link) {
                    e.preventDefault();
                    const tabId = link.dataset.tabLink;
                    if (tabId && self.config.endpoints[tabId]) {
                        self.switchTab(tabId);
                    }
                }
            });
        },

        /**
         * Extract tab ID from a tab navigation link
         * @param {HTMLElement} link - The tab link element
         * @returns {string|null} The tab ID
         */
        getTabIdFromLink: function(link) {
            const href = link.getAttribute('href') || '';

            // Check for data attribute first
            if (link.dataset.tabId) {
                return link.dataset.tabId;
            }

            // Parse from URL path
            const path = href.replace(/^\/Admin\/Performance\/?/, '').toLowerCase();

            if (!path || path === '' || path === 'index') return 'overview';
            if (path.includes('health')) return 'health';
            if (path.includes('commands')) return 'commands';
            if (path.includes('api')) return 'api';
            if (path.includes('system')) return 'system';
            if (path.includes('alerts')) return 'alerts';

            return null;
        },

        /**
         * Switch to a different tab
         * @param {string} tabId - The tab to switch to
         */
        switchTab: function(tabId) {
            if (!this.config.endpoints[tabId]) {
                console.warn('Unknown tab:', tabId);
                return;
            }

            // Cleanup current tab before switching
            this.destroyTab(this.state.activeTab);

            // Cancel any pending request
            this.cancelPendingRequest();

            // Update tab navigation active state
            this.updateTabNavigation(tabId);

            // Load the new tab
            this.state.activeTab = tabId;
            this.loadTab(tabId, this.state.currentHours, false);

            // Dispatch custom event for external listeners
            document.dispatchEvent(new CustomEvent('performanceTabChanged', {
                detail: { tabId: tabId, hours: this.state.currentHours }
            }));
        },

        /**
         * Update tab navigation UI to reflect active tab
         * @param {string} tabId - The active tab ID
         */
        updateTabNavigation: function(tabId) {
            document.querySelectorAll(this.config.tabLinkSelector).forEach(link => {
                const linkTabId = this.getTabIdFromLink(link);
                link.classList.toggle('active', linkTabId === tabId);
                link.setAttribute('aria-selected', linkTabId === tabId ? 'true' : 'false');
            });
        },

        /**
         * Change the time range and reload the current tab
         * @param {number} hours - Time range in hours (24, 168, or 720)
         */
        changeTimeRange: function(hours) {
            this.state.currentHours = hours;

            // Clear cache for all tabs (data is now stale)
            this.clearAllCache();

            // Reload current tab with new time range
            this.loadTab(this.state.activeTab, hours, true);

            // Dispatch custom event
            document.dispatchEvent(new CustomEvent('timeRangeChanged', {
                detail: { hours: hours, tabId: this.state.activeTab }
            }));
        },

        /**
         * Load a tab's content via AJAX
         * @param {string} tabId - The tab to load
         * @param {number} hours - Time range in hours
         * @param {boolean} forceRefresh - Force refresh even if cached
         * @returns {Promise<void>}
         */
        loadTab: async function(tabId, hours = 24, forceRefresh = false) {
            const endpoint = this.config.endpoints[tabId];
            if (!endpoint) {
                console.error('No endpoint for tab:', tabId);
                return;
            }

            const panel = this.getTabPanel();
            if (!panel) {
                console.error('Tab panel container not found');
                return;
            }

            // Check cache first
            const cached = this.state.loadedTabs.get(tabId);
            if (!forceRefresh && cached && this.isCacheValid(cached, hours)) {
                panel.innerHTML = cached.content;
                this.initTab(tabId);
                return;
            }

            // Cancel any pending request
            this.cancelPendingRequest();

            // Show loading state (with delay to prevent flash)
            let loadingTimeout = setTimeout(() => {
                this.showLoading(panel);
            }, this.config.loadingDelay);

            // Create abort controller for this request
            const abortController = new AbortController();
            this.state.currentRequest = abortController;

            try {
                const url = `${endpoint}?hours=${hours}`;
                const response = await fetch(url, {
                    signal: abortController.signal,
                    headers: {
                        'Accept': 'text/html',
                        'X-Requested-With': 'XMLHttpRequest'
                    }
                });

                // Clear loading timeout
                clearTimeout(loadingTimeout);

                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}: ${response.statusText}`);
                }

                const html = await response.text();

                // Check if this request is still current
                if (this.state.currentRequest !== abortController) {
                    return; // A newer request has superseded this one
                }

                // Update cache
                this.state.loadedTabs.set(tabId, {
                    timestamp: Date.now(),
                    hours: hours,
                    content: html
                });

                // Inject content
                this.hideLoading(panel);
                panel.innerHTML = html;

                // Initialize tab-specific JavaScript
                this.initTab(tabId);

            } catch (error) {
                clearTimeout(loadingTimeout);

                if (error.name === 'AbortError') {
                    console.log('Tab load aborted:', tabId);
                    return;
                }

                console.error('Failed to load tab:', tabId, error);
                this.showError(panel, error.message);
            } finally {
                if (this.state.currentRequest === abortController) {
                    this.state.currentRequest = null;
                }
            }
        },

        /**
         * Get the tab panel container element
         * @returns {HTMLElement|null}
         */
        getTabPanel: function() {
            return document.querySelector(this.config.tabPanelSelector) ||
                   document.getElementById('tabContent') ||
                   document.querySelector('[role="tabpanel"]');
        },

        /**
         * Check if cached content is still valid
         * @param {Object} cached - Cached entry
         * @param {number} hours - Current time range
         * @returns {boolean}
         */
        isCacheValid: function(cached, hours) {
            if (!cached || cached.hours !== hours) {
                return false;
            }
            return (Date.now() - cached.timestamp) < this.config.cacheTimeout;
        },

        /**
         * Cancel any pending tab load request
         */
        cancelPendingRequest: function() {
            if (this.state.currentRequest) {
                this.state.currentRequest.abort();
                this.state.currentRequest = null;
            }
        },

        /**
         * Clear cache for a specific tab
         * @param {string} tabId - Tab to clear cache for
         */
        clearCache: function(tabId) {
            this.state.loadedTabs.delete(tabId);
        },

        /**
         * Clear cache for all tabs
         */
        clearAllCache: function() {
            this.state.loadedTabs.clear();
        },

        /**
         * Show loading state in the tab panel
         * @param {HTMLElement} panel - The tab panel element
         */
        showLoading: function(panel) {
            panel.classList.add('tab-loading');

            // Add loading overlay if not already present
            if (!panel.querySelector('.tab-loading-overlay')) {
                const overlay = document.createElement('div');
                overlay.className = 'tab-loading-overlay';
                overlay.innerHTML = `
                    <div class="tab-loading-spinner">
                        <svg class="animate-spin h-8 w-8" viewBox="0 0 24 24" aria-hidden="true">
                            <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4" fill="none"></circle>
                            <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                        </svg>
                        <span class="tab-loading-text">Loading...</span>
                    </div>
                `;
                panel.appendChild(overlay);
            }
        },

        /**
         * Hide loading state from the tab panel
         * @param {HTMLElement} panel - The tab panel element
         */
        hideLoading: function(panel) {
            panel.classList.remove('tab-loading');
            const overlay = panel.querySelector('.tab-loading-overlay');
            if (overlay) {
                overlay.remove();
            }
        },

        /**
         * Show error state in the tab panel
         * @param {HTMLElement} panel - The tab panel element
         * @param {string} message - Error message to display
         */
        showError: function(panel, message) {
            panel.classList.remove('tab-loading');
            const overlay = panel.querySelector('.tab-loading-overlay');
            if (overlay) {
                overlay.remove();
            }

            panel.innerHTML = `
                <div class="tab-error-state">
                    <div class="tab-error-content">
                        <svg class="tab-error-icon" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
                        </svg>
                        <h3 class="tab-error-title">Failed to Load Content</h3>
                        <p class="tab-error-message">${this.escapeHtml(message)}</p>
                        <button class="btn btn-secondary tab-retry-btn" onclick="window.PerformanceTabs.retryCurrentTab()">
                            <svg class="btn-svg-icon" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                            </svg>
                            Retry
                        </button>
                    </div>
                </div>
            `;
        },

        /**
         * Retry loading the current tab
         */
        retryCurrentTab: function() {
            this.loadTab(this.state.activeTab, this.state.currentHours, true);
        },

        /**
         * Initialize tab-specific JavaScript after content loads
         * @param {string} tabId - The tab that was loaded
         */
        initTab: function(tabId) {
            // Call tab-specific init function if it exists
            const initFn = window['init' + this.capitalize(tabId) + 'Tab'];
            if (typeof initFn === 'function') {
                try {
                    initFn();
                } catch (error) {
                    console.error('Error initializing tab:', tabId, error);
                }
            }

            // Common initialization for all tabs
            this.initCommonFeatures();

            // Dispatch tab initialized event
            document.dispatchEvent(new CustomEvent('performanceTabInitialized', {
                detail: { tabId: tabId }
            }));
        },

        /**
         * Cleanup tab resources before switching away
         * @param {string} tabId - The tab being destroyed
         */
        destroyTab: function(tabId) {
            if (!tabId) return;

            // Call tab-specific destroy function if it exists
            const destroyFn = window['destroy' + this.capitalize(tabId) + 'Tab'];
            if (typeof destroyFn === 'function') {
                try {
                    destroyFn();
                } catch (error) {
                    console.error('Error destroying tab:', tabId, error);
                }
            }

            // Destroy any Chart.js instances in the current panel
            const panel = this.getTabPanel();
            if (panel) {
                panel.querySelectorAll('canvas').forEach(canvas => {
                    const chart = Chart?.getChart?.(canvas);
                    if (chart) {
                        chart.destroy();
                    }
                });
            }
        },

        /**
         * Initialize common features after tab load (timestamps, popups, etc.)
         */
        initCommonFeatures: function() {
            // Convert UTC timestamps to local
            this.convertTimestamps();

            // Re-initialize preview popups if available
            if (typeof window.PreviewPopup !== 'undefined' && window.PreviewPopup.init) {
                window.PreviewPopup.init();
            }
        },

        /**
         * Convert UTC timestamps to local time
         */
        convertTimestamps: function() {
            document.querySelectorAll('[data-utc-time]').forEach(el => {
                const utc = el.getAttribute('data-utc-time');
                const format = el.getAttribute('data-format');
                if (!utc) return;

                try {
                    const date = new Date(utc);

                    if (format === 'relative') {
                        const now = new Date();
                        const diff = now - date;
                        const mins = Math.floor(diff / 60000);
                        const hrs = Math.floor(mins / 60);
                        const days = Math.floor(hrs / 24);

                        if (days >= 1) {
                            el.textContent = days === 1 ? '1 day ago' : `${days} days ago`;
                        } else if (hrs >= 1) {
                            el.textContent = hrs === 1 ? '1 hour ago' : `${hrs} hours ago`;
                        } else if (mins >= 1) {
                            el.textContent = mins === 1 ? '1 minute ago' : `${mins} minutes ago`;
                        } else {
                            el.textContent = 'Just now';
                        }
                    } else {
                        el.textContent = date.toLocaleDateString('en-US', {
                            month: 'short',
                            day: '2-digit',
                            year: 'numeric'
                        }) + ' at ' + date.toLocaleTimeString('en-US', {
                            hour: '2-digit',
                            minute: '2-digit',
                            hour12: false
                        });
                    }
                } catch (e) {
                    console.error('Failed to parse timestamp:', utc, e);
                }
            });
        },

        /**
         * Helper: Capitalize first letter
         * @param {string} str - String to capitalize
         * @returns {string}
         */
        capitalize: function(str) {
            if (!str) return '';
            return str.charAt(0).toUpperCase() + str.slice(1);
        },

        /**
         * Helper: Escape HTML to prevent XSS
         * @param {string} str - String to escape
         * @returns {string}
         */
        escapeHtml: function(str) {
            const div = document.createElement('div');
            div.textContent = str;
            return div.innerHTML;
        },

        /**
         * Get the current active tab ID
         * @returns {string}
         */
        getActiveTab: function() {
            return this.state.activeTab;
        },

        /**
         * Get the current time range in hours
         * @returns {number}
         */
        getCurrentHours: function() {
            return this.state.currentHours;
        },

        /**
         * Refresh the current tab (force reload)
         */
        refresh: function() {
            this.loadTab(this.state.activeTab, this.state.currentHours, true);
        }
    };

    // Expose to global scope
    window.PerformanceTabs = PerformanceTabs;

    // Auto-initialize when DOM is ready (can be disabled with data-no-auto-init)
    document.addEventListener('DOMContentLoaded', function() {
        const container = document.querySelector('[data-performance-tabs]');
        if (container && !container.hasAttribute('data-no-auto-init')) {
            PerformanceTabs.init();
        }
    });

})();
