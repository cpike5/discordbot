/**
 * User moderation profile: tags and notes.
 *
 * Tabs come from tab-panel.js. Every request goes through ApiClient, every button shows a
 * pending state and the page is updated in place, so nothing reloads and no work is lost. Tag
 * names and note text are user-written, so they only ever reach the DOM through textContent
 * and data-* attributes. Discord IDs stay strings.
 */
(function () {
    'use strict';

    var guildId = null;
    var userId = null;
    var currentUserId = null;

    function byId(id) { return document.getElementById(id); }

    function usersUrl(suffix) {
        return '/api/guilds/' + encodeURIComponent(guildId) + '/users/' + encodeURIComponent(userId) + suffix;
    }

    /** Plain-language message from a failed request; null when the session toast already says it. */
    function failureMessage(err, fallback) {
        if (err && err.sessionExpired) return null;
        return (err && err.message) || fallback;
    }

    function reportFailure(err, fallback) {
        var message = failureMessage(err, fallback);
        if (message) toast.error(message);
    }

    /* ---------------------------------------------------------------- tab badges */

    /** Set the count badge on a tab, creating it when the count goes from zero. */
    function setTabCount(tabId, count) {
        var tab = byId('modProfileTabs-tab-' + tabId);
        if (!tab) return;
        var badge = tab.querySelector('.tab-badge');

        if (count <= 0) {
            if (badge) badge.remove();
            return;
        }
        if (!badge) {
            badge = document.createElement('span');
            badge.className = 'tab-badge' + (tabId === 'notes' ? ' tab-badge-info' : '');
            tab.appendChild(badge);
        }
        badge.textContent = String(count);
    }

    /* ---------------------------------------------------------------- tag menu */

    function tagMenu() {
        return {
            button: byId('addTagBtn'),
            panel: byId('tagDropdown'),
            empty: byId('tagDropdownEmpty')
        };
    }

    function setMenuOpen(open, returnFocus) {
        var menu = tagMenu();
        if (!menu.button || !menu.panel) return;
        menu.button.setAttribute('aria-expanded', String(open));
        menu.panel.classList.toggle('hidden', !open);
        if (open) {
            var first = menu.panel.querySelector('[data-add-tag]');
            if (first) first.focus();
        } else if (returnFocus) {
            menu.button.focus();
        }
    }

    function setupTagMenu() {
        var menu = tagMenu();
        if (!menu.button || !menu.panel) return;
        var container = byId('addTagDropdownContainer');

        menu.button.addEventListener('click', function () {
            setMenuOpen(menu.button.getAttribute('aria-expanded') !== 'true');
        });

        document.addEventListener('click', function (e) {
            if (!container.contains(e.target)) setMenuOpen(false);
        });

        container.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && menu.button.getAttribute('aria-expanded') === 'true') {
                e.preventDefault();
                e.stopPropagation();
                setMenuOpen(false, true);
            }
        });

        container.addEventListener('focusout', function (e) {
            if (e.relatedTarget && !container.contains(e.relatedTarget)) setMenuOpen(false);
        });
    }

    function refreshTagMenuEmptiness() {
        var menu = tagMenu();
        if (!menu.empty) return;
        var any = menu.panel.querySelector('[data-add-tag]');
        menu.empty.classList.toggle('hidden', !!any);
        if (!any) menu.empty.textContent = 'Every tag is already on this user.';
    }

    function refreshNoTagsText() {
        var none = byId('noTagsText');
        var any = byId('userTagsContainer').querySelector('[data-tag-chip]');
        if (none) none.classList.toggle('hidden', !!any);
    }

    /** Build a tag chip with its remove button. Name and class come in as data. */
    function buildTagChip(name, cssClass) {
        var chip = document.createElement('span');
        chip.className = 'user-tag' + (cssClass ? ' ' + cssClass : '');
        chip.dataset.tagChip = '';
        chip.dataset.tagName = name;
        chip.dataset.tagClass = cssClass || '';

        var label = document.createElement('span');
        label.className = 'break-all';
        label.textContent = name;
        chip.appendChild(label);

        var button = document.createElement('button');
        button.type = 'button';
        button.className = 'user-tag-remove';
        button.dataset.removeTag = name;
        button.setAttribute('aria-label', 'Remove tag ' + name);
        button.innerHTML = '<svg fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">' +
            '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" /></svg>';
        chip.appendChild(button);
        return chip;
    }

    /** Build the menu entry that offers a tag again once it is removed from the user. */
    function buildMenuItem(name, cssClass) {
        var item = document.createElement('button');
        item.type = 'button';
        item.className = 'w-full px-3 py-2 text-sm text-left text-text-primary hover:bg-bg-hover transition-colors';
        item.dataset.addTag = name;
        item.dataset.tagClass = cssClass || '';

        var pill = document.createElement('span');
        pill.className = 'user-tag break-all' + (cssClass ? ' ' + cssClass : '');
        pill.textContent = name;
        item.appendChild(pill);
        return item;
    }

    async function addTag(item) {
        var name = item.dataset.addTag;
        var cssClass = item.dataset.tagClass || '';
        if (item.getAttribute('aria-busy') === 'true') return;

        item.setAttribute('aria-busy', 'true');
        item.disabled = true;
        try {
            await ApiClient.post(usersUrl('/tags/' + encodeURIComponent(name)), { appliedById: String(currentUserId) });

            var container = byId('userTagsContainer');
            container.insertBefore(buildTagChip(name, cssClass), byId('noTagsText'));
            item.remove();
            refreshNoTagsText();
            refreshTagMenuEmptiness();
            setMenuOpen(false, true);
            toast.success('Added the tag "' + name + '".');
        } catch (err) {
            item.removeAttribute('aria-busy');
            item.disabled = false;
            reportFailure(err, 'Could not add the tag. Try again.');
        }
    }

    async function removeTag(button) {
        var name = button.dataset.removeTag;
        var chip = button.closest('[data-tag-chip]');

        var confirmed = await quickActions.confirm({
            title: 'Remove tag',
            message: 'Remove the tag "' + name + '" from this user?',
            variant: 'warning',
            confirmText: 'Remove'
        });
        if (!confirmed) return;

        button.disabled = true;
        button.setAttribute('aria-busy', 'true');
        try {
            await ApiClient.del(usersUrl('/tags/' + encodeURIComponent(name)));

            var cssClass = chip ? chip.dataset.tagClass || '' : '';
            if (chip) chip.remove();
            var menu = tagMenu();
            if (menu.panel) menu.panel.appendChild(buildMenuItem(name, cssClass));
            refreshNoTagsText();
            refreshTagMenuEmptiness();
            if (menu.button) menu.button.focus();
            toast.success('Removed the tag "' + name + '".');
        } catch (err) {
            button.disabled = false;
            button.removeAttribute('aria-busy');
            reportFailure(err, 'Could not remove the tag. Try again.');
        }
    }

    /* ---------------------------------------------------------------- notes */

    function setNoteError(message) {
        var error = byId('newNoteError');
        var field = byId('newNoteText');
        if (!error || !field) return;
        error.textContent = message || '';
        error.classList.toggle('hidden', !message);
        field.classList.toggle('input-validation-error', !!message);
        field.setAttribute('aria-invalid', message ? 'true' : 'false');
        var described = ['newNoteHelp'].concat(message ? ['newNoteError'] : []);
        field.setAttribute('aria-describedby', described.join(' '));
    }

    function refreshNotesState() {
        var list = byId('notesListContainer');
        var count = list.querySelectorAll('[data-note-id]').length;
        byId('notesEmpty').classList.toggle('hidden', count > 0);
        setTabCount('notes', count);
    }

    function buildNote(note) {
        var item = document.createElement('li');
        item.className = 'note-item';
        item.dataset.noteId = note.id;

        var meta = document.createElement('div');
        meta.className = 'note-meta';

        var author = document.createElement('span');
        author.className = 'font-medium text-text-secondary';
        author.textContent = note.authorUsername || 'You';
        meta.appendChild(author);

        var dash = document.createElement('span');
        dash.setAttribute('aria-hidden', 'true');
        dash.textContent = '-';
        meta.appendChild(dash);

        var when = document.createElement('span');
        when.dataset.utc = note.createdAt;
        when.dataset.format = 'datetime-short';
        when.textContent = Format.formatDate(note.createdAt, 'datetime-short');
        meta.appendChild(when);

        var del = document.createElement('button');
        del.type = 'button';
        del.className = 'btn btn-ghost btn-sm ml-auto text-error';
        del.dataset.deleteNote = note.id;
        del.setAttribute('aria-label', 'Delete this note');
        del.textContent = 'Delete';
        meta.appendChild(del);

        var content = document.createElement('p');
        content.className = 'note-content whitespace-pre-wrap break-words';
        content.setAttribute('dir', 'auto');
        content.textContent = note.content;

        item.appendChild(meta);
        item.appendChild(content);
        return item;
    }

    async function addNote(form) {
        var field = byId('newNoteText');
        var button = byId('addNoteBtn');
        var content = field.value.trim();

        if (!content) {
            setNoteError('Write something first.');
            field.focus();
            return;
        }
        setNoteError('');

        LoadingManager.setButtonLoading(button, true, 'Adding…');
        field.readOnly = true;
        try {
            var note = await ApiClient.post(usersUrl('/notes'), {
                guildId: guildId,
                targetUserId: userId,
                authorUserId: String(currentUserId),
                content: content
            });

            byId('notesListContainer').prepend(buildNote(note));
            field.value = '';
            refreshNotesState();
            toast.success('Note added.');
        } catch (err) {
            // The text stays in the box so it can be sent again
            reportFailure(err, 'Could not add the note. Try again.');
        } finally {
            LoadingManager.setButtonLoading(button, false);
            field.readOnly = false;
            field.focus();
        }
    }

    async function deleteNote(button) {
        var noteId = button.dataset.deleteNote;
        var item = button.closest('[data-note-id]');

        var confirmed = await quickActions.confirm({
            title: 'Delete note',
            message: 'Delete this note? It cannot be recovered.',
            variant: 'danger',
            confirmText: 'Delete'
        });
        if (!confirmed) return;

        LoadingManager.setButtonLoading(button, true, 'Deleting…');
        try {
            await ApiClient.del(usersUrl('/notes/' + encodeURIComponent(noteId)));
            if (item) item.remove();
            refreshNotesState();
            byId('newNoteText').focus();
            toast.success('Note deleted.');
        } catch (err) {
            LoadingManager.setButtonLoading(button, false);
            reportFailure(err, 'Could not delete the note. Try again.');
        }
    }

    /* ---------------------------------------------------------------- wiring */

    function init() {
        guildId = window.moderationGuildId;
        userId = window.moderationUserId;
        currentUserId = window.currentUserId;

        setupTagMenu();

        document.addEventListener('click', function (e) {
            var target = e.target;
            if (!target.closest) return;

            var add = target.closest('[data-add-tag]');
            if (add) { addTag(add); return; }

            var remove = target.closest('[data-remove-tag]');
            if (remove) { removeTag(remove); return; }

            var del = target.closest('[data-delete-note]');
            if (del) { deleteNote(del); }
        });

        var form = byId('addNoteForm');
        if (form) {
            form.addEventListener('submit', function (e) {
                e.preventDefault();
                addNote(form);
            });
            byId('newNoteText').addEventListener('input', function () { setNoteError(''); });
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
