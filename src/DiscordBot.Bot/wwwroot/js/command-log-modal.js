/**
 * Command Log Modal Module
 * Handles displaying command log details in a modal overlay.
 * Uses AJAX to load content without page navigation.
 */
(function() {
    'use strict';

    /**
     * CommandLogModal class
     * Manages modal state, content loading, and accessibility.
     */
    class CommandLogModal {
        constructor() {
            this.modal = document.getElementById('commandLogDetailsModal');
            this.modalContent = document.getElementById('commandLogModalContent');
            this.triggerElement = null;
            this.abortController = null;
            this.focusableElements = [];
            this.firstFocusableElement = null;
            this.lastFocusableElement = null;
            this.boundHandleTabKey = this.handleTabKey.bind(this);

            if (!this.modal || !this.modalContent) {
                console.error('CommandLogModal: Required modal elements not found');
                return;
            }

            this.setupEventListeners();
        }

        /**
         * Setup global event listeners for the modal.
         */
        setupEventListeners() {
            // ESC key to close modal
            document.addEventListener('keydown', (e) => {
                if (e.key === 'Escape' && !this.modal.classList.contains('hidden')) {
                    this.close();
                }
            });

            // Close button click (handled via onclick in HTML)
            // Backdrop click (handled via onclick in HTML)
        }

        /**
         * Open the modal and load command log details.
         * @param {string} logId - The command log GUID
         */
        async open(logId) {
            if (!logId) {
                console.error('CommandLogModal: logId is required');
                return;
            }

            // Store trigger element for focus return
            this.triggerElement = document.activeElement;

            // Abort previous request if pending
            if (this.abortController) {
                this.abortController.abort();
            }
            this.abortController = new AbortController();

            // Show modal immediately with loading state
            this.modal.classList.remove('hidden');

            // Show loading spinner
            this.modalContent.innerHTML = `
                <div class="flex items-center justify-center py-12">
                    <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-accent-blue"></div>
                </div>
            `;

            try {
                // Fetch content from API
                const response = await fetch(`/api/commands/log-details/${logId}`, {
                    method: 'GET',
                    headers: {
                        'X-Requested-With': 'XMLHttpRequest'
                    },
                    signal: this.abortController.signal
                });

                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}: ${response.statusText}`);
                }

                const html = await response.text();

                // Inject content
                this.modalContent.innerHTML = html;

                // Execute inline scripts manually (CSP requirement - innerHTML doesn't execute them)
                this.executeScripts(this.modalContent);

                // Setup focus trap
                this.setupFocusTrap();

                // Update URL hash
                this.updateHash(logId);

                // Initialize timezone conversion for timestamps (if available)
                if (window.TimezoneConverter) {
                    window.TimezoneConverter.convertAll();
                }

            } catch (error) {
                // Ignore abort errors
                if (error.name === 'AbortError') {
                    console.log('CommandLogModal: Request aborted');
                    return;
                }

                console.error('CommandLogModal: Failed to load details:', error);

                // Show error state
                this.modalContent.innerHTML = `
                    <div class="text-center py-12">
                        <div class="text-error mb-4">
                            <svg class="w-12 h-12 mx-auto" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                                      d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"></path>
                            </svg>
                        </div>
                        <p class="text-lg font-semibold text-text-primary mb-2">Failed to load details</p>
                        <p class="text-text-secondary mb-4">An error occurred while loading the command log details.</p>
                        <button type="button" onclick="commandLogModal.close()" class="btn btn-secondary">Close</button>
                    </div>
                `;
            } finally {
                this.abortController = null;
            }
        }

        /**
         * Close the modal and restore state.
         */
        close() {
            // Hide modal
            this.modal.classList.add('hidden');

            // Release focus trap
            this.releaseFocusTrap();

            // Return focus to trigger element
            if (this.triggerElement && document.body.contains(this.triggerElement)) {
                this.triggerElement.focus();
            }
            this.triggerElement = null;

            // Restore hash to execution logs tab
            if (window.location.hash.includes('/details/')) {
                window.location.hash = '#execution-logs';
            }

            // Abort any pending request
            if (this.abortController) {
                this.abortController.abort();
                this.abortController = null;
            }
        }

        /**
         * Setup focus trap within the modal.
         */
        setupFocusTrap() {
            // Get all focusable elements in modal
            this.focusableElements = this.modal.querySelectorAll(
                'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
            );

            if (this.focusableElements.length > 0) {
                this.firstFocusableElement = this.focusableElements[0];
                this.lastFocusableElement = this.focusableElements[this.focusableElements.length - 1];

                // Focus first element
                requestAnimationFrame(() => {
                    if (this.firstFocusableElement) {
                        this.firstFocusableElement.focus();
                    }
                });

                // Add tab trap listener
                this.modal.addEventListener('keydown', this.boundHandleTabKey);
            }
        }

        /**
         * Release focus trap.
         */
        releaseFocusTrap() {
            this.modal.removeEventListener('keydown', this.boundHandleTabKey);
            this.focusableElements = [];
            this.firstFocusableElement = null;
            this.lastFocusableElement = null;
        }

        /**
         * Handle Tab key for focus cycling within modal.
         * @param {KeyboardEvent} e - The keyboard event
         */
        handleTabKey(e) {
            if (e.key !== 'Tab') return;

            if (e.shiftKey) {
                // Shift+Tab: cycle backwards
                if (document.activeElement === this.firstFocusableElement) {
                    e.preventDefault();
                    this.lastFocusableElement.focus();
                }
            } else {
                // Tab: cycle forwards
                if (document.activeElement === this.lastFocusableElement) {
                    e.preventDefault();
                    this.firstFocusableElement.focus();
                }
            }
        }

        /**
         * Execute script tags manually (CSP requirement).
         * @param {HTMLElement} container - Container element with scripts
         */
        executeScripts(container) {
            const scripts = container.querySelectorAll('script');
            for (let i = 0; i < scripts.length; i++) {
                const oldScript = scripts[i];
                const newScript = document.createElement('script');

                // Copy attributes
                if (oldScript.src) {
                    newScript.src = oldScript.src;
                } else {
                    newScript.textContent = oldScript.textContent;
                }

                // Replace old script with new one to execute
                oldScript.parentNode.replaceChild(newScript, oldScript);
            }
        }

        /**
         * Update URL hash to reflect modal state.
         * @param {string} logId - The command log GUID
         */
        updateHash(logId) {
            // Optional URL hash update for deep linking
            window.location.hash = `#execution-logs/details/${logId}`;
        }
    }

    // Export to global scope
    window.CommandLogModal = CommandLogModal;

    // Auto-initialize if modal exists
    if (document.getElementById('commandLogDetailsModal')) {
        window.commandLogModal = new CommandLogModal();
    }
})();
