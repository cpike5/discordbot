/**
 * Purge pages (bulk purge and user purge): the typed confirmation before the purge, and progress.
 *
 *  - The purge button (`[data-purge-confirm]`) asks for a typed phrase through
 *    quickActions.typedConfirm, then submits its form with requestSubmit() so the submit guard
 *    (data-submit-guard) sees it: the button shows "Purging...", the form cannot be sent twice,
 *    and the progress panel opens. The dialog's text comes from the button's data-confirm-title,
 *    -message, -required, -label and -action attributes.
 *  - The server pushes `BulkPurgeProgress` to the hub group `bulk-purge`; the panel follows it.
 *    Without the hub the panel still shows that the purge is running.
 *
 * All text comes from data-* attributes and is written with textContent.
 *
 * Exposed as window.Purge (browser) and module.exports (Node/tests).
 */
(function (root, factory) {
    if (typeof module === 'object' && module.exports) {
        module.exports = factory(root);
    } else {
        root.Purge = factory(root);
        if (typeof document !== 'undefined') {
            if (document.readyState === 'loading') {
                document.addEventListener('DOMContentLoaded', root.Purge.init);
            } else {
                root.Purge.init();
            }
        }
    }
})(typeof self !== 'undefined' ? self : this, function (root) {
    'use strict';

    /**
     * What the progress panel shows for a hub payload: a 0-100 percent (null when the total is
     * not known yet), a heading and a detail line.
     */
    function progressView(dto) {
        if (!dto) return { percent: null, message: null, detail: null };
        var total = Number(dto.totalCount);
        var done = Number(dto.processedCount);
        var percent = null;
        if (typeof dto.percentComplete === 'number') {
            percent = dto.percentComplete;
        } else if (total > 0) {
            percent = Math.round(done / total * 100);
        }
        if (percent !== null) percent = Math.max(0, Math.min(100, percent));
        var detail = total > 0 ? done.toLocaleString() + ' of ' + total.toLocaleString() + ' records' : null;
        return { percent: percent, message: dto.message || null, detail: detail };
    }

    function byId(id) { return document.getElementById(id); }

    function showProgress() {
        var panel = byId('progress-container');
        if (panel) panel.classList.remove('hidden');
    }

    function applyProgress(dto) {
        var view = progressView(dto);
        showProgress();
        if (view.message) byId('progress-message').textContent = view.message;
        if (view.detail) byId('progress-detail').textContent = view.detail;
        if (view.percent !== null) {
            byId('progress-bar').style.width = view.percent + '%';
            byId('progress-percent').textContent = view.percent + '%';
            var track = byId('progress-track');
            if (track) track.setAttribute('aria-valuenow', String(view.percent));
        }
    }

    function joinHub() {
        var hub = root.DashboardHub;
        if (typeof hub === 'undefined' || !hub) return;
        hub.on('BulkPurgeProgress', applyProgress);
        var join = function () { hub.invoke('JoinBulkPurgeGroup'); };
        hub.on('connected', join);
        hub.on('reconnected', join);
        if (typeof hub.getIsConnected === 'function' && hub.getIsConnected()) join();
    }

    async function confirmAndSubmit(button) {
        var form = button.closest('form');
        if (!form || form.dataset.submitting === 'true') return;
        var ds = button.dataset;
        var confirmed = await root.quickActions.typedConfirm({
            title: ds.confirmTitle || 'Confirm purge',
            message: ds.confirmMessage || 'This cannot be undone.',
            requiredText: ds.confirmRequired || 'CONFIRM',
            inputLabel: ds.confirmLabel,
            variant: 'danger',
            confirmText: ds.confirmAction || 'Purge'
        });
        if (confirmed) form.requestSubmit(button);
    }

    function init() {
        var form = document.querySelector('form[data-purge-form]');
        if (!form) return;
        var bulk = !!byId('progress-container');

        form.addEventListener('click', function (event) {
            var button = event.target.closest && event.target.closest('[data-purge-confirm]');
            if (!button) return;
            event.preventDefault();
            confirmAndSubmit(button);
        });

        // The guard disables the button on submit; this opens the progress panel with it.
        form.addEventListener('submit', function (event) {
            if (event.defaultPrevented) return;
            showProgress();
        });

        if (bulk) joinHub();
    }

    return { init: init, progressView: progressView };
});
