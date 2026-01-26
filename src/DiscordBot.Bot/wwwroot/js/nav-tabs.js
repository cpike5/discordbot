/**
 * Nav Tabs Module
 * Provides accessible navigation tabs with multiple modes:
 * - PageNavigation: Traditional links with full page reload (default - no JS required)
 * - InPage: Show/hide pre-loaded tab panels without server requests
 * - Ajax: Fetch content dynamically from data-ajax-url
 *
 * Features:
 * - URL hash persistence with browser back/forward support
 * - LocalStorage persistence option
 * - Keyboard navigation (Arrow keys, Home, End, Enter, Space)
 * - Auto-scroll active tab into view
 * - Scroll indicators (gradient shadows)
 * - Screen reader announcements
 * - Icon switching (outline/solid)
 */
(function() {
    'use strict';

    const NavTabs = {
        // Configuration defaults
        config: {
            containerSelector: '[data-nav-tabs]',
            tabSelector: '.nav-tabs-item',
            panelSelector: '[data-nav-panel-for]',
            tablistSelector: '.nav-tabs-list',
            activeClass: 'active',
            loadingClass: 'loading',
            requestTimeout: 10000, // 10 seconds
            loadingDelay: 150, // Delay before showing loading state
            scrollThreshold: 5 // Pixels threshold for scroll indicators
        },

        // Track initialized containers by containerId
        instances: new Map(),

        // Screen reader live region element
        liveRegion: null,

        /**
         * Initialize a specific tab container.
         * @param {string} containerId - The unique container ID
         * @param {Object} options - Optional configuration overrides
         */
        init: function(containerId, options = {}) {
            if (!containerId) {
                console.error('NavTabs: containerId is required');
                return;
            }

            if (this.instances.has(containerId)) {
                console.warn('NavTabs: Container already initialized:', containerId);
                return;
            }

            const containerElement = document.querySelector(`[data-container-id="${containerId}"]`);
            if (!containerElement) {
                console.error('NavTabs: Container not found:', containerId);
                return;
            }

            // Create live region for screen reader announcements (shared across all instances)
            this.createLiveRegion();

            // Merge options with defaults
            const instanceConfig = Object.assign({}, this.config, options);

            // Get navigation and persistence modes
            const navigationMode = containerElement.dataset.navigationMode || 'pagenavigation';
            const persistenceMode = containerElement.dataset.persistenceMode || 'none';

            // Create instance state
            const instance = {
                containerId: containerId,
                containerElement: containerElement,
                navigationMode: navigationMode,
                persistenceMode: persistenceMode,
                config: instanceConfig,
                activeTabId: null,
                currentRequest: null,
                loadedPanels: new Map(), // tabId -> { timestamp, content }
                popstateListener: null,
                hashchangeListener: null
            };

            // Store instance
            this.instances.set(containerId, instance);

            // Get tablist and tabs
            const tablist = containerElement.querySelector(instance.config.tablistSelector);
            if (!tablist) {
                console.warn('NavTabs: No tablist found in container:', containerId);
                return;
            }

            const tabs = tablist.querySelectorAll(instance.config.tabSelector);
            if (tabs.length === 0) {
                console.warn('NavTabs: No tabs found in container:', containerId);
                return;
            }

            // Bind keyboard navigation (works for all modes)
            this.bindKeyboardNavigation(instance, tablist, tabs);

            // For PageNavigation mode, links work naturally - only set up scroll indicators
            if (navigationMode === 'pagenavigation') {
                this.setupScrollIndicators(instance, tablist);
                // Scroll active tab into view on initial load
                const activeTab = tablist.querySelector(`.${instance.config.activeClass}`);
                if (activeTab) {
                    this.scrollTabIntoView(instance, tablist, activeTab);
                }
                return;
            }

            // For InPage and Ajax modes, handle tab switching
            this.bindClickHandlers(instance, tabs);

            // Restore active tab from persistence
            const restoredTabId = this.restoreActiveTab(instance);
            if (restoredTabId) {
                instance.activeTabId = restoredTabId;
                this.activateTab(instance, restoredTabId, { updateHistory: false });
            } else {
                // Use the tab marked as active in HTML
                const activeTab = tablist.querySelector(`.${instance.config.activeClass}`);
                if (activeTab) {
                    const tabId = activeTab.dataset.tabId;
                    instance.activeTabId = tabId;

                    // For InPage mode, ensure panel visibility matches
                    if (navigationMode === 'inpage') {
                        this.showPanel(instance, tabId);
                    }

                    // For Ajax mode, load content if not pre-rendered
                    if (navigationMode === 'ajax') {
                        const panel = this.getPanel(instance, tabId);
                        if (!panel || !panel.innerHTML.trim()) {
                            this.loadTabContent(instance, tabId);
                        }
                    }
                }
            }

            // Set up scroll indicators
            this.setupScrollIndicators(instance, tablist);

            // Scroll active tab into view on initial load
            if (instance.activeTabId) {
                const activeTab = tablist.querySelector(`[data-tab-id="${instance.activeTabId}"]`);
                if (activeTab) {
                    this.scrollTabIntoView(instance, tablist, activeTab);
                }
            }

            // Listen for browser back/forward (only for hash persistence)
            if (persistenceMode === 'hash') {
                this.bindPopstateListener(instance);
            }

            console.log('NavTabs initialized:', containerId, 'mode:', navigationMode, 'persistence:', persistenceMode);
        },

        /**
         * Create an aria-live region for screen reader announcements.
         */
        createLiveRegion: function() {
            if (this.liveRegion) return;

            const region = document.createElement('div');
            region.id = 'nav-tabs-announcer';
            region.setAttribute('role', 'status');
            region.setAttribute('aria-live', 'polite');
            region.setAttribute('aria-atomic', 'true');
            region.style.cssText = 'position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0;';
            document.body.appendChild(region);
            this.liveRegion = region;
        },

        /**
         * Announce a message to screen readers.
         * @param {string} message - The message to announce
         */
        announce: function(message) {
            if (!this.liveRegion) return;

            // Clear and re-set to ensure announcement
            this.liveRegion.textContent = '';
            setTimeout(() => {
                this.liveRegion.textContent = message;
            }, 50);
        },

        /**
         * Bind keyboard navigation for tabs.
         * @param {Object} instance - The instance object
         * @param {HTMLElement} tablist - The tablist element
         * @param {NodeList} tabs - The tab elements
         */
        bindKeyboardNavigation: function(instance, tablist, tabs) {
            const self = this;
            const enabledTabs = Array.from(tabs).filter(tab =>
                !tab.disabled && !tab.hasAttribute('aria-disabled')
            );

            tablist.addEventListener('keydown', function(e) {
                const currentTab = document.activeElement;
                if (!currentTab || !currentTab.matches(instance.config.tabSelector)) return;

                const currentIndex = enabledTabs.indexOf(currentTab);
                if (currentIndex === -1) return;

                let targetTab = null;

                switch (e.key) {
                    case 'ArrowLeft':
                        e.preventDefault();
                        // Wrap around to last tab
                        targetTab = enabledTabs[currentIndex - 1] || enabledTabs[enabledTabs.length - 1];
                        break;

                    case 'ArrowRight':
                        e.preventDefault();
                        // Wrap around to first tab
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
                        if (instance.navigationMode === 'pagenavigation' && currentTab.tagName === 'A') {
                            // For page navigation, follow the link
                            currentTab.click();
                        } else {
                            // For in-page/ajax modes, activate the tab
                            self.activateTab(instance, currentTab.dataset.tabId);
                        }
                        break;
                }

                if (targetTab) {
                    targetTab.focus();
                    // In in-page/ajax modes, also activate on arrow key navigation
                    if (instance.navigationMode !== 'pagenavigation') {
                        self.activateTab(instance, targetTab.dataset.tabId);
                    }
                }
            });
        },

        /**
         * Bind click handlers for tabs.
         * @param {Object} instance - The instance object
         * @param {NodeList} tabs - The tab elements
         */
        bindClickHandlers: function(instance, tabs) {
            const self = this;

            tabs.forEach(tab => {
                tab.addEventListener('click', function(e) {
                    if (this.disabled || this.hasAttribute('aria-disabled')) {
                        e.preventDefault();
                        return;
                    }

                    e.preventDefault();
                    const tabId = this.dataset.tabId;
                    self.activateTab(instance, tabId);
                });
            });
        },

        /**
         * Bind popstate listener for browser back/forward navigation.
         * @param {Object} instance - The instance object
         */
        bindPopstateListener: function(instance) {
            const self = this;

            // Store listener reference for cleanup
            instance.popstateListener = function(e) {
                const tabId = self.getTabFromUrl(instance);
                if (tabId && tabId !== instance.activeTabId) {
                    self.activateTab(instance, tabId, { updateHistory: false });
                }
            };

            window.addEventListener('popstate', instance.popstateListener);
        },

        /**
         * Get tab ID from URL hash.
         * @param {Object} instance - The instance object
         * @returns {string|null} The tab ID or null
         */
        getTabFromUrl: function(instance) {
            const hash = window.location.hash.slice(1);

            // Verify the hash is a valid tab ID for this instance
            if (hash) {
                const tablist = instance.containerElement.querySelector(instance.config.tablistSelector);
                const tab = tablist?.querySelector(`[data-tab-id="${hash}"]`);
                if (tab) {
                    return hash;
                }
            }

            return null;
        },

        /**
         * Activate a specific tab.
         * @param {Object} instance - The instance object
         * @param {string} tabId - The tab ID to activate
         * @param {Object} options - Options { updateHistory: boolean }
         */
        activateTab: function(instance, tabId, options = {}) {
            const { updateHistory = true } = options;

            if (!tabId) return;

            const tablist = instance.containerElement.querySelector(instance.config.tablistSelector);
            const tabs = tablist.querySelectorAll(instance.config.tabSelector);
            const targetTab = tablist.querySelector(`[data-tab-id="${tabId}"]`);

            if (!targetTab) {
                console.warn('NavTabs: Tab not found:', tabId);
                return;
            }

            // Update tab states
            tabs.forEach(tab => {
                const isActive = tab.dataset.tabId === tabId;
                tab.classList.toggle(instance.config.activeClass, isActive);
                tab.setAttribute('aria-selected', isActive ? 'true' : 'false');
                tab.setAttribute('tabindex', isActive ? '0' : '-1');

                // Update icon
                this.updateTabIcon(instance, tab, isActive);
            });

            // Update state
            instance.activeTabId = tabId;

            // Scroll active tab into view if needed
            this.scrollTabIntoView(instance, tablist, targetTab);

            // Announce to screen readers
            const tabLabel = targetTab.textContent?.trim() || tabId;
            this.announce(`${tabLabel} tab selected`);

            // Handle content based on navigation mode
            if (instance.navigationMode === 'inpage') {
                this.showPanel(instance, tabId);
            } else if (instance.navigationMode === 'ajax') {
                this.loadTabContent(instance, tabId);
            }

            // Persist state
            if (updateHistory) {
                this.persistActiveTab(instance, tabId);
            }

            // Dispatch custom event
            instance.containerElement.dispatchEvent(new CustomEvent('navtabchange', {
                bubbles: true,
                detail: { containerId: instance.containerId, tabId: tabId }
            }));
        },

        /**
         * Update tab icon between outline and solid.
         * @param {Object} instance - The instance object
         * @param {HTMLElement} tab - The tab element
         * @param {boolean} isActive - Whether the tab is active
         */
        updateTabIcon: function(instance, tab, isActive) {
            const svg = tab.querySelector('.nav-tabs-icon');
            if (!svg) return;

            const path = svg.querySelector('path');
            if (!path) return;

            const outlinePath = tab.dataset.iconOutline;
            const solidPath = tab.dataset.iconSolid;

            if (!outlinePath || !solidPath) return;

            if (isActive) {
                // Switch to solid icon
                svg.setAttribute('fill', 'currentColor');
                svg.removeAttribute('stroke');
                path.setAttribute('d', solidPath);
                path.removeAttribute('stroke-linecap');
                path.removeAttribute('stroke-linejoin');
                path.removeAttribute('stroke-width');
            } else {
                // Switch to outline icon
                svg.setAttribute('fill', 'none');
                svg.setAttribute('stroke', 'currentColor');
                path.setAttribute('d', outlinePath);
                path.setAttribute('stroke-linecap', 'round');
                path.setAttribute('stroke-linejoin', 'round');
                path.setAttribute('stroke-width', '2');
            }
        },

        /**
         * Show a specific panel and hide others (InPage mode).
         * @param {Object} instance - The instance object
         * @param {string} tabId - The tab ID whose panel to show
         */
        showPanel: function(instance, tabId) {
            const panels = document.querySelectorAll(`[data-nav-panel-for="${instance.containerId}"]`);

            panels.forEach(panel => {
                const isActive = panel.dataset.tabId === tabId;
                panel.hidden = !isActive;
                panel.classList.toggle(instance.config.activeClass, isActive);
            });
        },

        /**
         * Get a specific panel element.
         * @param {Object} instance - The instance object
         * @param {string} tabId - The tab ID
         * @returns {HTMLElement|null} The panel element or null
         */
        getPanel: function(instance, tabId) {
            return document.querySelector(
                `[data-nav-panel-for="${instance.containerId}"][data-tab-id="${tabId}"]`
            );
        },

        /**
         * Load tab content via AJAX (Ajax mode).
         * @param {Object} instance - The instance object
         * @param {string} tabId - The tab ID to load
         */
        loadTabContent: async function(instance, tabId) {
            const tab = instance.containerElement.querySelector(`[data-tab-id="${tabId}"]`);
            if (!tab) return;

            const ajaxUrl = tab.dataset.ajaxUrl;
            if (!ajaxUrl) {
                console.warn('NavTabs: No ajax-url for tab:', tabId);
                return;
            }

            let panel = this.getPanel(instance, tabId);

            // Create panel if it doesn't exist
            if (!panel) {
                panel = document.createElement('div');
                panel.setAttribute('data-nav-panel-for', instance.containerId);
                panel.setAttribute('data-tab-id', tabId);
                panel.setAttribute('role', 'tabpanel');
                panel.setAttribute('aria-labelledby', `${instance.containerId}-tab-${tabId}`);
                panel.id = `${instance.containerId}-panel-${tabId}`;

                // Append after the nav tabs container
                instance.containerElement.insertAdjacentElement('afterend', panel);
            }

            // Show only this panel
            this.showPanel(instance, tabId);

            // Check if already loaded
            const cached = instance.loadedPanels.get(tabId);
            if (cached) {
                panel.innerHTML = cached.content;
                this.executeScripts(panel);
                return;
            }

            // Cancel any pending request
            if (instance.currentRequest) {
                instance.currentRequest.abort();
                instance.currentRequest = null;
            }

            // Show loading state (with delay to prevent flash)
            let loadingTimeout = setTimeout(() => {
                this.showLoading(instance, panel);
                tab.classList.add(instance.config.loadingClass);
                this.announce(`Loading ${tab.textContent?.trim() || tabId} tab...`);
            }, instance.config.loadingDelay);

            // Create abort controller
            const abortController = new AbortController();
            instance.currentRequest = abortController;

            try {
                const response = await fetch(ajaxUrl, {
                    signal: abortController.signal,
                    headers: {
                        'Accept': 'text/html',
                        'X-Requested-With': 'XMLHttpRequest'
                    }
                });

                clearTimeout(loadingTimeout);

                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}: ${response.statusText}`);
                }

                const html = await response.text();

                // Check if this request is still current
                if (instance.currentRequest !== abortController) {
                    return; // A newer request has superseded this one
                }

                // Cache the content
                instance.loadedPanels.set(tabId, {
                    timestamp: Date.now(),
                    content: html
                });

                // Update panel content
                panel.innerHTML = html;
                this.executeScripts(panel);

                // Announce completion
                this.announce(`${tab.textContent?.trim() || tabId} tab loaded`);

            } catch (error) {
                clearTimeout(loadingTimeout);

                if (error.name === 'AbortError') {
                    console.log('NavTabs: Request aborted:', tabId);
                    return;
                }

                console.error('NavTabs: Failed to load tab content:', tabId, error);
                this.showError(instance, panel, error.message);
                this.announce(`Failed to load ${tab.textContent?.trim() || tabId} tab`);

            } finally {
                tab.classList.remove(instance.config.loadingClass);
                this.hideLoading(instance, panel);

                if (instance.currentRequest === abortController) {
                    instance.currentRequest = null;
                }
            }
        },

        /**
         * Show loading state in panel.
         * @param {Object} instance - The instance object
         * @param {HTMLElement} panel - The panel element
         */
        showLoading: function(instance, panel) {
            panel.setAttribute('aria-busy', 'true');

            const spinner = document.createElement('div');
            spinner.className = 'nav-tabs-loading';
            spinner.innerHTML = `
                <svg class="nav-tabs-spinner" viewBox="0 0 24 24" aria-hidden="true">
                    <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4" fill="none"></circle>
                    <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                </svg>
                <span class="sr-only">Loading...</span>
            `;

            panel.innerHTML = '';
            panel.appendChild(spinner);
        },

        /**
         * Hide loading state from panel.
         * @param {Object} instance - The instance object
         * @param {HTMLElement} panel - The panel element
         */
        hideLoading: function(instance, panel) {
            panel.removeAttribute('aria-busy');
            const spinner = panel.querySelector('.nav-tabs-loading');
            if (spinner) {
                spinner.remove();
            }
        },

        /**
         * Show error state in panel.
         * @param {Object} instance - The instance object
         * @param {HTMLElement} panel - The panel element
         * @param {string} message - Error message
         */
        showError: function(instance, panel, message) {
            panel.removeAttribute('aria-busy');

            const errorDiv = document.createElement('div');
            errorDiv.className = 'nav-tabs-error';
            errorDiv.innerHTML = `
                <div class="nav-tabs-error-content">
                    <svg class="nav-tabs-error-icon" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
                    </svg>
                    <h3 class="nav-tabs-error-title">Failed to Load Content</h3>
                    <p class="nav-tabs-error-message">${this.escapeHtml(message)}</p>
                    <button type="button" class="btn btn-secondary nav-tabs-retry-btn" data-retry-container="${this.escapeHtml(instance.containerId)}" data-retry-tab="${this.escapeHtml(panel.dataset.tabId)}">
                        Retry
                    </button>
                </div>
            `;

            panel.innerHTML = '';
            panel.appendChild(errorDiv);
        },

        /**
         * Retry loading a tab.
         * @param {string} containerId - The container ID
         * @param {string} tabId - The tab ID to retry
         */
        retry: function(containerId, tabId) {
            const instance = this.instances.get(containerId);
            if (!instance) return;

            // Clear cached content
            instance.loadedPanels.delete(tabId);

            // Reload
            this.loadTabContent(instance, tabId);
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
         * Persist the active tab state.
         * @param {Object} instance - The instance object
         * @param {string} tabId - The active tab ID
         */
        persistActiveTab: function(instance, tabId) {
            switch (instance.persistenceMode) {
                case 'hash':
                    if (window.location.hash !== `#${tabId}`) {
                        history.pushState({ tabId: tabId, containerId: instance.containerId }, '', `#${tabId}`);
                    }
                    break;

                case 'localstorage':
                    try {
                        localStorage.setItem(`nav-tabs-${instance.containerId}`, tabId);
                    } catch (e) {
                        console.warn('NavTabs: Failed to save to localStorage', e);
                    }
                    break;
            }
        },

        /**
         * Restore the active tab from persisted state.
         * @param {Object} instance - The instance object
         * @returns {string|null} The restored tab ID or null
         */
        restoreActiveTab: function(instance) {
            switch (instance.persistenceMode) {
                case 'hash':
                    return this.getTabFromUrl(instance);

                case 'localstorage':
                    try {
                        return localStorage.getItem(`nav-tabs-${instance.containerId}`);
                    } catch (e) {
                        console.warn('NavTabs: Failed to read from localStorage', e);
                    }
                    break;
            }
            return null;
        },

        /**
         * Set up scroll indicators for tab overflow.
         * @param {Object} instance - The instance object
         * @param {HTMLElement} tablist - The tablist element
         */
        setupScrollIndicators: function(instance, tablist) {
            const container = instance.containerElement;
            const threshold = instance.config.scrollThreshold;

            function updateIndicators() {
                const scrollLeft = tablist.scrollLeft;
                const scrollWidth = tablist.scrollWidth;
                const clientWidth = tablist.clientWidth;
                const isScrollable = scrollWidth > clientWidth;

                if (isScrollable) {
                    container.classList.toggle('can-scroll-left', scrollLeft > threshold);
                    container.classList.toggle('can-scroll-right',
                        scrollLeft < scrollWidth - clientWidth - threshold);
                } else {
                    container.classList.remove('can-scroll-left', 'can-scroll-right');
                }
            }

            // Initial check
            updateIndicators();

            // Store listener references for cleanup
            instance.scrollHandler = updateIndicators;
            instance.resizeHandler = updateIndicators;

            // Update on scroll and resize
            tablist.addEventListener('scroll', instance.scrollHandler, { passive: true });
            window.addEventListener('resize', instance.resizeHandler, { passive: true });
        },

        /**
         * Scroll a tab into view if not fully visible.
         * @param {Object} instance - The instance object
         * @param {HTMLElement} tablist - The tablist element
         * @param {HTMLElement} tab - The tab to scroll into view
         */
        scrollTabIntoView: function(instance, tablist, tab) {
            const tabRect = tab.getBoundingClientRect();
            const listRect = tablist.getBoundingClientRect();

            // Check if tab is cut off
            if (tabRect.left < listRect.left || tabRect.right > listRect.right) {
                tab.scrollIntoView({
                    behavior: 'smooth',
                    inline: 'center',
                    block: 'nearest'
                });
            }
        },

        /**
         * Helper: Escape HTML to prevent XSS.
         * @param {string} str - String to escape
         * @returns {string}
         */
        escapeHtml: function(str) {
            const div = document.createElement('div');
            div.textContent = str;
            return div.innerHTML;
        },

        /**
         * Programmatically switch to a tab.
         * @param {string} containerId - The container ID
         * @param {string} tabId - The tab ID to activate
         */
        switchTo: function(containerId, tabId) {
            const instance = this.instances.get(containerId);
            if (instance) {
                this.activateTab(instance, tabId);
            }
        },

        /**
         * Get the currently active tab ID for a container.
         * @param {string} containerId - The container ID
         * @returns {string|null} The active tab ID or null
         */
        getActiveTab: function(containerId) {
            const instance = this.instances.get(containerId);
            return instance ? instance.activeTabId : null;
        },

        /**
         * Destroy a tab container instance.
         * @param {string} containerId - The container ID
         */
        destroy: function(containerId) {
            const instance = this.instances.get(containerId);
            if (!instance) return;

            // Cancel any pending requests
            if (instance.currentRequest) {
                instance.currentRequest.abort();
            }

            // Remove event listeners
            if (instance.popstateListener) {
                window.removeEventListener('popstate', instance.popstateListener);
            }

            // Remove scroll and resize listeners
            if (instance.scrollHandler) {
                const tablist = instance.containerElement.querySelector(instance.config.tablistSelector);
                if (tablist) {
                    tablist.removeEventListener('scroll', instance.scrollHandler);
                }
            }
            if (instance.resizeHandler) {
                window.removeEventListener('resize', instance.resizeHandler);
            }

            // Remove instance
            this.instances.delete(containerId);
        }
    };

    // Expose to global scope
    window.NavTabs = NavTabs;

    // Delegated event handler for retry buttons
    document.addEventListener('click', function(e) {
        const retryBtn = e.target.closest('.nav-tabs-retry-btn');
        if (retryBtn) {
            const containerId = retryBtn.dataset.retryContainer;
            const tabId = retryBtn.dataset.retryTab;
            if (containerId && tabId) {
                NavTabs.retry(containerId, tabId);
            }
        }
    });

    // Auto-initialize all nav-tabs containers on DOMContentLoaded
    document.addEventListener('DOMContentLoaded', function() {
        document.querySelectorAll('[data-nav-tabs]').forEach(container => {
            const containerId = container.dataset.containerId;
            if (containerId) {
                NavTabs.init(containerId);
            }
        });
    });

})();
