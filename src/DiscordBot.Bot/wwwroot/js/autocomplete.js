/**
 * Autocomplete Manager
 * Provides reusable autocomplete functionality for input fields.
 *
 * Usage:
 * AutocompleteManager.init({
 *     inputId: 'authorId',
 *     hiddenInputId: 'authorIdValue',
 *     endpoint: '/api/autocomplete/users',
 *     guildIdSource: 'guildId',      // Optional
 *     minChars: 2,
 *     debounceMs: 300,
 *     maxResults: 25,
 *     placeholder: 'Search by username...',
 *     noResultsMessage: 'No users found'
 * });
 */
const AutocompleteManager = (function() {
    'use strict';

    const instances = new Map();
    let activeInstance = null;

    const defaults = {
        minChars: 2,
        debounceMs: 300,
        maxResults: 25,
        placeholder: 'Search...',
        noResultsMessage: 'No results found'
    };

    /**
     * Escape HTML to prevent XSS
     * @param {string} text - Text to escape
     * @returns {string} Escaped text
     */
    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    /**
     * Debounce function to limit API calls
     * @param {Function} func - Function to debounce
     * @param {number} wait - Wait time in ms
     * @returns {Function} Debounced function
     */
    function debounce(func, wait) {
        let timeout;
        return function executedFunction(...args) {
            const later = () => {
                clearTimeout(timeout);
                func(...args);
            };
            clearTimeout(timeout);
            timeout = setTimeout(later, wait);
        };
    }

    /**
     * AutocompleteInstance class for managing a single autocomplete input
     */
    class AutocompleteInstance {
        constructor(config) {
            this.config = { ...defaults, ...config };
            this.input = document.getElementById(config.inputId);
            this.hiddenInput = document.getElementById(config.hiddenInputId);
            this.dropdown = null;
            this.results = [];
            this.selectedIndex = -1;
            this.isOpen = false;
            this.isLoading = false;
            this.abortController = null;

            if (!this.input) {
                console.error(`Autocomplete: Input with id "${config.inputId}" not found`);
                return;
            }

            if (!this.hiddenInput) {
                console.error(`Autocomplete: Hidden input with id "${config.hiddenInputId}" not found`);
                return;
            }

            this.init();
        }

        init() {
            // Set ARIA attributes
            this.input.setAttribute('role', 'combobox');
            this.input.setAttribute('aria-autocomplete', 'list');
            this.input.setAttribute('aria-expanded', 'false');
            this.input.setAttribute('aria-haspopup', 'listbox');
            this.input.setAttribute('autocomplete', 'off');

            if (this.config.placeholder) {
                this.input.placeholder = this.config.placeholder;
            }

            // Create dropdown container
            this.createDropdown();

            // Create clear button
            this.createClearButton();

            // Bind events
            this.bindEvents();
        }

        createDropdown() {
            this.dropdown = document.createElement('div');
            this.dropdown.className = 'autocomplete-dropdown';
            this.dropdown.setAttribute('role', 'listbox');
            this.dropdown.id = `${this.config.inputId}-listbox`;
            this.input.setAttribute('aria-controls', this.dropdown.id);

            // Position dropdown relative to input's container
            const wrapper = this.input.closest('.autocomplete-wrapper') || this.input.parentNode;
            wrapper.style.position = 'relative';
            wrapper.appendChild(this.dropdown);
        }

        createClearButton() {
            const wrapper = this.input.closest('.autocomplete-wrapper') || this.input.parentNode;

            this.clearButton = document.createElement('button');
            this.clearButton.type = 'button';
            this.clearButton.className = 'autocomplete-clear hidden';
            this.clearButton.setAttribute('aria-label', 'Clear selection');
            this.clearButton.innerHTML = `
                <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                </svg>
            `;

            wrapper.appendChild(this.clearButton);

            this.clearButton.addEventListener('click', () => {
                this.clear();
                this.input.focus();
            });
        }

        bindEvents() {
            // Debounced search
            const debouncedSearch = debounce((value) => {
                this.search(value);
            }, this.config.debounceMs);

            this.input.addEventListener('input', (e) => {
                const value = e.target.value.trim();

                // Clear hidden input when user types
                this.hiddenInput.value = '';
                this.updateClearButton();

                if (value.length >= this.config.minChars) {
                    debouncedSearch(value);
                } else {
                    this.close();
                }
            });

            // Keyboard navigation
            this.input.addEventListener('keydown', (e) => {
                this.handleKeydown(e);
            });

            // Focus handling
            this.input.addEventListener('focus', () => {
                if (this.results.length > 0 && this.input.value.length >= this.config.minChars) {
                    this.open();
                }
            });

            // Close on blur (with delay for click handling)
            this.input.addEventListener('blur', (e) => {
                setTimeout(() => {
                    if (!this.dropdown.contains(document.activeElement)) {
                        this.close();
                    }
                }, 150);
            });

            // Close on escape anywhere
            document.addEventListener('keydown', (e) => {
                if (e.key === 'Escape' && this.isOpen) {
                    this.close();
                    this.input.focus();
                }
            });

            // Close on click outside
            document.addEventListener('click', (e) => {
                if (!this.input.contains(e.target) && !this.dropdown.contains(e.target)) {
                    this.close();
                }
            });
        }

        handleKeydown(e) {
            if (!this.isOpen) {
                if (e.key === 'ArrowDown' && this.results.length > 0) {
                    e.preventDefault();
                    this.open();
                }
                return;
            }

            switch (e.key) {
                case 'ArrowDown':
                    e.preventDefault();
                    this.selectNext();
                    break;
                case 'ArrowUp':
                    e.preventDefault();
                    this.selectPrevious();
                    break;
                case 'Enter':
                    e.preventDefault();
                    if (this.selectedIndex >= 0) {
                        this.selectItem(this.results[this.selectedIndex]);
                    }
                    break;
                case 'Escape':
                    e.preventDefault();
                    this.close();
                    break;
                case 'Tab':
                    this.close();
                    break;
            }
        }

        async search(term) {
            // Cancel any pending request
            if (this.abortController) {
                this.abortController.abort();
            }

            this.abortController = new AbortController();
            this.showLoading();

            try {
                let url = `${this.config.endpoint}?search=${encodeURIComponent(term)}`;

                // Add guild ID if source is specified
                if (this.config.guildIdSource) {
                    const guildInput = document.getElementById(this.config.guildIdSource);
                    if (guildInput && guildInput.value) {
                        url += `&guildId=${encodeURIComponent(guildInput.value)}`;
                    }
                }

                const response = await fetch(url, {
                    signal: this.abortController.signal,
                    headers: {
                        'Accept': 'application/json'
                    }
                });

                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}`);
                }

                const data = await response.json();
                this.results = data.slice(0, this.config.maxResults);
                this.renderResults();
                this.announceResults();

            } catch (error) {
                if (error.name === 'AbortError') {
                    return; // Ignore abort errors
                }
                console.error('Autocomplete search error:', error);
                this.results = [];
                this.renderError();
            } finally {
                this.hideLoading();
            }
        }

        showLoading() {
            this.isLoading = true;
            this.input.classList.add('autocomplete-loading');
        }

        hideLoading() {
            this.isLoading = false;
            this.input.classList.remove('autocomplete-loading');
        }

        renderResults() {
            if (this.results.length === 0) {
                this.dropdown.innerHTML = `
                    <div class="autocomplete-no-results">
                        ${escapeHtml(this.config.noResultsMessage)}
                    </div>
                `;
            } else {
                const html = this.results.map((item, index) => `
                    <div class="autocomplete-item ${index === this.selectedIndex ? 'selected' : ''}"
                         role="option"
                         id="${this.config.inputId}-option-${index}"
                         aria-selected="${index === this.selectedIndex}"
                         data-index="${index}">
                        ${this.renderItem(item)}
                    </div>
                `).join('');

                this.dropdown.innerHTML = html;

                // Add click handlers
                this.dropdown.querySelectorAll('.autocomplete-item').forEach((el) => {
                    el.addEventListener('mousedown', (e) => {
                        e.preventDefault(); // Prevent blur
                        const index = parseInt(el.dataset.index, 10);
                        this.selectItem(this.results[index]);
                    });

                    el.addEventListener('mouseover', () => {
                        const index = parseInt(el.dataset.index, 10);
                        this.setSelectedIndex(index);
                    });
                });
            }

            this.open();
        }

        renderItem(item) {
            // Extend this for custom rendering based on item type
            if (item.channelType) {
                return `
                    <span class="autocomplete-item-icon autocomplete-icon-channel">
                        ${this.getChannelIcon(item.channelType)}
                    </span>
                    <span class="autocomplete-item-text">${escapeHtml(item.displayText)}</span>
                    <span class="autocomplete-item-meta">${escapeHtml(item.channelType)}</span>
                `;
            }

            return `
                <span class="autocomplete-item-icon autocomplete-icon-default">
                    ${this.getDefaultIcon()}
                </span>
                <span class="autocomplete-item-text">${escapeHtml(item.displayText)}</span>
            `;
        }

        getDefaultIcon() {
            return `
                <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
                </svg>
            `;
        }

        getChannelIcon(channelType) {
            const type = channelType.toLowerCase();
            if (type.includes('voice') || type.includes('stage')) {
                return `
                    <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15.536 8.464a5 5 0 010 7.072m2.828-9.9a9 9 0 010 12.728M5.586 15H4a1 1 0 01-1-1v-4a1 1 0 011-1h1.586l4.707-4.707C10.923 3.663 12 4.109 12 5v14c0 .891-1.077 1.337-1.707.707L5.586 15z" />
                    </svg>
                `;
            }
            return `
                <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 20l4-16m2 16l4-16M6 9h14M4 15h14" />
                </svg>
            `;
        }

        renderError() {
            this.dropdown.innerHTML = `
                <div class="autocomplete-error">
                    An error occurred. Please try again.
                </div>
            `;
            this.open();
        }

        announceResults() {
            // Announce to screen readers
            const count = this.results.length;
            const message = count === 0
                ? this.config.noResultsMessage
                : `${count} result${count === 1 ? '' : 's'} available`;

            // Use existing live region or create one
            let liveRegion = document.getElementById('autocomplete-live-region');
            if (!liveRegion) {
                liveRegion = document.createElement('div');
                liveRegion.id = 'autocomplete-live-region';
                liveRegion.className = 'sr-only';
                liveRegion.setAttribute('aria-live', 'polite');
                liveRegion.setAttribute('aria-atomic', 'true');
                document.body.appendChild(liveRegion);
            }

            liveRegion.textContent = message;

            // Clear after announcement
            setTimeout(() => {
                liveRegion.textContent = '';
            }, 1000);
        }

        open() {
            if (this.isOpen) return;

            this.isOpen = true;
            this.dropdown.classList.add('active');
            this.input.setAttribute('aria-expanded', 'true');
            this.positionDropdown();
            activeInstance = this;
        }

        close() {
            if (!this.isOpen) return;

            this.isOpen = false;
            this.selectedIndex = -1;
            this.dropdown.classList.remove('active');
            this.input.setAttribute('aria-expanded', 'false');
            this.input.removeAttribute('aria-activedescendant');

            if (activeInstance === this) {
                activeInstance = null;
            }
        }

        positionDropdown() {
            const inputRect = this.input.getBoundingClientRect();
            const viewportHeight = window.innerHeight;
            const spaceBelow = viewportHeight - inputRect.bottom;
            const spaceAbove = inputRect.top;
            const dropdownHeight = Math.min(300, this.dropdown.scrollHeight);

            // Position above if not enough space below
            if (spaceBelow < dropdownHeight && spaceAbove > spaceBelow) {
                this.dropdown.classList.add('above');
                this.dropdown.classList.remove('below');
            } else {
                this.dropdown.classList.add('below');
                this.dropdown.classList.remove('above');
            }
        }

        selectNext() {
            const newIndex = this.selectedIndex < this.results.length - 1
                ? this.selectedIndex + 1
                : 0;
            this.setSelectedIndex(newIndex);
        }

        selectPrevious() {
            const newIndex = this.selectedIndex > 0
                ? this.selectedIndex - 1
                : this.results.length - 1;
            this.setSelectedIndex(newIndex);
        }

        setSelectedIndex(index) {
            // Remove previous selection
            const previousItem = this.dropdown.querySelector('.autocomplete-item.selected');
            if (previousItem) {
                previousItem.classList.remove('selected');
                previousItem.setAttribute('aria-selected', 'false');
            }

            this.selectedIndex = index;

            // Add new selection
            const newItem = this.dropdown.querySelector(`[data-index="${index}"]`);
            if (newItem) {
                newItem.classList.add('selected');
                newItem.setAttribute('aria-selected', 'true');
                this.input.setAttribute('aria-activedescendant', newItem.id);

                // Scroll into view if needed
                newItem.scrollIntoView({ block: 'nearest' });
            }
        }

        selectItem(item) {
            if (!item) return;

            this.input.value = item.displayText;
            this.hiddenInput.value = item.id;
            this.close();
            this.updateClearButton();

            // Dispatch change event
            this.hiddenInput.dispatchEvent(new Event('change', { bubbles: true }));
            this.input.dispatchEvent(new CustomEvent('autocomplete:select', {
                bubbles: true,
                detail: { item }
            }));
        }

        clear() {
            this.input.value = '';
            this.hiddenInput.value = '';
            this.results = [];
            this.close();
            this.updateClearButton();

            // Dispatch change events
            this.hiddenInput.dispatchEvent(new Event('change', { bubbles: true }));
            this.input.dispatchEvent(new CustomEvent('autocomplete:clear', { bubbles: true }));
        }

        updateClearButton() {
            if (this.input.value || this.hiddenInput.value) {
                this.clearButton.classList.remove('hidden');
            } else {
                this.clearButton.classList.add('hidden');
            }
        }

        /**
         * Set the value programmatically
         * @param {string} id - The ID value
         * @param {string} displayText - The display text
         */
        setValue(id, displayText) {
            this.hiddenInput.value = id;
            this.input.value = displayText;
            this.updateClearButton();
        }

        /**
         * Get the current selected value
         * @returns {{ id: string, displayText: string } | null}
         */
        getValue() {
            if (!this.hiddenInput.value) return null;
            return {
                id: this.hiddenInput.value,
                displayText: this.input.value
            };
        }

        destroy() {
            if (this.dropdown) {
                this.dropdown.remove();
            }
            if (this.clearButton) {
                this.clearButton.remove();
            }
            this.input.removeAttribute('role');
            this.input.removeAttribute('aria-autocomplete');
            this.input.removeAttribute('aria-expanded');
            this.input.removeAttribute('aria-haspopup');
            this.input.removeAttribute('aria-controls');
            instances.delete(this.config.inputId);
        }
    }

    return {
        /**
         * Initialize an autocomplete instance
         * @param {Object} config - Configuration options
         * @param {string} config.inputId - ID of the visible input element
         * @param {string} config.hiddenInputId - ID of the hidden input for form submission
         * @param {string} config.endpoint - API endpoint for search
         * @param {string} [config.guildIdSource] - Optional ID of guild select to include in search
         * @param {number} [config.minChars=2] - Min characters before search
         * @param {number} [config.debounceMs=300] - Debounce delay in ms
         * @param {number} [config.maxResults=25] - Max results to display
         * @param {string} [config.placeholder] - Input placeholder text
         * @param {string} [config.noResultsMessage] - Message when no results found
         * @returns {AutocompleteInstance} The autocomplete instance
         */
        init(config) {
            if (!config.inputId || !config.hiddenInputId || !config.endpoint) {
                console.error('Autocomplete: inputId, hiddenInputId, and endpoint are required');
                return null;
            }

            // Destroy existing instance if reinitializing
            if (instances.has(config.inputId)) {
                instances.get(config.inputId).destroy();
            }

            const instance = new AutocompleteInstance(config);
            instances.set(config.inputId, instance);
            return instance;
        },

        /**
         * Get an existing autocomplete instance
         * @param {string} inputId - The input ID
         * @returns {AutocompleteInstance | undefined}
         */
        get(inputId) {
            return instances.get(inputId);
        },

        /**
         * Destroy an autocomplete instance
         * @param {string} inputId - The input ID
         */
        destroy(inputId) {
            const instance = instances.get(inputId);
            if (instance) {
                instance.destroy();
            }
        },

        /**
         * Destroy all autocomplete instances
         */
        destroyAll() {
            instances.forEach((instance) => {
                instance.destroy();
            });
            instances.clear();
        }
    };
})();

// Export for module usage
if (typeof module !== 'undefined' && module.exports) {
    module.exports = AutocompleteManager;
}
