/**
 * Notification history (/Admin/Notifications): selection, bulk actions and row actions.
 *
 * Every action updates the list in place, so the scroll position, the open filter panel and the
 * selection survive (they used to be thrown away by a full reload). The server renders each row
 * twice, as a table row and as a card; both carry `data-notification-row`, and everything here
 * works on all copies of an id. Requests go through ApiClient and results through toast.
 *
 * The pure helpers at the top are exported for the unit tests in __tests__/notification-history.test.js.
 */
(function (root, factory) {
    'use strict';
    const api = factory();
    if (typeof module === 'object' && module.exports) {
        module.exports = api;
    }
    if (typeof document !== 'undefined') {
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', function () { api.init(); });
        } else {
            api.init();
        }
    }
})(typeof self !== 'undefined' ? self : this, function () {
    'use strict';

    const FILTER_PANEL_KEY = 'notificationHistoryFilterExpanded';

    // ---- pure helpers ------------------------------------------------------

    /**
     * Whether a row stays in the list after its read state changes. A list filtered to Unread
     * loses a row the moment it is read, and a list filtered to Read loses it when it is marked
     * unread; with no read filter the row stays.
     * @param {string} readFilter 'true', 'false' or '' (no filter)
     * @param {boolean} isReadNow the row's new read state
     */
    function staysInList(readFilter, isReadNow) {
        if (readFilter !== 'true' && readFilter !== 'false') return true;
        return (readFilter === 'true') === Boolean(isReadNow);
    }

    /** "Showing 1 to 25 of 60 notifications", from the numbers on the summary element. */
    function summaryText(start, end, total) {
        const fmt = typeof Format !== 'undefined' ? Format : null;
        const num = (n) => (fmt ? fmt.number(n) : String(n));
        const noun = fmt ? fmt.plural(total, 'notification') : (total === 1 ? '1 notification' : total + ' notifications');
        return 'Showing ' + num(start) + ' to ' + num(end) + ' of ' + noun;
    }

    /** The message of the Delete all confirmation. */
    function deleteAllMessage(total, scope) {
        const count = total === 1 ? '1 notification' : total + ' notifications';
        return 'This permanently deletes ' + scope + ' (' + count + ' on this list). It cannot be undone.';
    }

    // ---- DOM helpers -------------------------------------------------------

    function resultsRoot() {
        return document.querySelector('[data-notification-results]');
    }

    function rowsFor(id) {
        return document.querySelectorAll('[data-notification-row="' + CSS.escape(id) + '"]');
    }

    function allRows() {
        return document.querySelectorAll('[data-notification-row]');
    }

    function uniqueIds() {
        return Array.from(new Set(Array.from(allRows()).map((row) => row.dataset.notificationRow)));
    }

    // ---- selection ---------------------------------------------------------

    function selectedIds() {
        return Array.from(new Set(
            Array.from(document.querySelectorAll('.notification-checkbox:checked')).map((cb) => cb.value)));
    }

    function setChecked(id, checked) {
        document.querySelectorAll('.notification-checkbox[value="' + CSS.escape(id) + '"]').forEach((cb) => {
            cb.checked = checked;
        });
    }

    function syncBulkBar() {
        const ids = selectedIds();
        const bar = document.getElementById('bulkActions');
        const count = document.getElementById('selectedCount');
        if (bar) bar.classList.toggle('hidden', ids.length === 0);
        if (count) count.textContent = String(ids.length);

        const selectAll = document.getElementById('selectAll');
        if (selectAll) {
            const total = uniqueIds().length;
            selectAll.checked = total > 0 && ids.length === total;
            selectAll.indeterminate = ids.length > 0 && ids.length < total;
        }
    }

    // ---- in-place updates --------------------------------------------------

    function updateTotals(delta) {
        const results = resultsRoot();
        if (!results) return;

        const total = Math.max(0, (parseInt(results.dataset.total, 10) || 0) + delta);
        results.dataset.total = String(total);

        const badge = document.querySelector('[data-total-badge] .badge');
        if (badge) badge.textContent = Format.number(total);

        const summary = results.querySelector('[data-results-summary]');
        if (summary) {
            const start = parseInt(summary.dataset.start, 10) || 0;
            const end = Math.max(0, (parseInt(summary.dataset.end, 10) || 0) + Math.min(0, delta));
            summary.dataset.total = String(total);
            summary.dataset.end = String(end);
            summary.classList.toggle('hidden', total === 0);
            summary.textContent = summaryText(total === 0 ? 0 : Math.min(start, end), end, total);
        }

        const deleteAll = document.getElementById('deleteAll');
        if (deleteAll) deleteAll.disabled = total === 0;
    }

    /** Shows the empty state once the last row on the page is gone. */
    function showEmptyIfNeeded() {
        const results = resultsRoot();
        if (!results || allRows().length > 0) return;

        const total = parseInt(results.dataset.total, 10) || 0;
        const hasFilters = results.dataset.hasFilters === 'true';
        const pageUrl = results.dataset.pageUrl || window.location.pathname;

        const options = total > 0
            // Rows exist on other pages: this page ran out, so send the user to the first one
            ? { type: 'noData', title: 'Nothing left on this page', description: 'More notifications are on other pages.',
                action: { text: 'Go to the first page', url: pageUrl, iconPath: '' } }
            : hasFilters
                ? { type: 'noResults', title: 'No notifications match your filters', description: 'Try different filters, or clear them to see every notification.',
                    action: { text: 'Clear filters', url: window.location.pathname, iconPath: '' } }
                : { type: 'noData', title: 'No notifications', description: 'You are all caught up.' };

        const body = results.querySelector('[data-results-body]');
        if (body) {
            const row = document.createElement('tr');
            row.setAttribute('data-results-empty', '');
            const cell = document.createElement('td');
            cell.colSpan = 7;
            cell.className = 'px-6 py-12 text-center';
            row.appendChild(cell);
            body.appendChild(row);
            EmptyState.render(cell, options);
        }

        const mobile = results.querySelector('[data-results-mobile]');
        if (mobile) {
            const card = document.createElement('div');
            card.className = 'bg-bg-secondary border border-border-primary rounded-lg p-8';
            card.setAttribute('data-results-empty', '');
            mobile.appendChild(card);
            EmptyState.render(card, options);
        }

        const pagination = results.querySelector('[data-results-pagination]');
        if (pagination && total === 0) pagination.remove();
    }

    function removeRows(ids) {
        let removed = 0;
        const gone = new Set(ids);
        gone.forEach((id) => {
            const rows = rowsFor(id);
            if (rows.length) removed += 1;
            rows.forEach((row) => row.remove());
            setChecked(id, false);
        });
        if (removed > 0) updateTotals(-removed);
        showEmptyIfNeeded();
        syncBulkBar();
    }

    function setRowRead(id, isRead) {
        rowsFor(id).forEach((row) => {
            row.dataset.isRead = String(isRead);

            const title = row.querySelector('[data-notification-title]');
            if (title) title.classList.toggle('font-semibold', !isRead);

            // Table rows tint when unread; cards get an accent edge
            if (row.tagName === 'TR') {
                row.classList.toggle('bg-bg-hover/30', !isRead);
            } else {
                row.classList.toggle('border-l-4', !isRead);
                row.classList.toggle('border-l-accent-blue', !isRead);
            }

            const unread = row.querySelector('[data-status="unread"]');
            const read = row.querySelector('[data-status="read"]');
            if (unread) unread.hidden = isRead;
            if (read) read.hidden = !isRead;

            const toggle = row.querySelector('[data-action="toggle-read"]');
            if (toggle) toggle.textContent = isRead ? 'Mark unread' : 'Mark read';
        });
    }

    /** Applies a read-state change to the list: patch the row, or drop it when the filter says so. */
    function applyReadChange(ids, isRead) {
        const readFilter = (resultsRoot() || { dataset: {} }).dataset.filterIsRead || '';
        if (staysInList(readFilter, isRead)) {
            ids.forEach((id) => setRowRead(id, isRead));
        } else {
            removeRows(ids);
        }
    }

    // ---- requests ----------------------------------------------------------

    /** Runs a request for a button: disabled while it runs, error toast on failure. */
    async function run(button, request, errorMessage) {
        if (button) {
            button.disabled = true;
            button.setAttribute('aria-busy', 'true');
        }
        try {
            return { ok: true, data: await request() };
        } catch (error) {
            ApiClient.showErrorToast(error && error.message ? error : errorMessage);
            return { ok: false };
        } finally {
            if (button) {
                button.disabled = false;
                button.removeAttribute('aria-busy');
            }
        }
    }

    async function markSelectedRead(button) {
        const ids = selectedIds();
        if (ids.length === 0) return;

        const result = await run(button, () => ApiClient.post('/api/notifications/mark-read', ids),
            'Could not mark those notifications as read.');
        if (!result.ok) return;

        applyReadChange(ids, true);
        ids.forEach((id) => setChecked(id, false));
        syncBulkBar();
        toast.success(Format.plural(ids.length, 'notification') + ' marked as read.');
    }

    async function deleteSelected(button) {
        const ids = selectedIds();
        if (ids.length === 0) return;

        const confirmed = await quickActions.confirm({
            title: 'Delete notifications',
            message: 'Delete ' + Format.plural(ids.length, 'selected notification') + '? This cannot be undone.',
            variant: 'danger',
            confirmText: 'Delete'
        });
        if (!confirmed) return;

        const result = await run(button, () => ApiClient.post('/api/notifications/delete', ids),
            'Could not delete those notifications.');
        if (!result.ok) return;

        removeRows(ids);
        toast.success(Format.plural(ids.length, 'notification') + ' deleted.');
    }

    async function markAllRead(button) {
        // Harmless and undoable per row, so no confirmation (only destructive actions ask)
        const result = await run(button, () => ApiClient.post('/api/notifications/mark-all-read', null),
            'Could not mark all notifications as read.');
        if (!result.ok) return;

        applyReadChange(uniqueIds(), true);
        syncBulkBar();
        toast.success('All notifications marked as read.');
    }

    async function deleteAll(button) {
        const results = resultsRoot();
        const total = results ? parseInt(results.dataset.total, 10) || 0 : 0;
        const scope = button.dataset.deleteScope || 'every notification';

        const confirmed = await quickActions.typedConfirm({
            title: 'Delete all notifications',
            message: deleteAllMessage(total, scope),
            requiredText: 'DELETE',
            variant: 'danger',
            confirmText: 'Delete all'
        });
        if (!confirmed) return;

        const query = button.dataset.deleteQuery || '';
        const result = await run(button, () => ApiClient.post('/api/notifications/delete-all' + query, null),
            'Could not delete the notifications.');
        if (!result.ok) return;

        const deleted = typeof result.data === 'number' ? result.data : total;

        // Everything this list showed is gone: clear the page, no reload needed. The total goes
        // to zero first, because every notification the filters matched was deleted, including
        // the ones on other pages.
        if (results) results.dataset.total = '0';
        removeRows(uniqueIds());
        updateTotals(0);
        toast.success(Format.plural(deleted, 'notification') + ' deleted.');
    }

    async function toggleRead(button) {
        const id = button.dataset.id;
        const row = button.closest('[data-notification-row]');
        const isRead = row ? row.dataset.isRead === 'true' : false;
        const url = '/api/notifications/' + encodeURIComponent(id) + '/' + (isRead ? 'unread' : 'read');

        const result = await run(button, () => ApiClient.post(url, null), 'Could not update the notification.');
        if (!result.ok) return;

        applyReadChange([id], !isRead);
        syncBulkBar();
    }

    async function deleteOne(button) {
        const id = button.dataset.id;
        const confirmed = await quickActions.confirm({
            title: 'Delete notification',
            message: 'Delete this notification? This cannot be undone.',
            variant: 'danger',
            confirmText: 'Delete'
        });
        if (!confirmed) return;

        const result = await run(button, () => ApiClient.del('/api/notifications/' + encodeURIComponent(id)),
            'Could not delete the notification.');
        if (!result.ok) return;

        removeRows([id]);
        toast.success('Notification deleted.');
    }

    // ---- filter panel and presets -----------------------------------------

    function readPanelState() {
        try {
            const saved = localStorage.getItem(FILTER_PANEL_KEY);
            if (saved === 'true' || saved === 'false') return saved === 'true';
        } catch (e) { /* storage blocked: use the default */ }
        return null;
    }

    function setPanelExpanded(expanded, persist) {
        const content = document.getElementById('filterContent');
        const toggle = document.getElementById('filterToggle');
        const chevron = document.getElementById('filterChevron');
        if (!content || !toggle) return;

        content.hidden = !expanded;
        toggle.setAttribute('aria-expanded', String(expanded));
        if (chevron) chevron.style.transform = expanded ? 'rotate(0deg)' : 'rotate(-90deg)';

        if (persist) {
            try { localStorage.setItem(FILTER_PANEL_KEY, String(expanded)); } catch (e) { /* ignore */ }
        }
    }

    function initFilterPanel() {
        const panel = document.querySelector('[data-filter-panel]');
        if (!panel) return;

        const saved = readPanelState();
        const hasFilters = panel.dataset.hasActiveFilters === 'true';
        // With no saved choice: open when filters are applied or the screen is wide enough that
        // the panel does not push the results out of view
        const roomy = window.matchMedia && window.matchMedia('(min-width: 1024px)').matches;
        setPanelExpanded(saved !== null ? saved : (hasFilters || roomy), false);
    }

    function highlightPreset() {
        const start = document.getElementById('StartDate');
        const end = document.getElementById('EndDate');
        const active = start && end && window.DateRangeFilter
            ? window.DateRangeFilter.detectPreset(start.value, end.value)
            : null;

        document.querySelectorAll('[data-date-preset]').forEach((button) => {
            const on = button.dataset.datePreset === active;
            button.setAttribute('aria-pressed', String(on));
            button.classList.toggle('btn-primary', on);
            button.classList.toggle('btn-secondary', !on);
        });
    }

    function applyPreset(button) {
        const start = document.getElementById('StartDate');
        const end = document.getElementById('EndDate');
        if (!window.DateRangeFilter || !window.DateRangeFilter.applyPreset(start, end, button.dataset.datePreset)) return;

        highlightPreset();
        const form = document.getElementById('filterForm');
        if (form && typeof form.requestSubmit === 'function') form.requestSubmit();
        else if (form) form.submit();
    }

    // ---- wiring ------------------------------------------------------------

    let initialized = false;

    function init() {
        if (initialized || typeof document === 'undefined') return;
        initialized = true;

        initFilterPanel();
        highlightPreset();
        syncBulkBar();

        document.addEventListener('change', (event) => {
            const target = event.target;
            if (!(target instanceof Element)) return;

            if (target.id === 'selectAll') {
                uniqueIds().forEach((id) => setChecked(id, target.checked));
                syncBulkBar();
            } else if (target.classList.contains('notification-checkbox')) {
                // The table row and the card share an id: keep both copies in step
                setChecked(target.value, target.checked);
                syncBulkBar();
            } else if (target.id === 'StartDate' || target.id === 'EndDate') {
                highlightPreset();
            }
        });

        document.addEventListener('click', (event) => {
            const target = event.target;
            if (!(target instanceof Element)) return;

            const presetButton = target.closest('[data-date-preset]');
            if (presetButton) return applyPreset(presetButton);

            if (target.closest('[data-filter-toggle]')) {
                const toggle = document.getElementById('filterToggle');
                return setPanelExpanded(toggle.getAttribute('aria-expanded') !== 'true', true);
            }

            const action = target.closest('[data-action]');
            if (action && action.closest('[data-notification-row]')) {
                if (action.dataset.action === 'toggle-read') return toggleRead(action);
                if (action.dataset.action === 'delete') return deleteOne(action);
            }

            const button = target.closest('button');
            if (!button) return;
            if (button.id === 'markSelectedRead') return markSelectedRead(button);
            if (button.id === 'deleteSelected') return deleteSelected(button);
            if (button.id === 'markAllRead') return markAllRead(button);
            if (button.id === 'deleteAll') return deleteAll(button);
        });
    }

    return { init, staysInList, summaryText, deleteAllMessage };
});
