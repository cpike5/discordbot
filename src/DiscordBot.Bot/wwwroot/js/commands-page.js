/**
 * commands-page.js - the Commands page (/Commands): tabs, filters, pages, command search.
 *
 * One place owns the page's state, and the URL is that state. The query string carries the tab
 * (`tab`), the filters under the page model's own names (`StartDate`, `EndDate`, `GuildId`,
 * `CommandName`, `StatusFilter`, `SearchTerm`), the page (`pageNumber`), the command-list search
 * (`q`) and an open log (`log`). The server renders the same forms from the same names, so a
 * refresh, a shared link or Back lands on the view that was on screen.
 *
 * Applying a filter is one path: validate the date range, write the URL, load the active tab
 * once. A load aborts the one before it, draws a skeleton only if it takes over 300 ms, and on
 * failure shows the server's message with Retry inside the tab, leaving the filters alone.
 *
 * The pure parts (parsing and building query strings, the date-range rule, the default range,
 * the command-list matcher) are exported for tests (wwwroot/js/__tests__/commands-page.test.js).
 */
(function (root, factory) {
    var api = factory();
    if (typeof module === 'object' && module.exports) {
        module.exports = api;
    } else {
        root.CommandsPage = api;
        if (typeof document !== 'undefined') {
            if (document.readyState === 'loading') {
                document.addEventListener('DOMContentLoaded', api.init);
            } else {
                api.init();
            }
        }
    }
})(typeof self !== 'undefined' ? self : this, function () {
    'use strict';

    // ------------------------------------------------------------------ pure helpers

    var DEFAULT_TAB = 'command-list';
    var TABS = ['command-list', 'execution-logs', 'analytics'];
    var MAX_RANGE_DAYS = 90;

    /** The filter fields each tab reads, under the page model's names. */
    var TAB_FIELDS = {
        'command-list': [],
        'execution-logs': ['StartDate', 'EndDate', 'GuildId', 'CommandName', 'StatusFilter', 'SearchTerm'],
        'analytics': ['StartDate', 'EndDate', 'GuildId']
    };
    var ALL_FIELDS = ['StartDate', 'EndDate', 'GuildId', 'CommandName', 'StatusFilter', 'SearchTerm'];

    /** The names /api/commands/* binds. */
    var API_NAMES = {
        StartDate: 'startDate',
        EndDate: 'endDate',
        GuildId: 'guildId',
        CommandName: 'commandName',
        StatusFilter: 'statusFilter',
        SearchTerm: 'searchTerm'
    };

    var API_ROUTES = { 'command-list': 'list', 'execution-logs': 'logs', 'analytics': 'analytics' };

    /** Tab names the server also accepts (links elsewhere use ?tab=logs). */
    function normalizeTab(value) {
        var v = String(value || '').trim().toLowerCase();
        if (v === 'logs' || v === 'execution' || v === 'execution-logs') return 'execution-logs';
        if (v === 'stats' || v === 'analytics') return 'analytics';
        if (v === 'command-list') return 'command-list';
        return DEFAULT_TAB;
    }

    function isTabId(value) {
        return TABS.indexOf(String(value || '')) >= 0;
    }

    function emptyState() {
        return { tab: DEFAULT_TAB, filters: {}, page: 1, q: '', log: '' };
    }

    /**
     * The page state a URL stands for. A date key that is present but empty (`StartDate=`) means
     * "no range on purpose", which is how the default range stays off after Clear filters.
     * @param {string} search location.search
     * @param {string} [hash] location.hash; an old `#analytics` link still picks the tab
     */
    function parseLocation(search, hash) {
        var params = new URLSearchParams(search || '');
        var state = emptyState();

        var rawTab = params.get('tab') || params.get('ActiveTab');
        if (rawTab) {
            state.tab = normalizeTab(rawTab);
        } else {
            var fromHash = String(hash || '').replace(/^#/, '');
            if (isTabId(fromHash)) state.tab = fromHash;
        }

        ALL_FIELDS.forEach(function (name) {
            if (params.has(name)) state.filters[name] = params.get(name) || '';
        });
        // /Search "View all" links use the short alias
        if (state.filters.SearchTerm === undefined && params.get('search')) {
            state.filters.SearchTerm = params.get('search');
        }

        var page = parseInt(params.get('pageNumber'), 10);
        state.page = page >= 1 ? page : 1;
        state.q = params.get('q') || '';
        state.log = /^[0-9a-f-]{36}$/i.test(params.get('log') || '') ? params.get('log') : '';
        return state;
    }

    /** The query string for the page URL (no leading "?"). */
    function buildPageQuery(state) {
        var params = new URLSearchParams();
        if (state.tab && state.tab !== DEFAULT_TAB) params.set('tab', state.tab);

        ALL_FIELDS.forEach(function (name) {
            var value = state.filters[name];
            if (value === undefined || value === null) return;
            // An empty date is kept: it records "no range on purpose"
            if (value === '' && name !== 'StartDate' && name !== 'EndDate') return;
            params.set(name, value);
        });

        if (state.tab === 'execution-logs' && state.page > 1) params.set('pageNumber', String(state.page));
        if (state.q) params.set('q', state.q);
        if (state.log) params.set('log', state.log);
        return params.toString();
    }

    /** The query string for /api/commands/{tab}, API names, only the fields that tab reads. */
    function buildApiQuery(tab, filters, page) {
        var params = new URLSearchParams();
        (TAB_FIELDS[tab] || []).forEach(function (name) {
            var value = filters[name];
            if (value !== undefined && value !== null && String(value).trim() !== '') {
                params.set(API_NAMES[name], String(value).trim());
            }
        });
        if (tab === 'execution-logs' && page > 1) params.set('pageNumber', String(page));
        return params.toString();
    }

    function apiUrl(tab, filters, page) {
        var query = buildApiQuery(tab, filters, page);
        return '/api/commands/' + API_ROUTES[tab] + (query ? '?' + query : '');
    }

    function parseDay(value) {
        var m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(String(value || ''));
        if (!m) return null;
        return Date.UTC(+m[1], +m[2] - 1, +m[3]);
    }

    /**
     * The date-range rule the server enforces too (CommandsApiController.ValidateDateRange):
     * start not after end, and at most 90 days. Returns the message, or null when fine.
     */
    function validateRange(start, end) {
        var s = parseDay(start);
        var e = parseDay(end);
        if (s === null || e === null) return null;
        if (s > e) return 'The start date must be on or before the end date.';
        if ((e - s) / 86400000 > MAX_RANGE_DAYS) return 'Choose a date range of ' + MAX_RANGE_DAYS + ' days or less.';
        return null;
    }

    /**
     * Whether the Execution Logs tab starts on "Last 7 days". Only for a bare /Commands: any
     * filter in the URL (a search link from the Search page, a shared link) means the person
     * is looking for something specific, and a date window would hide part of it. An explicit
     * empty range (`StartDate=`) also keeps the default off.
     */
    function needsDefaultRange(state) {
        if (state.tab !== 'execution-logs') return false;
        var f = state.filters;
        if (f.StartDate !== undefined || f.EndDate !== undefined) return false;
        return !ALL_FIELDS.some(function (name) { return f[name]; });
    }

    /** True when every whitespace-separated term of the query is in the haystack. */
    function matchesQuery(haystack, query) {
        var terms = String(query || '').toLowerCase().split(/\s+/).filter(Boolean);
        var text = String(haystack || '').toLowerCase();
        return terms.every(function (term) { return text.indexOf(term) >= 0; });
    }

    /** How many filters a tab is using, for the badge on its Filters panel. */
    function countFilters(tab, filters) {
        return (TAB_FIELDS[tab] || []).filter(function (name) { return !!filters[name]; }).length;
    }

    /** The page number a pagination link points at (`?pageNumber=3`), or null. */
    function pageFromHref(href) {
        try {
            var url = new URL(href, 'http://localhost');
            var page = parseInt(url.searchParams.get('pageNumber'), 10);
            return page >= 1 ? page : null;
        } catch (e) {
            return null;
        }
    }

    var pure = {
        TABS: TABS,
        TAB_FIELDS: TAB_FIELDS,
        normalizeTab: normalizeTab,
        parseLocation: parseLocation,
        buildPageQuery: buildPageQuery,
        buildApiQuery: buildApiQuery,
        apiUrl: apiUrl,
        validateRange: validateRange,
        needsDefaultRange: needsDefaultRange,
        matchesQuery: matchesQuery,
        countFilters: countFilters,
        pageFromHref: pageFromHref
    };

    if (typeof document === 'undefined') {
        return pure;
    }

    // ------------------------------------------------------------------ the page

    var SEARCH_DEBOUNCE_MS = 150;

    var page = null;          // [data-commands-page]
    var tabContainer = null;  // the TabPanel container
    var current = emptyState();
    var loaded = {};          // tab -> api url whose content is on screen
    var controller = null;    // the in-flight load's AbortController
    var loadToken = 0;

    function $(selector, scope) { return (scope || document).querySelector(selector); }
    function $all(selector, scope) { return Array.prototype.slice.call((scope || document).querySelectorAll(selector)); }

    function panelFor(tab) { return $('[data-tab-panel-for="commandTabs"][data-tab-id="' + tab + '"]'); }
    function formFor(tab) { return $('[data-commands-filter-form="' + tab + '"]'); }
    function regionFor(tab) {
        var panel = panelFor(tab);
        return panel ? $('[data-tab-content]', panel) : null;
    }

    // ---- URL

    function syncUrl(mode) {
        var query = buildPageQuery(current);
        var url = window.location.pathname + (query ? '?' + query : '');
        if (url === window.location.pathname + window.location.search && !window.location.hash) return;
        try {
            if (mode === 'push') window.history.pushState(null, '', url);
            else window.history.replaceState(null, '', url);
        } catch (e) { /* a sandboxed frame: the page still works */ }
    }

    // ---- forms

    function field(form, name) { return form ? form.elements[name] : null; }

    /** Put the state's values into a tab's form (both forms share dates and server). */
    function fillForm(tab) {
        var form = formFor(tab);
        if (!form) return;
        TAB_FIELDS[tab].forEach(function (name) {
            var input = field(form, name);
            if (!input) return;
            var value = current.filters[name];
            if (value === undefined) return;
            if (name === 'CommandName') return; // the autocomplete owns it (rendered from the URL)
            input.value = value;
        });
        updateFormChrome(tab);
    }

    function readForm(tab) {
        var form = formFor(tab);
        var values = {};
        if (!form) return values;
        TAB_FIELDS[tab].forEach(function (name) {
            var input = field(form, name);
            values[name] = input ? String(input.value || '').trim() : '';
        });
        return values;
    }

    function updateFormChrome(tab) {
        var form = formFor(tab);
        if (!form) return;

        // Preset buttons show which range is on
        var detect = window.DateRangeFilter && window.DateRangeFilter.detectPreset;
        var active = detect ? detect(field(form, 'StartDate').value, field(form, 'EndDate').value) : null;
        $all('[data-date-preset]', form).forEach(function (button) {
            var on = button.getAttribute('data-date-preset') === active;
            button.setAttribute('aria-pressed', on ? 'true' : 'false');
            button.classList.toggle('btn-primary', on);
            button.classList.toggle('btn-secondary', !on);
        });

        // The count on the Filters panel
        var panel = form.closest('[data-filter-panel]');
        var badge = panel ? $('[data-filter-count]', panel) : null;
        if (badge) {
            var n = countFilters(tab, current.filters);
            badge.textContent = n + ' active';
            badge.classList.toggle('hidden', n === 0);
        }
    }

    function showRangeError(tab, message) {
        var form = formFor(tab);
        if (!form) return;
        var slot = $('[data-range-error]', form);
        var inputs = $all('[data-range-input]', form);
        if (slot) {
            slot.textContent = message || '';
            slot.classList.toggle('hidden', !message);
        }
        inputs.forEach(function (input) {
            input.classList.toggle('input-validation-error', !!message);
            if (message) input.setAttribute('aria-invalid', 'true');
            else input.removeAttribute('aria-invalid');
        });
        if (message) {
            var panel = form.closest('details');
            if (panel) panel.open = true;
        }
    }

    function shareAcross(fromTab) {
        // Dates and server are one filter for both tabs: keep the other form in step
        var other = fromTab === 'analytics' ? 'execution-logs' : 'analytics';
        ['StartDate', 'EndDate', 'GuildId'].forEach(function (name) {
            var input = field(formFor(other), name);
            if (input && current.filters[name] !== undefined) input.value = current.filters[name];
        });
        updateFormChrome(other);
    }

    function applyForm(tab) {
        var values = readForm(tab);

        var message = validateRange(values.StartDate, values.EndDate);
        showRangeError(tab, message);
        if (message) {
            var bad = $('[data-range-input]', formFor(tab));
            if (bad) bad.focus();
            return;
        }

        TAB_FIELDS[tab].forEach(function (name) {
            // Dates stay in the state even when empty: "no range on purpose"
            if (values[name] === '' && name !== 'StartDate' && name !== 'EndDate') delete current.filters[name];
            else current.filters[name] = values[name];
        });
        current.page = 1;
        current.tab = tab;

        shareAcross(tab);
        updateFormChrome(tab);
        syncUrl('push');
        load(tab, { focus: true });
    }

    function clearFilters(tab) {
        var form = formFor(tab);
        TAB_FIELDS[tab].forEach(function (name) {
            if (name === 'StartDate' || name === 'EndDate') current.filters[name] = '';
            else delete current.filters[name];
            var input = field(form, name);
            if (input && name !== 'CommandName') input.value = '';
        });
        var picker = window.AutocompleteManager && window.AutocompleteManager.get('CommandName-search');
        if (picker && tab === 'execution-logs') picker.clear();

        current.page = 1;
        showRangeError(tab, '');
        shareAcross(tab);
        updateFormChrome(tab);
        syncUrl('push');
        load(tab, { focus: true });
    }

    function applyDefaultRange() {
        var range = window.DateRangeFilter && window.DateRangeFilter.presetRange
            ? window.DateRangeFilter.presetRange('7days') : null;
        if (!range) return;
        current.filters.StartDate = range.start;
        current.filters.EndDate = range.end;
        fillForm('execution-logs');
        shareAcross('execution-logs');
        syncUrl('replace');
    }

    // ---- loading a tab

    function abortLoad() {
        if (controller) controller.abort();
        controller = null;
    }

    function afterLoad(tab, region) {
        if (window.timezoneUtils && typeof window.timezoneUtils.scan === 'function') {
            window.timezoneUtils.scan(region);
        }
        if (tab === 'analytics' && window.CommandsCharts) {
            window.CommandsCharts.render(region);
        }
    }

    function showFailure(tab, region, error, retry) {
        var status = error && error.status;
        var clientError = status >= 400 && status < 500 && status !== 401 && status !== 429;
        var message = error && error.message ? error.message : 'Could not load this. Try again.';

        if (clientError) {
            // The request itself was refused (a date range, usually): say so next to the dates
            showRangeError(tab, message);
            EmptyState.render(region, {
                type: 'noResults',
                title: 'Check the filters',
                description: message,
                announce: true
            });
            return;
        }

        EmptyState.error(region, {
            title: 'Could not load ' + (tab === 'analytics' ? 'analytics' : 'command logs'),
            description: message,
            onRetry: retry
        });
    }

    function load(tab, options) {
        options = options || {};
        var region = regionFor(tab);
        if (!region) return Promise.resolve();

        abortLoad();
        var token = ++loadToken;
        var localController = new AbortController();
        controller = localController;

        var url = apiUrl(tab, current.filters, current.page);
        var skeleton = window.Skeleton.show(region, tab === 'analytics'
            ? { kind: 'card', type: 'stats', label: 'Loading analytics' }
            : { kind: 'table', rows: 6, columns: 5, label: 'Loading command logs' });
        showRangeError(tab, '');
        delete loaded[tab];

        return window.ApiClient.getHtml(url, { signal: localController.signal }).then(function (html) {
            if (token !== loadToken) return;
            skeleton.hide();
            region.innerHTML = html;
            loaded[tab] = url;
            afterLoad(tab, region);
            if (options.focus) focusRegion(region);
        }).catch(function (error) {
            if (token !== loadToken || (error && error.name === 'AbortError')) return;
            skeleton.hide();
            showFailure(tab, region, error, function () { load(tab, { focus: true }); });
        }).then(function () {
            if (token === loadToken) controller = null;
        });
    }

    /** After a swap the user's place is the top of the results (N-9). */
    function focusRegion(region) {
        try {
            region.focus({ preventScroll: true });
            var rect = region.getBoundingClientRect();
            if (rect.top < 0 || rect.top > window.innerHeight * 0.6) {
                var reduce = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
                region.scrollIntoView({ behavior: reduce ? 'auto' : 'smooth', block: 'start' });
            }
        } catch (e) { /* focus is a courtesy */ }
    }

    function ensureLoaded(tab) {
        if (tab === 'command-list') return;
        if (tab === 'execution-logs' && needsDefaultRange(current)) applyDefaultRange();
        var url = apiUrl(tab, current.filters, current.page);
        if (loaded[tab] === url) return;
        load(tab);
    }

    // ---- command list search and module toggles

    var searchTimer = null;

    function applyCommandSearch(query) {
        var root = $('[data-command-list]');
        if (!root) return;
        var rows = $all('[data-command-row]', root);
        var searching = String(query || '').trim() !== '';
        var visibleRows = 0;

        $all('[data-command-module]', root).forEach(function (module) {
            var moduleRows = $all('[data-command-row]', module);
            var shown = 0;
            moduleRows.forEach(function (row) {
                var match = !searching || matchesQuery(row.getAttribute('data-search'), query);
                row.hidden = !match;
                if (match) shown++;
            });
            visibleRows += shown;
            module.hidden = shown === 0;

            var count = $('[data-module-count]', module);
            if (count) {
                var total = parseInt(count.getAttribute('data-total'), 10) || moduleRows.length;
                count.textContent = searching
                    ? shown + ' of ' + total + ' ' + (total === 1 ? 'command' : 'commands')
                    : Format.plural(total, 'command');
            }

            // Matches inside a collapsed module would be invisible: show them while searching
            var body = $('[data-module-body]', module);
            var toggle = $('[data-module-toggle]', module);
            if (body && toggle) {
                var collapsed = toggle.getAttribute('data-collapsed') === 'true';
                body.hidden = collapsed && !searching;
                toggle.setAttribute('aria-expanded', body.hidden ? 'false' : 'true');
            }
        });

        var status = $('[data-command-search-status]', root);
        if (status) {
            status.textContent = searching
                ? Format.plural(visibleRows, 'command') + ' match' + (visibleRows === 1 ? 'es' : '') + ' “' + String(query).trim() + '”'
                : Format.plural(rows.length, 'command') + ' in ' + Format.plural($all('[data-command-module]', root).length, 'module');
        }
        var empty = $('[data-command-search-empty]', root);
        var list = $('[data-command-modules]', root);
        if (empty) empty.hidden = !(searching && visibleRows === 0);
        if (list) list.hidden = searching && visibleRows === 0;
    }

    function setCommandSearch(query, options) {
        options = options || {};
        var input = $('[data-command-search]');
        if (input && input.value !== query) input.value = query;
        current.q = String(query || '').trim();
        applyCommandSearch(current.q);
        if (!options.silentUrl) syncUrl('replace');
    }

    function toggleModule(button) {
        var module = button.closest('[data-command-module]');
        var body = module && $('[data-module-body]', module);
        if (!body) return;
        var collapse = button.getAttribute('data-collapsed') !== 'true';
        button.setAttribute('data-collapsed', collapse ? 'true' : 'false');
        body.hidden = collapse;
        button.setAttribute('aria-expanded', collapse ? 'false' : 'true');
        var chevron = $('.module-chevron', button);
        if (chevron) chevron.classList.toggle('rotate-180', collapse);
    }

    // ---- events

    function onClick(event) {
        var target = event.target;
        if (!target.closest) return;

        var preset = target.closest('[data-date-preset]');
        if (preset) {
            var form = preset.closest('form');
            var range = window.DateRangeFilter.presetRange(preset.getAttribute('data-date-preset'));
            if (form && range) {
                field(form, 'StartDate').value = range.start;
                field(form, 'EndDate').value = range.end;
                form.requestSubmit();
            }
            return;
        }

        var clear = target.closest('[data-commands-action="clear-filters"]');
        if (clear) {
            clearFilters(current.tab === 'analytics' ? 'analytics' : 'execution-logs');
            return;
        }

        var link = target.closest('[data-commands-pagination] a[href]');
        if (link) {
            var n = pageFromHref(link.getAttribute('href'));
            if (n && !event.ctrlKey && !event.metaKey && !event.shiftKey && event.button === 0) {
                event.preventDefault();
                current.page = n;
                syncUrl('push');
                load('execution-logs', { focus: true });
            }
            return;
        }

        var logButton = target.closest('[data-log-id]');
        if (logButton) {
            openLog(logButton.getAttribute('data-log-id'), logButton);
            return;
        }

        var toggle = target.closest('[data-module-toggle]');
        if (toggle) {
            toggleModule(toggle);
            return;
        }

        if (target.closest('[data-command-search-clear]')) {
            setCommandSearch('');
            var input = $('[data-command-search]');
            if (input) input.focus();
            return;
        }

        var confirm = target.closest('[data-open-confirm]');
        if (confirm && window.quickActions) {
            window.quickActions.showConfirmationModal(confirm.getAttribute('data-open-confirm'));
        }
    }

    function onSubmit(event) {
        var form = event.target.closest && event.target.closest('[data-commands-filter-form]');
        if (!form) return;
        event.preventDefault();
        applyForm(form.getAttribute('data-commands-filter-form'));
    }

    function onInput(event) {
        var input = event.target;
        if (input.matches && input.matches('[data-command-search]')) {
            clearTimeout(searchTimer);
            searchTimer = setTimeout(function () { setCommandSearch(input.value); }, SEARCH_DEBOUNCE_MS);
        }
        if (input.matches && input.matches('[data-range-input]')) {
            // A typed fix clears the message at once
            var tab = input.closest('[data-commands-filter-form]').getAttribute('data-commands-filter-form');
            showRangeError(tab, '');
            updateFormChrome(tab);
        }
    }

    function onTabChange(event) {
        var tab = event.detail && event.detail.tabId;
        if (!isTabId(tab) || tab === current.tab) return;
        current.tab = tab;
        syncUrl('replace');
        ensureLoaded(tab);
    }

    function openLog(id, trigger) {
        if (!id || !window.commandLogModal) return;
        current.log = id;
        syncUrl('replace');
        window.commandLogModal.open(id, {
            trigger: trigger,
            onClose: function () {
                current.log = '';
                syncUrl('replace');
            }
        });
    }

    // ---- start

    function init() {
        page = $('[data-commands-page]');
        if (!page || page.getAttribute('data-initialised')) return;
        page.setAttribute('data-initialised', 'true');

        current = parseLocation(window.location.search, window.location.hash);
        var serverTab = page.getAttribute('data-active-tab');
        if (!window.location.search && !isTabId(window.location.hash.replace(/^#/, '')) && isTabId(serverTab)) {
            current.tab = serverTab;
        }

        tabContainer = $('[data-panel-id="commandTabs"]');
        if (tabContainer) {
            tabContainer.addEventListener('tabchange', onTabChange);
            // An old #analytics link (or the query's tab) decides the tab before anything loads
            if (window.TabPanel && current.tab !== serverTab && isTabId(current.tab)) {
                window.TabPanel.switchTo('commandTabs', current.tab);
            }
        }
        if (window.location.hash) syncUrl('replace');

        // Forms start from the URL (the server rendered most of it; the other form needs the shared fields)
        TABS.forEach(function (tab) { if (TAB_FIELDS[tab].length) fillForm(tab); });
        shareAcross('execution-logs');
        shareAcross('analytics');

        document.addEventListener('click', onClick);
        document.addEventListener('submit', onSubmit);
        document.addEventListener('input', onInput);
        // Back and Forward: the URL is the state, so load the view it describes
        window.addEventListener('popstate', function () { window.location.reload(); });

        // Command list search from ?q=
        if (current.q) setCommandSearch(current.q, { silentUrl: true });

        ensureLoaded(current.tab);

        if (current.log) openLog(current.log, null);
    }

    pure.init = init;
    return pure;
});
