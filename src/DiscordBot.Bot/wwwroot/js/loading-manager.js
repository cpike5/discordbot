/**
 * Loading Manager
 * Provides loading states for page overlays, buttons, containers, and skeletons.
 *
 * Double-submit guard: add `data-submit-guard` to a <form>. On submit the submit button is
 * disabled, shows a spinner in place of its icon (text kept, or `data-loading-text` on the
 * button), and the form gets aria-busy. A second submit is ignored. Everything is restored
 * when the page comes back from the back/forward cache. Use `data-submit-guard="download"`
 * for a form that does not navigate away (a file export, target="_blank"): it re-enables
 * after a few seconds.
 */
const LoadingManager = {
  // State
  overlay: null,
  activeButtons: new WeakMap(),
  guardedButtons: new WeakMap(),
  submitGuardWired: false,
  activeContainers: new Map(),
  defaultTimeout: 30000, // 30 seconds max loading time
  timeoutId: null,
  cancelCallback: null,
  bodyScrollLocked: false,

  /**
   * Initialize the loading manager
   */
  init() {
    // Layouts without _PageLoadingOverlay (portal, standalone pages) still get button
    // states and the submit guard; the overlay methods do nothing there.
    this.overlay = document.getElementById('pageLoadingOverlay');

    // Set up cancel button if it exists
    const cancelBtn = document.getElementById('pageLoadingOverlayCancelBtn');
    if (cancelBtn) {
      cancelBtn.addEventListener('click', () => {
        if (this.cancelCallback && typeof this.cancelCallback === 'function') {
          this.cancelCallback();
        }
        this.hidePageLoading();
      });
    }
  },

  /**
   * Show page loading overlay
   * @param {string} message - Loading message
   * @param {object} options - Optional configuration
   * @param {string} options.subMessage - Optional sub-message
   * @param {boolean} options.showCancel - Show cancel button
   * @param {function} options.cancelCallback - Cancel button callback
   * @param {number} options.timeout - Auto-hide timeout in ms (default: 30000)
   */
  showPageLoading(message = 'Loading...', options = {}) {
    if (!this.overlay) {
      this.init();
    }
    if (!this.overlay) {
      return;
    }

    const {
      subMessage = null,
      showCancel = false,
      cancelCallback = null,
      timeout = this.defaultTimeout
    } = options;

    // Update messages if provided
    const messageEl = this.overlay.querySelector('p.text-sm');
    if (messageEl && message) {
      messageEl.textContent = message;
    }

    const subMessageEl = this.overlay.querySelector('p.text-xs');
    if (subMessageEl) {
      if (subMessage) {
        subMessageEl.textContent = subMessage;
        subMessageEl.classList.remove('hidden');
      } else {
        subMessageEl.classList.add('hidden');
      }
    }

    // Handle cancel button
    const cancelBtn = document.getElementById('pageLoadingOverlayCancelBtn');
    if (cancelBtn) {
      if (showCancel) {
        cancelBtn.classList.remove('hidden');
        this.cancelCallback = cancelCallback;
      } else {
        cancelBtn.classList.add('hidden');
      }
    }

    // Show overlay
    this.overlay.classList.remove('hidden');
    this.overlay.classList.add('active');
    this.overlay.setAttribute('aria-busy', 'true');

    // Lock body scroll
    this.lockBodyScroll();

    // Set timeout protection
    if (timeout > 0) {
      this.timeoutId = setTimeout(() => {
        console.warn('Loading timeout reached, auto-hiding overlay');
        this.hidePageLoading();
      }, timeout);
    }
  },

  /**
   * Hide page loading overlay
   */
  hidePageLoading() {
    if (!this.overlay) {
      return;
    }

    // Clear timeout
    if (this.timeoutId) {
      clearTimeout(this.timeoutId);
      this.timeoutId = null;
    }

    // Reset cancel callback
    this.cancelCallback = null;

    // Hide overlay
    this.overlay.classList.remove('active');
    this.overlay.setAttribute('aria-busy', 'false');

    // Wait for transition before hiding
    setTimeout(() => {
      this.overlay.classList.add('hidden');
    }, 200);

    // Unlock body scroll
    this.unlockBodyScroll();
  },

  /**
   * Set button loading state. The button is disabled and marked aria-busy; a spinner takes
   * the place of its icon (or goes before the text when it has none) and its label stays,
   * unless loadingText is given.
   * @param {HTMLElement|string} buttonOrId - Button element or ID
   * @param {boolean} isLoading - Loading state
   * @param {string|null} loadingText - Optional text to show during loading
   */
  setButtonLoading(buttonOrId, isLoading, loadingText = null) {
    const button = typeof buttonOrId === 'string'
      ? document.getElementById(buttonOrId)
      : buttonOrId;

    if (!button) {
      return;
    }

    if (isLoading) {
      if (this.activeButtons.has(button)) {
        return;
      }

      this.activeButtons.set(button, {
        html: button.innerHTML,
        value: button.value,
        disabled: button.disabled,
        ariaDisabled: button.getAttribute('aria-disabled')
      });

      button.disabled = true;
      button.setAttribute('aria-disabled', 'true');
      button.setAttribute('aria-busy', 'true');

      if (button.tagName === 'INPUT') {
        if (loadingText) button.value = loadingText;
        return;
      }

      const spinner = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
      spinner.setAttribute('class', 'animate-spin w-4 h-4 flex-shrink-0 lm-spinner');
      spinner.setAttribute('fill', 'none');
      spinner.setAttribute('viewBox', '0 0 24 24');
      spinner.setAttribute('aria-hidden', 'true');
      spinner.innerHTML = '<circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>' +
        '<path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>';

      // Swap the leading icon for the spinner so the button keeps its size and label
      const icon = button.querySelector('svg');
      if (icon) {
        icon.style.display = 'none';
        icon.insertAdjacentElement('beforebegin', spinner);
      } else {
        button.insertBefore(spinner, button.firstChild);
        if (!window.getComputedStyle(button).display.includes('flex')) {
          spinner.style.display = 'inline-block';
          spinner.style.marginRight = '0.5rem';
          spinner.style.verticalAlign = '-0.125em';
        }
      }

      if (loadingText) {
        this.setButtonLabel(button, loadingText);
      }
    } else {
      const originalState = this.activeButtons.get(button);
      if (originalState) {
        if (button.tagName === 'INPUT') {
          button.value = originalState.value;
        } else {
          button.innerHTML = originalState.html;
        }
        button.disabled = originalState.disabled;

        if (originalState.ariaDisabled !== null) {
          button.setAttribute('aria-disabled', originalState.ariaDisabled);
        } else {
          button.removeAttribute('aria-disabled');
        }

        button.removeAttribute('aria-busy');
        this.activeButtons.delete(button);
      }
    }
  },

  /**
   * Replace a button's visible label: its last non-empty text node, so icons and wrapper
   * spans stay where they are. Plain text, never HTML.
   * @private
   */
  setButtonLabel(button, text) {
    const walker = document.createTreeWalker(button, NodeFilter.SHOW_TEXT);
    let last = null;
    while (walker.nextNode()) {
      if (walker.currentNode.nodeValue.trim()) last = walker.currentNode;
    }
    if (last) {
      last.nodeValue = text;
    } else {
      button.appendChild(document.createTextNode(text));
    }
  },

  /**
   * Wire the global double-submit guard for forms marked data-submit-guard.
   * @private
   */
  initSubmitGuard() {
    if (this.submitGuardWired) return;
    this.submitGuardWired = true;

    document.addEventListener('submit', (e) => {
      const form = e.target;
      if (!(form instanceof HTMLFormElement) || !form.hasAttribute('data-submit-guard')) return;

      if (form.dataset.submitting === 'true') {
        e.preventDefault();
        return;
      }

      const submitter = e.submitter || form.querySelector('[type="submit"]');

      // Wait a tick: the browser has then built the form data (a button disabled now would
      // drop its name/value), and validation handlers have had their chance to cancel.
      setTimeout(() => {
        if (e.defaultPrevented) return;
        this.guardForm(form, submitter);
      }, 0);
    });

    // Back/forward cache restores the page as it was left: busy. Undo that.
    window.addEventListener('pageshow', (e) => {
      if (e.persisted) this.releaseAllForms();
    });
  },

  /**
   * Put a form in its submitting state.
   * @private
   */
  guardForm(form, submitter) {
    form.dataset.submitting = 'true';
    form.setAttribute('aria-busy', 'true');
    if (submitter && (submitter.tagName === 'BUTTON' || submitter.tagName === 'INPUT')) {
      this.setButtonLoading(submitter, true, submitter.dataset.loadingText || null);
      this.guardedButtons.set(form, submitter);
    }

    if (form.getAttribute('data-submit-guard') === 'download' || form.target === '_blank') {
      setTimeout(() => this.releaseForm(form), 4000);
    }
  },

  /**
   * Restore a guarded form.
   * @private
   */
  releaseForm(form) {
    delete form.dataset.submitting;
    form.removeAttribute('aria-busy');
    const button = this.guardedButtons.get(form);
    if (button) {
      this.setButtonLoading(button, false);
      this.guardedButtons.delete(form);
    }
  },

  /** @private */
  releaseAllForms() {
    document.querySelectorAll('form[data-submit-guard]').forEach(form => this.releaseForm(form));
  },

  /**
   * Show container loading overlay
   * @param {string} containerId - Container element ID
   * @param {string|null} message - Optional loading message
   */
  showContainerLoading(containerId, message = null) {
    const container = document.getElementById(containerId);
    if (!container) {
      console.error('Container not found:', containerId);
      return;
    }

    // Ensure container has relative positioning
    if (!container.classList.contains('loading-container')) {
      container.classList.add('loading-container');
    }

    // Create or get existing overlay
    let overlay = container.querySelector('.loading-container-overlay');
    if (!overlay) {
      overlay = document.createElement('div');
      overlay.className = 'loading-container-overlay';
      overlay.setAttribute('role', 'alert');
      overlay.setAttribute('aria-live', 'polite');
      overlay.setAttribute('aria-busy', 'true');

      // Add spinner
      const spinner = `
        <div class="w-10 h-10 border-[3px] border-white/20 border-t-white rounded-full animate-spin"></div>
      `;
      overlay.innerHTML = spinner;

      // Add message if provided
      if (message) {
        const messageEl = document.createElement('p');
        messageEl.className = 'text-sm text-white';
        messageEl.textContent = message;
        overlay.appendChild(messageEl);
      }

      container.appendChild(overlay);
    }

    // Show overlay
    overlay.classList.add('active');
    this.activeContainers.set(containerId, overlay);

    // Disable interactions in container
    this.disableInteractions(container);
  },

  /**
   * Hide container loading overlay
   * @param {string} containerId - Container element ID
   */
  hideContainerLoading(containerId) {
    const overlay = this.activeContainers.get(containerId);
    if (!overlay) {
      return;
    }

    // Hide overlay
    overlay.classList.remove('active');
    overlay.setAttribute('aria-busy', 'false');

    // Wait for transition before removing
    setTimeout(() => {
      overlay.remove();
      this.activeContainers.delete(containerId);
    }, 200);

    // Re-enable interactions
    const container = document.getElementById(containerId);
    if (container) {
      this.enableInteractions(container);
    }
  },

  /**
   * Show skeleton loader and hide content
   * @param {string} containerId - Container element ID
   */
  showSkeleton(containerId) {
    const container = document.getElementById(containerId);
    if (!container) {
      console.error('Container not found:', containerId);
      return;
    }

    // Find skeleton and content elements
    const skeleton = container.querySelector('[data-skeleton]');
    const content = container.querySelector('[data-content]');

    if (skeleton && content) {
      skeleton.classList.remove('hidden');
      content.classList.add('hidden');
    } else {
      console.warn('Container must have [data-skeleton] and [data-content] elements');
    }
  },

  /**
   * Hide skeleton loader and show content
   * @param {string} containerId - Container element ID
   */
  hideSkeleton(containerId) {
    const container = document.getElementById(containerId);
    if (!container) {
      console.error('Container not found:', containerId);
      return;
    }

    // Find skeleton and content elements
    const skeleton = container.querySelector('[data-skeleton]');
    const content = container.querySelector('[data-content]');

    if (skeleton && content) {
      skeleton.classList.add('hidden');
      content.classList.remove('hidden');
    } else {
      console.warn('Container must have [data-skeleton] and [data-content] elements');
    }
  },

  /**
   * Handle form submission with loading state
   * @param {HTMLFormElement|string} formOrId - Form element or ID
   * @param {object} options - Optional configuration
   * @param {string} options.buttonSelector - Submit button selector (default: '[type="submit"]')
   * @param {string} options.loadingText - Loading button text
   * @param {function} options.onSuccess - Success callback
   * @param {function} options.onError - Error callback
   */
  handleFormSubmit(formOrId, options = {}) {
    const form = typeof formOrId === 'string'
      ? document.getElementById(formOrId)
      : formOrId;

    if (!form) {
      console.error('Form not found:', formOrId);
      return;
    }

    const {
      buttonSelector = '[type="submit"]',
      loadingText = 'Submitting...',
      onSuccess = null,
      onError = null
    } = options;

    form.addEventListener('submit', async (e) => {
      const submitBtn = form.querySelector(buttonSelector);

      if (submitBtn) {
        this.setButtonLoading(submitBtn, true, loadingText);
      }

      // If onSuccess or onError are provided, prevent default and handle manually
      if (onSuccess || onError) {
        e.preventDefault();

        try {
          const formData = new FormData(form);
          const response = await fetch(form.action, {
            method: form.method || 'POST',
            body: formData
          });

          if (response.ok) {
            if (onSuccess) {
              await onSuccess(response);
            }
          } else {
            if (onError) {
              await onError(response);
            }
          }
        } catch (error) {
          if (onError) {
            await onError(error);
          }
          console.error('Form submission error:', error);
        } finally {
          if (submitBtn) {
            this.setButtonLoading(submitBtn, false);
          }
        }
      }
    });
  },

  /**
   * Disable all interactions in a container
   * @param {HTMLElement} container - Container element
   * @private
   */
  disableInteractions(container) {
    const interactiveElements = container.querySelectorAll('button, a, input, select, textarea');
    interactiveElements.forEach(el => {
      if (el.classList.contains('loading-container-overlay')) {
        return; // Skip the overlay itself
      }
      el.setAttribute('data-was-disabled', el.disabled || el.getAttribute('aria-disabled') || 'false');
      el.disabled = true;
      el.setAttribute('aria-disabled', 'true');
    });
  },

  /**
   * Enable all interactions in a container
   * @param {HTMLElement} container - Container element
   * @private
   */
  enableInteractions(container) {
    const interactiveElements = container.querySelectorAll('[data-was-disabled]');
    interactiveElements.forEach(el => {
      const wasDisabled = el.getAttribute('data-was-disabled') === 'true';
      el.disabled = wasDisabled;

      if (wasDisabled) {
        el.setAttribute('aria-disabled', 'true');
      } else {
        el.removeAttribute('aria-disabled');
      }

      el.removeAttribute('data-was-disabled');
    });
  },

  /**
   * Lock body scroll
   * @private
   */
  lockBodyScroll() {
    if (!this.bodyScrollLocked) {
      document.body.style.overflow = 'hidden';
      this.bodyScrollLocked = true;
    }
  },

  /**
   * Unlock body scroll
   * @private
   */
  unlockBodyScroll() {
    if (this.bodyScrollLocked) {
      document.body.style.overflow = '';
      this.bodyScrollLocked = false;
    }
  }
};

// The submit guard listens on document, so it can be wired before the DOM is ready
LoadingManager.initSubmitGuard();

// Auto-initialize on DOM ready
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', () => LoadingManager.init());
} else {
  LoadingManager.init();
}

// Export for use in other scripts
if (typeof module !== 'undefined' && module.exports) {
  module.exports = LoadingManager;
}
