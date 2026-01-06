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
            loadingDelay: 150, // Delay before showing loading spinner (prevents flash)
            transitionDuration: 200, // Duration for fade transitions in ms (must match CSS)
            timeRangeStorageKey: 'performance-dashboard-time-range' // localStorage key for time range
        },

        // Tab icon paths (outline and solid versions) for dynamic icon switching
        tabIcons: {
            'overview': {
                outline: 'M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z',
                solid: 'M2 11a1 1 0 011-1h2a1 1 0 011 1v5a1 1 0 01-1 1H3a1 1 0 01-1-1v-5zm6-4a1 1 0 011-1h2a1 1 0 011 1v9a1 1 0 01-1 1H9a1 1 0 01-1-1V7zm6-3a1 1 0 011-1h2a1 1 0 011 1v12a1 1 0 01-1 1h-2a1 1 0 01-1-1V4z'
            },
            'health': {
                outline: 'M4.318 6.318a4.5 4.5 0 000 6.364L12 20.364l7.682-7.682a4.5 4.5 0 00-6.364-6.364L12 7.636l-1.318-1.318a4.5 4.5 0 00-6.364 0z',
                solid: 'M3.172 5.172a4 4 0 015.656 0L12 8.343l3.172-3.171a4 4 0 115.656 5.656L12 19.657l-8.828-8.829a4 4 0 010-5.656z'
            },
            'commands': {
                outline: 'M13 10V3L4 14h7v7l9-11h-7z',
                solid: 'M11.3 1.046A1 1 0 0112 2v5h4a1 1 0 01.82 1.573l-7 10A1 1 0 018 18v-5H4a1 1 0 01-.82-1.573l7-10a1 1 0 011.12-.38z'
            },
            'api': {
                outline: 'M8 9l3 3-3 3m5 0h3M5 20h14a2 2 0 002-2V6a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z',
                solid: 'M14.447 3.027a.75.75 0 01.527.92l-4.5 16.5a.75.75 0 01-1.448-.394l4.5-16.5a.75.75 0 01.921-.526zM16.72 6.22a.75.75 0 011.06 0l5.25 5.25a.75.75 0 010 1.06l-5.25 5.25a.75.75 0 11-1.06-1.06L21.44 12l-4.72-4.72a.75.75 0 010-1.06zm-9.44 0a.75.75 0 010 1.06L2.56 12l4.72 4.72a.75.75 0 11-1.06 1.06L.97 12.53a.75.75 0 010-1.06l5.25-5.25a.75.75 0 011.06 0z'
            },
            'system': {
                outline: 'M5 12h14M5 12a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v4a2 2 0 01-2 2M5 12a2 2 0 00-2 2v4a2 2 0 002 2h14a2 2 0 002-2v-4a2 2 0 00-2-2m-2-4h.01M17 16h.01',
                solid: 'M4.5 3A1.5 1.5 0 003 4.5v4A1.5 1.5 0 004.5 10h11a1.5 1.5 0 001.5-1.5v-4A1.5 1.5 0 0015.5 3h-11zm0 11A1.5 1.5 0 003 15.5v4A1.5 1.5 0 004.5 21h11a1.5 1.5 0 001.5-1.5v-4a1.5 1.5 0 00-1.5-1.5h-11zM13 7a1 1 0 100-2 1 1 0 000 2zm-3 0a1 1 0 100-2 1 1 0 000 2zm6 11a1 1 0 100-2 1 1 0 000 2zm-3 0a1 1 0 100-2 1 1 0 000 2z'
            },
            'alerts': {
                outline: 'M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9',
                solid: 'M10 2a6 6 0 00-6 6v3.586l-.707.707A1 1 0 004 14h12a1 1 0 00.707-1.707L16 11.586V8a6 6 0 00-6-6zM10 18a3 3 0 01-3-3h6a3 3 0 01-3 3z'
            }
        },

        // State management
        state: {
            loadedTabs: new Map(), // tabId -> { timestamp, content, abortController }
            activeTab: null,
            currentRequest: null,
            currentHours: 24,
            isInitialized: false,
            historyTimeout: null, // For debouncing history pushes
            isPopstateEvent: false // Flag to prevent double history pushes
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

            // Restore time range from localStorage, then sync from PerformanceShell if available
            this.restoreTimeRange();
            if (window.PerformanceShell && typeof window.PerformanceShell.getTimeRange === 'function') {
                this.state.currentHours = window.PerformanceShell.getTimeRange();
            }

            // Bind event listeners
            this.bindTabListeners();
            this.bindTimeRangeListeners();
            this.bindInternalLinkListeners();

            // Set up popstate listener for browser back/forward
            this.bindPopstateListener();

            // Determine initial tab from URL hash or fallback to active tab or default
            let initialTab = this.getTabFromUrl();
            if (!initialTab) {
                const activeTabLink = document.querySelector(this.config.tabLinkSelector + '.active');
                initialTab = activeTabLink ? this.getTabIdFromLink(activeTabLink) : this.config.defaultTab;
            }

            this.state.activeTab = initialTab;

            // Update tab navigation UI to match the initial tab (important for deep links)
            this.updateTabNavigation(initialTab);

            // Update time range button UI to match restored value
            this.updateTimeRangeButtons(this.state.currentHours);

            // Set initial history state so back button works from first tab
            history.replaceState({ tabId: initialTab, timeRange: this.state.currentHours, timestamp: Date.now() }, '', `#${initialTab}`);

            // Load the initial tab
            this.loadTab(initialTab, this.state.currentHours, false);

            this.state.isInitialized = true;
            console.log('PerformanceTabs initialized, loading tab:', initialTab, 'time range:', this.state.currentHours);
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

            // Also listen for timeRangeChanged events from PerformanceShell
            document.addEventListener('timeRangeChanged', function(e) {
                const hours = e.detail?.hours;
                if (hours && hours !== self.state.currentHours) {
                    self.changeTimeRange(hours);
                }
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
         * Bind popstate event listener for browser back/forward navigation
         */
        bindPopstateListener: function() {
            const self = this;
            window.addEventListener('popstate', function(e) {
                const tabId = self.handlePopState(e);
                if (tabId && tabId !== self.state.activeTab) {
                    // Set flag so switchTab knows this came from popstate
                    self.state.isPopstateEvent = true;
                    self.switchTab(tabId, { updateHistory: false });
                    self.state.isPopstateEvent = false;
                }
            });
        },

        /**
         * Handle popstate event from browser back/forward
         * @param {PopStateEvent} event - The popstate event
         * @returns {string|null} The tab ID to navigate to
         */
        handlePopState: function(event) {
            // If we have state from our history.pushState, use it
            if (event.state && event.state.tabId) {
                return event.state.tabId;
            }
            // Otherwise, parse from the current URL hash
            return this.getTabFromUrl();
        },

        /**
         * Parse tab ID from URL hash
         * @returns {string|null} The tab ID if valid, otherwise null
         */
        getTabFromUrl: function() {
            const hash = window.location.hash.slice(1); // Remove the '#' character

            // Check if the hash is a valid tab ID
            if (hash && this.config.endpoints[hash]) {
                return hash;
            }

            return null;
        },

        /**
         * Push a new history state for the given tab
         * Debounced to prevent flooding history with rapid tab switches
         * @param {string} tabId - The tab ID to push to history
         */
        pushHistory: function(tabId) {
            const self = this;

            // Clear any pending history push
            if (this.state.historyTimeout) {
                clearTimeout(this.state.historyTimeout);
            }

            // Debounce: wait 100ms before actually pushing to history
            this.state.historyTimeout = setTimeout(function() {
                const state = {
                    tabId: tabId,
                    timeRange: self.state.currentHours,
                    timestamp: Date.now()
                };
                history.pushState(state, '', `#${tabId}`);
                self.state.historyTimeout = null;
            }, 100);
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
         * Switch to a different tab with transition animations
         * @param {string} tabId - The tab to switch to
         * @param {Object} options - Optional configuration
         * @param {boolean} options.updateHistory - Whether to update URL and history (default: true)
         */
        switchTab: function(tabId, options = {}) {
            const { updateHistory = true } = options;
            const self = this;

            if (!this.config.endpoints[tabId]) {
                console.warn('Unknown tab:', tabId);
                return;
            }

            const panel = this.getTabPanel();
            if (!panel) {
                console.error('Tab panel container not found');
                return;
            }

            // Cleanup current tab before switching
            this.destroyTab(this.state.activeTab);

            // Cancel any pending request
            this.cancelPendingRequest();

            // Update tab navigation active state
            this.updateTabNavigation(tabId);

            // Add loading state to the tab button
            this.setTabButtonLoading(tabId, true);

            // Announce loading state to screen readers
            this.announceLoading(tabId);

            // Fade out current content (uses transitionDuration from config to match CSS)
            const currentContent = panel.querySelector('.tab-content');
            if (currentContent) {
                currentContent.classList.add('leaving');
                setTimeout(() => {
                    // After fade out, show skeleton or loading state
                    self.loadTab(tabId, self.state.currentHours, false);
                }, this.config.transitionDuration);
            } else {
                // If no current content, load immediately
                this.loadTab(tabId, this.state.currentHours, false);
            }

            // Update state
            this.state.activeTab = tabId;

            // Update URL and history if requested (user clicked tab) or not from popstate
            if (updateHistory && !this.state.isPopstateEvent) {
                this.pushHistory(tabId);
            }

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
            const self = this;
            document.querySelectorAll(this.config.tabLinkSelector).forEach(link => {
                const linkTabId = self.getTabIdFromLink(link);
                const isActive = linkTabId === tabId;
                link.classList.toggle('active', isActive);
                link.setAttribute('aria-selected', isActive ? 'true' : 'false');

                // Update icon to solid (active) or outline (inactive)
                self.updateTabIcon(link, linkTabId, isActive);
            });
        },

        /**
         * Update a tab's icon to solid (active) or outline (inactive)
         * @param {HTMLElement} link - The tab link element
         * @param {string} tabId - The tab identifier
         * @param {boolean} isActive - Whether the tab is active
         */
        updateTabIcon: function(link, tabId, isActive) {
            const iconData = this.tabIcons[tabId];
            if (!iconData) return;

            const svg = link.querySelector('.tab-icon');
            if (!svg) return;

            const path = svg.querySelector('path');
            if (!path) return;

            if (isActive) {
                // Switch to solid icon
                svg.setAttribute('fill', 'currentColor');
                svg.removeAttribute('stroke');
                path.setAttribute('d', iconData.solid);
                path.removeAttribute('stroke-linecap');
                path.removeAttribute('stroke-linejoin');
                path.removeAttribute('stroke-width');
            } else {
                // Switch to outline icon
                svg.setAttribute('fill', 'none');
                svg.setAttribute('stroke', 'currentColor');
                path.setAttribute('d', iconData.outline);
                path.setAttribute('stroke-linecap', 'round');
                path.setAttribute('stroke-linejoin', 'round');
                path.setAttribute('stroke-width', '2');
            }
        },

        /**
         * Change the time range and reload the current tab
         * @param {number} hours - Time range in hours (24, 168, or 720)
         */
        changeTimeRange: function(hours) {
            this.state.currentHours = hours;

            // Persist to localStorage
            this.saveTimeRange(hours);

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
         * Save time range to localStorage
         * @param {number} hours - Time range in hours
         */
        saveTimeRange: function(hours) {
            try {
                localStorage.setItem(this.config.timeRangeStorageKey, String(hours));
            } catch (e) {
                console.warn('Failed to save time range to localStorage:', e);
            }
        },

        /**
         * Restore time range from localStorage
         */
        restoreTimeRange: function() {
            try {
                const stored = localStorage.getItem(this.config.timeRangeStorageKey);
                if (stored) {
                    const hours = parseInt(stored, 10);
                    // Validate it's one of the allowed values
                    if ([24, 168, 720].includes(hours)) {
                        this.state.currentHours = hours;
                    }
                }
            } catch (e) {
                console.warn('Failed to restore time range from localStorage:', e);
            }
        },

        /**
         * Update time range button UI to reflect active state
         * @param {number} hours - The active time range in hours
         */
        updateTimeRangeButtons: function(hours) {
            document.querySelectorAll('.time-range-btn').forEach(btn => {
                const btnHours = parseInt(btn.dataset.hours, 10);
                btn.classList.toggle('active', btnHours === hours);
            });
        },

        /**
         * Load a tab's content via AJAX with transitions and skeleton loading
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

            const self = this;

            // Check cache first
            const cached = this.state.loadedTabs.get(tabId);
            if (!forceRefresh && cached && this.isCacheValid(cached, hours)) {
                // Load from cache with transition
                this.loadCachedContent(panel, cached.content, tabId);
                return;
            }

            // Cancel any pending request
            this.cancelPendingRequest();

            // Show skeleton loader (with delay to prevent flash)
            let skeletonTimeout = setTimeout(() => {
                self.showSkeleton(panel);
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

                // Clear skeleton timeout
                clearTimeout(skeletonTimeout);

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

                // Load content with fade-in transition
                this.loadContentWithTransition(panel, html, tabId);

            } catch (error) {
                clearTimeout(skeletonTimeout);

                if (error.name === 'AbortError') {
                    console.log('Tab load aborted:', tabId);
                    return;
                }

                console.error('Failed to load tab:', tabId, error);
                this.showError(panel, error.message);
            } finally {
                // Remove loading state from tab button
                self.setTabButtonLoading(tabId, false);

                if (this.state.currentRequest === abortController) {
                    this.state.currentRequest = null;
                }
            }
        },

        /**
         * Load content from cache with smooth transition
         * @param {HTMLElement} panel - The tab panel
         * @param {string} html - The cached HTML content
         * @param {string} tabId - The tab ID being loaded
         */
        loadCachedContent: function(panel, html, tabId) {
            const self = this;

            // Wrap content in tab-content div with visible state
            const wrappedHtml = `<div class="tab-content visible">${html}</div>`;
            panel.innerHTML = wrappedHtml;

            // Execute any script tags
            this.executeScripts(panel);

            // Initialize tab-specific JavaScript
            this.initTab(tabId);
        },

        /**
         * Load content with fade-in transition
         * @param {HTMLElement} panel - The tab panel
         * @param {string} html - The HTML content to load
         * @param {string} tabId - The tab ID being loaded
         */
        loadContentWithTransition: function(panel, html, tabId) {
            const self = this;

            // Hide skeleton loader first
            this.hideSkeleton(panel);

            // Wrap content in tab-content div with entering state (opacity: 0)
            const wrappedHtml = `<div class="tab-content entering">${html}</div>`;
            panel.innerHTML = wrappedHtml;

            // Execute any script tags
            this.executeScripts(panel);

            // Trigger fade-in animation (after next frame to ensure CSS transition applies)
            const contentDiv = panel.querySelector('.tab-content');
            if (contentDiv) {
                requestAnimationFrame(() => {
                    contentDiv.classList.remove('entering');
                    contentDiv.classList.add('visible');
                });
            }

            // Initialize tab-specific JavaScript
            this.initTab(tabId);

            // Announce completion to screen readers
            this.announceCompletion(tabId);
        },

        /**
         * Show skeleton loader placeholder
         * @param {HTMLElement} panel - The tab panel
         */
        showSkeleton: function(panel) {
            panel.classList.add('tab-loading');

            // Create skeleton structure
            const skeleton = document.createElement('div');
            skeleton.className = 'tab-skeleton';
            skeleton.innerHTML = `
                <div class="skeleton-header"></div>
                <div class="skeleton-grid">
                    <div class="skeleton-card"></div>
                    <div class="skeleton-card"></div>
                    <div class="skeleton-card"></div>
                </div>
                <div class="skeleton-chart"></div>
            `;

            // Clear panel and add skeleton
            panel.innerHTML = '';
            panel.appendChild(skeleton);
        },

        /**
         * Hide skeleton loader
         * @param {HTMLElement} panel - The tab panel
         */
        hideSkeleton: function(panel) {
            panel.classList.remove('tab-loading');
            const skeleton = panel.querySelector('.tab-skeleton');
            if (skeleton) {
                skeleton.remove();
            }
        },

        /**
         * Set loading state on tab button
         * @param {string} tabId - The tab ID
         * @param {boolean} isLoading - Whether to show loading state
         */
        setTabButtonLoading: function(tabId, isLoading) {
            const self = this;
            document.querySelectorAll(this.config.tabLinkSelector).forEach(link => {
                const linkTabId = self.getTabIdFromLink(link);
                if (linkTabId === tabId) {
                    if (isLoading) {
                        link.classList.add('loading');
                    } else {
                        link.classList.remove('loading');
                    }
                }
            });
        },

        /**
         * Announce loading state to screen readers
         * @param {string} tabId - The tab ID being loaded
         */
        announceLoading: function(tabId) {
            // Get tab label for announcement
            const tabLabel = this.getTabLabel(tabId);

            // Create or update live region for announcements
            let liveRegion = document.querySelector('[aria-live="polite"][data-tab-loading]');
            if (!liveRegion) {
                liveRegion = document.createElement('div');
                liveRegion.setAttribute('aria-live', 'polite');
                liveRegion.setAttribute('data-tab-loading', 'true');
                liveRegion.className = 'sr-only'; // Visually hidden but accessible to screen readers
                document.body.appendChild(liveRegion);
            }

            liveRegion.textContent = `Loading ${tabLabel} tab...`;
        },

        /**
         * Announce tab content loaded to screen readers
         * @param {string} tabId - The tab ID that was loaded
         */
        announceCompletion: function(tabId) {
            const tabLabel = this.getTabLabel(tabId);

            // Reuse the same live region
            const liveRegion = document.querySelector('[aria-live="polite"][data-tab-loading]');
            if (liveRegion) {
                liveRegion.textContent = `${tabLabel} tab loaded`;
            }
        },

        /**
         * Announce error state to screen readers
         * @param {string} tabId - The tab ID that failed to load
         * @param {string} message - The error message
         */
        announceError: function(tabId, message) {
            const tabLabel = this.getTabLabel(tabId);

            // Reuse the same live region
            const liveRegion = document.querySelector('[aria-live="polite"][data-tab-loading]');
            if (liveRegion) {
                liveRegion.textContent = `Failed to load ${tabLabel} tab. ${message}`;
            }
        },

        /**
         * Get human-readable label for a tab
         * @param {string} tabId - The tab ID
         * @returns {string}
         */
        getTabLabel: function(tabId) {
            const labels = {
                'overview': 'Overview',
                'health': 'Health',
                'commands': 'Commands',
                'api': 'API & Rate Limits',
                'system': 'System Health',
                'alerts': 'Alerts'
            };
            return labels[tabId] || tabId;
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
         * Show error state in the tab panel with transition
         * @param {HTMLElement} panel - The tab panel element
         * @param {string} message - Error message to display
         */
        showError: function(panel, message) {
            const self = this;

            // Hide skeleton if present
            this.hideSkeleton(panel);
            panel.classList.remove('tab-loading');

            // Wrap error content in tab-content div with visible state
            const errorHtml = `
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

            // Wrap in tab-content div
            const wrappedHtml = `<div class="tab-content visible">${errorHtml}</div>`;
            panel.innerHTML = wrappedHtml;

            // Announce error to screen readers
            this.announceError(this.state.activeTab, message);
        },

        /**
         * Retry loading the current tab
         */
        retryCurrentTab: function() {
            // Remove loading state from button before retry
            this.setTabButtonLoading(this.state.activeTab, false);

            // Force refresh on retry
            this.loadTab(this.state.activeTab, this.state.currentHours, true);

            // Re-add loading state
            this.setTabButtonLoading(this.state.activeTab, true);
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
                    // Pass the current time range (hours) to the init function
                    initFn(this.state.currentHours);
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
         * Execute script tags in dynamically loaded content
         * Scripts inserted via innerHTML don't execute automatically, so we need to
         * create new script elements and append them to the document.
         * @param {HTMLElement} container - The container with the loaded content
         */
        executeScripts: function(container) {
            const scripts = container.querySelectorAll('script');
            scripts.forEach(oldScript => {
                const newScript = document.createElement('script');

                // Copy all attributes
                Array.from(oldScript.attributes).forEach(attr => {
                    newScript.setAttribute(attr.name, attr.value);
                });

                // Copy the content
                newScript.textContent = oldScript.textContent;

                // Replace the old script with the new one (which will execute)
                oldScript.parentNode.replaceChild(newScript, oldScript);
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
