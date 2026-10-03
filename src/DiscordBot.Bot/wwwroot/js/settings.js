/**
 * Settings page (Admin > Settings).
 *
 * Markup contract (Pages/Admin/Settings.cshtml):
 *  - Tabs are the shared tab panel (id `settingsTabs`, tab-panel.js); this file keeps `?category=`
 *    in the address in step with the active tab, so a reload or a shared link opens the same tab.
 *  - Each tab that saves is its own `<form data-settings-form="General" data-settings-handler="SaveCategory"
 *    data-unsaved-changes>`. A save posts only that form's fields, so a tab never overwrites another.
 *    unsaved-changes.js tracks which forms are dirty; Save all saves exactly the dirty ones.
 *  - Reset buttons open static confirm modals (`data-modal-open`); those post to the page, which
 *    redirects, and quick-actions.js reloads the page once so the outcome toast shows.
 *  - The Bot Control tab polls /api/bot/status while it is visible. A restart follows the dashboard's
 *    flow (BotStatus.watchRestart, bot-status-refresh.js); there is no second restart loop here.
 *
 * Exposed as window.settingsManager (browser) and module.exports (Node/tests; the pure helpers).
 */
(function (root, factory) {
    if (typeof module === 'object' && module.exports) {
        module.exports = factory(root);
    } else {
        root.settingsManager = factory(root);
        if (typeof document !== 'undefined') {
            if (document.readyState === 'loading') {
                document.addEventListener('DOMContentLoaded', root.settingsManager.init);
            } else {
                root.settingsManager.init();
            }
        }
    }
})(typeof self !== 'undefined' ? self : this, function (root) {
    'use strict';

    var TABS_ID = 'settingsTabs';
    var STATUS_ENDPOINT = '/api/bot/status';
    var STATUS_POLL_MS = 5000;
    var TAB_LABELS = {
        General: 'General', Features: 'Features', Commands: 'Commands', Advanced: 'Advanced',
        BotControl: 'Bot Control', AiModels: 'AI Models', Appearance: 'Appearance'
    };

    // ------------------------------------------------------------------ pure helpers

    function plural(count, one, other) {
        return count === 1 ? one : other;
    }

    /**
     * The name/value pairs a tab posts. Checkboxes always say "true" or "false" (an unchecked box
     * would otherwise post nothing, which the server cannot tell from "not on this form"); radios
     * count only when checked; disabled controls, buttons and framework fields (`__...`) are left out.
     * @param {Iterable<Object>} controls form.elements, or control-like objects in tests
     * @returns {Array<[string, string]>}
     */
    function buildEntries(controls) {
        var entries = [];
        Array.prototype.forEach.call(controls, function (control) {
            if (!control || !control.name || control.disabled) return;
            if (control.name.indexOf('__') === 0) return;
            var type = String(control.type || '').toLowerCase();
            if (type === 'submit' || type === 'button' || type === 'reset' || type === 'image' || type === 'file') return;
            if (type === 'checkbox') {
                entries.push([control.name, control.checked ? 'true' : 'false']);
            } else if (type === 'radio') {
                if (control.checked) entries.push([control.name, control.value]);
            } else {
                entries.push([control.name, control.value]);
            }
        });
        return entries;
    }

    /**
     * Reads a save answer. `unchanged` is a save that went through but changed nothing (the server
     * says so with changeCount 0); it is reported as that, never as a save.
     * @param {{ok: boolean, data: Object}} result an ApiClient raw result
     * @returns {{kind: 'saved'|'unchanged'|'error', message: string, restartRequired: boolean}}
     */
    function readSaveResult(result) {
        var data = (result && result.data && typeof result.data === 'object') ? result.data : {};
        if (result && result.ok && data.success !== false) {
            var unchanged = data.changeCount === 0;
            return {
                kind: unchanged ? 'unchanged' : 'saved',
                message: data.message || (unchanged ? 'Nothing changed.' : 'Saved.'),
                restartRequired: !!data.restartRequired
            };
        }
        var errors = Array.isArray(data.errors) ? data.errors.filter(Boolean) : [];
        return {
            kind: 'error',
            message: errors.length ? errors.join(' ') : (data.message || 'The settings could not be saved. Try again.'),
            restartRequired: false
        };
    }

    /**
     * One toast line for Save all.
     * @param {Array<{category: string, kind: string}>} outcomes
     */
    function summarizeSaveAll(outcomes) {
        var saved = outcomes.filter(function (o) { return o.kind === 'saved'; });
        var unchanged = outcomes.filter(function (o) { return o.kind === 'unchanged'; });
        var failed = outcomes.filter(function (o) { return o.kind === 'error'; });
        var names = function (list) {
            return list.map(function (o) { return TAB_LABELS[o.category] || o.category; }).join(', ');
        };
        if (outcomes.length === 0) {
            return { kind: 'info', message: 'Nothing to save. No tab has unsaved changes.' };
        }
        if (failed.length === 0) {
            if (saved.length === 0) {
                return { kind: 'info', message: 'Nothing changed. These values were already saved.' };
            }
            return {
                kind: 'success',
                message: 'Saved ' + names(saved) + ' ' + plural(saved.length, 'tab', 'tabs') + '.' +
                    (unchanged.length ? ' ' + names(unchanged) + ' ' + plural(unchanged.length, 'was', 'were') + ' already up to date.' : '')
            };
        }
        return {
            kind: 'error',
            message: (saved.length ? 'Saved ' + names(saved) + '. ' : '') +
                'Could not save ' + names(failed) + '. Open ' + plural(failed.length, 'that tab', 'those tabs') + ' to see why.'
        };
    }

    /** The category in a query string, when it names one of `valid`; otherwise null. */
    function categoryFromSearch(search, valid) {
        var match = /[?&]category=([^&#]*)/.exec(search || '');
        if (!match) return null;
        var value;
        try { value = decodeURIComponent(match[1]); } catch (e) { return null; }
        return valid.indexOf(value) >= 0 ? value : null;
    }

    /** A TimeSpan string ("d.hh:mm:ss" or "hh:mm:ss") as "3d 4h 5m" / "4h 5m 6s" / "5m 6s" / "6s". */
    function formatUptime(timeSpan) {
        if (!timeSpan) return '0s';
        var parts = String(timeSpan).split(':');
        if (parts.length !== 3) return String(timeSpan);
        var days = 0;
        var hours;
        var first = parts[0];
        if (first.indexOf('.') >= 0) {
            var dh = first.split('.');
            days = parseInt(dh[0], 10);
            hours = parseInt(dh[1], 10);
        } else {
            hours = parseInt(first, 10);
        }
        var minutes = parseInt(parts[1], 10);
        var seconds = parseInt(parts[2].split('.')[0], 10);
        if (days > 0) return days + 'd ' + hours + 'h ' + minutes + 'm';
        if (hours > 0) return hours + 'h ' + minutes + 'm ' + seconds + 's';
        if (minutes > 0) return minutes + 'm ' + seconds + 's';
        return seconds + 's';
    }

    var helpers = {
        buildEntries: buildEntries,
        readSaveResult: readSaveResult,
        summarizeSaveAll: summarizeSaveAll,
        categoryFromSearch: categoryFromSearch,
        formatUptime: formatUptime
    };

    // ------------------------------------------------------------------ page behaviour

    var statusTimer = null;
    var restarting = false;
    var shutdownRequested = false;

    function qs(selector, scope) { return (scope || document).querySelector(selector); }
    function qsa(selector, scope) { return Array.prototype.slice.call((scope || document).querySelectorAll(selector)); }

    function notify(kind, message, options) {
        if (root.toast && typeof root.toast[kind] === 'function') root.toast[kind](message, options || {});
    }

    function now() {
        return root.Format && typeof root.Format.formatDate === 'function'
            ? root.Format.formatDate(new Date(), 'time')
            : new Date().toLocaleTimeString();
    }

    function activeTabId() {
        var tab = qs('#' + TABS_ID + '-container .tab-panel-tab.active');
        return tab ? tab.dataset.tabId : null;
    }

    function validTabs() {
        return qsa('#' + TABS_ID + '-container .tab-panel-tab').map(function (tab) { return tab.dataset.tabId; });
    }

    function switchTab(category) {
        if (root.TabPanel && typeof root.TabPanel.switchTo === 'function') {
            root.TabPanel.switchTo(TABS_ID, category);
        }
    }

    // ---- tabs: address, polling, unsaved dots

    /** Keeps ?category= current without adding history entries. */
    function syncUrl(category) {
        try {
            var url = new URL(root.location.href);
            if (url.searchParams.get('category') === category) return;
            url.searchParams.set('category', category);
            root.history.replaceState(null, '', url.pathname + url.search + url.hash);
        } catch (e) { /* the address bar is a convenience */ }
    }

    function onTabChange(event) {
        var detail = event.detail || {};
        if (detail.panelId !== TABS_ID) return;
        syncUrl(detail.tabId);
        if (detail.tabId === 'BotControl') startPolling(); else stopPolling();
    }

    /** A dot on the tab (with a text alternative) while its form has unsaved edits. */
    function onUnsavedChange(event) {
        var form = event.target;
        if (!form || !form.matches || !form.matches('form[data-settings-form]')) return;
        var tab = document.getElementById(TABS_ID + '-tab-' + form.dataset.settingsForm);
        if (!tab) return;
        var dirty = !!(event.detail && event.detail.dirty);
        var marker = qs('.settings-tab-dirty', tab);
        if (dirty && !marker) {
            marker = document.createElement('span');
            marker.className = 'settings-tab-dirty';
            var dot = document.createElement('span');
            dot.className = 'tab-dirty-dot';
            dot.setAttribute('aria-hidden', 'true');
            var text = document.createElement('span');
            text.className = 'sr-only';
            text.textContent = '(unsaved changes)';
            marker.appendChild(dot);
            marker.appendChild(text);
            tab.appendChild(marker);
        } else if (!dirty && marker) {
            marker.remove();
        }
    }

    // ---- saving

    function isDirty(form) {
        return !root.UnsavedChanges || root.UnsavedChanges.isDirty(form);
    }

    function saveButtonOf(form) {
        return qs('[data-settings-save]', form);
    }

    function errorBox(form) {
        return qs('[data-settings-error]', form);
    }

    function hideError(form) {
        var box = errorBox(form);
        if (box) box.classList.add('hidden');
    }

    function showError(form, message, focus) {
        var box = errorBox(form);
        if (!box) return;
        var text = qs('[data-alert] p', box);
        if (text) text.textContent = message;
        box.classList.remove('hidden');
        if (focus) box.focus();
    }

    function payloadFor(form) {
        var body = new FormData();
        buildEntries(form.elements).forEach(function (pair) { body.append(pair[0], pair[1]); });
        return body;
    }

    function urlFor(form) {
        var handler = form.dataset.settingsHandler || 'SaveCategory';
        var category = form.dataset.settingsForm;
        return handler === 'SaveCategory'
            ? '?handler=SaveCategory&category=' + encodeURIComponent(category)
            : '?handler=' + encodeURIComponent(handler);
    }

    function showRestartBanner() {
        var banner = document.getElementById('restartBanner');
        if (banner) banner.classList.remove('hidden');
    }

    /**
     * Saves one tab. Resolves with { category, kind } (kind: saved, unchanged, error or clean).
     * @param {HTMLFormElement} form
     * @param {{quiet?: boolean}} [options] quiet: no toast here (Save all reports once)
     */
    async function saveForm(form, options) {
        var quiet = !!(options && options.quiet);
        var category = form.dataset.settingsForm;
        if (!isDirty(form)) {
            if (!quiet) notify('info', 'Nothing to save here. You have not changed anything on this tab.', { key: 'settings-clean' });
            return { category: category, kind: 'clean' };
        }

        var button = saveButtonOf(form);
        var loading = root.LoadingManager;
        hideError(form);
        if (button && loading) loading.setButtonLoading(button, true, 'Saving...');

        var outcome;
        try {
            var result = await root.ApiClient.postRaw(urlFor(form), payloadFor(form));
            if (result.sessionExpired) {
                // ApiClient already told the user to sign in again
                outcome = { kind: 'error', message: '', restartRequired: false, expired: true };
            } else {
                outcome = readSaveResult(result);
            }
        } catch (error) {
            outcome = { kind: 'error', message: (error && error.message) || 'The settings could not be saved. Try again.', restartRequired: false };
        } finally {
            if (button && loading) loading.setButtonLoading(button, false);
        }

        if (outcome.kind === 'error') {
            if (!outcome.expired) {
                showError(form, outcome.message, !quiet);
                form.dispatchEvent(new CustomEvent('settings:save-failed', { bubbles: true, detail: { category: category, message: outcome.message } }));
            }
            return { category: category, kind: 'error' };
        }

        if (root.UnsavedChanges) root.UnsavedChanges.markClean(form);
        if (outcome.restartRequired) showRestartBanner();
        if (!quiet) notify(outcome.kind === 'unchanged' ? 'info' : 'success', outcome.message, { key: 'settings-save-' + category });
        form.dispatchEvent(new CustomEvent('settings:saved', { bubbles: true, detail: { category: category, kind: outcome.kind } }));
        return { category: category, kind: outcome.kind };
    }

    /** Saves every tab with unsaved changes, one after another, and reports once. */
    async function saveAll(button) {
        var forms = qsa('form[data-settings-form]').filter(isDirty);
        var loading = root.LoadingManager;
        if (button && loading) loading.setButtonLoading(button, true, 'Saving...');
        var outcomes = [];
        try {
            for (var i = 0; i < forms.length; i++) {
                outcomes.push(await saveForm(forms[i], { quiet: true }));
            }
        } finally {
            if (button && loading) loading.setButtonLoading(button, false);
        }

        var summary = summarizeSaveAll(outcomes);
        notify(summary.kind, summary.message, { key: 'settings-save-all' });

        var firstFailed = outcomes.filter(function (o) { return o.kind === 'error'; })[0];
        if (firstFailed) {
            switchTab(firstFailed.category);
            var failedForm = qs('form[data-settings-form="' + firstFailed.category + '"]');
            var box = failedForm && errorBox(failedForm);
            if (box && !box.classList.contains('hidden')) box.focus();
        }
        return outcomes;
    }

    // ---- Bot Control

    function setText(selector, value) {
        var el = qs(selector);
        if (el) el.textContent = value;
    }

    function setIndicator(state) {
        var indicator = qs('[data-status-indicator]');
        if (!indicator) return;
        indicator.classList.remove('bg-success', 'bg-error', 'bg-warning', 'animate-pulse');
        if (state === 'online') indicator.classList.add('bg-success', 'animate-pulse');
        else if (state === 'restarting') indicator.classList.add('bg-warning', 'animate-pulse');
        else indicator.classList.add('bg-error');
    }

    function statusFailed(failed) {
        var box = document.getElementById('botStatusError');
        if (box) box.classList.toggle('hidden', !failed);
        var updated = qs('[data-last-updated]');
        if (updated) {
            updated.classList.toggle('text-error', failed);
            if (failed) updated.textContent = 'update failed at ' + now();
        }
    }

    async function refreshStatus() {
        if (!qs('[data-bot-control-status]') || restarting || shutdownRequested) return;
        try {
            var data = await root.ApiClient.get(STATUS_ENDPOINT);
            setText('[data-connection-state]', data.connectionState);
            setText('[data-latency]', data.latencyMs + ' ms');
            setText('[data-guild-count]', data.guildCount);
            setText('[data-uptime]', formatUptime(data.uptime));
            setIndicator(String(data.connectionState).toUpperCase() === 'CONNECTED' ? 'online' : 'offline');
            statusFailed(false);
            setText('[data-last-updated]', now());
        } catch (error) {
            statusFailed(true);
        }
    }

    function startPolling() {
        if (statusTimer || !qs('[data-bot-control-status]')) return;
        refreshStatus();
        statusTimer = setInterval(function () { if (!document.hidden) refreshStatus(); }, STATUS_POLL_MS);
    }

    function stopPolling() {
        if (statusTimer) {
            clearInterval(statusTimer);
            statusTimer = null;
        }
    }

    /** After "Restart bot" is confirmed: show it, wait for the bot to report Connected, say so. */
    async function afterRestart() {
        if (!root.BotStatus || typeof root.BotStatus.watchRestart !== 'function') return;
        restarting = true;
        setText('[data-connection-state]', 'Restarting');
        setIndicator('restarting');
        var button = qs('[data-restart-button]');
        if (button) button.disabled = true;
        var online;
        try {
            online = await root.BotStatus.watchRestart();
        } finally {
            restarting = false;
            if (button) button.disabled = false;
        }
        await refreshStatus();
        if (online) notify('success', 'The bot is back online.');
        else notify('warning', 'The bot has not reconnected yet. Check the status again in a moment, or look at the logs.');
    }

    /** After "Shut down bot" is confirmed: stop asking, and say what happens next. */
    function afterShutdown() {
        shutdownRequested = true;
        stopPolling();
        var notice = document.getElementById('botShutdownNotice');
        if (notice) notice.classList.remove('hidden');
        setText('[data-connection-state]', 'Shutting down');
        setIndicator('offline');
        ['[data-restart-button]', '[data-shutdown-button]'].forEach(function (selector) {
            var button = qs(selector);
            if (button) button.disabled = true;
        });
    }

    // ---- events

    function onClick(event) {
        var target = event.target;
        if (!target || !target.closest) return;

        var opener = target.closest('[data-modal-open]');
        if (opener) {
            if (root.quickActions) root.quickActions.showConfirmationModal(opener.getAttribute('data-modal-open'));
            return;
        }

        var saveAllButton = target.closest('[data-settings-save-all]');
        if (saveAllButton) {
            saveAll(saveAllButton);
            return;
        }

        var tabLink = target.closest('[data-settings-tab-link]');
        if (tabLink) {
            event.preventDefault();
            switchTab(tabLink.getAttribute('data-settings-tab-link'));
            var tabs = document.getElementById(TABS_ID + '-container');
            if (tabs && typeof tabs.scrollIntoView === 'function') tabs.scrollIntoView({ block: 'start' });
            return;
        }

        if (target.closest('[data-status-retry]')) refreshStatus();
    }

    function onSubmit(event) {
        var form = event.target;
        if (!form || !form.matches) return;

        if (form.matches('form[data-settings-form]')) {
            event.preventDefault();
            saveForm(form);
            return;
        }

        // A reset confirmation reloads the page (its toast shows after the reload). What is on the
        // page is being thrown away on purpose, so leaving is not a loss to warn about.
        var modal = form.closest && form.closest('[data-confirm-modal]');
        if (modal && /^reset-/.test(modal.id) && root.UnsavedChanges) {
            qsa('form[data-settings-form]').forEach(function (settingsForm) { root.UnsavedChanges.markClean(settingsForm); });
        }
    }

    function onConfirmed(event) {
        var id = event.detail && event.detail.modalId;
        if (id === 'restartModal') afterRestart();
        else if (id === 'shutdownModal') afterShutdown();
    }

    function init() {
        if (!document.getElementById(TABS_ID + '-container')) return;

        document.addEventListener('click', onClick);
        document.addEventListener('submit', onSubmit, true);
        document.addEventListener('tabchange', onTabChange);
        document.addEventListener('unsavedchange', onUnsavedChange);
        document.addEventListener('quickactions:confirmed', onConfirmed);
        document.addEventListener('visibilitychange', function () {
            if (!document.hidden && statusTimer) refreshStatus();
        });
        window.addEventListener('pagehide', stopPolling);

        // A shared link names its tab in the address; the server already drew it as active.
        var current = activeTabId();
        var requested = categoryFromSearch(root.location.search, validTabs());
        if (current && !requested) syncUrl(current);
        if (current === 'BotControl') startPolling();
    }

    return {
        init: init,
        switchTab: switchTab,
        saveForm: saveForm,
        saveAll: function () { return saveAll(qs('[data-settings-save-all]')); },
        refreshStatus: refreshStatus,
        // pure helpers, for tests
        buildEntries: buildEntries,
        readSaveResult: readSaveResult,
        summarizeSaveAll: summarizeSaveAll,
        categoryFromSearch: categoryFromSearch,
        formatUptime: formatUptime,
        helpers: helpers
    };
});
