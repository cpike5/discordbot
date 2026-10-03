/**
 * Flagged events list: selection, confirmation before a review action, and filter checks.
 *
 * Review actions are ordinary form posts (the server redirects back to the same filtered page
 * and shows the result as a toast), so the outcome survives the redirect and a partial failure
 * is reported with counts. This script only asks first and, for the bulk form, writes the
 * selected ids into the form. Ids are GUIDs and stay strings.
 */
(function () {
    'use strict';

    var selection = null;

    function describeCount(count) {
        return Format.plural(count, 'selected event', 'selected events');
    }

    /** One hidden input per selected event, so the server binds them as a list. */
    function writeBulkIds(form) {
        var holder = form.querySelector('[data-bulk-ids]');
        if (!holder) return;
        holder.replaceChildren();
        selection.ids().forEach(function (id) {
            var input = document.createElement('input');
            input.type = 'hidden';
            input.name = 'ids';
            input.value = id;
            holder.appendChild(input);
        });
    }

    function setupConfirmedSubmits() {
        // Capture phase: this runs before the global double-submit guard, which skips a
        // submit that something cancelled, then guards the confirmed one that follows.
        document.addEventListener('submit', async function (e) {
            var form = e.target;
            if (!(form instanceof HTMLFormElement) || !form.hasAttribute('data-event-action-form')) return;
            if (form.dataset.confirmed === 'true') {
                delete form.dataset.confirmed;
                return;
            }

            var submitter = e.submitter;
            if (!submitter) return;

            e.preventDefault();

            var isBulk = form.id === 'bulkForm';
            if (isBulk) {
                if (!selection || selection.count() === 0) {
                    toast.warning('Select at least one event first.');
                    return;
                }
                writeBulkIds(form);
            }

            var message = submitter.dataset.confirmMessage || 'Are you sure?';
            if (isBulk) message = message.replace('{count}', describeCount(selection.count()));
            // A preset outcome button carries its text as its value
            message = message.replace('{value}', submitter.value || '');

            var confirmed = await quickActions.confirm({
                title: submitter.dataset.confirmTitle || 'Confirm',
                message: message,
                variant: 'warning',
                confirmText: submitter.dataset.confirmText || 'Confirm'
            });
            if (!confirmed) return;

            form.dataset.confirmed = 'true';
            form.requestSubmit(submitter);
        }, true);
    }

    /** "To date" must not be earlier than "From date"; the browser shows the message. */
    function setupDateValidation() {
        var from = document.getElementById('filterDateFrom');
        var to = document.getElementById('filterDateTo');
        if (!from || !to) return;

        function check() {
            var invalid = from.value && to.value && to.value < from.value;
            to.setCustomValidity(invalid ? '"To date" must be on or after "From date".' : '');
        }
        from.addEventListener('input', check);
        to.addEventListener('input', check);
        check();
    }

    /** Buttons with data-open-dialog="id" open that element as a dialog. */
    function setupDialogOpeners() {
        document.addEventListener('click', function (e) {
            var opener = e.target.closest && e.target.closest('[data-open-dialog]');
            if (!opener) return;
            var dialog = document.getElementById(opener.dataset.openDialog);
            if (dialog) quickActions.openDialog(dialog);
        });
    }

    function init() {
        setupDialogOpeners();
        if (typeof BulkSelection !== 'undefined') {
            selection = BulkSelection.init({ noun: ['event', 'events'] });
        }
        setupConfirmedSubmits();
        setupDateValidation();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
