// Quick Actions Module
// Handles confirmation modals, AJAX form submissions, and toast notifications

(function () {
  'use strict';

  // Track the element that triggered the modal for focus return
  let triggerElement = null;
  let focusableElements = [];
  let firstFocusableElement = null;
  let lastFocusableElement = null;

  /**
   * Shows a confirmation modal by ID
   * @param {string} modalId - The ID of the modal to show
   */
  function showConfirmationModal(modalId) {
    const modal = document.getElementById(modalId);
    if (!modal) {
      console.error(`Modal with ID "${modalId}" not found`);
      return;
    }

    // Store trigger element for focus return
    triggerElement = document.activeElement;

    // Show modal
    modal.classList.remove('hidden');

    // Setup focus trap
    setupFocusTrap(modal);

    // Focus the first focusable element
    requestAnimationFrame(() => {
      if (firstFocusableElement) {
        firstFocusableElement.focus();
      }
    });

    // Add escape key listener
    document.addEventListener('keydown', handleEscapeKey);
  }

  /**
   * Hides a confirmation modal by ID
   * @param {string} modalId - The ID of the modal to hide
   */
  function hideConfirmationModal(modalId) {
    const modal = document.getElementById(modalId);
    if (!modal) {
      console.error(`Modal with ID "${modalId}" not found`);
      return;
    }

    // Hide modal
    modal.classList.add('hidden');

    // Remove escape key listener
    document.removeEventListener('keydown', handleEscapeKey);

    // Return focus to trigger element
    if (triggerElement) {
      triggerElement.focus();
      triggerElement = null;
    }
  }

  /**
   * Setup focus trap within modal
   * @param {HTMLElement} modal - The modal element
   */
  function setupFocusTrap(modal) {
    focusableElements = modal.querySelectorAll(
      'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
    );

    if (focusableElements.length > 0) {
      firstFocusableElement = focusableElements[0];
      lastFocusableElement = focusableElements[focusableElements.length - 1];

      // Add tab trap
      modal.addEventListener('keydown', handleTabKey);
    }
  }

  /**
   * Handle tab key for focus trap
   * @param {KeyboardEvent} e - The keyboard event
   */
  function handleTabKey(e) {
    if (e.key !== 'Tab') return;

    if (e.shiftKey) {
      // Shift + Tab
      if (document.activeElement === firstFocusableElement) {
        e.preventDefault();
        lastFocusableElement.focus();
      }
    } else {
      // Tab
      if (document.activeElement === lastFocusableElement) {
        e.preventDefault();
        firstFocusableElement.focus();
      }
    }
  }

  /**
   * Handle escape key to close modal
   * @param {KeyboardEvent} e - The keyboard event
   */
  function handleEscapeKey(e) {
    if (e.key === 'Escape') {
      const visibleModal = document.querySelector('[role="alertdialog"]:not(.hidden)');
      if (visibleModal) {
        hideConfirmationModal(visibleModal.id);
      }
    }
  }

  /**
   * Get the anti-forgery token from the page
   * @returns {string|null} The anti-forgery token value
   */
  function getAntiForgeryToken() {
    const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
    return tokenInput ? tokenInput.value : null;
  }

  /**
   * Submit a quick action via AJAX
   * @param {string} handler - The page handler name
   * @param {HTMLElement} buttonElement - The button that was clicked
   */
  async function submitQuickAction(handler, buttonElement) {
    if (!handler) {
      console.error('No handler specified for quick action');
      return;
    }

    const token = getAntiForgeryToken();
    if (!token) {
      console.error('Anti-forgery token not found');
      showToast('Security token not found. Please refresh the page.', 'error');
      return;
    }

    // Disable button and show loading state
    const originalText = buttonElement.querySelector('span')?.textContent || '';
    const iconContainer = buttonElement.querySelector('div');
    buttonElement.disabled = true;

    if (iconContainer) {
      // Add a simple loading indicator
      const originalHTML = iconContainer.innerHTML;
      iconContainer.innerHTML = `
        <svg class="w-6 h-6 animate-spin" fill="none" viewBox="0 0 24 24">
          <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
          <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
        </svg>
      `;

      // Store original HTML for restore
      buttonElement.dataset.originalHtml = originalHTML;
    }

    try {
      const response = await fetch(`?handler=${handler}`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/x-www-form-urlencoded',
          'RequestVerificationToken': token
        },
        body: `__RequestVerificationToken=${encodeURIComponent(token)}`
      });

      const data = await response.json();

      if (response.ok && data.success) {
        showToast(data.message || 'Action completed successfully', 'success');
      } else {
        showToast(data.message || 'Action failed. Please try again.', 'error');
      }
    } catch (error) {
      console.error('Quick action error:', error);
      showToast('An error occurred. Please try again.', 'error');
    } finally {
      // Re-enable button and restore icon
      buttonElement.disabled = false;

      if (iconContainer && buttonElement.dataset.originalHtml) {
        iconContainer.innerHTML = buttonElement.dataset.originalHtml;
        delete buttonElement.dataset.originalHtml;
      }
    }
  }

  /**
   * Show a toast notification
   * @param {string} message - The message to display
   * @param {string} variant - The toast variant (success, error, warning, info)
   */
  function showToast(message, variant = 'info') {
    const container = document.getElementById('toastContainer');
    if (!container) {
      console.error('Toast container not found');
      return;
    }

    const toast = document.createElement('div');
    const variantClasses = {
      success: 'bg-success text-white',
      error: 'bg-error text-white',
      warning: 'bg-warning text-text-inverse',
      info: 'bg-accent-blue text-white'
    };

    const icons = {
      success: '<svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" /></svg>',
      error: '<svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" /></svg>',
      warning: '<svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" /></svg>',
      info: '<svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>'
    };

    toast.className = `flex items-center gap-3 px-4 py-3 rounded-lg shadow-lg transform transition-all duration-300 ${variantClasses[variant] || variantClasses.info}`;
    toast.innerHTML = `
      ${icons[variant] || icons.info}
      <span class="text-sm font-medium">${message}</span>
    `;

    // Start offscreen
    toast.style.transform = 'translateX(100%)';
    toast.style.opacity = '0';

    container.appendChild(toast);

    // Animate in
    requestAnimationFrame(() => {
      requestAnimationFrame(() => {
        toast.style.transform = 'translateX(0)';
        toast.style.opacity = '1';
      });
    });

    // Remove after 3 seconds
    setTimeout(() => {
      toast.style.transform = 'translateX(100%)';
      toast.style.opacity = '0';
      setTimeout(() => {
        toast.remove();
      }, 300);
    }, 3000);
  }

  // Expose public API
  window.quickActions = {
    showConfirmationModal,
    hideConfirmationModal,
    submitQuickAction,
    showToast
  };

  // Initialize
  console.log('Quick Actions module initialized');
})();
