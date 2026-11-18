/**
 * Moderation profile: the server watchlist section.
 *
 * Lists the server's watchlist entries, adds a user by ID (with an optional reason) and removes
 * one after a confirmation. Every request goes through ApiClient. Usernames and reasons are
 * user-written, so they only ever reach the DOM through textContent and data-* attributes.
 * Discord IDs are strings end to end: the API writes them as strings, and they are never parsed
 * to numbers here.
 */
(function () {
    'use strict';

    // The API caps a page at 100; the section shows the first page and says when there is more.
    var PAGE_SIZE = 100;
    var SNOWFLAKE = /^\d{15,20}$/;

    var guildId = null;
    var viewedUserId = null;
    var currentUserId = null;

    function byId(id) { return document.getElementById(id); }

    function baseUrl() {
        return '/api/guilds/' + encodeURIComponent(guildId) + '/watchlist';
    }

    function reportFailure(err, fallback) {
        if (err && err.sessionExpired) return;
        toast.error((err && err.message) || fallback);
    }

    function setFormError(message) {
        var error = byId('watchlistFormError');
        var field = byId('watchlistUserId');
        if (!error || !field) return;
        error.textContent = message || '';
        error.classList.toggle('hidden', !message);
        field.classList.toggle('input-validation-error', !!message);
        field.setAttribute('aria-invalid', message ? 'true' : 'false');
    }

    /** Show the count, the list or the empty state for the entries now on the page. */
    function refreshState(total) {
        var list = byId('watchlistList');
        var count = list.querySelectorAll('[data-watchlist-entry]').length;
        list.classList.toggle('hidden', count === 0);
        byId('watchlistEmpty').classList.toggle('hidden', count !== 0);

        var label = byId('watchlistCount');
        if (count === 0) {
            label.textContent = '';
        } else if (typeof total === 'number' && total > count) {
            label.textContent = 'Showing ' + count + ' of ' + total;
        } else {
            label.textContent = count === 1 ? '1 user' : count + ' users';
        }
    }

    function buildEntry(entry) {
        var userId = String(entry.userId);

        var item = document.createElement('li');
        item.className = 'py-3 flex flex-wrap items-start gap-x-4 gap-y-2';
        item.dataset.watchlistEntry = '';
        item.dataset.userId = userId;
        item.dataset.username = entry.username || '';
        if (userId === String(viewedUserId)) item.classList.add('bg-bg-hover/50');

        var body = document.createElement('div');
        body.className = 'flex-1 min-w-[12rem]';

        var title = document.createElement('p');
        title.className = 'text-sm text-text-primary break-words';
        var name = document.createElement('span');
        name.className = 'font-medium';
        name.textContent = entry.username || 'Unknown user';
        title.appendChild(name);
        var id = document.createElement('span');
        id.className = 'ml-2 font-mono text-xs text-text-secondary break-all';
        id.textContent = userId;
        title.appendChild(id);
        if (userId === String(viewedUserId)) {
            var here = document.createElement('span');
            here.className = 'ml-2 text-xs text-warning';
            here.textContent = 'This member';
            title.appendChild(here);
        }
        body.appendChild(title);

        var reason = document.createElement('p');
        reason.className = 'text-sm text-text-secondary break-words';
        reason.setAttribute('dir', 'auto');
        reason.textContent = entry.reason || 'No reason given';
        body.appendChild(reason);

        var meta = document.createElement('p');
        meta.className = 'text-xs text-text-tertiary';
        var by = entry.addedByUsername || 'a moderator';
        var when = document.createElement('span');
        when.textContent = Format.formatDate(entry.addedAt, 'datetime-short');
        meta.appendChild(document.createTextNode('Added by ' + by + ' on '));
        meta.appendChild(when);
        body.appendChild(meta);
        item.appendChild(body);

        var remove = document.createElement('button');
        remove.type = 'button';
        remove.className = 'btn btn-ghost btn-sm text-error';
        remove.dataset.watchlistRemove = userId;
        remove.dataset.username = entry.username || '';
        remove.setAttribute('aria-label', 'Remove ' + (entry.username || userId) + ' from the watchlist');
        remove.textContent = 'Remove';
        item.appendChild(remove);

        return item;
    }

    async function load() {
        var loading = byId('watchlistLoading');
        var failure = byId('watchlistLoadError');
        var list = byId('watchlistList');

        try {
            var result = await ApiClient.get(baseUrl() + '?page=1&pageSize=' + PAGE_SIZE);
            list.textContent = '';
            (result.items || []).forEach(function (entry) { list.appendChild(buildEntry(entry)); });
            failure.classList.add('hidden');
            loading.classList.add('hidden');
            refreshState(result.totalCount);
        } catch (err) {
            loading.classList.add('hidden');
            if (err && err.sessionExpired) return;
            failure.textContent = (err && err.message) || 'Could not load the watchlist. Reload the page to try again.';
            failure.classList.remove('hidden');
        }
    }

    async function addEntry() {
        var userField = byId('watchlistUserId');
        var reasonField = byId('watchlistReason');
        var button = byId('watchlistAddBtn');

        var userId = userField.value.trim();
        if (!SNOWFLAKE.test(userId)) {
            setFormError('Enter a Discord user ID: 15 to 20 digits.');
            userField.focus();
            return;
        }
        setFormError('');

        LoadingManager.setButtonLoading(button, true, 'Adding…');
        try {
            await ApiClient.post(baseUrl(), {
                guildId: String(guildId),
                userId: userId,
                addedByUserId: String(currentUserId),
                reason: reasonField.value.trim() || null
            });
            reasonField.value = '';
            await load();
            toast.success('Added to the watchlist.');
        } catch (err) {
            // The input stays so it can be corrected and sent again
            if (err && err.status === 400) {
                setFormError(err.message);
            } else {
                reportFailure(err, 'Could not add the user. Try again.');
            }
        } finally {
            LoadingManager.setButtonLoading(button, false);
        }
    }

    async function removeEntry(button) {
        var userId = button.dataset.watchlistRemove;
        var item = button.closest('[data-watchlist-entry]');
        var name = button.dataset.username || userId;

        var confirmed = await quickActions.confirm({
            title: 'Remove from watchlist',
            message: 'Remove ' + name + ' (' + userId + ') from the watchlist?',
            variant: 'warning',
            confirmText: 'Remove'
        });
        if (!confirmed) return;

        LoadingManager.setButtonLoading(button, true, 'Removing…');
        try {
            await ApiClient.del(baseUrl() + '/' + encodeURIComponent(userId));
            if (item) item.remove();
            refreshState();
            toast.success('Removed from the watchlist.');
        } catch (err) {
            LoadingManager.setButtonLoading(button, false);
            reportFailure(err, 'Could not remove the user. Try again.');
        }
    }

    function init() {
        var section = byId('watchlistSection');
        if (!section) return;

        guildId = window.moderationGuildId;
        viewedUserId = window.moderationUserId;
        currentUserId = window.currentUserId;

        var form = byId('watchlistAddForm');
        form.addEventListener('submit', function (e) {
            e.preventDefault();
            addEntry();
        });
        byId('watchlistUserId').addEventListener('input', function () { setFormError(''); });

        section.addEventListener('click', function (e) {
            var target = e.target;
            if (!target.closest) return;
            var remove = target.closest('[data-watchlist-remove]');
            if (remove) removeEntry(remove);
        });

        load();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
