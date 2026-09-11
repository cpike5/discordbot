/**
 * The wallet and ledger panel (Pages/Shared/Components/_CurrencyWalletPanel.cshtml).
 *
 * Reads the holder list for one currency, the ledger for a selected holder, and drives the three
 * balance actions the portal offers: mint, fine and adjust. Used by the guild currency detail page
 * and the bot-wide currency page, which differ only in which currency is loaded and what the
 * viewer is allowed to do.
 *
 * Every Discord snowflake stays a string: user IDs are larger than Number.MAX_SAFE_INTEGER, so
 * parsing one as a number rounds the last digits and every lookup fails.
 */
(function () {
    'use strict';

    const TYPE_LABELS = {
        0: 'Mint', 1: 'Transfer in', 2: 'Transfer out',
        3: 'Spend', 4: 'Refund', 5: 'Fine', 6: 'Adjustment'
    };

    const PAGE_SIZE = 20;

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
        adjustTransactionId: null
    };

    function el(id) {
        return document.getElementById(id);
    }

    function panel() {
        return el('currency-wallet-panel');
    }

    function toast(message, type) {
        if (window.quickActions && typeof window.quickActions.showToast === 'function') {
            window.quickActions.showToast(message, type);
        }
    }

    function escapeHtml(value) {
        const div = document.createElement('div');
        div.textContent = value === null || value === undefined ? '' : String(value);
        return div.innerHTML;
    }

    function formatAmount(amount) {
        return `${Number(amount).toLocaleString()} ${state.symbol}`.trim();
    }

    /**
     * Server timestamps are UTC but carry no offset, so they are marked as UTC before being
     * rendered in the reader's own timezone.
     */
    function formatTimestamp(value) {
        if (!value) return '';
        const normalized = /[Zz]|[+-]\d{2}:\d{2}$/.test(value) ? value : `${value}Z`;
        const date = new Date(normalized);
        return isNaN(date.getTime()) ? value : date.toLocaleString();
    }

    // ---- holders -----------------------------------------------------------

    function visibleWallets() {
        const term = (el('wallet-search').value || '').trim().toLowerCase();
        if (!term) return state.wallets;

        return state.wallets.filter(w =>
            (w.username || '').toLowerCase().includes(term) || String(w.userId).includes(term));
    }

    function renderWallets() {
        const list = el('wallet-list');
        const wallets = visibleWallets();

        if (!state.currencyId) {
            list.innerHTML = '<p class="px-4 py-6 text-sm text-text-secondary">Select a currency to see its holders.</p>';
            return;
        }

        if (!wallets.length) {
            list.innerHTML = '<p class="px-4 py-6 text-sm text-text-secondary">No holders match.</p>';
            return;
        }

        list.innerHTML = wallets.map(function (wallet) {
            const selected = state.selected && state.selected.walletId === wallet.walletId;

            return `
                <button type="button" data-wallet-select="${escapeHtml(wallet.walletId)}"
                        class="w-full text-left px-4 py-3 flex items-center justify-between gap-3 hover:bg-bg-hover transition-colors ${selected ? 'bg-bg-hover' : ''}">
                    <span class="min-w-0">
                        <span class="block text-sm text-text-primary truncate">${escapeHtml(wallet.username)}</span>
                        <span class="block text-xs text-text-tertiary font-mono truncate">${escapeHtml(wallet.userId)}</span>
                    </span>
                    <span class="text-sm font-medium whitespace-nowrap ${wallet.isInDebt ? 'text-error' : 'text-text-primary'}">
                        ${escapeHtml(formatAmount(wallet.balance))}
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
        list.innerHTML = '<p class="px-4 py-6 text-sm text-text-secondary">Loading holders…</p>';

        const debtorsOnly = el('wallet-debtors-only').checked;

        try {
            state.wallets = await window.ApiClient.get(
                `/api/currencies/${state.currencyId}/wallets?debtorsOnly=${debtorsOnly}`,
                { errorMessage: 'Failed to load holders' }) || [];
            renderWallets();
        } catch (error) {
            list.innerHTML = `<p class="px-4 py-6 text-sm text-error">${escapeHtml(error.message || 'Failed to load holders.')}</p>`;
        }
    }

    // ---- ledger ------------------------------------------------------------

    function renderLedger(result) {
        const rows = el('ledger-rows');
        const items = (result && result.items) || [];

        if (!items.length) {
            rows.innerHTML = '<p class="px-4 py-6 text-sm text-text-secondary">No transactions yet.</p>';
            el('ledger-pager').classList.add('hidden');
            return;
        }

        rows.innerHTML = items.map(function (row) {
            const positive = row.amount >= 0;
            const detail = row.reason || row.featureKey || '';

            const adjust = state.canAdminister
                ? `<button type="button" data-ledger-adjust="${escapeHtml(row.id)}"
                           class="px-2 py-1 text-xs font-medium text-text-secondary border border-border-primary rounded-md hover:bg-bg-hover transition-colors">Adjust</button>`
                : '';

            return `
                <div class="px-4 py-3 flex items-start justify-between gap-3">
                    <div class="min-w-0">
                        <div class="flex items-center gap-2 flex-wrap">
                            <span class="text-sm font-medium ${positive ? 'text-success' : 'text-error'}">
                                ${positive ? '+' : '−'}${escapeHtml(formatAmount(Math.abs(row.amount)))}
                            </span>
                            <span class="px-2 py-0.5 text-xs rounded-full bg-bg-tertiary text-text-secondary border border-border-primary">
                                ${escapeHtml(TYPE_LABELS[row.type] || 'Entry')}
                            </span>
                        </div>
                        ${detail ? `<p class="text-sm text-text-secondary mt-1 break-words">${escapeHtml(detail)}</p>` : ''}
                        <p class="text-xs text-text-tertiary mt-1">
                            ${escapeHtml(formatTimestamp(row.createdAt))} • balance ${escapeHtml(formatAmount(row.balanceAfter))}
                        </p>
                    </div>
                    ${adjust}
                </div>`;
        }).join('');

        state.ledgerTotalPages = Math.max(1, result.totalPages || 1);
        el('ledger-page-label').textContent =
            `Page ${result.page} of ${state.ledgerTotalPages} • ${result.totalCount} entries`;
        el('ledger-pager').classList.remove('hidden');
    }

    async function loadLedger() {
        if (!state.selected) return;

        const rows = el('ledger-rows');
        rows.innerHTML = '<p class="px-4 py-6 text-sm text-text-secondary">Loading history…</p>';

        try {
            const result = await window.ApiClient.get(
                `/api/wallets/${state.selected.walletId}/ledger?page=${state.ledgerPage}&pageSize=${PAGE_SIZE}`,
                { errorMessage: 'Failed to load the ledger' });
            renderLedger(result);
        } catch (error) {
            rows.innerHTML = `<p class="px-4 py-6 text-sm text-error">${escapeHtml(error.message || 'Failed to load the ledger.')}</p>`;
            el('ledger-pager').classList.add('hidden');
        }
    }

    function selectWallet(walletId) {
        const wallet = state.wallets.find(w => w.walletId === walletId);
        if (!wallet) return;

        state.selected = wallet;
        state.ledgerPage = 1;

        el('ledger-subject').textContent = `${wallet.username} • ${formatAmount(wallet.balance)}`;

        const fineButton = document.querySelector('[data-wallet-action="fine"]');
        if (fineButton) fineButton.disabled = false;

        renderWallets();
        loadLedger();
    }

    // ---- balance actions ---------------------------------------------------

    function openAction(action) {
        state.action = action;
        state.adjustTransactionId = action === 'adjust' ? state.adjustTransactionId : null;

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
        el('wallet-action-reason').value = '';
        el('wallet-action-case').checked = false;

        const userRow = el('wallet-action-user-row');
        userRow.classList.toggle('hidden', action === 'adjust');
        el('wallet-action-user').value = action !== 'adjust' && state.selected ? state.selected.userId : '';

        const caseRow = el('wallet-action-case-row');
        caseRow.classList.toggle('hidden', action !== 'fine');
        caseRow.classList.toggle('flex', action === 'fine');

        el('wallet-action-modal').classList.remove('hidden');
        (action === 'adjust' ? el('wallet-action-amount') : el('wallet-action-user')).focus();
    }

    function closeAction() {
        el('wallet-action-modal').classList.add('hidden');
        state.action = null;
        state.adjustTransactionId = null;
    }

    function actionError(message) {
        const box = el('wallet-action-error');
        box.textContent = message;
        box.classList.remove('hidden');
    }

    async function submitAction(event) {
        event.preventDefault();

        const amount = Number(el('wallet-action-amount').value);
        const reason = el('wallet-action-reason').value.trim();
        const userId = el('wallet-action-user').value.trim();

        if (!reason) {
            actionError('A reason is required.');
            return;
        }

        if (!Number.isFinite(amount) || amount === 0) {
            actionError('Enter an amount.');
            return;
        }

        if (state.action !== 'adjust') {
            if (amount <= 0) {
                actionError('The amount must be greater than zero.');
                return;
            }
            if (!/^\d{1,20}$/.test(userId)) {
                actionError('Enter the Discord ID of the member.');
                return;
            }
        }

        const submit = el('wallet-action-submit');
        submit.disabled = true;

        try {
            if (state.action === 'mint') {
                await window.ApiClient.post(`/api/currencies/${state.currencyId}/mint`,
                    { userId: userId, amount: amount, reason: reason },
                    { errorMessage: 'Failed to mint' });
                toast('Minted.', 'success');
            } else if (state.action === 'fine') {
                const result = await window.ApiClient.post(`/api/currencies/${state.currencyId}/fine`,
                    { userId: userId, amount: amount, reason: reason, openCase: el('wallet-action-case').checked },
                    { errorMessage: 'Failed to issue the fine' });

                toast(result && result.clampedAmount !== null && result.clampedAmount !== undefined
                    ? `Fine applied, clamped to ${formatAmount(result.clampedAmount)}.`
                    : 'Fine applied.', 'success');
            } else {
                await window.ApiClient.post(`/api/ledger/${state.adjustTransactionId}/adjust`,
                    { amount: amount, reason: reason },
                    { errorMessage: 'Failed to adjust the transaction' });
                toast('Adjustment written.', 'success');
            }

            closeAction();
            await loadWallets();

            if (state.selected) {
                const refreshed = state.wallets.find(w => w.walletId === state.selected.walletId);
                if (refreshed) {
                    state.selected = refreshed;
                    el('ledger-subject').textContent = `${refreshed.username} • ${formatAmount(refreshed.balance)}`;
                }
                await loadLedger();
            }
        } catch (error) {
            actionError(error.message || 'The request could not be completed.');
        } finally {
            submit.disabled = false;
        }
    }

    // ---- wiring ------------------------------------------------------------

    function onClick(event) {
        const walletButton = event.target.closest('[data-wallet-select]');
        if (walletButton) {
            selectWallet(walletButton.getAttribute('data-wallet-select'));
            return;
        }

        const adjustButton = event.target.closest('[data-ledger-adjust]');
        if (adjustButton) {
            state.adjustTransactionId = adjustButton.getAttribute('data-ledger-adjust');
            openAction('adjust');
            return;
        }

        const actionButton = event.target.closest('[data-wallet-action]');
        if (!actionButton) return;

        const action = actionButton.getAttribute('data-wallet-action');

        if (action === 'refresh') loadWallets();
        if (action === 'mint') openAction('mint');
        if (action === 'fine') openAction('fine');
        if (action === 'close-modal') closeAction();

        if (action === 'ledger-prev' && state.ledgerPage > 1) {
            state.ledgerPage -= 1;
            loadLedger();
        }

        if (action === 'ledger-next' && state.ledgerPage < state.ledgerTotalPages) {
            state.ledgerPage += 1;
            loadLedger();
        }
    }

    function init() {
        const root = panel();
        if (!root) return;

        state.currencyId = root.getAttribute('data-currency-id') || null;
        state.symbol = root.getAttribute('data-currency-symbol') || '';
        state.canMint = root.getAttribute('data-can-mint') === 'true';
        state.canFine = root.getAttribute('data-can-fine') === 'true';
        state.canAdminister = root.getAttribute('data-can-administer') === 'true';

        document.addEventListener('click', onClick);
        el('wallet-action-form').addEventListener('submit', submitAction);
        el('wallet-search').addEventListener('input', renderWallets);
        el('wallet-debtors-only').addEventListener('change', loadWallets);

        document.addEventListener('keydown', function (event) {
            if (event.key === 'Escape') closeAction();
        });

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

        el('ledger-subject').textContent = 'Select a holder to read their history.';
        el('ledger-rows').innerHTML = '<p class="px-4 py-6 text-sm text-text-secondary">No holder selected.</p>';
        el('ledger-pager').classList.add('hidden');

        const fineButton = document.querySelector('[data-wallet-action="fine"]');
        if (fineButton) fineButton.disabled = true;

        loadWallets();
    }

    window.CurrencyWallets = { setCurrency: setCurrency };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
