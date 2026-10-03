/**
 * Focus the first error after a form is shown again with server-side validation errors (UX F-4).
 *
 * Opt in with `data-focus-first-error` on the form. When the page loads, the first control that
 * the server marked `aria-invalid="true"` (or the first radio of a card group marked
 * `.radio-card-group-invalid`) takes focus, so a keyboard or screen-reader user lands on the
 * problem instead of at the top of the page. A form with no errors is left alone.
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

    var INVALID = '[aria-invalid="true"], .radio-card-group-invalid input[type="radio"]';

    /** The first invalid control inside `form`, or null. */
    function firstInvalid(form) {
        if (!form || typeof form.querySelector !== 'function') return null;
        return form.querySelector(INVALID);
    }

    /** Focuses the form's first invalid control. Returns it, or null when there is none. */
    function focusFirstError(form) {
        var control = firstInvalid(form);
        if (!control) return null;
        control.focus();
        if (typeof control.scrollIntoView === 'function') {
            var reduce = typeof window !== 'undefined' && typeof window.matchMedia === 'function' &&
                window.matchMedia('(prefers-reduced-motion: reduce)').matches;
            control.scrollIntoView({ block: 'center', behavior: reduce ? 'auto' : 'smooth' });
        }
        return control;
    }

    function init() {
        var forms = document.querySelectorAll('form[data-focus-first-error]');
        for (var i = 0; i < forms.length; i++) {
            if (focusFirstError(forms[i])) return;
        }
    }

    return { init: init, firstInvalid: firstInvalid, focusFirstError: focusFirstError };
});
