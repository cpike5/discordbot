/**
 * Toast Notification Manager
 * Provides toast notifications with auto-dismiss, hover-to-pause, and progress bar.
 */
const ToastManager = {
  container: null,
  toasts: [],
  maxToasts: 5,

  // Toast icon SVG templates
  icons: {
    info: '<svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>',
    success: '<svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>',
    warning: '<svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" /></svg>',
    error: '<svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 14l2-2m0 0l2-2m-2 2l-2-2m2 2l2 2m7-2a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>'
  },

  /**
   * Initialize the toast manager
   */
  init() {
    this.container = document.getElementById('toastContainer');
    if (!this.container) {
      console.error('Toast container not found. Add _ToastContainer.cshtml to your page.');
    }
  },

  /**
   * Show a toast notification
   * @param {string} type - Toast type: 'success', 'error', 'warning', 'info'
   * @param {string} message - The message to display
   * @param {object} options - Optional configuration
   * @param {string} options.title - Optional title
   * @param {number} options.duration - Auto-dismiss duration in ms (default: 5000)
   */
  show(type, message, options = {}) {
    if (!this.container) {
      this.init();
    }

    const {
      title = null,
      duration = 5000
    } = options;

    // Enforce max toasts limit
    while (this.toasts.length >= this.maxToasts) {
      const oldestToast = this.toasts.shift();
      clearTimeout(oldestToast.timeoutId);
      oldestToast.element.remove();
    }

    // Create and add new toast
    const toastData = this.createToast(type, message, title, duration);
    this.toasts.push(toastData);

    // Insert at beginning (newest on top)
    this.container.insertBefore(toastData.element, this.container.firstChild);

    // Announce to screen readers via live region
    this.announceToScreenReader(type, message, title);
  },

  /**
   * Announce toast message to screen readers
   * @private
   */
  announceToScreenReader(type, message, title) {
    const liveRegion = document.getElementById('toastLiveRegion');
    if (liveRegion) {
      const typeLabels = {
        success: 'Success',
        error: 'Error',
        warning: 'Warning',
        info: 'Information'
      };
      const announcement = title
        ? `${typeLabels[type]}: ${title}. ${message}`
        : `${typeLabels[type]}: ${message}`;
      liveRegion.textContent = announcement;

      // Clear after a short delay to allow for repeated announcements
      setTimeout(() => {
        liveRegion.textContent = '';
      }, 1000);
    }
  },

  /**
   * Create a toast element
   * @private
   */
  createToast(type, message, title, duration) {
    const toast = document.createElement('div');
    toast.className = `toast toast-${type}`;
    toast.setAttribute('role', 'alert');
    toast.setAttribute('aria-live', 'polite');

    // Build content HTML
    let contentHTML = '';
    if (title) {
      contentHTML = `
        <p class="text-sm font-semibold text-text-primary">${this.escapeHtml(title)}</p>
        <p class="text-sm text-text-secondary mt-0.5">${this.escapeHtml(message)}</p>
      `;
    } else {
      contentHTML = `<p class="text-sm text-text-primary">${this.escapeHtml(message)}</p>`;
    }

    toast.innerHTML = `
      <div class="toast-icon flex-shrink-0 mt-0.5">
        ${this.icons[type]}
      </div>
      <div class="flex-1 min-w-0">
        ${contentHTML}
      </div>
      <button class="toast-close" aria-label="Dismiss notification">
        <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
        </svg>
      </button>
      <div class="toast-progress animating" style="animation-duration: ${duration}ms;"></div>
    `;

    // Store timer reference
    const toastData = {
      element: toast,
      timeoutId: null,
      remainingTime: duration,
      startTime: Date.now()
    };

    // Set up auto-dismiss
    toastData.timeoutId = setTimeout(() => {
      this.dismissToast(toastData);
    }, duration);

    // Pause on hover
    toast.addEventListener('mouseenter', () => {
      const elapsed = Date.now() - toastData.startTime;
      toastData.remainingTime = Math.max(0, toastData.remainingTime - elapsed);

      clearTimeout(toastData.timeoutId);

      const progressBar = toast.querySelector('.toast-progress');
      if (progressBar) {
        progressBar.classList.add('paused');
      }
    });

    toast.addEventListener('mouseleave', () => {
      toastData.startTime = Date.now();
      toastData.timeoutId = setTimeout(() => {
        this.dismissToast(toastData);
      }, toastData.remainingTime);

      const progressBar = toast.querySelector('.toast-progress');
      if (progressBar) {
        progressBar.classList.remove('paused');
      }
    });

    // Close button handler
    const closeBtn = toast.querySelector('.toast-close');
    closeBtn.addEventListener('click', () => {
      clearTimeout(toastData.timeoutId);
      this.dismissToast(toastData);
    });

    return toastData;
  },

  /**
   * Dismiss a toast
   * @private
   */
  dismissToast(toastData) {
    const toast = toastData.element;

    // Add dismissing animation class
    toast.classList.add('dismissing');

    // Remove after animation (200ms)
    setTimeout(() => {
      toast.remove();

      // Remove from toasts array
      const index = this.toasts.indexOf(toastData);
      if (index > -1) {
        this.toasts.splice(index, 1);
      }
    }, 200);
  },

  /**
   * Clear all toasts
   */
  clearAll() {
    const toastsCopy = [...this.toasts];

    toastsCopy.forEach(toastData => {
      clearTimeout(toastData.timeoutId);
      this.dismissToast(toastData);
    });
  },

  /**
   * Escape HTML to prevent XSS
   * @private
   */
  escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
  }
};

// Auto-initialize on DOM ready
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', () => ToastManager.init());
} else {
  ToastManager.init();
}

// Export for use in other scripts
if (typeof module !== 'undefined' && module.exports) {
  module.exports = ToastManager;
}
