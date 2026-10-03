/**
 * Rat Watch incidents page (Pages/Guilds/RatWatch/Incidents.cshtml): the incident details dialog.
 *
 * A row or card with [data-incident-id] (or its [data-incident-view] button) opens the dialog,
 * which loads the incident through ApiClient and is built with textContent only: names and the
 * custom message come from guild members and are never parsed as markup. A failed load shows a
 * plain message with Retry. The dialog itself is the shared quickActions dialog layer.
 *
 * Exposed as window.RatWatchIncidents (and module.exports for tests).
 */
(function (root, factory) {
    'use strict';
    const api = factory(root);
    if (typeof module !== 'undefined' && module.exports) {
        module.exports = api;
    }
    if (typeof window !== 'undefined' && root === window) {
        window.RatWatchIncidents = api;
    }
})(typeof window !== 'undefined' ? window : globalThis, function (root) {
    'use strict';

    const STATUS_CLASSES = {
        Guilty: 'bg-error/20 text-error',
        NotGuilty: 'bg-success/20 text-success',
        Voting: 'bg-accent-blue/20 text-accent-blue',
        Pending: 'bg-warning/20 text-warning',
        ClearedEarly: 'bg-success/20 text-success',
        Cancelled: 'bg-error/20 text-error'
    };
    const STATUS_DEFAULT = 'bg-bg-tertiary text-text-secondary';

    /** A Discord id is digits only; anything else never reaches a link. */
    function isSnowflake(value) {
        return typeof value === 'string' && /^\d{1,20}$/.test(value);
    }

    function el(tag, className, text) {
        const node = root.document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined && text !== null) node.textContent = text;
        return node;
    }

    function formatTime(iso) {
        return root.Format && root.Format.formatDate ? root.Format.formatDate(iso, 'datetime') : String(iso || '');
    }

    function card(label) {
        const box = el('div', 'bg-bg-primary border border-border-primary rounded-lg p-4');
        box.appendChild(el('h3', 'text-xs font-medium text-text-tertiary uppercase tracking-wider mb-3', label));
        return box;
    }

    function person(label, name, id) {
        const box = card(label);
        box.appendChild(el('p', 'text-sm font-medium text-text-primary break-words', name));
        if (isSnowflake(id)) box.appendChild(el('p', 'text-xs text-text-tertiary font-mono mt-1', id));
        return box;
    }

    // Whole class names, so Tailwind (which scans wwwroot/js) generates them
    const VOTE_TONES = {
        error: { box: 'bg-error/10 border-error/20', text: 'text-error' },
        success: { box: 'bg-success/10 border-success/20', text: 'text-success' }
    };

    function voteBox(label, count, tone) {
        const classes = VOTE_TONES[tone];
        const box = el('div', 'rounded-lg p-3 border ' + classes.box);
        const row = el('div', 'flex items-center justify-between');
        row.appendChild(el('span', 'text-sm font-medium ' + classes.text, label));
        row.appendChild(el('span', 'text-lg font-bold ' + classes.text, String(count)));
        box.appendChild(row);
        return box;
    }

    /**
     * Builds the dialog body for an incident.
     * @param {object} data the IncidentDetail handler's JSON
     * @param {string} guildId
     * @returns {DocumentFragment}
     */
    function buildDetail(data, guildId) {
        const frag = root.document.createDocumentFragment();

        const people = el('div', 'grid grid-cols-1 md:grid-cols-2 gap-4 mb-4');
        people.appendChild(person('Accused', data.accusedUsername, data.accusedUserId));
        people.appendChild(person('Initiated by', data.initiatorUsername, data.initiatorUserId));
        frag.appendChild(people);

        const details = card('Details');
        const dates = el('div', 'grid grid-cols-1 sm:grid-cols-2 gap-3 text-sm');
        const scheduled = el('div');
        scheduled.appendChild(el('span', 'text-text-tertiary', 'Scheduled: '));
        scheduled.appendChild(el('span', 'text-text-primary', formatTime(data.scheduledAt)));
        const created = el('div');
        created.appendChild(el('span', 'text-text-tertiary', 'Created: '));
        created.appendChild(el('span', 'text-text-primary', formatTime(data.createdAt)));
        dates.appendChild(scheduled);
        dates.appendChild(created);
        details.appendChild(dates);
        if (data.customMessage) {
            const message = el('div', 'mt-4');
            message.appendChild(el('span', 'text-xs font-medium text-text-tertiary uppercase tracking-wider', 'Custom message'));
            message.appendChild(el('p', 'mt-1 text-sm text-text-primary bg-bg-tertiary border border-border-secondary rounded p-3 break-words', data.customMessage));
            details.appendChild(message);
        }
        details.classList.add('mb-4');
        frag.appendChild(details);

        if (data.totalVotes > 0) {
            const votes = card('Vote breakdown');
            const grid = el('div', 'grid grid-cols-2 gap-4');
            grid.appendChild(voteBox('Guilty', data.guiltyVotes, 'error'));
            grid.appendChild(voteBox('Not guilty', data.notGuiltyVotes, 'success'));
            votes.appendChild(grid);
            votes.classList.add('mb-4');
            frag.appendChild(votes);
        }

        if (isSnowflake(guildId) && isSnowflake(data.channelId) && isSnowflake(data.originalMessageId)) {
            const row = el('div', 'flex items-center justify-between gap-3 p-3 bg-bg-tertiary border border-border-secondary rounded-lg');
            row.appendChild(el('span', 'text-sm text-text-secondary', 'Original Discord message'));
            const link = el('a', 'text-sm text-accent-blue hover:text-accent-blue-hover transition-colors', 'View in Discord');
            link.href = 'https://discord.com/channels/' + guildId + '/' + data.channelId + '/' + data.originalMessageId;
            link.target = '_blank';
            link.rel = 'noopener noreferrer';
            link.appendChild(el('span', 'sr-only', ' (opens in a new tab)'));
            row.appendChild(link);
            frag.appendChild(row);
        }
        return frag;
    }

    function setBadge(badge, data) {
        badge.textContent = data.statusText || '';
        badge.className = 'inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold ' +
            (STATUS_CLASSES[data.status] || STATUS_DEFAULT);
    }

    function init() {
        const doc = root.document;
        const dialog = doc.getElementById('incidentModal');
        const body = doc.getElementById('modalBody');
        const badge = doc.getElementById('modalStatusBadge');
        const page = doc.querySelector('[data-incident-page]');
        if (!dialog || !body || !badge || !page || !root.quickActions) return;

        const guildId = page.getAttribute('data-guild-id');
        const urlTemplate = page.getAttribute('data-detail-url');
        let current = null;

        async function load(incidentId) {
            current = incidentId;
            badge.textContent = '';
            body.textContent = '';
            body.appendChild(el('p', 'text-text-secondary', 'Loading incident...'));
            try {
                const data = await root.ApiClient.get(urlTemplate.replace('__id__', encodeURIComponent(incidentId)));
                if (current !== incidentId) return;
                setBadge(badge, data);
                body.textContent = '';
                body.appendChild(buildDetail(data, guildId));
            } catch (error) {
                if (current !== incidentId) return;
                if (error && error.sessionExpired) {
                    body.textContent = '';
                    return;
                }
                if (root.EmptyState && root.EmptyState.error) {
                    root.EmptyState.error(body, {
                        title: 'Could not load this incident',
                        description: error && error.status === 404
                            ? 'It may have been removed. Close this and reload the list.'
                            : 'Something went wrong while loading it. Try again.',
                        size: 'compact',
                        onRetry: function () { load(incidentId); }
                    });
                } else {
                    body.textContent = 'This incident could not be loaded.';
                }
            }
        }

        function open(incidentId) {
            root.quickActions.openDialog(dialog, {
                initialFocus: '[data-modal-initial-focus]',
                onClose: function () { current = null; }
            });
            load(incidentId);
        }

        doc.addEventListener('click', function (event) {
            const target = event.target && event.target.closest ? event.target : null;
            if (!target) return;
            const view = target.closest('[data-incident-view]');
            if (view) {
                open(view.getAttribute('data-incident-view'));
                return;
            }
            // A click on the row (not on a name preview, link or button inside it) opens it too
            if (target.closest('a, button, .preview-trigger')) return;
            const row = target.closest('[data-incident-id]');
            if (row) open(row.getAttribute('data-incident-id'));
        });
    }

    if (root.document) {
        if (root.document.readyState === 'loading') {
            root.document.addEventListener('DOMContentLoaded', init);
        } else {
            init();
        }
    }

    return { buildDetail, isSnowflake };
});
