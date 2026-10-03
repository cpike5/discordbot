/**
 * Toast notifications - the one toast API for the app (UX plan decision D1).
 *
 *   toast.success('Saved.');
 *   toast.error('Could not save.', { action: { label: 'Retry', onClick: save } });
 *   toast.info('Copied.', { title: 'Clipboard' });
 *   toast.warning('Rate limit approaching.');
 *
 * Options: { title, duration (ms, 0 = stay until dismissed), action: { label, onClick }, key }.
 * Error toasts stay until dismissed. A toast whose type, title and message (or `key`) match
 * one already on screen is not shown twice; the existing one restarts its timer instead.
 * Timers pause while the toast is hovered, focused or touched.
 *
 * Server-side messages arrive through the TempData bridge (TempData.SetSuccessToast etc.),
 * which _ToastContainer renders as JSON in #serverToasts; they are shown on load.
 *
 * Older call shapes still work and route here:
 *   ToastManager.show(type, message, options)
 *   quickActions.showToast(message, type)
 *   showToast(type, message) and showToast(message, type)
 *   Toast.show(message, type)
 */
(function () {
  'use strict';

  const TYPES = ['success', 'error', 'warning', 'info'];
  const TYPE_LABELS = { success: 'Success', error: 'Error', warning: 'Warning', info: 'Information' };

  const ToastManager = {
    container: null,
    toasts: [],
    maxToasts: 5,

    icons: {
      info: '<svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>',
      success: '<svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>',
      warning: '<svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" /></svg>',
      error: '<svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 14l2-2m0 0l2-2m-2 2l-2-2m2 2l2 2m7-2a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>'
    },

    /**
     * Find or create the container and its live regions, then show any toasts the
     * server queued through TempData. Safe to call more than once.
     */
    init() {
      if (this.container && document.body.contains(this.container)) return;

      this.container = document.getElementById('toastContainer');
      if (!this.container) {
        // Pages without _ToastContainer (standalone pages) still get toasts
        this.container = document.createElement('div');
        this.container.id = 'toastContainer';
        this.container.className = 'toast-container';
        this.container.setAttribute('role', 'region');
        this.container.setAttribute('aria-label', 'Notifications');
        document.body.appendChild(this.container);
      }
      this.ensureLiveRegion('toastLiveRegion', 'polite');
      this.ensureLiveRegion('toastAlertRegion', 'assertive');

      this.showServerToasts();
    },

    /** @private */
    ensureLiveRegion(id, politeness) {
      if (document.getElementById(id)) return;
      const region = document.createElement('div');
      region.id = id;
      region.className = 'sr-only';
      region.setAttribute('aria-live', politeness);
      region.setAttribute('aria-atomic', 'true');
      this.container.appendChild(region);
    },

    /**
     * Show the toasts _ToastContainer rendered from TempData, once.
     * @private
     */
    showServerToasts() {
      const data = document.getElementById('serverToasts');
      if (!data || data.dataset.shown === 'true') return;
      data.dataset.shown = 'true';

      let queued = [];
      try {
        queued = JSON.parse(data.textContent || '[]');
      } catch (e) {
        return;
      }
      queued.forEach(t => this.show(t.type, t.message, { title: t.title || null }));
    },

    /**
     * Show a toast.
     * @param {string} type - 'success', 'error', 'warning' or 'info'
     * @param {string} message
     * @param {object} [options]
     * @param {string} [options.title]
     * @param {number} [options.duration] - ms before it closes; 0 keeps it until dismissed.
     *   Defaults: success 4s, info/warning 6s, error stays.
     * @param {{label: string, onClick: function}} [options.action]
     * @param {string} [options.key] - identity for de-duplication (defaults to type + title + message)
     */
    show(type, message, options = {}) {
      if (!this.container) this.init();
      if (!TYPES.includes(type)) type = 'info';
      message = message == null ? '' : String(message);

      const defaultDuration = type === 'error' ? 0 : type === 'success' ? 4000 : 6000;
      const {
        title = null,
        duration = defaultDuration,
        action = null,
        key = `${type}|${title || ''}|${message}`
      } = options || {};

      // Repeats: keep the one on screen and give it a fresh timer
      const existing = this.toasts.find(t => t.key === key && !t.dismissing);
      if (existing) {
        this.restartTimer(existing);
        existing.element.classList.remove('toast-bump');
        void existing.element.offsetWidth;
        existing.element.classList.add('toast-bump');
        return existing;
      }

      while (this.toasts.length >= this.maxToasts) {
        const oldest = this.toasts.shift();
        clearTimeout(oldest.timeoutId);
        oldest.element.remove();
      }

      const toastData = this.createToast(type, message, title, duration, action, key);
      this.toasts.push(toastData);
      this.container.insertBefore(toastData.element, this.container.firstChild);
      this.announce(type, message, title);
      return toastData;
    },

    success(message, options) { return this.show('success', message, options); },
    error(message, options) { return this.show('error', message, options); },
    warning(message, options) { return this.show('warning', message, options); },
    info(message, options) { return this.show('info', message, options); },

    /**
     * Announce through the shared live regions: errors assertively, the rest politely.
     * The toast element itself carries no live role, so nothing is read twice.
     * @private
     */
    announce(type, message, title) {
      const region = document.getElementById(type === 'error' ? 'toastAlertRegion' : 'toastLiveRegion');
      if (!region) return;
      region.textContent = title
        ? `${TYPE_LABELS[type]}: ${title}. ${message}`
        : `${TYPE_LABELS[type]}: ${message}`;
      setTimeout(() => { region.textContent = ''; }, 1000);
    },

    /** @private */
    createToast(type, message, title, duration, action, key) {
      const toast = document.createElement('div');
      toast.className = `toast toast-${type}`;

      const autoDismiss = duration > 0;
      const escape = SafeHtml.escape;

      const contentHTML = title
        ? `<p class="text-sm font-semibold text-text-primary">${escape(title)}</p>
           <p class="text-sm text-text-secondary mt-0.5">${escape(message)}</p>`
        : `<p class="text-sm text-text-primary">${escape(message)}</p>`;

      const actionHTML = action && action.label
        ? `<button class="toast-action" type="button">${escape(action.label)}</button>`
        : '';

      toast.innerHTML = `
        <div class="toast-icon flex-shrink-0 mt-0.5">${this.icons[type]}</div>
        <div class="flex-1 min-w-0 flex items-start gap-2">
          <div class="flex-1 min-w-0 break-words">${contentHTML}</div>
          ${actionHTML}
        </div>
        <button class="toast-close" type="button" aria-label="Dismiss notification">
          <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
          </svg>
        </button>
        ${autoDismiss ? `<div class="toast-progress animating" style="animation-duration: ${duration}ms;"></div>` : ''}
      `;

      const toastData = {
        element: toast,
        key,
        duration,
        timeoutId: null,
        remainingTime: duration,
        startTime: Date.now(),
        pauseReasons: new Set(),
        dismissing: false
      };

      if (autoDismiss) {
        this.startTimer(toastData);

        // Pause while the pointer is over it, focus is inside it, or a finger is on it
        const pause = reason => this.pause(toastData, reason);
        const resume = reason => this.resume(toastData, reason);
        toast.addEventListener('mouseenter', () => pause('hover'));
        toast.addEventListener('mouseleave', () => resume('hover'));
        toast.addEventListener('focusin', () => pause('focus'));
        toast.addEventListener('focusout', e => {
          if (!toast.contains(e.relatedTarget)) resume('focus');
        });
        toast.addEventListener('touchstart', () => pause('touch'), { passive: true });
        toast.addEventListener('touchend', () => setTimeout(() => resume('touch'), 1500), { passive: true });
        toast.addEventListener('touchcancel', () => resume('touch'), { passive: true });
      }

      toast.querySelector('.toast-close').addEventListener('click', () => this.dismissToast(toastData));

      if (action && typeof action.onClick === 'function') {
        const actionBtn = toast.querySelector('.toast-action');
        if (actionBtn) {
          actionBtn.addEventListener('click', e => {
            e.stopPropagation();
            action.onClick();
            this.dismissToast(toastData);
          });
        }
      }

      return toastData;
    },

    /** @private */
    startTimer(toastData) {
      clearTimeout(toastData.timeoutId);
      toastData.startTime = Date.now();
      toastData.timeoutId = setTimeout(() => this.dismissToast(toastData), toastData.remainingTime);
    },

    /** @private */
    restartTimer(toastData) {
      if (!(toastData.duration > 0)) return;
      toastData.remainingTime = toastData.duration;
      const bar = toastData.element.querySelector('.toast-progress');
      if (bar) {
        bar.classList.remove('animating');
        void bar.offsetWidth;
        bar.classList.add('animating');
      }
      if (toastData.pauseReasons.size === 0) {
        this.startTimer(toastData);
      } else {
        clearTimeout(toastData.timeoutId);
        if (bar) bar.classList.add('paused');
      }
    },

    /** @private */
    pause(toastData, reason) {
      const wasRunning = toastData.pauseReasons.size === 0;
      toastData.pauseReasons.add(reason);
      if (!wasRunning) return;
      clearTimeout(toastData.timeoutId);
      toastData.remainingTime = Math.max(0, toastData.remainingTime - (Date.now() - toastData.startTime));
      const bar = toastData.element.querySelector('.toast-progress');
      if (bar) bar.classList.add('paused');
    },

    /** @private */
    resume(toastData, reason) {
      if (!toastData.pauseReasons.delete(reason) || toastData.pauseReasons.size > 0) return;
      if (toastData.dismissing) return;
      this.startTimer(toastData);
      const bar = toastData.element.querySelector('.toast-progress');
      if (bar) bar.classList.remove('paused');
    },

    /** Dismiss one toast. */
    dismissToast(toastData) {
      if (toastData.dismissing) return;
      toastData.dismissing = true;
      clearTimeout(toastData.timeoutId);

      const toast = toastData.element;
      // Keep focus on the page rather than losing it to <body> when the toast goes
      const hadFocus = toast.contains(document.activeElement);
      toast.classList.add('dismissing');

      setTimeout(() => {
        toast.remove();
        const index = this.toasts.indexOf(toastData);
        if (index > -1) this.toasts.splice(index, 1);
        if (hadFocus) {
          const main = document.getElementById('main-content');
          if (main) {
            if (!main.hasAttribute('tabindex')) main.setAttribute('tabindex', '-1');
            main.focus({ preventScroll: true });
          }
        }
      }, 200);
    },

    /** Dismiss every toast. */
    clearAll() {
      [...this.toasts].forEach(t => this.dismissToast(t));
    },

  };

  /** Accepts (type, message) or (message, type), whichever order a caller used. */
  function showEitherOrder(a, b, options) {
    if (TYPES.includes(a) && !TYPES.includes(b)) return ToastManager.show(a, b, options);
    return ToastManager.show(TYPES.includes(b) ? b : 'info', a, options);
  }

  const toast = {
    success: (message, options) => ToastManager.success(message, options),
    error: (message, options) => ToastManager.error(message, options),
    warning: (message, options) => ToastManager.warning(message, options),
    info: (message, options) => ToastManager.info(message, options),
    show: (type, message, options) => ToastManager.show(type, message, options),
    dismissAll: () => ToastManager.clearAll()
  };

  window.ToastManager = ToastManager;
  window.toast = toast;
  // Legacy shapes
  if (typeof window.showToast !== 'function') window.showToast = showEitherOrder;
  if (!window.Toast) window.Toast = { show: (a, b, options) => showEitherOrder(a, b, options) };

  // Dismissible _Alert banners: remove the alert, then call its optional callback by name.
  document.addEventListener('click', e => {
    const button = e.target instanceof Element ? e.target.closest('[data-alert-dismiss]') : null;
    if (!button) return;
    const alert = button.closest('[data-alert]');
    if (!alert) return;
    const callbackName = button.dataset.dismissCallback;
    alert.remove();
    if (callbackName && /^[A-Za-z_$][\w$]*$/.test(callbackName) && typeof window[callbackName] === 'function') {
      window[callbackName](alert);
    }
  });

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => ToastManager.init());
  } else {
    ToastManager.init();
  }
})();
