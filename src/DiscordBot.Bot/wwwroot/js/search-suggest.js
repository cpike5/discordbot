/**
 * SearchSuggest: live results under the navbar search and in the mobile search overlay.
 *
 * As the user types (from 2 characters, 200ms debounce) the list shows:
 *   - Pages: sidebar links whose label matches. Built from the links the sidebar already rendered,
 *     so role filtering is free and there is no server call.
 *   - Servers and Commands: from `GET /Search?handler=Suggest&q=` (SearchModel.OnGetSuggestAsync).
 *     Guild ids arrive as strings and stay strings.
 *   - "See all results for '…'", which goes to the full /Search page.
 *
 * The input is a WAI-ARIA combobox: role="combobox", aria-expanded, aria-controls and
 * aria-activedescendant point at a role="listbox" of role="option" links. Focus never leaves the
 * input. Down/Up move through the rows, Enter opens the highlighted row (or, with nothing
 * highlighted, opens the only server when exactly one matched, else falls through to the full
 * search), Escape closes the list.
 *
 * A newer keystroke aborts the request in flight, and a response that still arrives late is dropped
 * by its sequence number. Every string from the server goes through SafeHtml.escape.
 *
 * search.js wires it to the desktop dropdown and the mobile overlay (`attach`). The pure helpers are
 * exported for the tests in __tests__/search-suggest.test.js.
 */
(function (root, factory) {
    'use strict';
    const api = factory(root);
    if (typeof module !== 'undefined' && module.exports) {
        module.exports = api;
    }
    if (typeof window !== 'undefined' && root === window) {
        window.SearchSuggest = api;
    }
})(typeof window !== 'undefined' ? window : globalThis, function (root) {
    'use strict';

    const MIN_LENGTH = 2;
    const DEBOUNCE_MS = 200;
    const MAX_PAGES = 5;

    const GROUPS = [
        { key: 'pages', heading: 'Pages' },
        { key: 'guilds', heading: 'Servers' },
        { key: 'commands', heading: 'Commands' }
    ];

    function escape(value) {
        return root.SafeHtml.escape(value);
    }

    function normalize(text) {
        return String(text || '').replace(/\s+/g, ' ').trim().toLowerCase();
    }

    // ---- Pages index -------------------------------------------------------------------------

    /**
     * Reads the sidebar's links into `{ label, href, external }` entries, in sidebar order, one per
     * href. The label is the link's visible text (`.sidebar-link-text`), else its title.
     */
    function pagesFromLinks(links) {
        const seen = new Set();
        const pages = [];
        Array.prototype.forEach.call(links || [], (link) => {
            const href = link.getAttribute('href');
            if (!href || href.charAt(0) === '#' || seen.has(href)) return;
            const textNode = typeof link.querySelector === 'function' ? link.querySelector('.sidebar-link-text') : null;
            const label = ((textNode && textNode.textContent) || link.getAttribute('title') || link.textContent || '')
                .replace(/\s+/g, ' ').trim();
            if (!label) return;
            seen.add(href);
            pages.push({ label, href, external: link.getAttribute('target') === '_blank' });
        });
        return pages;
    }

    /** The sidebar's navigation links, read once per page. */
    function pagesFromDocument(doc) {
        if (!doc || typeof doc.querySelectorAll !== 'function') return [];
        return pagesFromLinks(doc.querySelectorAll('#sidebar a.sidebar-link-redesign[href]'));
    }

    /**
     * Pages whose label matches the term: first those where the label or any word in it starts
     * with the term, then those that only contain it. Sidebar order within each.
     */
    function matchPages(pages, term, max) {
        const needle = normalize(term);
        if (needle.length === 0) return [];
        const limit = max || MAX_PAGES;
        const prefix = [];
        const substring = [];
        (pages || []).forEach((page) => {
            const label = normalize(page.label);
            if (label.startsWith(needle) || label.split(' ').some((word) => word.startsWith(needle))) {
                prefix.push(page);
            } else if (label.includes(needle)) {
                substring.push(page);
            }
        });
        return prefix.concat(substring).slice(0, limit);
    }

    // ---- Rows and markup ---------------------------------------------------------------------

    /**
     * The selectable rows, in display order. `model` is
     * `{ term, pages, guilds, commands, status: 'loading' | 'ready' | 'error' }`.
     */
    function buildRows(model) {
        const rows = [];
        (model.pages || []).forEach((page) => rows.push({ group: 'pages', label: page.label, href: page.href, external: !!page.external }));
        (model.guilds || []).forEach((guild) => rows.push({ group: 'guilds', label: guild.name, href: guild.url, guildId: String(guild.id), iconUrl: guild.iconUrl || null }));
        (model.commands || []).forEach((command) => rows.push({ group: 'commands', label: command.name, href: command.url }));
        rows.push({ group: 'all', label: "See all results for '" + model.term + "'", href: searchUrl(model.term) });
        return rows;
    }

    function searchUrl(term) {
        return '/Search?q=' + encodeURIComponent(term);
    }

    /** The line shown above the list for the model's state, or '' for none. */
    function messageFor(model) {
        if (model.status === 'error') {
            return "Couldn't search servers. Press Enter for full search.";
        }
        if (model.status === 'ready' && (model.guilds || []).length === 0 && (model.commands || []).length === 0) {
            return "No servers or commands match '" + model.term + "'";
        }
        return '';
    }

    /** What a screen reader hears once the list settles. */
    function announcementFor(model, rows) {
        if (model.status === 'loading') return '';
        const message = messageFor(model);
        const count = rows.length - 1; // the See all row is always there
        const summary = count === 1 ? '1 suggestion' : count + ' suggestions';
        return message ? message + '. ' + summary + '.' : summary + '. Use the arrow keys to choose one.';
    }

    function optionId(idPrefix, index) {
        return idPrefix + '-option-' + index;
    }

    function guildAvatar(row) {
        if (row.iconUrl && /^https:\/\//i.test(row.iconUrl)) {
            return '<img class="search-suggest-avatar" src="' + escape(row.iconUrl) + '" alt="" loading="lazy" />';
        }
        const initial = (row.label || '?').trim().charAt(0).toUpperCase() || '?';
        return '<span class="search-suggest-avatar search-suggest-avatar-initial" aria-hidden="true">' + escape(initial) + '</span>';
    }

    function optionHtml(row, index, idPrefix, activeIndex) {
        const active = index === activeIndex;
        const classes = 'search-suggest-option' + (row.group === 'all' ? ' search-suggest-all' : '') + (active ? ' is-active' : '');
        const target = row.external ? ' target="_blank" rel="noopener noreferrer"' : '';
        const leading = row.group === 'guilds' ? guildAvatar(row) : '';
        return '<a id="' + escape(optionId(idPrefix, index)) + '" role="option" tabindex="-1" aria-selected="' + (active ? 'true' : 'false') + '"' +
            ' class="' + classes + '" href="' + escape(row.href) + '" data-suggest-index="' + index + '"' + target + '>' +
            leading + '<span class="search-suggest-label">' + escape(row.label) + '</span></a>';
    }

    /**
     * The panel markup: a progress line while loading, the state message, and the listbox with one
     * group per non-empty category and the See all row last.
     */
    function renderHtml(model, rows, options) {
        const idPrefix = options.idPrefix;
        const activeIndex = typeof options.activeIndex === 'number' ? options.activeIndex : -1;
        let html = '';

        if (model.status === 'loading') {
            html += '<div class="search-suggest-progress" aria-hidden="true"></div>';
        }

        const message = messageFor(model);
        if (message) {
            html += '<p class="search-suggest-message' + (model.status === 'error' ? ' is-error' : '') + '">' + escape(message) + '</p>';
        }

        html += '<div id="' + escape(options.listboxId) + '" role="listbox" class="search-suggest-list" aria-label="Search suggestions">';
        GROUPS.forEach((group) => {
            const members = [];
            rows.forEach((row, index) => { if (row.group === group.key) members.push(optionHtml(row, index, idPrefix, activeIndex)); });
            if (members.length === 0) return;
            const headingId = idPrefix + '-heading-' + group.key;
            html += '<div role="group" class="search-suggest-group" aria-labelledby="' + escape(headingId) + '">' +
                '<div id="' + escape(headingId) + '" class="search-suggest-heading" role="presentation">' + escape(group.heading) + '</div>' +
                members.join('') + '</div>';
        });
        const allIndex = rows.length - 1;
        html += optionHtml(rows[allIndex], allIndex, idPrefix, activeIndex);
        html += '</div>';
        return html;
    }

    // ---- Keyboard ----------------------------------------------------------------------------

    /** The next highlighted index after an arrow key; wraps, and -1 means none. */
    function moveActive(index, delta, count) {
        if (count <= 0) return -1;
        if (index < 0) return delta > 0 ? 0 : count - 1;
        return (index + delta + count) % count;
    }

    /**
     * What Enter does: `{ type: 'open', row, index }` for the highlighted row, or for the only server
     * when exactly one matched and nothing is highlighted; otherwise `{ type: 'submit' }` (the full
     * search, as before suggestions existed).
     */
    function resolveEnter(model, rows, activeIndex) {
        if (activeIndex >= 0 && activeIndex < rows.length) {
            return { type: 'open', row: rows[activeIndex], index: activeIndex };
        }
        if (model.status === 'ready' && (model.guilds || []).length === 1) {
            const index = rows.findIndex((row) => row.group === 'guilds');
            if (index >= 0) return { type: 'open', row: rows[index], index };
        }
        return { type: 'submit' };
    }

    // ---- Requests ----------------------------------------------------------------------------

    /**
     * Sequences suggestion requests: each one aborts the one before, and a response that arrives
     * after a newer request started is ignored even if the transport ignored the abort.
     *
     * @param {object} options
     * @param {(term: string, signal: AbortSignal) => Promise<object>} options.fetch
     * @param {(term: string, data: object) => void} options.onResult
     * @param {(term: string, error: Error) => void} options.onError
     */
    function createRequester(options) {
        let controller = null;
        let sequence = 0;

        function request(term) {
            if (controller) controller.abort();
            const mine = new AbortController();
            controller = mine;
            const ticket = ++sequence;
            let pending;
            try {
                pending = Promise.resolve(options.fetch(term, mine.signal));
            } catch (error) {
                pending = Promise.reject(error);
            }
            return pending.then((data) => {
                if (ticket !== sequence) return;
                controller = null;
                options.onResult(term, data || {});
            }, (error) => {
                if (ticket !== sequence || (error && error.name === 'AbortError')) return;
                controller = null;
                options.onError(term, error);
            });
        }

        function cancel() {
            if (controller) controller.abort();
            controller = null;
            sequence += 1;
        }

        return { request, cancel };
    }

    /** The default transport: the Search page's Suggest handler through ApiClient. */
    function fetchSuggestions(term, signal) {
        return root.ApiClient.get('/Search?handler=Suggest&q=' + encodeURIComponent(term), {
            signal,
            timeout: 10000,
            errorMessage: "Couldn't search servers."
        });
    }

    // ---- Wiring ------------------------------------------------------------------------------

    /**
     * Turns a search input into a combobox that renders suggestions into `panel`.
     *
     * @param {object} options
     * @param {HTMLInputElement} options.input
     * @param {HTMLElement} options.panel - where the list is rendered (dropdown or overlay region)
     * @param {string} options.listboxId - id for the listbox, unique on the page
     * @param {() => void} options.show - make the panel visible
     * @param {() => void} options.hide - hide the panel
     * @param {(term: string) => void} [options.onOpen] - a suggestion was chosen (save the recent search)
     * @param {(term: string) => void} [options.onSubmit] - Enter with nothing to open; omit to let the
     *   key do what it did before (submit the surrounding form)
     * @param {() => Array} [options.pages] - the pages index; defaults to the sidebar's links
     * @param {Function} [options.fetch] - transport; defaults to the Suggest page handler
     * @param {number} [options.debounceMs]
     */
    function attach(options) {
        const input = options.input;
        const panel = options.panel;
        if (!input || !panel) return null;

        const idPrefix = options.listboxId;
        const debounceMs = typeof options.debounceMs === 'number' ? options.debounceMs : DEBOUNCE_MS;
        let pagesIndex = null;
        let model = null;
        let rows = [];
        let activeIndex = -1;
        let expanded = false;
        let timer = null;

        input.setAttribute('role', 'combobox');
        input.setAttribute('aria-autocomplete', 'list');
        input.setAttribute('aria-expanded', 'false');
        input.setAttribute('aria-controls', options.listboxId);

        // One persistent live region, so results are announced once they settle and not on every
        // re-render of the panel
        const live = document.createElement('div');
        live.className = 'sr-only';
        live.setAttribute('role', 'status');
        live.setAttribute('aria-live', 'polite');
        (panel.parentNode || document.body).appendChild(live);

        function pages() {
            if (!pagesIndex) pagesIndex = options.pages ? options.pages() : pagesFromDocument(document);
            return pagesIndex;
        }

        const requester = createRequester({
            fetch: options.fetch || fetchSuggestions,
            onResult(term, data) {
                if (!model || model.term !== term) return;
                setModel(Object.assign({}, model, {
                    status: 'ready',
                    guilds: Array.isArray(data.guilds) ? data.guilds : [],
                    commands: Array.isArray(data.commands) ? data.commands : []
                }));
            },
            onError(term) {
                if (!model || model.term !== term) return;
                setModel(Object.assign({}, model, { status: 'error', guilds: [], commands: [] }));
            }
        });

        function setModel(next) {
            const activeRow = activeIndex >= 0 ? rows[activeIndex] : null;
            model = next;
            rows = buildRows(model);
            // Keep the highlight on the same row when results arrive under it
            activeIndex = activeRow
                ? rows.findIndex((row) => row.group === activeRow.group && row.href === activeRow.href)
                : -1;
            render();
            live.textContent = announcementFor(model, rows);
        }

        function render() {
            panel.innerHTML = renderHtml(model, rows, { idPrefix, listboxId: options.listboxId, activeIndex });
            if (!expanded) {
                expanded = true;
                input.setAttribute('aria-expanded', 'true');
                options.show();
            }
            syncActive();
        }

        function syncActive() {
            if (activeIndex >= 0) {
                input.setAttribute('aria-activedescendant', optionId(idPrefix, activeIndex));
                const option = document.getElementById(optionId(idPrefix, activeIndex));
                if (option && typeof option.scrollIntoView === 'function') option.scrollIntoView({ block: 'nearest' });
            } else {
                input.removeAttribute('aria-activedescendant');
            }
        }

        function collapse() {
            if (timer) clearTimeout(timer);
            timer = null;
            activeIndex = -1;
            input.removeAttribute('aria-activedescendant');
            if (expanded) {
                expanded = false;
                input.setAttribute('aria-expanded', 'false');
                options.hide();
            }
        }

        /** Called on every input event with the current value. */
        function update(value) {
            const term = String(value || '').trim();
            if (term.length < MIN_LENGTH) {
                requester.cancel();
                model = null;
                collapse();
                live.textContent = '';
                return false;
            }
            if (model && model.term === term && model.status !== 'loading') {
                if (!expanded) render();
                return true;
            }

            // Pages show at once; servers and commands from the last answer stay under the progress
            // line until the new one lands
            activeIndex = -1;
            setModel({
                term,
                pages: matchPages(pages(), term, MAX_PAGES),
                guilds: model ? model.guilds : [],
                commands: model ? model.commands : [],
                status: 'loading'
            });

            if (timer) clearTimeout(timer);
            requester.cancel();
            timer = setTimeout(() => {
                timer = null;
                requester.request(term);
            }, debounceMs);
            return true;
        }

        /** Stop showing suggestions (the input was emptied or the overlay closed). */
        function reset() {
            requester.cancel();
            model = null;
            collapse();
            live.textContent = '';
        }

        function open(choice) {
            const option = document.getElementById(optionId(idPrefix, choice.index));
            if (option && typeof option.click === 'function') {
                // A click keeps target="_blank" on external links; the panel's click listener saves
                // the recent search
                option.click();
            } else {
                if (options.onOpen && model) options.onOpen(model.term);
                root.location.href = choice.row.href;
            }
        }

        function onKeydown(event) {
            if (event.isComposing) return;
            if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
                if (!model) return;
                event.preventDefault();
                if (!expanded) {
                    render();
                    return;
                }
                activeIndex = moveActive(activeIndex, event.key === 'ArrowDown' ? 1 : -1, rows.length);
                render();
                return;
            }

            if (event.key === 'Escape') {
                if (!expanded) return;
                event.preventDefault();
                event.stopPropagation();
                collapse();
                return;
            }

            if (event.key === 'Enter') {
                if (!model || !expanded) {
                    if (options.onSubmit) options.onSubmit(input.value.trim());
                    return;
                }
                const choice = resolveEnter(model, rows, activeIndex);
                if (choice.type === 'open') {
                    event.preventDefault();
                    open(choice);
                } else if (options.onSubmit) {
                    event.preventDefault();
                    options.onSubmit(model.term);
                }
                // Otherwise the form submits to /Search as before
            }
        }

        // A pointer choice also counts as using the search
        panel.addEventListener('click', (event) => {
            const option = event.target && typeof event.target.closest === 'function' ? event.target.closest('[role="option"]') : null;
            if (option && model && options.onOpen) options.onOpen(model.term);
        });

        // Keep focus in the input when an option is pressed with the mouse
        panel.addEventListener('mousedown', (event) => {
            const option = event.target && typeof event.target.closest === 'function' ? event.target.closest('[role="option"]') : null;
            if (option) event.preventDefault();
        });

        input.addEventListener('keydown', onKeydown);

        return {
            update,
            reset,
            collapse,
            isExpanded: () => expanded,
            onKeydown
        };
    }

    return {
        MIN_LENGTH,
        DEBOUNCE_MS,
        pagesFromLinks,
        pagesFromDocument,
        matchPages,
        buildRows,
        renderHtml,
        messageFor,
        moveActive,
        resolveEnter,
        createRequester,
        attach
    };
});
