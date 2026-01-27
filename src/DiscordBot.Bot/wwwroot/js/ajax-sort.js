/**
 * AJAX Sort Module
 * Handles dynamic content updates when sort options change.
 *
 * Usage:
 *   1. Include this script in your page: <script src="~/js/ajax-sort.js"></script>
 *   2. Set UseAjax=true on your SortDropdownViewModel
 *   3. The module auto-initializes and listens for 'sortchange' events
 *   4. Optionally call AjaxSort.init() with custom callbacks
 */
(function() {
    'use strict';

    const AjaxSort = {
        /**
         * Initialize AJAX sort handling.
         * @param {Object} options - Configuration options
         * @param {Function} options.onBeforeLoad - Called before fetch starts
         * @param {Function} options.onAfterLoad - Called after content swapped
         * @param {Function} options.onError - Called on fetch error
         */
        init: function(options = {}) {
            document.addEventListener('sortchange', async function(e) {
                await AjaxSort.handleSortChange(e, options);
            });

            window.addEventListener('popstate', function(e) {
                if (e.state && e.state.sort) {
                    location.reload();
                }
            });
        },

        /**
         * Handle sort change event.
         */
        handleSortChange: async function(e, options) {
            const { sortValue, paramName, targetSelector, partialUrl } = e.detail;

            if (!targetSelector || !partialUrl) {
                console.error('AjaxSort: Missing targetSelector or partialUrl');
                return;
            }

            const target = document.querySelector(targetSelector);
            if (!target) {
                console.error('AjaxSort: Target element not found:', targetSelector);
                return;
            }

            // Build URL
            const url = new URL(partialUrl, window.location.origin);
            url.searchParams.set(paramName, sortValue);

            // Callbacks
            if (options.onBeforeLoad) {
                options.onBeforeLoad(target, sortValue);
            }

            // Show loading
            target.setAttribute('aria-busy', 'true');
            this.showLoading(target);

            try {
                const response = await fetch(url.toString(), {
                    headers: {
                        'Accept': 'text/html',
                        'X-Requested-With': 'XMLHttpRequest'
                    }
                });

                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}: ${response.statusText}`);
                }

                const html = await response.text();

                // Swap content
                target.innerHTML = html;

                // Update URL
                const currentUrl = new URL(window.location);
                currentUrl.searchParams.set(paramName, sortValue);
                history.pushState(
                    { sort: sortValue },
                    '',
                    currentUrl.toString()
                );

                // Callback
                if (options.onAfterLoad) {
                    options.onAfterLoad(target, sortValue);
                }

            } catch (error) {
                console.error('AjaxSort: Failed to load content:', error);
                this.showError(target, error.message);

                if (options.onError) {
                    options.onError(error, target);
                }

            } finally {
                target.removeAttribute('aria-busy');
            }
        },

        /**
         * Show loading indicator.
         */
        showLoading: function(target) {
            const spinner = document.createElement('div');
            spinner.className = 'flex items-center justify-center p-8';
            spinner.innerHTML = `
                <svg class="animate-spin h-8 w-8 text-accent-orange" viewBox="0 0 24 24">
                    <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4" fill="none"></circle>
                    <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                </svg>
            `;
            target.innerHTML = '';
            target.appendChild(spinner);
        },

        /**
         * Show error message.
         */
        showError: function(target, message) {
            target.innerHTML = `
                <div class="flex flex-col items-center justify-center p-8 text-center">
                    <svg class="w-12 h-12 text-accent-red mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                              d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
                    </svg>
                    <h3 class="text-lg font-semibold text-text-primary mb-2">Failed to Load</h3>
                    <p class="text-sm text-text-secondary mb-4">${this.escapeHtml(message)}</p>
                    <button type="button" class="btn btn-secondary" onclick="location.reload()">
                        Reload Page
                    </button>
                </div>
            `;
        },

        /**
         * Escape HTML to prevent XSS.
         */
        escapeHtml: function(str) {
            const div = document.createElement('div');
            div.textContent = str;
            return div.innerHTML;
        }
    };

    // Expose to global scope
    window.AjaxSort = AjaxSort;

    // Auto-initialize on DOMContentLoaded
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function() {
            AjaxSort.init();
        });
    } else {
        // DOM is already ready
        AjaxSort.init();
    }
})();
