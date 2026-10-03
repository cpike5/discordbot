/**
 * Rat Watch settings page (Pages/Guilds/RatWatch/Index.cshtml).
 *
 * - [data-settings-toggle] buttons show and hide the settings form (aria-expanded, focus moves into
 *   the form, and back to the Edit button on cancel).
 * - [data-watch-action] buttons (cancel a watch, end its vote) ask through the shared confirm
 *   dialog, then post the page's one hidden form (#watch-action-form) to the row's handler URL.
 *   Names and ids reach the dialog as text through data-* attributes, never as markup.
 *
 * Exposed as window.RatWatchManage (and module.exports for tests).
 */
(function (root, factory) {
    'use strict';
    const api = factory(root);
    if (typeof module !== 'undefined' && module.exports) {
        module.exports = api;
    }
    if (typeof window !== 'undefined' && root === window) {
        window.RatWatchManage = api;
    }
})(typeof window !== 'undefined' ? window : globalThis, function (root) {
    'use strict';

    /**
     * What the confirm dialog says for a row action.
     * @param {{action: string, accused: string, guilty?: string, notGuilty?: string}} data
     * @returns {{title: string, message: string, confirmText: string, cancelText: string, variant: string}|null}
     */
    function dialogFor(data) {
        const who = data.accused || 'this user';
        if (data.action === 'cancel') {
            return {
                title: 'Cancel Rat Watch',
                message: 'Cancel the watch for ' + who + '? This cannot be undone.',
                confirmText: 'Cancel watch',
                cancelText: 'Keep watch',
                variant: 'danger'
            };
        }
        if (data.action === 'end-vote') {
            return {
                title: 'End vote early',
                message: 'End voting for ' + who + ' now? The current tally is ' +
                    (data.guilty || '0') + ' guilty and ' + (data.notGuilty || '0') +
                    ' not guilty, and the verdict follows it. This cannot be undone.',
                confirmText: 'End vote now',
                cancelText: 'Keep voting',
                variant: 'info'
            };
        }
        return null;
    }

    function toggleSettings(open) {
        const form = root.document.getElementById('settings-form');
        const display = root.document.getElementById('settings-display');
        const edit = root.document.getElementById('settings-toggle');
        if (!form || !display || !edit) return;
        const show = typeof open === 'boolean' ? open : form.classList.contains('hidden');
        form.classList.toggle('hidden', !show);
        display.classList.toggle('hidden', show);
        edit.setAttribute('aria-expanded', show ? 'true' : 'false');
        if (show) {
            const first = form.querySelector('select, input:not([type="hidden"])');
            if (first) first.focus();
        } else {
            edit.focus();
        }
    }

    async function runWatchAction(button) {
        const data = {
            action: button.getAttribute('data-watch-action'),
            accused: button.getAttribute('data-accused'),
            guilty: button.getAttribute('data-guilty'),
            notGuilty: button.getAttribute('data-not-guilty')
        };
        const dialog = dialogFor(data);
        const form = root.document.getElementById('watch-action-form');
        if (!dialog || !form || !root.quickActions) return;

        const confirmed = await root.quickActions.confirm(dialog);
        if (!confirmed) return;

        form.setAttribute('action', button.getAttribute('data-action-url'));
        form.querySelector('[name="watchId"]').value = button.getAttribute('data-watch-id');
        if (typeof form.requestSubmit === 'function') form.requestSubmit();
        else form.submit();
    }

    function init() {
        root.document.addEventListener('click', function (event) {
            const target = event.target && event.target.closest ? event.target : null;
            if (!target) return;
            if (target.closest('[data-settings-toggle]')) {
                toggleSettings();
                return;
            }
            const button = target.closest('[data-watch-action]');
            if (button) runWatchAction(button);
        });
    }

    if (root.document) {
        if (root.document.readyState === 'loading') {
            root.document.addEventListener('DOMContentLoaded', init);
        } else {
            init();
        }
    }

    return { dialogFor, toggleSettings };
});
