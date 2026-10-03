/**
 * Focus the first error after a page is shown again with server-side validation errors (UX F-4).
 *
 * Opt in with `data-focus-first-error`. Put it on the form the server re-renders, or on any
 * wrapper element to cover everything inside it. When the page loads, the first control in that
 * scope that the server marked `aria-invalid="true"` (or has the `input-validation-error` class, or
 * is the first radio of a `.radio-card-group-invalid` card group) takes focus and scrolls into
 * view, so a keyboard or screen-reader user lands on the problem instead of at the top of the page.
 * A scope with no errors is left alone, and so is one inside an `inert` region.
 *
 * A script that renders errors of its own can call `FormFocus.focusFirstInvalid(scope)` again
 * (scope defaults to the document).
 *
 * Exposed as window.FormFocus (browser) and module.exports (Node/tests).
 */
(function (root, factory) {
    if (typeof module === 'object' && module.exports) {
        module.exports = factory(root);
    } else {
        root.FormFocus = factory(root);
        if (typeof document !== 'undefined') {
            if (document.readyState === 'loading') {
                document.addEventListener('DOMContentLoaded', root.FormFocus.init);
            } else {
                root.FormFocus.init();
            }
        }
    }
})(typeof self !== 'undefined' ? self : this, function () {
    'use strict';

    var INVALID = '[aria-invalid="true"], .input-validation-error, .radio-card-group-invalid input[type="radio"]';

    /** The first invalid control inside `scope` (a form or any element), or null. */
    function firstInvalid(scope) {
        if (!scope || typeof scope.querySelector !== 'function') return null;
        return scope.querySelector(INVALID);
    }

    /**
     * Focuses the first invalid control inside `scope` (the document when omitted). Returns the
     * control, or null when there is none or it sits in an `inert` region.
     */
    function focusFirstInvalid(scope) {
        var control = firstInvalid(scope || (typeof document !== 'undefined' ? document : null));
        if (!control) return null;
        if (typeof control.closest === 'function' && control.closest('[inert]')) return null;
        if (typeof control.focus === 'function') {
            try {
                control.focus({ preventScroll: true });
            } catch (e) {
                control.focus();
            }
        }
        if (typeof control.scrollIntoView === 'function') {
            var reduce = typeof window !== 'undefined' && typeof window.matchMedia === 'function' &&
                window.matchMedia('(prefers-reduced-motion: reduce)').matches;
            control.scrollIntoView({ block: 'center', behavior: reduce ? 'auto' : 'smooth' });
        }
        return control;
    }

    function init() {
        var scopes = document.querySelectorAll('[data-focus-first-error]');
        for (var i = 0; i < scopes.length; i++) {
            if (focusFirstInvalid(scopes[i])) return;
        }
    }

    return {
        init: init,
        firstInvalid: firstInvalid,
        focusFirstInvalid: focusFirstInvalid,
        focusFirstError: focusFirstInvalid
    };
});
