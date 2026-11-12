/**
 * Guild switcher and recent guilds (docs/plans/ui-audit-improvements.md § 5).
 *
 * Switcher: on a guild page with more than one guild to choose from, the guild name in the header
 * (_GuildHeader.cshtml) is a button that opens a server-rendered listbox of the guilds the user can
 * open. Each option is a link to the same section of that guild (GuildSwitchTarget on the server).
 * Keyboard, as the More menu in guild-nav.js: Enter, Space or ArrowDown on the button opens the
 * list (ArrowUp opens it at the end); Escape closes it and returns focus to the button; Tab and a
 * click outside close it. Inside, as the search combobox in search-suggest.js, focus stays on one
 * control (the filter input when there are more than 8 guilds, else the listbox itself) and
 * aria-activedescendant points at the highlighted option: Up/Down/Home/End move, Enter goes.
 * Typing in the filter narrows the list; without a filter a letter jumps to the next guild
 * starting with it.
 *
 * Recent guilds: every guild page records `{ id, name, iconUrl }` in localStorage['recentGuilds']
 * (most recent first, at most 3, IDs always strings; per browser, decision D4). The sidebar's
 * `[data-recent-guilds]` list, which the server renders only for users with more than one guild,
 * shows them with the current guild highlighted. It carries the IDs the user can open now, and
 * entries outside that set are dropped, so a guild the user lost access to (or another account's
 * guild in a shared browser) disappears. Storage that is blocked or throws leaves the list empty.
 *
 * The pure parts are exported for __tests__/guild-switcher.test.js.
 */
(function (root, factory) {
    'use strict';
    const api = factory(root);
    if (typeof module !== 'undefined' && module.exports) {
        module.exports = api;
    }
    if (typeof window !== 'undefined' && root === window) {
        window.GuildSwitcher = api;
        api.init(window.document);
    }
})(typeof window !== 'undefined' ? window : globalThis, function (root) {
    'use strict';

    const STORAGE_KEY = 'recentGuilds';
    const MAX_RECENT = 3;
    const SNOWFLAKE = /^\d{1,20}$/;

    // ---- Recent guilds -----------------------------------------------------------------------

    /** A clean entry, or null when the value is not a usable guild. IDs stay strings. */
    function normalizeEntry(value) {
        if (!value || typeof value !== 'object') return null;
        const id = typeof value.id === 'string' ? value.id : '';
        const name = typeof value.name === 'string' ? value.name.trim() : '';
        if (!SNOWFLAKE.test(id) || !name) return null;
        const iconUrl = typeof value.iconUrl === 'string' && /^https:\/\//i.test(value.iconUrl) ? value.iconUrl : null;
        return { id: id, name: name, iconUrl: iconUrl };
    }

    /** Clean, de-duplicated (first wins) and capped copy of a stored list. */
    function sanitizeList(list, max) {
        const limit = max || MAX_RECENT;
        const seen = new Set();
        const result = [];
        (Array.isArray(list) ? list : []).forEach((value) => {
            const entry = normalizeEntry(value);
            if (!entry || seen.has(entry.id) || result.length >= limit) return;
            seen.add(entry.id);
            result.push(entry);
        });
        return result;
    }

    /** The list with `entry` moved (or added) to the front, de-duplicated and capped at 3. */
    function addRecent(list, entry, max) {
        const clean = normalizeEntry(entry);
        const rest = sanitizeList(list, Number.MAX_SAFE_INTEGER);
        if (!clean) return sanitizeList(rest, max);
        return sanitizeList([clean].concat(rest.filter((e) => e.id !== clean.id)), max);
    }

    /** Only the entries whose ID is in `allowedIds` (strings). */
    function pruneRecent(list, allowedIds) {
        const allowed = new Set((allowedIds || []).map(String));
        return sanitizeList(list).filter((e) => allowed.has(e.id));
    }

    /** The stored list; empty when storage is missing, blocked, or holds something else. */
    function readRecent(storage) {
        try {
            if (!storage) return [];
            const raw = storage.getItem(STORAGE_KEY);
            if (!raw) return [];
            return sanitizeList(JSON.parse(raw));
        } catch (e) {
            return [];
        }
    }

    /** Stores the list; false when storage refused. */
    function writeRecent(storage, list) {
        try {
            if (!storage) return false;
            storage.setItem(STORAGE_KEY, JSON.stringify(sanitizeList(list)));
            return true;
        } catch (e) {
            return false;
        }
    }

    function initial(name) {
        const ch = String(name || '').trim().charAt(0).toUpperCase();
        return ch || '?';
    }

    /**
     * Markup for the sidebar list. Every value goes through `escape` (SafeHtml.escape in the page).
     * @param {Array<{id: string, name: string, iconUrl: string|null}>} entries
     * @param {string|null} currentId The guild the page is about, if any.
     * @param {(value: *) => string} escape
     */
    function recentListHtml(entries, currentId, escape) {
        return entries.map((e) => {
            const current = currentId !== null && currentId !== undefined && e.id === String(currentId);
            const avatar = e.iconUrl
                ? '<img class="sidebar-recent-guild-avatar" src="' + escape(e.iconUrl) + '" alt="" loading="lazy" />'
                : '<span class="sidebar-recent-guild-avatar sidebar-recent-guild-initial" aria-hidden="true">' + escape(initial(e.name)) + '</span>';
            return '<li><a class="sidebar-recent-guild' + (current ? ' active' : '') + '"' +
                ' href="/Guilds/Details/' + escape(e.id) + '"' +
                ' data-guild-id="' + escape(e.id) + '"' +
                ' title="' + escape(e.name) + '"' +
                (current ? ' aria-current="true"' : '') + '>' +
                avatar + '<span class="sidebar-recent-guild-name">' + escape(e.name) + '</span></a></li>';
        }).join('');
    }

    // ---- Switcher ----------------------------------------------------------------------------

    function normalize(text) {
        return String(text || '').replace(/\s+/g, ' ').trim().toLowerCase();
    }

    /** Indexes of the names that contain the term (all of them for an empty term). */
    function filterIndexes(names, term) {
        const needle = normalize(term);
        const result = [];
        (names || []).forEach((name, i) => {
            if (!needle || normalize(name).includes(needle)) result.push(i);
        });
        return result;
    }

    /** The next position after an arrow key; wraps, and -1 means none. */
    function moveActive(index, delta, count) {
        if (count <= 0) return -1;
        if (index < 0) return delta > 0 ? 0 : count - 1;
        return (index + delta + count) % count;
    }

    /** Type-ahead without a filter: the next name after `from` that starts with `ch`, or -1. */
    function typeAhead(names, from, ch) {
        const needle = normalize(ch);
        if (!needle) return -1;
        const n = names.length;
        for (let step = 1; step <= n; step++) {
            const i = (from + step + n) % n;
            if (normalize(names[i]).startsWith(needle)) return i;
        }
        return -1;
    }

    /**
     * Open/close, highlight and keyboard behaviour, against a small interface so tests can drive it
     * with stubs.
     * @param {object} o
     * @param {object} o.button   Needs setAttribute, focus.
     * @param {object} o.popover  Needs classList (add/remove/contains).
     * @param {object} o.control  The element keeping focus while open (filter input or listbox):
     *                            setAttribute, removeAttribute, focus, and `value` for a filter.
     * @param {boolean} o.hasFilter
     * @param {Array<{name: string, id: string, setActive(on: boolean): void, setHidden(h: boolean): void, isCurrent: boolean}>} o.options
     * @param {(option: object) => void} o.go  Navigates to an option.
     * @param {(count: number, term: string) => void} [o.onFiltered]
     */
    function createSwitcher(o) {
        const options = o.options;
        const names = options.map((opt) => opt.name);
        let visible = options.map((_, i) => i);
        let active = -1; // position in `visible`

        function isOpen() { return o.popover.classList.contains('active'); }

        function highlight(position) {
            if (active >= 0 && active < visible.length) options[visible[active]].setActive(false);
            active = position;
            if (active >= 0 && active < visible.length) {
                const opt = options[visible[active]];
                opt.setActive(true);
                o.control.setAttribute('aria-activedescendant', opt.id);
            } else {
                o.control.removeAttribute('aria-activedescendant');
            }
        }

        function applyFilter(term) {
            const matches = filterIndexes(names, term);
            const keep = new Set(matches);
            options.forEach((opt, i) => opt.setHidden(!keep.has(i)));
            if (active >= 0 && active < visible.length) options[visible[active]].setActive(false);
            active = -1;
            visible = matches;
            highlight(visible.length > 0 ? 0 : -1);
            if (o.onFiltered) o.onFiltered(visible.length, term);
        }

        function open(which) {
            if (!isOpen()) {
                o.popover.classList.add('active');
                o.button.setAttribute('aria-expanded', 'true');
                if (o.hasFilter) {
                    o.control.setAttribute('aria-expanded', 'true');
                    if (o.control.value) { o.control.value = ''; }
                    applyFilter('');
                }
            }
            o.control.focus();
            if (which === 'last') highlight(visible.length - 1);
            else if (which === 'first') highlight(visible.length > 0 ? 0 : -1);
            else {
                const current = visible.findIndex((i) => options[i].isCurrent);
                highlight(current >= 0 ? current : (visible.length > 0 ? 0 : -1));
            }
        }

        function close(restoreFocus) {
            if (!isOpen()) return;
            o.popover.classList.remove('active');
            o.button.setAttribute('aria-expanded', 'false');
            if (o.hasFilter) o.control.setAttribute('aria-expanded', 'false');
            highlight(-1);
            if (restoreFocus) o.button.focus();
        }

        function toggle() { if (isOpen()) close(false); else open(); }

        function choose() {
            if (active < 0 || active >= visible.length) return;
            const opt = options[visible[active]];
            if (opt.isCurrent) { close(true); return; }
            o.go(opt);
        }

        function onButtonKeydown(e) {
            if (e.key === 'ArrowDown') { e.preventDefault(); open(); }
            else if (e.key === 'ArrowUp') { e.preventDefault(); open('last'); }
            else if (e.key === 'Escape' && isOpen()) { e.preventDefault(); close(true); }
        }

        function onControlKeydown(e) {
            switch (e.key) {
                case 'ArrowDown': e.preventDefault(); highlight(moveActive(active, 1, visible.length)); break;
                case 'ArrowUp': e.preventDefault(); highlight(moveActive(active, -1, visible.length)); break;
                case 'Home': if (!o.hasFilter) { e.preventDefault(); highlight(visible.length > 0 ? 0 : -1); } break;
                case 'End': if (!o.hasFilter) { e.preventDefault(); highlight(visible.length - 1); } break;
                case 'Enter': e.preventDefault(); choose(); break;
                case ' ': if (!o.hasFilter) { e.preventDefault(); choose(); } break;
                case 'Escape': e.preventDefault(); close(true); break;
                case 'Tab': close(false); break;
                default:
                    if (!o.hasFilter && e.key && e.key.length === 1 && !e.ctrlKey && !e.metaKey && !e.altKey) {
                        const from = active >= 0 ? visible[active] : -1;
                        const hit = typeAhead(names, from, e.key);
                        if (hit >= 0) highlight(visible.indexOf(hit));
                    }
            }
        }

        function onInput(term) { applyFilter(term); }

        function activePosition() { return active; }

        return { isOpen, open, close, toggle, onButtonKeydown, onControlKeydown, onInput, choose, activePosition, highlightIndex: (i) => highlight(visible.indexOf(i)) };
    }

    // ---- Page wiring -------------------------------------------------------------------------

    function storage() {
        try {
            return root.localStorage || null;
        } catch (e) {
            return null; // access itself throws when storage is blocked
        }
    }

    function currentGuildFrom(doc) {
        const header = doc.querySelector('[data-guild-header]');
        if (!header) return null;
        return normalizeEntry({
            id: header.getAttribute('data-guild-id'),
            name: header.getAttribute('data-guild-name'),
            iconUrl: header.getAttribute('data-guild-icon') || null
        });
    }

    function initSwitcher(doc) {
        const wrapper = doc.querySelector('[data-guild-switcher]');
        if (!wrapper) return;
        const button = wrapper.querySelector('.guild-switcher-button');
        const popover = wrapper.querySelector('.guild-switcher-popover');
        const listbox = wrapper.querySelector('[role="listbox"]');
        const filter = wrapper.querySelector('.guild-switcher-filter');
        const empty = wrapper.querySelector('.guild-switcher-empty');
        const status = wrapper.querySelector('.guild-switcher-status');
        if (!button || !popover || !listbox) return;

        const elements = Array.from(listbox.querySelectorAll('[role="option"]'));
        const options = elements.map((el) => ({
            id: el.id,
            name: el.getAttribute('data-guild-name') || el.textContent,
            isCurrent: el.getAttribute('aria-current') === 'page',
            el: el,
            setActive(on) {
                el.classList.toggle('is-active', on);
                el.setAttribute('aria-selected', on ? 'true' : 'false');
                if (on && typeof el.scrollIntoView === 'function') el.scrollIntoView({ block: 'nearest' });
            },
            setHidden(h) { el.hidden = h; }
        }));

        const switcher = createSwitcher({
            button: button,
            popover: popover,
            control: filter || listbox,
            hasFilter: !!filter,
            options: options,
            go: (opt) => { root.location.assign(opt.el.getAttribute('href')); },
            onFiltered: (count, term) => {
                if (empty) empty.hidden = count > 0;
                if (status) status.textContent = term ? (count === 1 ? '1 server' : count + ' servers') : '';
            }
        });

        button.addEventListener('click', (e) => { e.stopPropagation(); switcher.toggle(); });
        button.addEventListener('keydown', switcher.onButtonKeydown);
        (filter || listbox).addEventListener('keydown', switcher.onControlKeydown);
        if (filter) filter.addEventListener('input', () => switcher.onInput(filter.value));
        // A pointer press on an option must not blur the control first
        listbox.addEventListener('mousedown', (e) => { if (filter) e.preventDefault(); });
        listbox.addEventListener('mousemove', (e) => {
            const el = e.target.closest && e.target.closest('[role="option"]');
            if (el) switcher.highlightIndex(elements.indexOf(el));
        });
        listbox.addEventListener('click', (e) => {
            const el = e.target.closest && e.target.closest('[role="option"]');
            if (el && el.getAttribute('aria-current') === 'page') { e.preventDefault(); switcher.close(true); }
        });
        doc.addEventListener('click', (e) => { if (!wrapper.contains(e.target)) switcher.close(false); });
        wrapper.addEventListener('focusout', (e) => {
            if (switcher.isOpen() && e.relatedTarget && !wrapper.contains(e.relatedTarget)) switcher.close(false);
        });
    }

    function renderRecent(doc, current) {
        const list = doc.querySelector('[data-recent-guilds]');
        if (!list) return;
        const allowed = (list.getAttribute('data-accessible-guild-ids') || '').split(',').filter(Boolean);
        const store = storage();
        const stored = readRecent(store);
        const entries = pruneRecent(stored, allowed);
        if (entries.length !== stored.length) writeRecent(store, entries);
        if (entries.length === 0) { list.hidden = true; return; }
        list.innerHTML = recentListHtml(entries, current ? current.id : null, root.SafeHtml.escape);
        list.hidden = false;
    }

    function init(doc) {
        function run() {
            const current = currentGuildFrom(doc);
            if (current) {
                const store = storage();
                writeRecent(store, addRecent(readRecent(store), current));
            }
            initSwitcher(doc);
            renderRecent(doc, current);
        }
        if (doc.readyState === 'loading') doc.addEventListener('DOMContentLoaded', run);
        else run();
    }

    return {
        STORAGE_KEY: STORAGE_KEY,
        MAX_RECENT: MAX_RECENT,
        normalizeEntry: normalizeEntry,
        addRecent: addRecent,
        pruneRecent: pruneRecent,
        readRecent: readRecent,
        writeRecent: writeRecent,
        recentListHtml: recentListHtml,
        filterIndexes: filterIndexes,
        moveActive: moveActive,
        typeAhead: typeAhead,
        createSwitcher: createSwitcher,
        init: init
    };
});
