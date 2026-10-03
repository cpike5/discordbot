/**
 * Confirm before a form posts.
 *
 * A row action that destroys something (delete, cancel, reject) is an ordinary form whose handler
 * is in its action URL. Mark it with the words to ask and this module asks first, using the shared
 * dialog (`quickActions.confirm`), then submits the form the normal way, so the page's redirect and
 * its TempData toast work as usual and `data-submit-guard` still shows the pending state.
 *
 *   <form method="post" asp-page-handler="Delete" asp-route-id="@id"
 *         data-submit-guard
 *         data-confirm-title="Delete scheduled message?"
 *         data-confirm-message="..."
 *         data-confirm-text="Delete"
 *         data-confirm-variant="danger">
 *       <button type="submit">...</button>
 *   </form>
 *
 * The message is read from an attribute and shown as text, never as markup, so user text in it is
 * safe. A form without `data-confirm-message` is untouched.
 *
 * Exposed as window.ConfirmForms (browser) and module.exports (Node/tests).
 */
(function (root, factory) {
    if (typeof module === 'object' && module.exports) {
        module.exports = factory();
    } else {
        root.ConfirmForms = factory();
    }
})(typeof self !== 'undefined' ? self : this, function () {
    'use strict';

    /** The dialog options a form asks for. */
    function optionsFor(dataset) {
        return {
            title: dataset.confirmTitle || 'Are you sure?',
            message: dataset.confirmMessage || '',
            confirmText: dataset.confirmText || 'Confirm',
            cancelText: dataset.confirmCancel || 'Cancel',
            variant: dataset.confirmVariant || 'warning'
        };
    }

    /** True when this submit has to be held back and confirmed first. */
    function needsConfirmation(form) {
        var data = form && form.dataset;
        return !!data && data.confirmMessage !== undefined && data.confirmed !== 'true';
    }

    if (typeof document !== 'undefined') {
        // Capture phase, on the document: runs before the double-submit guard and the unsaved-
        // changes tracker see the event, so neither goes busy for a submit that is only a question.
        document.addEventListener('submit', function (event) {
            var form = event.target;
            if (!(form instanceof HTMLFormElement)) return;

            if (form.dataset.confirmed === 'true') {
                delete form.dataset.confirmed; // let this one through, ask again next time
                return;
            }
            if (!needsConfirmation(form)) return;
            if (!window.quickActions || typeof window.quickActions.confirm !== 'function') return;

            event.preventDefault();
            event.stopImmediatePropagation();

            var submitter = event.submitter || null;
            window.quickActions.confirm(optionsFor(form.dataset)).then(function (confirmed) {
                if (!confirmed) return;
                form.dataset.confirmed = 'true';
                if (typeof form.requestSubmit === 'function') {
                    form.requestSubmit(submitter && submitter.form === form ? submitter : undefined);
                } else {
                    form.submit();
                }
            });
        }, true);
    }

    return { optionsFor: optionsFor, needsConfirmation: needsConfirmation };
});
