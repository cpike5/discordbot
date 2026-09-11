/**
 * The reconcile check on the currency detail page.
 *
 * Compares every wallet's cached balance against the sum of its ledger rows. The rows are the
 * record and the cached balance is a convenience, so anything this reports is a bug worth
 * chasing, not a number to correct by hand.
 */
(function () {
    'use strict';

    const config = window.currencyDetails;
    if (!config) return;

    function escapeHtml(value) {
        const div = document.createElement('div');
        div.textContent = value === null || value === undefined ? '' : String(value);
        return div.innerHTML;
    }

    function render(html) {
        const box = document.getElementById('reconcile-result');
        box.innerHTML = html;
        box.classList.remove('hidden');
    }

    async function run() {
        const button = document.getElementById('reconcile-button');
        button.disabled = true;
        render('<p class="px-4 py-3 text-sm text-text-secondary bg-bg-secondary border border-border-primary rounded-lg">Checking every wallet against its ledger rows…</p>');

        try {
            const drifted = await window.ApiClient.get(`/api/currencies/${config.currencyId}/reconcile`, {
                errorMessage: 'Failed to run the reconcile check'
            }) || [];

            if (!drifted.length) {
                render('<p class="px-4 py-3 text-sm text-success bg-success/10 border border-success/40 rounded-lg">Every wallet matches the sum of its ledger rows.</p>');
                return;
            }

            const rows = drifted.map(function (wallet) {
                return `<li class="flex items-center justify-between gap-3 py-1">
                    <span class="font-mono text-xs">${escapeHtml(wallet.userId)}</span>
                    <span>cached ${escapeHtml(wallet.cachedBalance)} • ledger ${escapeHtml(wallet.ledgerSum)} • off by ${escapeHtml(wallet.difference)}</span>
                </li>`;
            }).join('');

            render(`
                <div class="px-4 py-3 text-sm bg-error/10 border border-error/40 rounded-lg text-text-primary">
                    <p class="font-medium text-error mb-2">${drifted.length} wallet(s) do not reconcile.</p>
                    <ul class="divide-y divide-border-primary">${rows}</ul>
                </div>`);
        } catch (error) {
            render(`<p class="px-4 py-3 text-sm text-error bg-error/10 border border-error/40 rounded-lg">${escapeHtml(error.message || 'Failed to run the reconcile check.')}</p>`);
        } finally {
            button.disabled = false;
        }
    }

    function init() {
        const button = document.getElementById('reconcile-button');
        if (button) button.addEventListener('click', run);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
