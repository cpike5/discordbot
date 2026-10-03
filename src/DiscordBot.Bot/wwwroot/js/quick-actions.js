// Quick Actions Module
//
// The one modal layer for the portal. It does three jobs:
//
//   1. Dialog behaviour for any element: enter/exit motion, scroll lock, `inert` background,
//      focus trap, Escape, focus return, and stacking (quickActions.openDialog / closeDialog).
//   2. Static confirmation modals rendered by _ConfirmationModal / _TypedConfirmationModal,
//      including their AJAX form submission (quickActions.showConfirmationModal / hideConfirmationModal).
//   3. Promise-based dynamic dialogs (quickActions.confirm / alert / typedConfirm).
//
// Documented in docs/articles/component-api.md ("Modals").

(function () {
  'use strict';

  var FOCUSABLE = 'a[href], button:not([disabled]), input:not([disabled]):not([type="hidden"]), ' +
    'select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

  // How long the exit transition runs (matches .qa-modal-* in site.css)
  var EXIT_MS = 180;

  function prefersReducedMotion() {
    return typeof window.matchMedia === 'function' &&
      window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  }

  // ============================================
  // Dialog core: stack, inert, scroll lock, focus
  // ============================================

  /** Open dialogs, bottom to top. The last one owns Escape and Tab. */
  var stack = [];
  /** Elements currently made inert by an open dialog, with how many dialogs asked for it. */
  var inertCounts = new Map();
  /** Pending exit timers, so reopening a dialog that is still fading out cancels its removal. */
  var exitTimers = new WeakMap();
  var scrollLocks = 0;

  function isVisible(el) {
    return !!(el.offsetWidth || el.offsetHeight || el.getClientRects().length);
  }

  function focusableIn(root) {
    return Array.prototype.filter.call(root.querySelectorAll(FOCUSABLE), isVisible);
  }

  /**
   * Make everything outside the dialog inert: the siblings of the dialog and of each of its
   * ancestors. A static modal lives inside the page content, so ancestors matter. The toast
   * region stays live so a failed action can still be read and dismissed.
   * @returns {Element[]} the elements this call made (or kept) inert, for releaseInert
   */
  function applyInert(dialog) {
    var affected = [];
    var node = dialog;
    while (node && node !== document.body && node.parentElement) {
      var parent = node.parentElement;
      Array.prototype.forEach.call(parent.children, function (sibling) {
        if (sibling === node) return;
        if (/^(SCRIPT|STYLE|TEMPLATE|LINK)$/.test(sibling.tagName)) return;
        if (sibling.id === 'toastContainer' || sibling.hasAttribute('data-modal-keep-interactive')) return;
        var count = inertCounts.get(sibling);
        if (count === undefined) {
          if (sibling.inert) return; // the page made it inert for its own reasons
          sibling.inert = true;
          inertCounts.set(sibling, 1);
        } else {
          inertCounts.set(sibling, count + 1);
        }
        affected.push(sibling);
      });
      node = parent;
    }
    return affected;
  }

  function releaseInert(affected) {
    affected.forEach(function (el) {
      var count = inertCounts.get(el);
      if (count === undefined) return;
      if (count <= 1) {
        inertCounts.delete(el);
        // The sidebar's inert state is navigation.js's to decide (a closed mobile drawer stays inert)
        if (el.id === 'sidebar' && typeof window.syncSidebarInert === 'function') window.syncSidebarInert();
        else el.inert = false;
      } else {
        inertCounts.set(el, count - 1);
      }
    });
  }

  function lockScroll() {
    if (scrollLocks++ === 0) document.documentElement.classList.add('qa-scroll-lock');
  }

  function unlockScroll() {
    if (scrollLocks > 0 && --scrollLocks === 0) document.documentElement.classList.remove('qa-scroll-lock');
  }

  function findEntry(dialog) {
    for (var i = 0; i < stack.length; i++) {
      if (stack[i].dialog === dialog) return stack[i];
    }
    return null;
  }

  /**
   * Open any element as a modal dialog.
   *
   * The element should be hidden with the `hidden` class and carry role="dialog" or
   * role="alertdialog" with aria-modal="true". To get the motion, give the backdrop
   * `qa-modal-backdrop` and the panel `qa-modal-panel`.
   *
   * @param {HTMLElement} dialog
   * @param {Object} [options]
   * @param {string|HTMLElement} [options.initialFocus] - selector or element to focus first
   * @param {Function} [options.onClose] - called once, after the dialog starts closing
   * @param {boolean} [options.dismissOnEscape=true]
   * @returns {Object|null} the dialog's entry; pass the element to closeDialog to close it
   */
  function openDialog(dialog, options) {
    if (!dialog) return null;
    options = options || {};

    var existing = findEntry(dialog);
    if (existing) return existing;

    // A dialog that is still fading out comes back instead of being removed
    var pending = exitTimers.get(dialog);
    if (pending) {
      clearTimeout(pending);
      exitTimers.delete(dialog);
    }

    // Remember where focus was before anything becomes inert
    var active = document.activeElement;
    var trigger = active && active !== document.body && !dialog.contains(active) ? active : null;

    var entry = {
      dialog: dialog,
      trigger: trigger,
      onClose: options.onClose || null,
      dismissOnEscape: options.dismissOnEscape !== false,
      inerted: []
    };
    stack.push(entry);

    dialog.style.zIndex = 'calc(var(--z-modal) + ' + (stack.length - 1) + ')';
    dialog.classList.remove('hidden');
    entry.inerted = applyInert(dialog);
    lockScroll();

    // Reflow so the entry transition runs from the closed state
    void dialog.offsetWidth;
    dialog.classList.add('qa-open');

    var target = null;
    if (options.initialFocus) {
      target = typeof options.initialFocus === 'string'
        ? dialog.querySelector(options.initialFocus)
        : options.initialFocus;
    }
    if (!target) target = dialog.querySelector('[data-modal-initial-focus]');
    if (!target) target = focusableIn(dialog)[0];
    if (target && typeof target.focus === 'function') target.focus();

    return entry;
  }

  /**
   * Close a dialog opened with openDialog. Safe to call twice.
   * @param {HTMLElement} dialog
   * @returns {boolean} true when it was open
   */
  function closeDialog(dialog) {
    var entry = findEntry(dialog);
    if (!entry) return false;

    stack.splice(stack.indexOf(entry), 1);

    // Lift inert before moving focus: an element inside an inert subtree cannot take it
    releaseInert(entry.inerted);
    unlockScroll();

    dialog.classList.remove('qa-open');

    var trigger = entry.trigger;
    if (trigger && document.contains(trigger) && typeof trigger.focus === 'function') {
      trigger.focus();
    } else if (stack.length > 0) {
      var below = focusableIn(stack[stack.length - 1].dialog)[0];
      if (below) below.focus();
    }

    function finish() {
      exitTimers.delete(dialog);
      dialog.classList.add('hidden');
      dialog.style.zIndex = '';
    }
    if (prefersReducedMotion()) {
      finish();
    } else {
      exitTimers.set(dialog, setTimeout(finish, EXIT_MS));
    }

    if (entry.onClose) {
      var onClose = entry.onClose;
      entry.onClose = null;
      onClose();
    }
    return true;
  }

  // One keyboard handler for every dialog. Capture phase, so Escape closes the dialog
  // before anything underneath (a dropdown, the page) sees it.
  document.addEventListener('keydown', function (e) {
    if (stack.length === 0) return;
    var entry = stack[stack.length - 1];
    var dialog = entry.dialog;

    if (e.key === 'Escape') {
      if (!entry.dismissOnEscape || dialog.getAttribute('aria-busy') === 'true') return;
      e.preventDefault();
      e.stopPropagation();
      closeDialog(dialog);
      return;
    }

    if (e.key === 'Tab') {
      var focusable = focusableIn(dialog);
      if (focusable.length === 0) {
        e.preventDefault();
        return;
      }
      var first = focusable[0];
      var last = focusable[focusable.length - 1];
      var current = document.activeElement;
      if (!dialog.contains(current)) {
        e.preventDefault();
        first.focus();
      } else if (e.shiftKey && current === first) {
        e.preventDefault();
        last.focus();
      } else if (!e.shiftKey && current === last) {
        e.preventDefault();
        first.focus();
      }
    }
  }, true);

  // ============================================
  // Static confirmation modals
  // ============================================

  function setBusy(modal, busy) {
    var form = modal.querySelector('form');
    var button = form && form.querySelector('button[type="submit"]');
    var text = button && button.querySelector('.confirm-btn-text');
    var spinner = button && button.querySelector('.confirm-btn-spinner');
    if (busy) {
      modal.setAttribute('aria-busy', 'true');
      if (button) {
        button.disabled = true;
        button.setAttribute('aria-disabled', 'true');
      }
      if (text) text.classList.add('hidden');
      if (spinner) spinner.classList.remove('hidden');
    } else {
      modal.removeAttribute('aria-busy');
      if (button) {
        button.removeAttribute('aria-disabled');
        // A typed modal stays disabled until the phrase matches again
        var typed = modal.querySelector('[data-typed-input]');
        button.disabled = typed ? typed.value !== typed.dataset.requiredText : false;
      }
      if (text) text.classList.remove('hidden');
      if (spinner) spinner.classList.add('hidden');
    }
  }

  /** Clear a typed modal's input and lock its confirm button. */
  function resetTypedInput(modal) {
    var input = modal.querySelector('[data-typed-input]');
    if (!input) return;
    input.value = '';
    var button = document.getElementById(input.dataset.confirmBtn);
    if (button) button.disabled = true;
  }

  /**
   * Shows a static confirmation modal by ID
   * @param {string} modalId - The ID of the modal to show
   */
  function showConfirmationModal(modalId) {
    var modal = document.getElementById(modalId);
    if (!modal) {
      console.error('Modal with ID "' + modalId + '" not found');
      return;
    }
    resetTypedInput(modal);
    setBusy(modal, false);
    openDialog(modal, {
      initialFocus: '[data-typed-input]',
      onClose: function () {
        resetTypedInput(modal);
        setBusy(modal, false);
      }
    });
  }

  /**
   * Hides a static confirmation modal by ID
   * @param {string} modalId - The ID of the modal to hide
   */
  function hideConfirmationModal(modalId) {
    var modal = document.getElementById(modalId);
    if (!modal) {
      console.error('Modal with ID "' + modalId + '" not found');
      return;
    }
    closeDialog(modal);
  }

  // Dismiss controls (Cancel buttons, the backdrop) are plain data attributes, so no handler
  // text is ever written into markup.
  document.addEventListener('click', function (e) {
    var control = e.target.closest && e.target.closest('[data-modal-dismiss]');
    if (!control) return;
    var dialog = control.closest('[role="alertdialog"], [role="dialog"]');
    if (!dialog || dialog.getAttribute('aria-busy') === 'true') return;
    closeDialog(dialog);
  });

  // Typed confirmation: the confirm button unlocks when the input matches the required phrase
  document.addEventListener('input', function (e) {
    var input = e.target;
    if (!input.matches || !input.matches('[data-typed-input]')) return;
    var button = document.getElementById(input.dataset.confirmBtn);
    if (button) button.disabled = input.value !== input.dataset.requiredText;
  });

  /**
   * The address a confirmation form posts to: its own action, with the handler in the query
   * string (Razor Pages reads the handler from there, not from the body).
   */
  function resolveFormUrl(form) {
    var url = new URL(form.getAttribute('action') || window.location.href, window.location.href);
    var handler = form.querySelector('input[name="handler"]');
    if (handler && handler.value && !url.searchParams.has('handler')) {
      url.searchParams.set('handler', handler.value);
    }
    return url.pathname + url.search;
  }

  /**
   * Submit a confirmation form without leaving the page.
   *
   * - A JSON answer `{ success, message }` shows a toast and closes the modal.
   * - A redirect answer means the handler finished and wants the page drawn again (TempData
   *   toasts and one-time values travel with it). The redirect is NOT followed, because
   *   following it would run the target's GET and spend that TempData; the page is loaded
   *   once, by the browser, instead.
   * - A form that opts out with data-custom-submit (its page script handles the submit) or
   *   data-submit-mode="navigate" is not touched here.
   *
   * Registered once on the document, in the bubble phase, so a page script's own submit
   * handler (which calls preventDefault) runs first and this one stands down.
   */
  async function handleConfirmationSubmit(e) {
    var form = e.target;
    if (e.defaultPrevented || !form.closest) return;
    var modal = form.closest('[data-confirm-modal]');
    if (!modal) return;
    if (form.hasAttribute('data-custom-submit')) return;

    if (form.dataset.submitMode === 'navigate') {
      setBusy(modal, true);
      return;
    }

    e.preventDefault();
    if (modal.getAttribute('aria-busy') === 'true') return; // one request per confirmation

    if (!window.ApiClient) {
      form.submit();
      return;
    }

    setBusy(modal, true);
    try {
      var tokenInput = form.querySelector('input[name="__RequestVerificationToken"]');
      var result = await window.ApiClient.requestRaw(resolveFormUrl(form), {
        method: 'POST',
        body: new FormData(form),
        token: false,
        headers: tokenInput ? { RequestVerificationToken: tokenInput.value } : {},
        redirect: 'manual'
      });

      if (result.redirected) {
        // Leave the modal busy while the page loads. assign, not reload: after a failed
        // save the history entry can be a POST, which reload would offer to resubmit. The
        // fragment is dropped, because assigning the same URL plus a #hash would not reload.
        window.location.assign(window.location.pathname + window.location.search);
        return;
      }

      var data = result.data && typeof result.data === 'object' ? result.data : null;
      if (result.ok && (!data || data.success !== false)) {
        showToast((data && data.message) || 'Done.', 'success');
        closeDialog(modal);
        modal.dispatchEvent(new CustomEvent('quickactions:confirmed', {
          bubbles: true,
          detail: { modalId: modal.id, form: form, data: data }
        }));
        return;
      }

      if (!result.sessionExpired) {
        var fallback = window.ApiClient.statusMessage(result.status);
        showToast(window.ApiClient.extractErrorMessage(result.data, fallback, result.status), 'error');
      }
      setBusy(modal, false);
    } catch (error) {
      showToast(error && error.message ? error.message : 'Something went wrong. Try again.', 'error');
      setBusy(modal, false);
    }
  }
  document.addEventListener('submit', handleConfirmationSubmit);

  /**
   * Get the anti-forgery token from the page
   * @returns {string|null} The anti-forgery token value
   */
  function getAntiForgeryToken() {
    var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
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

    var token = getAntiForgeryToken();
    if (!token) {
      console.error('Anti-forgery token not found');
      showToast('Security token not found. Please refresh the page.', 'error');
      return;
    }

    // Use LoadingManager if available, otherwise fallback to manual loading
    var useLoadingManager = typeof LoadingManager !== 'undefined';
    var iconContainer = buttonElement.querySelector('div');

    if (useLoadingManager) {
      LoadingManager.setButtonLoading(buttonElement, true);
    } else {
      // Fallback: Disable button and show loading state
      buttonElement.disabled = true;

      if (iconContainer) {
        var originalHTML = iconContainer.innerHTML;
        iconContainer.innerHTML =
          '<svg class="w-6 h-6 animate-spin" fill="none" viewBox="0 0 24 24">' +
          '<circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>' +
          '<path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>' +
          '</svg>';
        buttonElement.dataset.originalHtml = originalHTML;
      }
    }

    try {
      // Same path as the confirmation forms: the handler goes in the URL (Razor Pages reads it
      // from the query string), a redirect answer is not followed as data, and the answer is
      // never assumed to be JSON.
      var url = new URL(window.location.href);
      url.searchParams.set('handler', handler);
      var result = await window.ApiClient.requestRaw(url.pathname + url.search, {
        method: 'POST',
        token: false,
        headers: { RequestVerificationToken: token },
        redirect: 'manual'
      });

      if (result.redirected) {
        // The handler finished and wants the page drawn again (TempData toasts travel with it).
        // assign, not reload: after a failed save the history entry can be a POST.
        window.location.assign(window.location.pathname + window.location.search);
        return;
      }

      var data = result.data && typeof result.data === 'object' ? result.data : null;
      if (result.ok && (!data || data.success !== false)) {
        showToast((data && data.message) || 'Action completed successfully', 'success');
      } else if (!result.sessionExpired) {
        var fallback = window.ApiClient.statusMessage(result.status);
        showToast(window.ApiClient.extractErrorMessage(result.data, fallback, result.status), 'error');
      }
    } catch (error) {
      console.error('Quick action error:', error);
      showToast(error && error.message ? error.message : 'Something went wrong. Try again.', 'error');
    } finally {
      // Reset button state
      if (useLoadingManager) {
        LoadingManager.setButtonLoading(buttonElement, false);
      } else {
        buttonElement.disabled = false;
        if (iconContainer && buttonElement.dataset.originalHtml) {
          iconContainer.innerHTML = buttonElement.dataset.originalHtml;
          delete buttonElement.dataset.originalHtml;
        }
      }
    }
  }

  /**
   * Show a toast notification (delegates to the shared toast API)
   * @param {string} message - The message to display
   * @param {string} variant - The toast variant (success, error, warning, info)
   */
  function showToast(message, variant) {
    variant = variant || 'info';
    if (window.toast && typeof window.toast[variant] === 'function') {
      window.toast[variant](message);
    } else if (window.ToastManager) {
      window.ToastManager.show(variant, message);
    }
  }

  // ============================================
  // Promise-based Dynamic Modal API
  // ============================================

  var modalCounter = 0;

  // Button classes are component classes (site.css), so nothing here needs safelisting.
  var variantConfig = {
    info: {
      color: 'accent-blue',
      btnClass: 'btn btn-accent',
      iconPath: 'M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z'
    },
    warning: {
      color: 'warning',
      btnClass: 'btn bg-warning hover:bg-warning-hover active:bg-warning-active text-on-warning border-transparent',
      iconPath: 'M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z'
    },
    danger: {
      color: 'error',
      btnClass: 'btn btn-danger',
      iconPath: 'M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z'
    }
  };

  /**
   * Escape HTML for text placed in dynamic modal markup (quote-safe)
   * @param {string} str - The string to escape
   * @returns {string} Escaped string
   */
  function escapeHtml(str) {
    if (window.SafeHtml && typeof window.SafeHtml.escape === 'function') {
      return window.SafeHtml.escape(str);
    }
    var div = document.createElement('div');
    div.textContent = str;
    return div.innerHTML.replace(/"/g, '&quot;').replace(/'/g, '&#39;');
  }

  function generateModalId() {
    return 'quickActions-modal-' + (++modalCounter) + '-' + Date.now();
  }

  function buildIconHtml(config) {
    return '<div class="flex-shrink-0 w-10 h-10 rounded-full bg-' + config.color + '/20 flex items-center justify-center">' +
      '<svg class="w-5 h-5 text-' + config.color + '" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">' +
      '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="' + config.iconPath + '" />' +
      '</svg></div>';
  }

  /**
   * Build a dynamic dialog element. Escaped text only; buttons are looked up by data attribute
   * afterwards, so nothing the caller passes becomes script.
   */
  function buildDialog(id, config, title, message, extraBody, footerHtml) {
    var modal = document.createElement('div');
    modal.id = id;
    modal.className = 'hidden fixed inset-0 z-[var(--z-modal)]';
    modal.setAttribute('role', 'alertdialog');
    modal.setAttribute('aria-modal', 'true');
    modal.setAttribute('aria-labelledby', id + '-title');
    modal.setAttribute('aria-describedby', id + '-desc');

    modal.innerHTML =
      '<div class="fixed inset-0 bg-black/70 backdrop-blur-sm qa-modal-backdrop" data-modal-dismiss aria-hidden="true"></div>' +
      '<div class="fixed inset-0 flex items-center justify-center p-4 pointer-events-none">' +
        '<div class="bg-bg-tertiary border border-border-primary rounded-lg shadow-xl max-w-md w-full qa-modal-panel pointer-events-auto" role="document">' +
          '<div class="p-6">' +
            '<div class="flex items-start gap-4">' +
              buildIconHtml(config) +
              '<div class="flex-1 min-w-0">' +
                '<h3 id="' + id + '-title" class="text-lg font-semibold text-text-primary">' + escapeHtml(title) + '</h3>' +
                '<p id="' + id + '-desc" class="mt-2 text-sm text-text-secondary">' + escapeHtml(message) + '</p>' +
                extraBody +
              '</div>' +
            '</div>' +
          '</div>' +
          '<div class="flex justify-end gap-3 px-6 py-4 bg-bg-secondary border-t border-border-primary rounded-b-lg">' +
            footerHtml +
          '</div>' +
        '</div>' +
      '</div>';

    document.body.appendChild(modal);
    return modal;
  }

  /**
   * Open a dynamic dialog and wire its single close path. The Promise resolves once, even if
   * close is called twice (Escape and a click in the same frame).
   */
  function presentDynamic(modal, resolve, focusSelector) {
    var settled = false;
    function close(result) {
      if (settled) return;
      settled = true;
      closeDialog(modal);
      resolve(result);
    }
    openDialog(modal, {
      initialFocus: focusSelector,
      onClose: function () {
        // Escape and backdrop clicks close through closeDialog directly
        if (!settled) {
          settled = true;
          resolve(false);
        }
        // Remove after the exit transition
        setTimeout(function () { modal.remove(); }, prefersReducedMotion() ? 0 : EXIT_MS + 20);
      }
    });
    return close;
  }

  /**
   * Confirmation dialog - returns Promise<boolean>
   * @param {Object} options - Dialog options
   * @param {string} options.title - Dialog title
   * @param {string} options.message - Dialog message
   * @param {string} [options.variant='warning'] - 'info' | 'warning' | 'danger'
   * @param {string} [options.confirmText='Confirm'] - Confirm button text
   * @param {string} [options.cancelText='Cancel'] - Cancel button text
   * @returns {Promise<boolean>} true if confirmed, false if cancelled
   */
  function confirmDialog(options) {
    var o = options || {};
    var title = o.title || 'Confirm';
    var message = o.message || 'Are you sure?';
    var config = variantConfig[o.variant] || variantConfig.warning;
    var confirmText = o.confirmText || 'Confirm';
    var cancelText = o.cancelText || 'Cancel';

    return new Promise(function (resolve) {
      var modal = buildDialog(generateModalId(), config, title, message, '',
        '<button type="button" data-modal-cancel class="btn btn-secondary">' + escapeHtml(cancelText) + '</button>' +
        '<button type="button" data-modal-confirm class="' + config.btnClass + '">' + escapeHtml(confirmText) + '</button>');

      var close = presentDynamic(modal, resolve, '[data-modal-cancel]');
      modal.querySelector('[data-modal-cancel]').addEventListener('click', function () { close(false); });
      modal.querySelector('[data-modal-confirm]').addEventListener('click', function () { close(true); });
    });
  }

  /**
   * Alert dialog (info/error feedback) - returns Promise<void>
   * @param {Object} options - Dialog options
   * @param {string} options.title - Dialog title
   * @param {string} options.message - Dialog message
   * @param {string} [options.variant='info'] - 'info' | 'warning' | 'danger'
   * @param {string} [options.okText='OK'] - OK button text
   * @returns {Promise<void>}
   */
  function alertDialog(options) {
    var o = options || {};
    var title = o.title || 'Alert';
    var message = o.message || '';
    var config = variantConfig[o.variant] || variantConfig.info;
    var okText = o.okText || 'OK';

    return new Promise(function (resolve) {
      var modal = buildDialog(generateModalId(), config, title, message, '',
        '<button type="button" data-modal-ok class="' + config.btnClass + '">' + escapeHtml(okText) + '</button>');

      var close = presentDynamic(modal, function () { resolve(); }, '[data-modal-ok]');
      modal.querySelector('[data-modal-ok]').addEventListener('click', function () { close(true); });
    });
  }

  /**
   * Typed confirmation dialog - returns Promise<boolean>
   * @param {Object} options - Dialog options
   * @param {string} options.title - Dialog title
   * @param {string} options.message - Dialog message
   * @param {string} options.requiredText - Text the user must type to confirm
   * @param {string} [options.inputLabel] - Label for the input field
   * @param {string} [options.variant='danger'] - 'info' | 'warning' | 'danger'
   * @param {string} [options.confirmText='Confirm'] - Confirm button text
   * @param {string} [options.cancelText='Cancel'] - Cancel button text
   * @returns {Promise<boolean>} true if confirmed, false if cancelled
   */
  function typedConfirmDialog(options) {
    var o = options || {};
    var title = o.title || 'Confirm';
    var message = o.message || '';
    var requiredText = o.requiredText || 'CONFIRM';
    var inputLabel = o.inputLabel || ('Type ' + requiredText + ' to confirm');
    var config = variantConfig[o.variant] || variantConfig.danger;
    var confirmText = o.confirmText || 'Confirm';
    var cancelText = o.cancelText || 'Cancel';
    var id = generateModalId();

    return new Promise(function (resolve) {
      var extra =
        '<div class="mt-4">' +
          '<label for="' + id + '-input" class="form-label block mb-2">' + escapeHtml(inputLabel) + '</label>' +
          '<input type="text" id="' + id + '-input" data-modal-input class="form-input" ' +
            'placeholder="' + escapeHtml(requiredText) + '" autocomplete="off" autocapitalize="off" spellcheck="false" />' +
        '</div>';
      var modal = buildDialog(id, config, title, message, extra,
        '<button type="button" data-modal-cancel class="btn btn-secondary">' + escapeHtml(cancelText) + '</button>' +
        '<button type="button" data-modal-confirm disabled class="' + config.btnClass + '">' + escapeHtml(confirmText) + '</button>');

      var close = presentDynamic(modal, resolve, '[data-modal-input]');
      var input = modal.querySelector('[data-modal-input]');
      var confirmBtn = modal.querySelector('[data-modal-confirm]');

      input.addEventListener('input', function () {
        confirmBtn.disabled = input.value !== requiredText;
      });
      input.addEventListener('keydown', function (e) {
        if (e.key === 'Enter' && input.value === requiredText) {
          e.preventDefault();
          close(true);
        }
      });
      modal.querySelector('[data-modal-cancel]').addEventListener('click', function () { close(false); });
      confirmBtn.addEventListener('click', function () { close(true); });
    });
  }

  // Expose public API
  window.quickActions = {
    // Any dialog element
    openDialog: openDialog,
    closeDialog: closeDialog,
    // Static confirmation modals
    showConfirmationModal: showConfirmationModal,
    hideConfirmationModal: hideConfirmationModal,
    showTypedConfirmationModal: showConfirmationModal,
    hideTypedConfirmationModal: hideConfirmationModal,
    submitQuickAction: submitQuickAction,
    showToast: showToast,
    // Dynamic dialogs
    confirm: confirmDialog,
    alert: alertDialog,
    typedConfirm: typedConfirmDialog
  };
})();
