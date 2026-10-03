/**
 * The wallet and ledger panel (Pages/Shared/Components/_CurrencyWalletPanel.cshtml).
 *
 * Reads the holder list for one currency, the ledger for a selected holder, and drives the three
 * balance actions the portal offers: mint, fine and adjust. Used by the guild currency detail page
 * and the bot-wide currency page, which differ only in which currency is loaded and what the
 * viewer is allowed to do.
 *
 * Every Discord snowflake stays a string: user IDs are larger than Number.MAX_SAFE_INTEGER, so
 * parsing one as a number rounds the last digits and every lookup fails. The member for mint and
 * fine is chosen with the user picker (an autocomplete); its hidden input holds the ID string.
 * The pure helpers at the top are exported for __tests__/currency-wallets.test.js.
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

    const TYPE_LABELS = {
        0: 'Mint', 1: 'Transfer in', 2: 'Transfer out',
        3: 'Spend', 4: 'Refund', 5: 'Fine', 6: 'Adjustment'
    };

    const PAGE_SIZE = 20;
    const SNOWFLAKE = /^\d{1,20}$/;
    const WHOLE_NUMBER = /^-?\d+$/;

    // ---- pure helpers ------------------------------------------------------

    /**
     * Checks a balance action's inputs and builds its request body.
     * @param {'mint'|'fine'|'adjust'} action
     * @param {{userId: string, amount: string, reason: string, openCase?: boolean}} input raw field values
     * @returns {{error: string|null, field?: string, body?: object}}
     */
    function validateAction(action, input) {
        const reason = (input.reason || '').trim();
        const rawAmount = String(input.amount === undefined || input.amount === null ? '' : input.amount).trim();

        if (action !== 'adjust' && !SNOWFLAKE.test(input.userId || '')) {
            return { error: 'Search for the member and pick them from the list.', field: 'wallet-action-user-search' };
        }

        if (!WHOLE_NUMBER.test(rawAmount)) {
            return { error: 'The amount must be a whole number.', field: 'wallet-action-amount' };
        }
        const amount = Number(rawAmount);
        if (!Number.isSafeInteger(amount) || amount === 0) {
            return { error: 'Enter an amount other than zero.', field: 'wallet-action-amount' };
        }
        if (action !== 'adjust' && amount < 0) {
            return { error: 'The amount must be greater than zero.', field: 'wallet-action-amount' };
        }

        if (!reason) return { error: 'A reason is required.', field: 'wallet-action-reason' };

        if (action === 'mint') return { error: null, body: { userId: input.userId, amount: amount, reason: reason } };
        if (action === 'fine') {
            return { error: null, body: { userId: input.userId, amount: amount, reason: reason, openCase: !!input.openCase } };
        }
        return { error: null, body: { amount: amount, reason: reason } };
    }

    /** Holders matching the search box: by name or by ID. */
    function filterWallets(wallets, term) {
        const needle = (term || '').trim().toLowerCase();
        if (!needle) return wallets;
        return wallets.filter((w) =>
            (w.username || '').toLowerCase().includes(needle) || String(w.userId).includes(needle));
    }

    // ---- browser -----------------------------------------------------------

    const state = {
        currencyId: null,
        symbol: '',
        canMint: false,
        canFine: false,
        canAdminister: false,
        wallets: [],
        selected: null,
        ledgerPage: 1,
        ledgerTotalPages: 1,
        action: null,
        adjustTransactionId: null,
        // Each load takes a number; only the latest may draw
        walletSeq: 0,
        ledgerSeq: 0
    };

    function el(id) {
        return document.getElementById(id);
    }

    function esc(value) {
        return SafeHtml.escape(value);
    }

    function formatAmount(amount) {
        return Format.currency(Number(amount), state.symbol);
    }

    function formatTimestamp(value) {
        return value ? Format.formatDate(value, 'datetime') : '';
    }

    function setMintEnabled() {
        const mint = document.querySelector('[data-wallet-action="mint"]');
        if (!mint) return;
        mint.disabled = !state.currencyId;
        mint.title = state.currencyId ? 'Mint units' : 'Pick a currency first';
    }

    function setFineEnabled(enabled) {
        const fine = document.querySelector('[data-wallet-action="fine"]');
        if (!fine) return;
        fine.disabled = !enabled;
        fine.title = enabled ? 'Fine this holder' : 'Select a holder first';
    }

    // ---- holders -----------------------------------------------------------

    function renderWallets() {
        const list = el('wallet-list');

        if (!state.currencyId) {
            list.innerHTML = '<p class="px-4 py-6 text-sm text-text-secondary">Select a currency to see its holders.</p>';
            return;
        }

        const wallets = filterWallets(state.wallets, el('wallet-search').value);

        if (!state.wallets.length) {
            list.innerHTML = '';
            EmptyState.render(list, {
                type: 'noData',
                title: el('wallet-debtors-only').checked ? 'No one is in debt' : 'No holders yet',
                description: el('wallet-debtors-only').checked
                    ? 'Every wallet is at zero or above.'
                    : 'Wallets appear here after the first mint or payment.',
                size: 'compact'
            });
            return;
        }

        if (!wallets.length) {
            list.innerHTML = '';
            EmptyState.filtered(list, {
                noun: 'holders',
                size: 'compact',
                onClear: () => { el('wallet-search').value = ''; renderWallets(); }
            });
            return;
        }

        list.innerHTML = wallets.map(function (wallet) {
            const selected = state.selected && state.selected.walletId === wallet.walletId;

            return `
                <button type="button" data-wallet-select="${esc(wallet.walletId)}" aria-pressed="${selected ? 'true' : 'false'}"
                        class="w-full text-left px-4 py-3 flex items-center justify-between gap-3 hover:bg-bg-hover transition-colors ${selected ? 'bg-bg-hover' : ''}">
                    <span class="min-w-0">
                        <span class="block text-sm text-text-primary truncate">${esc(wallet.username)}</span>
                        <span class="block text-xs text-text-tertiary font-mono truncate">${esc(wallet.userId)}</span>
                    </span>
                    <span class="text-sm font-medium whitespace-nowrap ${wallet.isInDebt ? 'text-error' : 'text-text-primary'}">
                        ${esc(formatAmount(wallet.balance))}${wallet.isInDebt ? ' <span class="sr-only">(in debt)</span>' : ''}
                    </span>
                </button>`;
        }).join('');
    }

    async function loadWallets() {
        if (!state.currencyId) {
            renderWallets();
            return;
        }

        const list = el('wallet-list');
        const mine = ++state.walletSeq;
        list.replaceChildren();
        const loading = Skeleton.show(list, { kind: 'list', rows: 4, delay: 200 });

        try {
            const wallets = await ApiClient.get(
                '/api/currencies/' + state.currencyId + '/wallets?debtorsOnly=' + el('wallet-debtors-only').checked,
                { errorMessage: 'The holders could not be loaded.' });
            if (mine !== state.walletSeq) return;

            loading.hide();
            state.wallets = wallets || [];
            renderWallets();
        } catch (error) {
            if (mine !== state.walletSeq) return;
            loading.hide();
            EmptyState.error(list, {
                title: 'Could not load the holders',
                description: 'Check your connection and try again.',
                size: 'compact',
                onRetry: loadWallets
            });
        }
    }

    // ---- ledger ------------------------------------------------------------

    function renderLedger(result) {
        const rows = el('ledger-rows');
        const items = (result && result.items) || [];

        if (!items.length) {
            rows.replaceChildren();
            EmptyState.render(rows, { type: 'noData', title: 'No transactions yet', description: 'This wallet has no history.', size: 'compact' });
            el('ledger-pager').classList.add('hidden');
            return;
        }

        rows.innerHTML = items.map(function (row) {
            const positive = row.amount >= 0;
            const detail = row.reason || row.featureKey || '';

            const adjust = state.canAdminister
                ? `<button type="button" data-ledger-adjust="${esc(row.id)}" class="btn btn-secondary btn-sm shrink-0">Adjust</button>`
                : '';

            return `
                <div class="px-4 py-3 flex items-start justify-between gap-3">
                    <div class="min-w-0">
                        <div class="flex items-center gap-2 flex-wrap">
                            <span class="text-sm font-medium ${positive ? 'text-success' : 'text-error'}">
                                ${positive ? '+' : '−'}${esc(formatAmount(Math.abs(row.amount)))}
                            </span>
                            <span class="badge badge-gray">${esc(TYPE_LABELS[row.type] || 'Entry')}</span>
                        </div>
                        ${detail ? `<p class="text-sm text-text-secondary mt-1 break-words">${esc(detail)}</p>` : ''}
                        <p class="text-xs text-text-tertiary mt-1">
                            ${esc(formatTimestamp(row.createdAt))} • balance ${esc(formatAmount(row.balanceAfter))}
                        </p>
                    </div>
                    ${adjust}
                </div>`;
        }).join('');

        state.ledgerTotalPages = Math.max(1, result.totalPages || 1);
        el('ledger-page-label').textContent =
            'Page ' + result.page + ' of ' + state.ledgerTotalPages + ' • ' + Format.plural(result.totalCount, 'entry', 'entries');
        el('ledger-pager').classList.remove('hidden');

        const prev = document.querySelector('[data-wallet-action="ledger-prev"]');
        const next = document.querySelector('[data-wallet-action="ledger-next"]');
        if (prev) prev.disabled = state.ledgerPage <= 1;
        if (next) next.disabled = state.ledgerPage >= state.ledgerTotalPages;
    }

    async function loadLedger() {
        if (!state.selected) return;

        const rows = el('ledger-rows');
        const mine = ++state.ledgerSeq;
        rows.replaceChildren();
        el('ledger-pager').classList.add('hidden');
        const loading = Skeleton.show(rows, { kind: 'list', rows: 4, delay: 200 });

        try {
            const result = await ApiClient.get(
                '/api/wallets/' + state.selected.walletId + '/ledger?page=' + state.ledgerPage + '&pageSize=' + PAGE_SIZE,
                { errorMessage: 'The ledger could not be loaded.' });
            if (mine !== state.ledgerSeq) return;

            loading.hide();
            renderLedger(result);
        } catch (error) {
            if (mine !== state.ledgerSeq) return;
            loading.hide();
            EmptyState.error(rows, {
                title: 'Could not load the ledger',
                description: 'Check your connection and try again.',
                size: 'compact',
                onRetry: loadLedger
            });
        }
    }

    function selectWallet(walletId) {
        const wallet = state.wallets.find((w) => w.walletId === walletId);
        if (!wallet) return;

        state.selected = wallet;
        state.ledgerPage = 1;

        el('ledger-subject').textContent = wallet.username + ' • ' + formatAmount(wallet.balance);
        setFineEnabled(state.canFine);

        renderWallets();
        loadLedger();

        // Below the wide layout the ledger sits under the holder list, often off screen
        const heading = el('ledger-heading');
        const reduced = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        if (heading && heading.getBoundingClientRect().top > window.innerHeight * 0.6) {
            heading.scrollIntoView({ behavior: reduced ? 'auto' : 'smooth', block: 'start' });
            heading.focus({ preventScroll: true });
        }
    }

    // ---- balance actions ---------------------------------------------------

    function picker() {
        // AutocompleteManager is a top-level const of autocomplete.js, so it is not on window
        return typeof AutocompleteManager !== 'undefined' ? AutocompleteManager.get('wallet-action-user-search') : null;
    }

    function openAction(action) {
        if (action !== 'adjust' && !state.currencyId) {
            toast.warning('Pick a currency first.');
            return;
        }

        state.action = action;
        if (action !== 'adjust') state.adjustTransactionId = null;

        const titles = { mint: 'Mint units', fine: 'Issue a fine', adjust: 'Adjust a transaction' };
        const help = {
            mint: 'Creates new units and credits them to a wallet. Every mint is recorded with its actor and reason.',
            fine: 'Takes units from a member. A fine stops at zero, or at this currency’s debt floor, and the receipt says when it was clamped.',
            adjust: 'Writes a signed correction against the same wallet, referencing the row you picked. This is the only way to fix a mistake.'
        };
        const amountHelp = {
            mint: 'Whole units, greater than zero.',
            fine: 'Whole units, greater than zero.',
            adjust: 'Signed: a positive amount credits the wallet, a negative one debits it.'
        };

        el('wallet-action-title').textContent = titles[action];
        el('wallet-action-help').textContent = help[action];
        el('wallet-action-amount-help').textContent = amountHelp[action];
        el('wallet-action-error').classList.add('hidden');
        el('wallet-action-amount').value = '';
        el('wallet-action-amount').min = action === 'adjust' ? '' : '1';
        el('wallet-action-reason').value = '';
        el('wallet-action-case').checked = false;

        el('wallet-action-user-row').classList.toggle('hidden', action === 'adjust');

        // Mint and fine start on the selected holder, by name
        const instance = picker();
        if (instance) {
            if (action !== 'adjust' && state.selected) instance.setValue(String(state.selected.userId), state.selected.username);
            else instance.clear();
        }

        const caseRow = el('wallet-action-case-row');
        caseRow.classList.toggle('hidden', action !== 'fine');
        caseRow.classList.toggle('flex', action === 'fine');

        quickActions.openDialog(el('wallet-action-modal'), {
            initialFocus: action === 'adjust' || state.selected ? '#wallet-action-amount' : '#wallet-action-user-search'
        });
    }

    function actionError(message, fieldId) {
        const box = el('wallet-action-error');
        box.textContent = message;
        box.classList.remove('hidden');
        const field = fieldId ? el(fieldId) : null;
        if (field) field.focus();
    }

    async function submitAction(event) {
        event.preventDefault();

        const checked = validateAction(state.action, {
            userId: el('wallet-action-user').value.trim(),
            amount: el('wallet-action-amount').value,
            reason: el('wallet-action-reason').value,
            openCase: el('wallet-action-case').checked
        });

        if (checked.error) {
            actionError(checked.error, checked.field);
            return;
        }

        el('wallet-action-error').classList.add('hidden');
        const submit = el('wallet-action-submit');
        if (typeof LoadingManager !== 'undefined') LoadingManager.setButtonLoading(submit, true);
        else submit.disabled = true;

        try {
            if (state.action === 'mint') {
                await ApiClient.post('/api/currencies/' + state.currencyId + '/mint', checked.body,
                    { errorMessage: 'The mint could not be completed.' });
                toast.success('Minted.');
            } else if (state.action === 'fine') {
                const result = await ApiClient.post('/api/currencies/' + state.currencyId + '/fine', checked.body,
                    { errorMessage: 'The fine could not be issued.' });

                toast.success(result && result.clampedAmount !== null && result.clampedAmount !== undefined
                    ? 'Fine applied, clamped to ' + formatAmount(result.clampedAmount) + '.'
                    : 'Fine applied.');
            } else {
                await ApiClient.post('/api/ledger/' + state.adjustTransactionId + '/adjust', checked.body,
                    { errorMessage: 'The adjustment could not be written.' });
                toast.success('Adjustment written.');
            }

            quickActions.closeDialog(el('wallet-action-modal'));
            await loadWallets();

            if (state.selected) {
                const refreshed = state.wallets.find((w) => w.walletId === state.selected.walletId);
                if (refreshed) {
                    state.selected = refreshed;
                    el('ledger-subject').textContent = refreshed.username + ' • ' + formatAmount(refreshed.balance);
                }
                await loadLedger();
            }
        } catch (error) {
            actionError(error.message || 'The request could not be completed.');
        } finally {
            if (typeof LoadingManager !== 'undefined') LoadingManager.setButtonLoading(submit, false);
            else submit.disabled = false;
        }
    }

    // ---- wiring ------------------------------------------------------------

    function onClick(event) {
        const target = event.target;
        if (!(target instanceof Element)) return;

        const walletButton = target.closest('[data-wallet-select]');
        if (walletButton) {
            selectWallet(walletButton.getAttribute('data-wallet-select'));
            return;
        }

        const adjustButton = target.closest('[data-ledger-adjust]');
        if (adjustButton) {
            state.adjustTransactionId = adjustButton.getAttribute('data-ledger-adjust');
            openAction('adjust');
            return;
        }

        const actionButton = target.closest('[data-wallet-action]');
        if (!actionButton || actionButton.disabled) return;

        const action = actionButton.getAttribute('data-wallet-action');

        if (action === 'refresh') loadWallets();
        if (action === 'mint') openAction('mint');
        if (action === 'fine') openAction('fine');

        if (action === 'ledger-prev' && state.ledgerPage > 1) {
            state.ledgerPage -= 1;
            loadLedger();
        }

        if (action === 'ledger-next' && state.ledgerPage < state.ledgerTotalPages) {
            state.ledgerPage += 1;
            loadLedger();
        }
    }

    let initialized = false;

    function init() {
        if (initialized || typeof document === 'undefined') return;
        const panel = el('currency-wallet-panel');
        if (!panel) return;
        initialized = true;

        state.currencyId = panel.getAttribute('data-currency-id') || null;
        state.symbol = panel.getAttribute('data-currency-symbol') || '';
        state.canMint = panel.getAttribute('data-can-mint') === 'true';
        state.canFine = panel.getAttribute('data-can-fine') === 'true';
        state.canAdminister = panel.getAttribute('data-can-administer') === 'true';

        document.addEventListener('click', onClick);
        el('wallet-action-form').addEventListener('submit', submitAction);
        el('wallet-search').addEventListener('input', renderWallets);
        el('wallet-debtors-only').addEventListener('change', loadWallets);

        setMintEnabled();
        loadWallets();
    }

    /**
     * Points the panel at a different currency. The bot-wide page calls this when the operator
     * picks a currency; the guild page renders with one already set and never calls it.
     */
    function setCurrency(currencyId, symbol) {
        state.currencyId = currencyId;
        state.symbol = symbol || '';
        state.selected = null;
        state.wallets = [];
        state.ledgerPage = 1;
        state.ledgerSeq += 1;

        el('ledger-subject').textContent = 'Select a holder to read their history.';
        el('ledger-rows').innerHTML = '<p class="px-4 py-6 text-sm text-text-secondary">No holder selected.</p>';
        el('ledger-pager').classList.add('hidden');

        setFineEnabled(false);
        setMintEnabled();
        loadWallets();
    }

    if (typeof window !== 'undefined') window.CurrencyWallets = { setCurrency: setCurrency };

    return { init, setCurrency, validateAction, filterWallets };
});
