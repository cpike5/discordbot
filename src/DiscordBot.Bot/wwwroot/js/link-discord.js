/**
 * Link Discord page (Pages/Account/LinkDiscord.cshtml).
 *
 * - Verification countdown: `[data-verification]` carries `data-seconds-remaining` (measured by the
 *   server when the page was rendered) and `data-expires-at` (UTC ISO). The countdown runs from the
 *   seconds, against the browser's own elapsed time, so a wrong device clock cannot shorten or
 *   stretch it; the ISO time is the fallback when the seconds are missing. The timer
 *   shows the time left, and at zero locks the code form and reveals the expired panel with a
 *   "Start again" button (a reload: the server no longer reports an expired code as pending).
 *   The visible countdown is aria-hidden; screen readers get one polite message when the code
 *   is about to expire and one when it has.
 * - Copy buttons: `[data-copy-text]` copies the text and confirms with a toast. `data-copy-label`
 *   names what was copied. IDs are kept out of the page text and offered this way instead.
 *
 * Exposed as window.LinkDiscord (browser) and module.exports (Node/tests).
 */
(function (root, factory) {
    if (typeof module === 'object' && module.exports) {
        module.exports = factory(root);
    } else {
        root.LinkDiscord = factory(root);
        if (typeof document !== 'undefined') {
            if (document.readyState === 'loading') {
                document.addEventListener('DOMContentLoaded', root.LinkDiscord.init);
            } else {
                root.LinkDiscord.init();
            }
        }
    }
})(typeof self !== 'undefined' ? self : this, function () {
    'use strict';

    var WARN_AT_MS = 60 * 1000;

    /** Milliseconds from `now` until the ISO time, or null if it does not parse. */
    function msUntil(iso, now) {
        var at = Date.parse(iso);
        if (isNaN(at)) return null;
        return at - (now === undefined ? Date.now() : now);
    }

    /**
     * The instant (ms since the epoch, on the browser's clock) the countdown ends at: the server's
     * remaining seconds counted from `now`, else the ISO expiry. Null when neither is usable.
     */
    function deadline(secondsRemaining, iso, now) {
        var start = now === undefined ? Date.now() : now;
        var seconds = secondsRemaining === null || secondsRemaining === undefined || secondsRemaining === ''
            ? NaN
            : Number(secondsRemaining);
        if (isFinite(seconds) && seconds >= 0) return start + seconds * 1000;
        var at = Date.parse(iso);
        return isNaN(at) ? null : at;
    }

    /** "14:32" for the time left, "0:05" near the end. */
    function clock(ms) {
        var total = Math.max(0, Math.ceil(ms / 1000));
        var minutes = Math.floor(total / 60);
        var seconds = total % 60;
        return minutes + ':' + (seconds < 10 ? '0' : '') + seconds;
    }

    function initCountdown(container) {
        var remaining = container.querySelector('[data-verification-remaining]');
        var status = container.querySelector('[data-verification-status]');
        var expiredPanel = container.querySelector('[data-verification-expired]');
        var form = container.querySelector('[data-verification-form]');
        var timerLabel = container.querySelector('[data-verification-timer]');
        var restart = container.querySelector('[data-verification-restart]');
        var endsAt = deadline(container.getAttribute('data-seconds-remaining'), container.getAttribute('data-expires-at'));
        var warned = false;
        var timer = null;

        if (restart) {
            restart.addEventListener('click', function () { window.location.reload(); });
        }

        function expire() {
            if (timer) clearInterval(timer);
            if (remaining) remaining.textContent = '';
            if (timerLabel) timerLabel.style.display = 'none';
            if (expiredPanel) expiredPanel.hidden = false;
            if (status) status.textContent = 'This verification code has expired. Start again to get a new one.';
            if (form) {
                form.querySelectorAll('input, button').forEach(function (el) { el.disabled = true; });
                form.setAttribute('aria-disabled', 'true');
            }
        }

        function tick() {
            var ms = endsAt === null ? null : endsAt - Date.now();
            if (ms === null) {
                if (timer) clearInterval(timer);
                return;
            }
            if (ms <= 0) {
                expire();
                return;
            }
            if (remaining) remaining.textContent = '(' + clock(ms) + ' left)';
            if (!warned && ms <= WARN_AT_MS) {
                warned = true;
                if (status) status.textContent = 'This verification code expires in less than a minute.';
            }
        }

        tick();
        timer = setInterval(tick, 1000);
        return { tick: tick };
    }

    /** Copies text with the async clipboard API, falling back to a hidden textarea. */
    function copyText(text) {
        if (navigator.clipboard && navigator.clipboard.writeText) {
            return navigator.clipboard.writeText(text);
        }
        return new Promise(function (resolve, reject) {
            var area = document.createElement('textarea');
            area.value = text;
            area.setAttribute('readonly', '');
            area.style.position = 'fixed';
            area.style.opacity = '0';
            document.body.appendChild(area);
            area.select();
            try {
                document.execCommand('copy') ? resolve() : reject(new Error('copy failed'));
            } catch (e) {
                reject(e);
            } finally {
                document.body.removeChild(area);
            }
        });
    }

    function initCopyButtons() {
        document.addEventListener('click', function (event) {
            var button = event.target.closest && event.target.closest('[data-copy-text]');
            if (!button) return;
            var label = button.getAttribute('data-copy-label') || 'Text';
            copyText(button.getAttribute('data-copy-text')).then(function () {
                if (window.toast) window.toast.success(label + ' copied.', { key: 'copy-' + label });
            }, function () {
                if (window.toast) window.toast.error('Could not copy. Select the text and copy it by hand.');
            });
        });
    }

    function init() {
        document.querySelectorAll('[data-verification]').forEach(initCountdown);
        initCopyButtons();
    }

    return { init: init, msUntil: msUntil, deadline: deadline, clock: clock };
});
