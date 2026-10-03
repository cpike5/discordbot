/**
 * Users list: confirm before disabling a user.
 *
 * A "Disable" button (`[data-confirm-disable]`) asks first, through the shared dialog, then
 * submits its form with requestSubmit() so the submit guard sees it. "Enable" is harmless and
 * submits straight away. The user's email comes from a data attribute and is written as text.
 */
(function () {
    'use strict';

    document.addEventListener('click', async function (event) {
        var button = event.target.closest && event.target.closest('[data-confirm-disable]');
        if (!button || !window.quickActions) return;

        var form = button.closest('form');
        if (!form || form.dataset.submitting === 'true') return;

        event.preventDefault();
        var confirmed = await window.quickActions.confirm({
            title: 'Disable this user?',
            message: button.dataset.userEmail + ' will not be able to sign in until you enable them again. Their data is kept.',
            variant: 'warning',
            confirmText: 'Disable user'
        });
        if (confirmed) form.requestSubmit(button);
    });
})();
