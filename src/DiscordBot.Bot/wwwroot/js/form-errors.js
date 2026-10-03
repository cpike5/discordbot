/**
 * Move focus to the first invalid field after a form was re-rendered with errors.
 *
 * The server marks a field with `aria-invalid="true"` (the form partials do) or the
 * `input-validation-error` class. On load, the first of them takes focus and scrolls into view, so
 * a keyboard or screen reader user lands on the problem instead of at the top of the page.
 * It does nothing on a page without errors.
 *
 * Exposed as window.FormErrors; `focusFirstInvalid(scope)` can be called again after a script
 * renders errors of its own.
 */
(function (root, factory) {
    if (typeof module === 'object' && module.exports) {
        module.exports = factory();
    } else {
        root.FormErrors = factory();
    }
})(typeof self !== 'undefined' ? self : this, function () {
    'use strict';

    var INVALID = '[aria-invalid="true"], .input-validation-error, .radio-card-group-invalid input';

    /** Focus the first invalid control under `scope`; true when one was found. */
    function focusFirstInvalid(scope) {
        var root = scope || document;
        var field = root.querySelector(INVALID);
        if (!field) return false;
        if (field.closest && field.closest('[inert]')) return false;
        if (typeof field.focus === 'function') {
            try {
                field.focus({ preventScroll: true });
            } catch (e) {
                field.focus();
            }
        }
        if (typeof field.scrollIntoView === 'function') {
            var reduce = typeof window !== 'undefined' && typeof window.matchMedia === 'function' &&
                window.matchMedia('(prefers-reduced-motion: reduce)').matches;
            field.scrollIntoView({ block: 'center', behavior: reduce ? 'auto' : 'smooth' });
        }
        return true;
    }

    if (typeof document !== 'undefined') {
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', function () { focusFirstInvalid(document); });
        } else {
            focusFirstInvalid(document);
        }
    }

    return { focusFirstInvalid: focusFirstInvalid };
});
