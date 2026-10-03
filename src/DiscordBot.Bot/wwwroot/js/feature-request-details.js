/**
 * Feature request details: the Reject dialog.
 *
 * "Reject..." opens a dialog (the shared quickActions dialog layer) that asks for the reason. The
 * reason is required: an empty one shows the error under the field instead of posting. The form
 * posts normally, so the redirect and its toast work as for any other action.
 */
(function () {
    'use strict';

    var dialog = document.getElementById('reject-dialog');
    if (!dialog) return;

    var form = document.getElementById('reject-form');
    var reason = document.getElementById('reject-reason');
    var error = document.getElementById('reject-reason-error');

    function setInvalid(invalid) {
        if (error) error.classList.toggle('hidden', !invalid);
        if (reason) {
            reason.classList.toggle('input-validation-error', invalid);
            if (invalid) reason.setAttribute('aria-invalid', 'true'); else reason.removeAttribute('aria-invalid');
        }
    }

    document.addEventListener('click', function (event) {
        var opener = event.target.closest && event.target.closest('[data-open-dialog="reject-dialog"]');
        if (!opener || !window.quickActions) return;
        setInvalid(false);
        window.quickActions.openDialog(dialog);
    });

    if (form && reason) {
        // Capture phase: runs before the double-submit guard marks the form busy
        form.addEventListener('submit', function (event) {
            if (reason.value.trim()) return;
            event.preventDefault();
            event.stopImmediatePropagation();
            setInvalid(true);
            reason.focus();
        }, true);

        reason.addEventListener('input', function () {
            if (reason.value.trim()) setInvalid(false);
        });
    }
})();
