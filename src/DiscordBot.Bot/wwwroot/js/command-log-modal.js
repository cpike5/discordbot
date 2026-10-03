/**
 * command-log-modal.js - the command log details dialog on /Commands.
 *
 * The dialog is a quickActions dialog (_CommandLogDetailsModal.cshtml), so focus trap, Escape,
 * scroll lock, inert background and focus return come from quick-actions.js. This file only
 * fills it: it fetches the details partial through ApiClient, shows a skeleton while that takes
 * a moment, and shows the server's message with Retry when it fails.
 *
 *   commandLogModal.open(id, { trigger, onClose })
 *   commandLogModal.close()
 *
 * It never touches the URL hash (that belongs to the tabs). commands-page.js keeps the open log
 * in the `log` query parameter through `onClose`.
 */
(function () {
    'use strict';

    var dialog = document.getElementById('commandLogDetailsModal');
    var content = document.getElementById('commandLogModalContent');
    if (!dialog || !content) return;

    var controller = null;
    var token = 0;

    function detailsHref(link) {
        // The full page's Back link returns to this exact view
        var returnUrl = window.location.pathname + window.location.search;
        var base = link.getAttribute('href').split('?')[0];
        return base + '?returnUrl=' + encodeURIComponent(returnUrl);
    }

    function load(id) {
        if (controller) controller.abort();
        var mine = ++token;
        controller = new AbortController();

        var skeleton = window.Skeleton.show(content, { kind: 'lines', count: 8, label: 'Loading command log' });

        return window.ApiClient.getHtml('/api/commands/log-details/' + encodeURIComponent(id), { signal: controller.signal })
            .then(function (html) {
                if (mine !== token) return;
                skeleton.hide();
                content.innerHTML = html;
                var link = content.querySelector('[data-log-details-link]');
                if (link) link.setAttribute('href', detailsHref(link));
            })
            .catch(function (error) {
                if (mine !== token || (error && error.name === 'AbortError')) return;
                skeleton.hide();
                window.EmptyState.error(content, {
                    title: 'Could not load this log',
                    description: error && error.message ? error.message : 'Try again.',
                    size: 'compact',
                    onRetry: error && error.status === 404 ? null : function () { load(id); }
                });
            });
    }

    function open(id, options) {
        options = options || {};
        if (!id) return;
        var userOnClose = options.onClose;
        // Focus returns to the button that opened it (quickActions remembers the active element)
        if (options.trigger && typeof options.trigger.focus === 'function') options.trigger.focus();

        window.quickActions.openDialog(dialog, {
            onClose: function () {
                token++;
                if (controller) controller.abort();
                controller = null;
                if (userOnClose) userOnClose();
            }
        });
        load(id);
    }

    function close() {
        window.quickActions.closeDialog(dialog);
    }

    window.commandLogModal = { open: open, close: close };
})();
