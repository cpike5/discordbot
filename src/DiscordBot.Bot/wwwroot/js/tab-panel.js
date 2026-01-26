/**
 * Tab Panel Module
 * Provides accessible tab panel functionality with keyboard navigation,
 * URL hash persistence, and localStorage persistence options.
 */
(function() {
    'use strict';

    const TabPanel = {
        // Configuration defaults
        config: {
            containerSelector: '[data-tab-panel]',
            tabSelector: '.tab-panel-tab',
            panelSelector: '[role="tabpanel"]',
            activeClass: 'active',
            storageKeyPrefix: 'tab-panel-',
            maxCacheSize: 10,  // Maximum number of cached tab contents
            cacheExpiryMs: 5 * 60 * 1000  // 5 minutes
        },

        // Track initialized panels
        initializedPanels: new Set(),

        // Screen reader live region element
        liveRegion: null,

        /**
         * Initialize all tab panels on the page.
         * Safe to call multiple times - will skip already initialized panels.
         */
        init: function() {
            // Create live region for screen reader announcements
            this.createLiveRegion();

            const containers = document.querySelectorAll(this.config.containerSelector);
            containers.forEach(container => {
                const panelId = container.dataset.panelId;
                if (panelId && !this.initializedPanels.has(panelId)) {
                    this.initPanel(container);
                    this.initializedPanels.add(panelId);
                }
            });
        },

        /**
         * Create an aria-live region for screen reader announcements.
         * This region is visually hidden but announced to screen readers.
         */
        createLiveRegion: function() {
            if (this.liveRegion) return;

            const region = document.createElement('div');
            region.id = 'tab-panel-announcer';
            region.setAttribute('role', 'status');
            region.setAttribute('aria-live', 'polite');
            region.setAttribute('aria-atomic', 'true');
            // Visually hidden but accessible to screen readers
            region.style.cssText = 'position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0;';
            document.body.appendChild(region);
            this.liveRegion = region;
        },

        /**
         * Announce a message to screen readers via the live region.
         * @param {string} message - The message to announce
         */
        announce: function(message) {
            if (!this.liveRegion) return;

            // Clear and re-set to ensure announcement
            this.liveRegion.textContent = '';
            // Use setTimeout to ensure the DOM change is detected
            setTimeout(() => {
                this.liveRegion.textContent = message;
            }, 50);
        },

        /**
         * Initialize a single tab panel container.
         * @param {HTMLElement} container - The tab panel container element
         */
        initPanel: function(container) {
            const panelId = container.dataset.panelId;
            const navigationMode = container.dataset.navigationMode || 'inpage';
            const persistenceMode = container.dataset.persistenceMode || 'urlhash';
            const tablist = container.querySelector('[role="tablist"]');

            if (!tablist) {
                console.warn('TabPanel: No tablist found in container', panelId);
                return;
            }

            const tabs = tablist.querySelectorAll(this.config.tabSelector);
            if (tabs.length === 0) {
                console.warn('TabPanel: No tabs found in container', panelId);
                return;
            }

            // Store AJAX configuration if in ajax mode
            if (navigationMode === 'ajax') {
                container._ajaxUrlPattern = container.dataset.ajaxUrlPattern;
                container._ajaxContentTarget = container.dataset.ajaxContentTarget;
                container._loadedPanels = new Map();
                container._currentRequest = null;
            }

            // Bind keyboard navigation
            this.bindKeyboardNavigation(tablist, tabs, navigationMode);

            // Handle click events for in-page and ajax navigation modes
            if (navigationMode === 'inpage' || navigationMode === 'ajax') {
                this.bindClickHandlers(container, tabs, panelId, persistenceMode);

                // Restore active tab from persistence
                const restoredTabId = this.restoreActiveTab(panelId, persistenceMode);
                if (restoredTabId) {
                    this.activateTab(container, restoredTabId, persistenceMode);
                }

                // Listen for hash changes
                if (persistenceMode === 'urlhash') {
                    this.bindHashChangeListener(container, panelId);
                }
            }

            // Set up scroll indicators
            this.setupScrollIndicators(container, tablist);
        },

        /**
         * Bind keyboard navigation (arrow keys, Home, End).
         * @param {HTMLElement} tablist - The tablist element
         * @param {NodeList} tabs - The tab elements
         * @param {string} navigationMode - 'inpage' or 'pagenavigation'
         */
        bindKeyboardNavigation: function(tablist, tabs, navigationMode) {
            const self = this;
            const enabledTabs = Array.from(tabs).filter(tab => !tab.disabled && !tab.hasAttribute('aria-disabled'));

            tablist.addEventListener('keydown', function(e) {
                const currentTab = document.activeElement;
                if (!currentTab || !currentTab.matches(self.config.tabSelector)) return;

                const currentIndex = enabledTabs.indexOf(currentTab);
                if (currentIndex === -1) return;

                let targetTab = null;

                switch (e.key) {
                    case 'ArrowLeft':
                        e.preventDefault();
                        targetTab = enabledTabs[currentIndex - 1] || enabledTabs[enabledTabs.length - 1];
                        break;

                    case 'ArrowRight':
                        e.preventDefault();
                        targetTab = enabledTabs[currentIndex + 1] || enabledTabs[0];
                        break;

                    case 'Home':
                        e.preventDefault();
                        targetTab = enabledTabs[0];
                        break;

                    case 'End':
                        e.preventDefault();
                        targetTab = enabledTabs[enabledTabs.length - 1];
                        break;

                    case 'Enter':
                    case ' ':
                        e.preventDefault();
                        if (navigationMode === 'pagenavigation' && currentTab.tagName === 'A') {
                            // For page navigation mode, follow the link
                            currentTab.click();
                        } else if (navigationMode === 'inpage' || navigationMode === 'ajax') {
                            // For in-page and ajax modes, activate the tab
                            const container = tablist.closest(self.config.containerSelector);
                            const panelId = container?.dataset.panelId;
                            const persistenceMode = container?.dataset.persistenceMode || 'urlhash';
                            if (container && panelId) {
                                self.activateTab(container, currentTab.dataset.tabId, persistenceMode);
                            }
                        }
                        break;
                }

                if (targetTab) {
                    targetTab.focus();
                    // In in-page and ajax modes, also activate on arrow key navigation
                    if (navigationMode === 'inpage' || navigationMode === 'ajax') {
                        const container = tablist.closest(self.config.containerSelector);
                        const panelId = container?.dataset.panelId;
                        const persistenceMode = container?.dataset.persistenceMode || 'urlhash';
                        if (container && panelId) {
                            self.activateTab(container, targetTab.dataset.tabId, persistenceMode);
                        }
                    }
                }
            });
        },

        /**
         * Bind click handlers for in-page tab switching.
         * @param {HTMLElement} container - The tab panel container
         * @param {NodeList} tabs - The tab elements
         * @param {string} panelId - The panel identifier
         * @param {string} persistenceMode - Persistence mode
         */
        bindClickHandlers: function(container, tabs, panelId, persistenceMode) {
            const self = this;

            tabs.forEach(tab => {
                tab.addEventListener('click', function(e) {
                    if (this.disabled || this.hasAttribute('aria-disabled')) {
                        e.preventDefault();
                        return;
                    }

                    e.preventDefault();
                    const tabId = this.dataset.tabId;
                    self.activateTab(container, tabId, persistenceMode);
                });
            });
        },

        /**
         * Activate a specific tab by ID.
         * @param {HTMLElement} container - The tab panel container
         * @param {string} tabId - The ID of the tab to activate
         * @param {string} persistenceMode - Persistence mode
         */
        activateTab: function(container, tabId, persistenceMode) {
            const panelId = container.dataset.panelId;
            const navigationMode = container.dataset.navigationMode || 'inpage';
            const tablist = container.querySelector('[role="tablist"]');
            const tabs = tablist.querySelectorAll(this.config.tabSelector);

            // Update tab states
            tabs.forEach(tab => {
                const isActive = tab.dataset.tabId === tabId;
                tab.classList.toggle(this.config.activeClass, isActive);
                tab.setAttribute('aria-selected', isActive ? 'true' : 'false');
                tab.setAttribute('tabindex', isActive ? '0' : '-1');

                // Update icon to solid/outline
                this.updateTabIcon(tab, isActive);
            });

            // Handle content based on navigation mode
            if (navigationMode === 'inpage') {
                // Update panel visibility (find associated panels by convention)
                const panels = document.querySelectorAll(`[data-tab-panel-for="${panelId}"]`);
                panels.forEach(panel => {
                    const isActive = panel.dataset.tabId === tabId;
                    panel.hidden = !isActive;
                    panel.classList.toggle(this.config.activeClass, isActive);
                });
            } else if (navigationMode === 'ajax') {
                // Load content via AJAX
                this.loadTabContent(container, tabId);
            }

            // Persist the active tab
            this.persistActiveTab(panelId, tabId, persistenceMode);

            // Scroll active tab into view if needed
            const activeTab = tablist.querySelector(`[data-tab-id="${tabId}"]`);
            if (activeTab) {
                this.scrollTabIntoView(tablist, activeTab);

                // Announce tab change to screen readers
                const tabLabel = activeTab.textContent?.trim() || tabId;
                this.announce(`${tabLabel} tab selected`);
            }

            // Dispatch custom event
            container.dispatchEvent(new CustomEvent('tabchange', {
                bubbles: true,
                detail: { panelId, tabId }
            }));
        },

        /**
         * Update a tab's icon between outline and solid versions.
         * @param {HTMLElement} tab - The tab element
         * @param {boolean} isActive - Whether the tab is active
         */
        updateTabIcon: function(tab, isActive) {
            const svg = tab.querySelector('.tab-icon');
            if (!svg) return;

            const path = svg.querySelector('path');
            if (!path) return;

            // Check if we have both outline and solid paths stored
            const outlinePath = tab.dataset.iconOutline;
            const solidPath = tab.dataset.iconSolid;

            if (outlinePath && solidPath) {
                if (isActive) {
                    svg.setAttribute('fill', 'currentColor');
                    svg.removeAttribute('stroke');
                    path.setAttribute('d', solidPath);
                    path.removeAttribute('stroke-linecap');
                    path.removeAttribute('stroke-linejoin');
                    path.removeAttribute('stroke-width');
                } else {
                    svg.setAttribute('fill', 'none');
                    svg.setAttribute('stroke', 'currentColor');
                    path.setAttribute('d', outlinePath);
                    path.setAttribute('stroke-linecap', 'round');
                    path.setAttribute('stroke-linejoin', 'round');
                    path.setAttribute('stroke-width', '2');
                }
            }
        },

        /**
         * Persist the active tab state.
         * @param {string} panelId - The panel identifier
         * @param {string} tabId - The active tab ID
         * @param {string} mode - Persistence mode
         */
        persistActiveTab: function(panelId, tabId, mode) {
            switch (mode) {
                case 'urlhash':
                    const newHash = tabId;
                    if (window.location.hash !== `#${newHash}`) {
                        history.replaceState(null, '', `#${newHash}`);
                    }
                    break;

                case 'localstorage':
                    try {
                        localStorage.setItem(this.config.storageKeyPrefix + panelId, tabId);
                    } catch (e) {
                        console.warn('TabPanel: Failed to save to localStorage', e);
                    }
                    break;
            }
        },

        /**
         * Restore the active tab from persisted state.
         * @param {string} panelId - The panel identifier
         * @param {string} mode - Persistence mode
         * @returns {string|null} The restored tab ID or null
         */
        restoreActiveTab: function(panelId, mode) {
            switch (mode) {
                case 'urlhash':
                    const hash = window.location.hash.slice(1);
                    // Return the hash directly (no longer using panelId prefix)
                    if (hash) {
                        return hash;
                    }
                    break;

                case 'localstorage':
                    try {
                        return localStorage.getItem(this.config.storageKeyPrefix + panelId);
                    } catch (e) {
                        console.warn('TabPanel: Failed to read from localStorage', e);
                    }
                    break;
            }
            return null;
        },

        /**
         * Bind listener for URL hash changes.
         * @param {HTMLElement} container - The tab panel container
         * @param {string} panelId - The panel identifier
         */
        bindHashChangeListener: function(container, panelId) {
            const self = this;
            window.addEventListener('hashchange', function() {
                const tabId = self.restoreActiveTab(panelId, 'urlhash');
                if (tabId) {
                    self.activateTab(container, tabId, 'urlhash');
                }
            });
        },

        /**
         * Set up scroll indicators for overflow detection.
         * @param {HTMLElement} container - The tab panel container
         * @param {HTMLElement} tablist - The tablist element
         */
        setupScrollIndicators: function(container, tablist) {
            const self = this;

            function updateIndicators() {
                const scrollLeft = tablist.scrollLeft;
                const scrollWidth = tablist.scrollWidth;
                const clientWidth = tablist.clientWidth;
                const isScrollable = scrollWidth > clientWidth;

                if (isScrollable) {
                    container.classList.toggle('can-scroll-left', scrollLeft > 5);
                    container.classList.toggle('can-scroll-right', scrollLeft < scrollWidth - clientWidth - 5);
                } else {
                    container.classList.remove('can-scroll-left', 'can-scroll-right');
                }
            }

            // Initial check
            updateIndicators();

            // Update on scroll and resize
            tablist.addEventListener('scroll', updateIndicators, { passive: true });
            window.addEventListener('resize', updateIndicators, { passive: true });
        },

        /**
         * Scroll a tab into view if it's not fully visible.
         * @param {HTMLElement} tablist - The tablist element
         * @param {HTMLElement} tab - The tab to scroll into view
         */
        scrollTabIntoView: function(tablist, tab) {
            const tabRect = tab.getBoundingClientRect();
            const listRect = tablist.getBoundingClientRect();

            if (tabRect.left < listRect.left || tabRect.right > listRect.right) {
                tab.scrollIntoView({ behavior: 'smooth', inline: 'center', block: 'nearest' });
            }
        },

        /**
         * Programmatically switch to a tab.
         * @param {string} panelId - The panel identifier
         * @param {string} tabId - The tab ID to activate
         */
        switchTo: function(panelId, tabId) {
            const container = document.querySelector(`[data-panel-id="${panelId}"]`);
            if (container) {
                const persistenceMode = container.dataset.persistenceMode || 'urlhash';
                this.activateTab(container, tabId, persistenceMode);
            }
        },

        /**
         * Get the currently active tab ID for a panel.
         * @param {string} panelId - The panel identifier
         * @returns {string|null} The active tab ID or null
         */
        getActiveTab: function(panelId) {
            const container = document.querySelector(`[data-panel-id="${panelId}"]`);
            if (container) {
                const activeTab = container.querySelector('.tab-panel-tab.active');
                return activeTab?.dataset.tabId || null;
            }
            return null;
        },

        /**
         * Load tab content via AJAX.
         * @param {HTMLElement} container - The tab panel container
         * @param {string} tabId - The tab ID to load
         */
        loadTabContent: async function(container, tabId) {
            const urlPattern = container._ajaxUrlPattern;
            const contentTarget = container._ajaxContentTarget;

            if (!urlPattern || !contentTarget) {
                console.warn('TabPanel: Missing AJAX configuration for tab:', tabId);
                return;
            }

            // Validate URL pattern contains placeholder
            if (!urlPattern.includes('{tabId}')) {
                console.error('TabPanel: URL pattern missing {tabId} placeholder:', urlPattern);
                return;
            }

            // Build URL by replacing {tabId} placeholder
            const url = urlPattern.replace('{tabId}', tabId);

            // Get content container
            const contentContainer = document.querySelector(contentTarget);
            if (!contentContainer) {
                console.warn('TabPanel: Content container not found:', contentTarget);
                return;
            }

            // Check if already loaded and cached
            const cached = container._loadedPanels.get(tabId);
            if (cached) {
                // Check if cache entry has expired
                const age = Date.now() - cached.timestamp;
                if (age < this.config.cacheExpiryMs) {
                    contentContainer.innerHTML = cached.content;
                    this.executeScripts(contentContainer);
                    return;
                } else {
                    // Cache expired, remove it
                    container._loadedPanels.delete(tabId);
                }
            }

            // Cancel any pending request
            if (container._currentRequest) {
                container._currentRequest.abort();
                container._currentRequest = null;
            }

            // Show loading state
            this.showLoadingState(contentContainer);
            const tabElement = container.querySelector(`[data-tab-id="${tabId}"]`);
            if (tabElement) {
                tabElement.classList.add('loading');
            }
            this.announce(`Loading ${tabId} tab...`);

            // Create abort controller
            const abortController = new AbortController();
            container._currentRequest = abortController;

            try {
                const response = await fetch(url, {
                    signal: abortController.signal,
                    headers: {
                        'Accept': 'text/html',
                        'X-Requested-With': 'XMLHttpRequest'
                    }
                });

                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}: ${response.statusText}`);
                }

                // Validate content type
                const contentType = response.headers.get('Content-Type');
                if (contentType && !contentType.includes('text/html')) {
                    throw new Error(`Expected text/html but received ${contentType}`);
                }

                const html = await response.text();

                // Check if this request is still current
                if (container._currentRequest !== abortController) {
                    return; // A newer request has superseded this one
                }

                // Enforce cache size limit by removing oldest entries
                if (container._loadedPanels.size >= this.config.maxCacheSize) {
                    // Find and remove the oldest cache entry
                    let oldestKey = null;
                    let oldestTimestamp = Infinity;

                    for (const [key, value] of container._loadedPanels.entries()) {
                        if (value.timestamp < oldestTimestamp) {
                            oldestTimestamp = value.timestamp;
                            oldestKey = key;
                        }
                    }

                    if (oldestKey) {
                        container._loadedPanels.delete(oldestKey);
                        console.log('TabPanel: Removed oldest cache entry:', oldestKey);
                    }
                }

                // Cache the content
                container._loadedPanels.set(tabId, {
                    timestamp: Date.now(),
                    content: html
                });

                // Update content
                contentContainer.innerHTML = html;
                this.executeScripts(contentContainer);

                // Announce completion
                this.announce(`${tabId} tab loaded`);

            } catch (error) {
                if (error.name === 'AbortError') {
                    console.log('TabPanel: Request aborted:', tabId);
                    // Note: Loading state still needs to be cleared even when aborted
                    // This is handled in the finally block below
                    return;
                }

                console.error('TabPanel: Failed to load tab content:', tabId, error);
                this.showErrorState(contentContainer, error.message, container.dataset.panelId, tabId);
                this.announce(`Failed to load ${tabId} tab`);

            } finally {
                // Always clear loading state, even if request was aborted
                if (tabElement) {
                    tabElement.classList.remove('loading');
                }
                contentContainer.removeAttribute('aria-busy');

                if (container._currentRequest === abortController) {
                    container._currentRequest = null;
                }
            }
        },

        /**
         * Show loading state in content container.
         * @param {HTMLElement} container - The content container
         */
        showLoadingState: function(container) {
            container.setAttribute('aria-busy', 'true');
            container.innerHTML = `
                <div class="tab-panel-loading">
                    <svg class="tab-panel-spinner" viewBox="0 0 24 24" aria-hidden="true">
                        <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4" fill="none"></circle>
                        <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                    </svg>
                    <span class="sr-only">Loading...</span>
                </div>
            `;
        },

        /**
         * Show error state in content container.
         * @param {HTMLElement} container - The content container
         * @param {string} message - Error message
         * @param {string} panelId - The panel identifier
         * @param {string} tabId - The tab ID
         */
        showErrorState: function(container, message, panelId, tabId) {
            container.removeAttribute('aria-busy');
            const escapedMessage = this.escapeHtml(message);
            const escapedPanelId = this.escapeHtml(panelId);
            const escapedTabId = this.escapeHtml(tabId);

            container.innerHTML = `
                <div class="tab-panel-error">
                    <div class="tab-panel-error-content">
                        <svg class="tab-panel-error-icon" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
                        </svg>
                        <h3 class="tab-panel-error-title">Failed to Load Content</h3>
                        <p class="tab-panel-error-message">${escapedMessage}</p>
                        <button type="button" class="btn btn-secondary tab-panel-retry-btn" data-panel-id="${escapedPanelId}" data-tab-id="${escapedTabId}">
                            Retry
                        </button>
                    </div>
                </div>
            `;

            // Attach event listener to retry button (no inline onclick)
            const retryBtn = container.querySelector('.tab-panel-retry-btn');
            if (retryBtn) {
                retryBtn.addEventListener('click', () => {
                    this.retryLoad(panelId, tabId);
                });
            }
        },

        /**
         * Retry loading a tab.
         * @param {string} panelId - The panel identifier
         * @param {string} tabId - The tab ID to retry
         */
        retryLoad: function(panelId, tabId) {
            const container = document.querySelector(`[data-panel-id="${panelId}"]`);
            if (!container) return;

            // Clear cached content
            if (container._loadedPanels) {
                container._loadedPanels.delete(tabId);
            }

            // Reload
            this.loadTabContent(container, tabId);
        },

        /**
         * Execute script tags in dynamically loaded content.
         * @param {HTMLElement} container - The container element
         */
        executeScripts: function(container) {
            const scripts = container.querySelectorAll('script');
            scripts.forEach(oldScript => {
                const newScript = document.createElement('script');

                // Copy attributes
                Array.from(oldScript.attributes).forEach(attr => {
                    newScript.setAttribute(attr.name, attr.value);
                });

                // Copy content
                newScript.textContent = oldScript.textContent;

                // Replace to execute
                oldScript.parentNode.replaceChild(newScript, oldScript);
            });
        },

        /**
         * Escape HTML to prevent XSS.
         * @param {string} text - The text to escape
         * @returns {string} The escaped text
         */
        escapeHtml: function(text) {
            const div = document.createElement('div');
            div.textContent = text;
            return div.innerHTML;
        },

        /**
         * Set loading state on a tab panel and announce to screen readers.
         * @param {string} panelId - The panel identifier
         * @param {boolean} isLoading - Whether the panel is loading
         * @param {string} [message] - Optional loading message for screen readers
         */
        setLoading: function(panelId, isLoading, message) {
            const panel = document.querySelector(`[data-tab-panel-for="${panelId}"][aria-selected="true"], [data-tab-panel-for="${panelId}"]:not([hidden])`);
            if (panel) {
                panel.setAttribute('aria-busy', isLoading ? 'true' : 'false');
            }

            if (isLoading) {
                this.announce(message || 'Loading content...');
            } else if (message) {
                this.announce(message);
            }
        }
    };

    // Expose to global scope
    window.TabPanel = TabPanel;

    // Auto-initialize when DOM is ready
    document.addEventListener('DOMContentLoaded', function() {
        TabPanel.init();
    });

})();
