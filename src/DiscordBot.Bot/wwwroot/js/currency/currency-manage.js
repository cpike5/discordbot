/**
 * Currency management: the create/edit dialog, deactivation, and the mint authority list.
 *
 * Shared by the guild currency page (/Guilds/{guildId}/Currency) and the bot-wide one
 * (/Admin/Currency); the page supplies the endpoints that differ through window.currencyPage, and
 * everything keyed by currency id is the same on both.
 *
 * Nothing here reloads the page. A saved currency is patched into its card or row (the elements
 * marked data-field, rendered by _CurrencyCard / _CurrencyRow), a new one is cloned from the
 * page's <template id="currency-item-template">, and a deactivated one is patched the same way.
 * Both dialogs are quickActions dialogs, so focus, Escape, scroll lock and stacking come from there.
 *
 * Discord snowflakes are handled as strings throughout: a role or user ID is larger than
 * Number.MAX_SAFE_INTEGER, and rendering one as a number silently rounds the last digits.
 * The pure helpers at the top are exported for __tests__/currency-manage.test.js.
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

    const PRINCIPAL = { user: 0, role: 1, system: 2 };
    const PRINCIPAL_LABELS = { 0: 'User', 1: 'Role', 2: 'Background jobs' };
    const SNOWFLAKE = /^\d{1,20}$/;
    const WHOLE_NUMBER = /^-?\d+$/;

    // ---- pure helpers ------------------------------------------------------

    /**
     * Checks the currency form and builds the request body.
     * @param {{name: string, symbol: string, allowNegative: boolean, debtFloor: string, isTransferable: boolean}} form
     * @returns {{error: string|null, field?: string, payload?: object}}
     */
    function validateCurrencyForm(form) {
        const name = (form.name || '').trim();
        const symbol = (form.symbol || '').trim();

        if (!name) return { error: 'Give the currency a name.', field: 'currency-name' };
        if (!symbol) return { error: 'Give the currency a symbol, such as an emoji.', field: 'currency-symbol' };

        let debtFloor = null;
        if (form.allowNegative) {
            const raw = String(form.debtFloor === undefined || form.debtFloor === null ? '' : form.debtFloor).trim();
            if (!WHOLE_NUMBER.test(raw)) {
                return { error: 'The debt floor must be a whole number, such as -100.', field: 'currency-debt-floor' };
            }
            debtFloor = Number(raw);
            if (!Number.isSafeInteger(debtFloor) || debtFloor >= 0) {
                return { error: 'The debt floor must be a whole number below zero, such as -100.', field: 'currency-debt-floor' };
            }
        }

        return {
            error: null,
            payload: {
                name: name,
                symbol: symbol,
                isTransferable: !!form.isTransferable,
                allowNegative: !!form.allowNegative,
                debtFloor: debtFloor
            }
        };
    }

    /**
     * Builds the grant request from the dialog's choices.
     * @param {number} type 0 user, 1 role, 2 system
     * @param {string} userId the picked user's ID (a string)
     * @param {string} roleId the picked role's ID (a string)
     * @returns {{error: string|null, field?: string, body?: object}}
     */
    function buildGrant(type, userId, roleId) {
        if (type === PRINCIPAL.system) {
            return { error: null, body: { principalType: type, principalId: null } };
        }
        if (type === PRINCIPAL.role) {
            if (!SNOWFLAKE.test(roleId || '')) return { error: 'Choose the role to grant.', field: 'authority-role' };
            return { error: null, body: { principalType: type, principalId: roleId } };
        }
        if (!SNOWFLAKE.test(userId || '')) {
            return { error: 'Search for the user and pick them from the list.', field: 'authority-user-search' };
        }
        return { error: null, body: { principalType: type, principalId: userId } };
    }

    /** "Transferable • debt to -100", as the bot-wide table shows it. */
    function rulesText(currency) {
        return (currency.isTransferable ? 'Transferable' : 'Not transferable') +
            (currency.allowNegative ? ' • debt to ' + currency.debtFloor : '');
    }

    /** What to call a grant: its name when the server found one, otherwise what it is. */
    function authorityLabel(authority) {
        if (authority.principalName) return authority.principalName;
        if (authority.principalType === PRINCIPAL.role) return 'Unknown role';
        if (authority.principalType === PRINCIPAL.system) return 'System';
        return 'Unknown user';
    }

    // ---- browser -----------------------------------------------------------

    const state = {
        currencies: [],
        editingId: null,
        authorityCurrencyId: null,
        authorityCurrencyName: ''
    };

    function config() {
        return (typeof window !== 'undefined' && window.currencyPage) || null;
    }

    function el(id) {
        return document.getElementById(id);
    }

    function showFormError(elementId, message) {
        const box = el(elementId);
        if (!box) return;
        box.textContent = message || '';
        box.classList.toggle('hidden', !message);
    }

    function setBusy(button, busy) {
        if (typeof LoadingManager !== 'undefined') LoadingManager.setButtonLoading(button, busy);
        else button.disabled = busy;
    }

    // ---- items on the page -------------------------------------------------

    function itemNode(id) {
        return document.querySelector('[data-currency-card="' + CSS.escape(id) + '"], [data-currency-row="' + CSS.escape(id) + '"]');
    }

    /** Writes a currency into a card or row: the text, the chips, the buttons and the links. */
    function patchItem(node, currency) {
        const field = (name) => node.querySelector('[data-field="' + name + '"]');
        const setText = (name, text) => { const target = field(name); if (target) target.textContent = text; };

        setText('symbol', currency.symbol);
        setText('name', currency.name);

        const circulation = field('circulation');
        if (circulation) {
            circulation.textContent = Format.number(Number(circulation.dataset.circulation || 0)) + ' ' + currency.symbol;
        }

        const deactivated = field('deactivated');
        if (deactivated) deactivated.classList.toggle('hidden', currency.isActive);
        node.classList.toggle('opacity-70', !currency.isActive);

        const transferable = field('transferable');
        if (transferable) {
            transferable.textContent = currency.isTransferable ? 'Transferable' : 'Not transferable';
            transferable.classList.toggle('badge-blue', currency.isTransferable);
            transferable.classList.toggle('badge-gray', !currency.isTransferable);
        }

        const debt = field('debt');
        if (debt) {
            debt.textContent = 'Debt to ' + currency.debtFloor;
            debt.classList.toggle('hidden', !currency.allowNegative);
        }

        setText('rules', rulesText(currency));

        // Edit and Deactivate only make sense while the currency is active
        node.querySelectorAll('[data-active-only]').forEach((button) => button.classList.toggle('hidden', !currency.isActive));

        // The buttons that name the currency in their own dialogs
        node.querySelectorAll('[data-currency-name]').forEach((target) => target.setAttribute('data-currency-name', currency.name));
        node.querySelectorAll('[data-currency-symbol]').forEach((target) => target.setAttribute('data-currency-symbol', currency.symbol));
    }

    function remember(currency) {
        const at = state.currencies.findIndex((c) => c.id === currency.id);
        if (at >= 0) state.currencies[at] = currency;
        else state.currencies.push(currency);
    }

    function updateStat() {
        const stat = el('admin-stat-currencies');
        if (stat) stat.textContent = Format.number(document.querySelectorAll('[data-currency-row]').length);
    }

    function syncEmptyState() {
        const any = document.querySelector('[data-currency-card], [data-currency-row]') !== null;
        const empty = el('currency-empty');
        const wrap = el('currency-table-wrap');
        if (empty) empty.classList.toggle('hidden', any);
        if (wrap) wrap.classList.toggle('hidden', !any);
    }

    /** Adds a new currency from the page's template, in name order, and brings it into view. */
    function addItem(currency) {
        const template = el('currency-item-template');
        const container = el('currency-grid') || el('currency-rows');
        if (!template || !container) return;

        const node = template.content.firstElementChild.cloneNode(true);
        const urls = config().walletsUrlTemplate;

        node.setAttribute(node.tagName === 'TR' ? 'data-currency-row' : 'data-currency-card', currency.id);
        node.setAttribute('data-currency-id', currency.id);
        node.querySelectorAll('[data-currency-id]').forEach((target) => target.setAttribute('data-currency-id', currency.id));
        node.querySelectorAll('[data-admin-currency-select]').forEach((target) => target.setAttribute('data-admin-currency-select', currency.id));

        const link = node.querySelector('[data-field="wallets-link"]');
        if (link && urls) link.setAttribute('href', urls.replace('00000000-0000-0000-0000-000000000000', currency.id));

        patchItem(node, currency);

        // Keep the list in name order, as the server renders it
        const name = (currency.name || '').toLowerCase();
        const siblings = Array.from(container.children);
        const before = siblings.find((sibling) => {
            const other = sibling.querySelector('[data-field="name"]');
            return other && other.textContent.toLowerCase().localeCompare(name) > 0;
        });
        container.insertBefore(node, before || null);

        syncEmptyState();
        updateStat();
        node.scrollIntoView({ block: 'nearest', behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth' });
    }

    // ---- currency dialog ---------------------------------------------------

    function syncDebtFloorVisibility() {
        el('currency-debt-floor-row').classList.toggle('hidden', !el('currency-allow-negative').checked);
    }

    function openCurrencyDialog(currency) {
        state.editingId = currency ? currency.id : null;

        el('currency-modal-title').textContent = currency ? 'Edit ' + currency.name : 'New currency';
        el('currency-name').value = currency ? currency.name : '';
        el('currency-symbol').value = currency ? currency.symbol : '';
        el('currency-transferable').checked = currency ? !!currency.isTransferable : config().scope !== 'global';
        el('currency-allow-negative').checked = currency ? !!currency.allowNegative : false;
        el('currency-debt-floor').value = currency && currency.debtFloor !== null && currency.debtFloor !== undefined
            ? currency.debtFloor
            : (config().defaultDebtFloor !== undefined ? config().defaultDebtFloor : -100);

        showFormError('currency-form-error', '');
        syncDebtFloorVisibility();
        quickActions.openDialog(el('currency-modal'), { initialFocus: '#currency-name' });
    }

    async function submitCurrency(event) {
        event.preventDefault();

        const checked = validateCurrencyForm({
            name: el('currency-name').value,
            symbol: el('currency-symbol').value,
            isTransferable: el('currency-transferable').checked,
            allowNegative: el('currency-allow-negative').checked,
            debtFloor: el('currency-debt-floor').value
        });

        if (checked.error) {
            showFormError('currency-form-error', checked.error);
            const field = el(checked.field);
            if (field) field.focus();
            return;
        }

        showFormError('currency-form-error', '');
        const saveButton = el('currency-save');
        setBusy(saveButton, true);

        try {
            if (state.editingId) {
                const updated = await ApiClient.put('/api/currencies/' + state.editingId, checked.payload, {
                    errorMessage: 'The currency could not be saved.'
                });
                remember(updated);
                const node = itemNode(updated.id);
                if (node) patchItem(node, updated);
                toast.success('Currency updated.');
            } else {
                const created = await ApiClient.post(config().createUrl, checked.payload, {
                    errorMessage: 'The currency could not be created.'
                });
                remember(created);
                addItem(created);
                toast.success('Currency created.');
            }

            quickActions.closeDialog(el('currency-modal'));
        } catch (error) {
            showFormError('currency-form-error', error.message || 'The currency could not be saved.');
        } finally {
            setBusy(saveButton, false);
        }
    }

    async function deactivate(button) {
        const currencyId = button.getAttribute('data-currency-id');
        const currencyName = button.getAttribute('data-currency-name') || 'this currency';

        const confirmed = await quickActions.confirm({
            title: 'Deactivate currency',
            message: 'Deactivate ' + currencyName + '? No more minting, spending, transfers or fines. Balances and history stay readable, and currencies are never deleted.',
            variant: 'warning',
            confirmText: 'Deactivate'
        });
        if (!confirmed) return;

        setBusy(button, true);
        try {
            await ApiClient.post('/api/currencies/' + currencyId + '/deactivate', null, {
                errorMessage: 'The currency could not be deactivated.'
            });

            const known = state.currencies.find((c) => c.id === currencyId);
            const frozen = Object.assign({}, known || {}, { id: currencyId, isActive: false });
            if (known) remember(frozen);

            const node = itemNode(currencyId);
            if (node && known) patchItem(node, frozen);
            else if (node) window.location.reload();

            toast.success(currencyName + ' deactivated.');
        } catch (error) {
            ApiClient.showErrorToast(error);
        } finally {
            setBusy(button, false);
        }
    }

    // ---- mint authorities --------------------------------------------------

    function syncAuthorityRows() {
        const type = Number(el('authority-type').value);
        el('authority-user-row').classList.toggle('hidden', type !== PRINCIPAL.user);
        const role = el('authority-role-row');
        if (role) role.classList.toggle('hidden', type !== PRINCIPAL.role);
    }

    function authorityItem(authority) {
        const row = document.createElement('div');
        row.className = 'flex items-center justify-between gap-3 px-3 py-2 bg-bg-primary border border-border-primary rounded-md';

        const text = document.createElement('div');
        text.className = 'min-w-0';

        const name = document.createElement('span');
        name.className = 'block text-sm text-text-primary break-words';
        name.textContent = authorityLabel(authority);
        text.appendChild(name);

        const meta = document.createElement('span');
        meta.className = 'flex items-center gap-2 text-xs text-text-tertiary';
        const kind = document.createElement('span');
        kind.textContent = PRINCIPAL_LABELS[authority.principalType] || 'Unknown';
        meta.appendChild(kind);

        if (authority.principalId) {
            // The ID stays available, one click from the clipboard
            const copy = document.createElement('button');
            copy.type = 'button';
            copy.className = 'font-mono rounded hover:text-text-primary break-all text-left';
            copy.setAttribute('data-copy', authority.principalId);
            copy.setAttribute('title', 'Copy ID');
            copy.setAttribute('aria-label', 'Copy ID ' + authority.principalId);
            copy.textContent = authority.principalId;
            meta.appendChild(copy);
        }
        text.appendChild(meta);
        row.appendChild(text);

        const revoke = document.createElement('button');
        revoke.type = 'button';
        revoke.className = 'btn btn-danger btn-sm shrink-0';
        revoke.setAttribute('data-authority-revoke', authority.id);
        revoke.setAttribute('data-authority-name', authorityLabel(authority));
        revoke.textContent = 'Revoke';
        row.appendChild(revoke);

        return row;
    }

    async function loadAuthorities() {
        const list = el('authorities-list');
        const currencyId = state.authorityCurrencyId;
        showFormError('authorities-error', '');
        list.replaceChildren();
        const loading = Skeleton.show(list, { kind: 'lines', count: 2, delay: 200 });

        try {
            const authorities = await ApiClient.get('/api/currencies/' + currencyId + '/mint-authorities', {
                errorMessage: 'The mint authorities could not be loaded.'
            });
            if (state.authorityCurrencyId !== currencyId) return;

            loading.hide();
            list.replaceChildren();

            if (!authorities || !authorities.length) {
                const none = document.createElement('p');
                none.className = 'text-sm text-text-tertiary';
                none.textContent = 'No one may mint this currency yet.';
                list.appendChild(none);
                return;
            }

            authorities.forEach((authority) => list.appendChild(authorityItem(authority)));
        } catch (error) {
            if (state.authorityCurrencyId !== currencyId) return;
            loading.hide();
            EmptyState.error(list, {
                title: 'Could not load the mint authorities',
                description: 'Check your connection and try again.',
                size: 'compact',
                onRetry: loadAuthorities
            });
        }
    }

    function openAuthorities(button) {
        state.authorityCurrencyId = button.getAttribute('data-currency-id');
        state.authorityCurrencyName = button.getAttribute('data-currency-name') || 'this currency';

        el('authorities-modal-title').textContent = 'Mint authorities for ' + state.authorityCurrencyName;
        el('authority-type').value = String(PRINCIPAL.user);
        syncAuthorityRows();
        clearPickers();
        showFormError('authorities-error', '');

        quickActions.openDialog(el('authorities-modal'), { initialFocus: '#authority-user-search' });
        loadAuthorities();
    }

    function clearPickers() {
        // AutocompleteManager is a top-level const of autocomplete.js, so it is not on window
        const picker = typeof AutocompleteManager !== 'undefined' ? AutocompleteManager.get('authority-user-search') : null;
        if (picker) picker.clear();
        const role = el('authority-role');
        if (role) role.value = '';
    }

    async function grantAuthority(event) {
        event.preventDefault();

        const checked = buildGrant(
            Number(el('authority-type').value),
            el('authority-user').value.trim(),
            el('authority-role') ? el('authority-role').value : '');

        if (checked.error) {
            showFormError('authorities-error', checked.error);
            const field = el(checked.field);
            if (field) field.focus();
            return;
        }

        showFormError('authorities-error', '');
        const button = el('authority-grant');
        setBusy(button, true);

        try {
            await ApiClient.post('/api/currencies/' + state.authorityCurrencyId + '/mint-authorities', checked.body, {
                errorMessage: 'Minting could not be granted.'
            });
            clearPickers();
            toast.success('Mint authority granted.');
            await loadAuthorities();
        } catch (error) {
            showFormError('authorities-error', error.message || 'Minting could not be granted.');
        } finally {
            setBusy(button, false);
        }
    }

    async function revokeAuthority(button) {
        const name = button.getAttribute('data-authority-name') || 'this grant';

        const confirmed = await quickActions.confirm({
            title: 'Revoke mint authority',
            message: 'Stop ' + name + ' minting ' + state.authorityCurrencyName + '? Units they already minted stay where they are.',
            variant: 'danger',
            confirmText: 'Revoke'
        });
        if (!confirmed) return;

        setBusy(button, true);
        try {
            await ApiClient.del('/api/currencies/' + state.authorityCurrencyId + '/mint-authorities/' + button.getAttribute('data-authority-revoke'), {
                errorMessage: 'The mint authority could not be revoked.'
            });
            toast.success('Mint authority revoked.');
            await loadAuthorities();
        } catch (error) {
            showFormError('authorities-error', error.message || 'The mint authority could not be revoked.');
            setBusy(button, false);
        }
    }

    function copyId(button) {
        const id = button.getAttribute('data-copy');
        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(id).then(
                () => toast.success('ID copied.'),
                () => toast.error('The ID could not be copied.'));
        }
    }

    // ---- wiring ------------------------------------------------------------

    async function loadCurrencies() {
        try {
            state.currencies = await ApiClient.get(config().listUrl + '?includeInactive=true', {
                errorMessage: 'The currencies could not be loaded.'
            }) || [];
            return true;
        } catch (error) {
            // The page is already rendered; the list only prefills the edit dialog, so say so when
            // it is needed rather than on every visit
            return false;
        }
    }

    async function edit(button) {
        const id = button.getAttribute('data-currency-id');
        let currency = state.currencies.find((c) => c.id === id);
        if (!currency && await loadCurrencies()) currency = state.currencies.find((c) => c.id === id);

        if (currency) openCurrencyDialog(currency);
        else toast.error('That currency could not be loaded. Refresh the page and try again.');
    }

    let initialized = false;

    function init() {
        if (initialized || typeof document === 'undefined' || !config() || !el('currency-form')) return;
        initialized = true;

        document.addEventListener('click', (event) => {
            const target = event.target;
            if (!(target instanceof Element)) return;

            const action = target.closest('[data-currency-action]');
            if (action) {
                const name = action.getAttribute('data-currency-action');
                if (name === 'create') openCurrencyDialog(null);
                if (name === 'edit') edit(action);
                if (name === 'deactivate') deactivate(action);
                if (name === 'authorities') openAuthorities(action);
                return;
            }

            const revoke = target.closest('[data-authority-revoke]');
            if (revoke) return revokeAuthority(revoke);

            const copy = target.closest('[data-copy]');
            if (copy) copyId(copy);
        });

        el('currency-form').addEventListener('submit', submitCurrency);
        el('currency-allow-negative').addEventListener('change', syncDebtFloorVisibility);
        el('authority-form').addEventListener('submit', grantAuthority);
        el('authority-type').addEventListener('change', function () {
            syncAuthorityRows();
            showFormError('authorities-error', '');
        });
        // Picking someone answers the "pick them from the list" message
        el('authority-form').addEventListener('autocomplete:select', function () { showFormError('authorities-error', ''); });
        el('authority-role') && el('authority-role').addEventListener('change', function () { showFormError('authorities-error', ''); });

        loadCurrencies();
    }

    return { init, validateCurrencyForm, buildGrant, rulesText, authorityLabel };
});
