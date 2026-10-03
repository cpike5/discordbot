/**
 * Privacy page (Pages/Account/Privacy.cshtml).
 *
 * - Consent toggles: each `[data-consent-toggle]` checkbox sits in a `form[data-consent-form]`
 *   (carrying `data-consent-name` and `data-consent-action`). Changing the switch asks first;
 *   a "no" puts the switch back, a "yes" submits the form, which posts the new state as `grant`.
 *   The server redirects back to the same row, so the page does not jump to the top.
 * - Delete my data: `[data-delete-data]` asks for a typed confirmation, posts to its
 *   `data-delete-url` with ApiClient, and on success shows a toast and goes to the signed-out
 *   page (the server has already ended the session).
 *
 * Exposed as window.PrivacyPage (browser) and module.exports (Node/tests).
 */
(function (root, factory) {
    if (typeof module === 'object' && module.exports) {
        module.exports = factory(root);
    } else {
        root.PrivacyPage = factory(root);
        if (typeof document !== 'undefined') {
            if (document.readyState === 'loading') {
                document.addEventListener('DOMContentLoaded', root.PrivacyPage.init);
            } else {
                root.PrivacyPage.init();
            }
        }
    }
})(typeof self !== 'undefined' ? self : this, function (root) {
    'use strict';

    /** Words for the confirm dialog, from the form's data-consent-action ("grant" or "revoke"). */
    function consentCopy(action, name) {
        if (action === 'revoke') {
            return {
                title: 'Turn off ' + name + '?',
                message: 'The bot will stop using your data for "' + name + '". You can turn it back on at any time.',
                confirmText: 'Turn off',
                variant: 'warning'
            };
        }
        return {
            title: 'Turn on ' + name + '?',
            message: 'The bot may use your data for "' + name + '". You can turn it off at any time.',
            confirmText: 'Turn on',
            variant: 'info'
        };
    }

    async function onConsentChange(checkbox) {
        var form = checkbox.closest('form[data-consent-form]');
        if (!form || form.dataset.submitting === 'true') return;

        var copy = consentCopy(form.dataset.consentAction, form.dataset.consentName || 'this consent');
        var confirmed = await root.quickActions.confirm(copy);

        if (!confirmed) {
            checkbox.checked = !checkbox.checked;
            return;
        }

        form.dataset.submitting = 'true';
        form.setAttribute('aria-busy', 'true');
        form.requestSubmit();
    }

    async function onDeleteClick(button) {
        if (button.dataset.busy === 'true') return;

        var confirmed = await root.quickActions.typedConfirm({
            title: 'Permanently delete all your data',
            message: 'This permanently deletes all your personal data, including message logs, preferences and consent records, and signs you out. This can\'t be undone.',
            requiredText: 'DELETE',
            inputLabel: 'Type DELETE to confirm',
            variant: 'danger',
            confirmText: 'Delete all data'
        });
        if (!confirmed) return;

        button.dataset.busy = 'true';
        if (root.LoadingManager) root.LoadingManager.setButtonLoading(button, true, button.dataset.loadingText || null);

        try {
            var data = await root.ApiClient.post(button.dataset.deleteUrl, null, {
                errorMessage: 'Could not delete your data. Try again.',
                timeout: 120000 // a purge across every table can take a while
            });
            root.toast.success((data && data.message) || 'Your data has been deleted.');
            setTimeout(function () {
                root.location.href = (data && data.redirectUrl) || '/';
            }, 2000);
        } catch (error) {
            root.ApiClient.showErrorToast(error);
            delete button.dataset.busy;
            if (root.LoadingManager) root.LoadingManager.setButtonLoading(button, false);
        }
    }

    function init() {
        document.addEventListener('change', function (event) {
            var checkbox = event.target.closest && event.target.closest('[data-consent-toggle]');
            if (checkbox) onConsentChange(checkbox);
        });

        document.addEventListener('click', function (event) {
            var button = event.target.closest && event.target.closest('[data-delete-data]');
            if (button) onDeleteClick(button);
        });

        // Coming back through Back/Forward restores the page as it was left: busy. Undo that.
        root.addEventListener('pageshow', function (event) {
            if (!event.persisted) return;
            document.querySelectorAll('form[data-consent-form]').forEach(function (form) {
                delete form.dataset.submitting;
                form.removeAttribute('aria-busy');
            });
        });
    }

    return { init: init, consentCopy: consentCopy };
});
