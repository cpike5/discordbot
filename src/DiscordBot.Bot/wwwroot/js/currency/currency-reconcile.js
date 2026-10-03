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

    function esc(value) {
        return SafeHtml.escape(value);
    }

    function render(html, live) {
        const box = document.getElementById('reconcile-result');
        box.innerHTML = html;
        // The result of a button press is announced; the "checking" line is not repeated
        box.setAttribute('role', live ? 'status' : 'presentation');
        box.classList.remove('hidden');
    }

    async function run() {
        const button = document.getElementById('reconcile-button');
        if (typeof LoadingManager !== 'undefined') LoadingManager.setButtonLoading(button, true);
        else button.disabled = true;
        render('<p class="px-4 py-3 text-sm text-text-secondary bg-bg-secondary border border-border-primary rounded-lg">Checking every wallet against its ledger rows…</p>', false);

        try {
            const drifted = await ApiClient.get('/api/currencies/' + encodeURIComponent(config.currencyId) + '/reconcile', {
                errorMessage: 'The reconcile check could not be run.'
            }) || [];

            if (!drifted.length) {
                render('<p class="px-4 py-3 text-sm text-success bg-success/10 border border-success/40 rounded-lg">Every wallet matches the sum of its ledger rows.</p>', true);
                return;
            }

            const rows = drifted.map(function (wallet) {
                return `<li class="flex flex-wrap items-center justify-between gap-x-3 py-1">
                    <span class="font-mono text-xs break-all">${esc(wallet.userId)}</span>
                    <span>cached ${esc(Format.number(Number(wallet.cachedBalance)))} • ledger ${esc(Format.number(Number(wallet.ledgerSum)))} • off by ${esc(Format.number(Number(wallet.difference)))}</span>
                </li>`;
            }).join('');

            render(`
                <div class="px-4 py-3 text-sm bg-error/10 border border-error/40 rounded-lg text-text-primary">
                    <p class="font-medium text-error mb-2">${esc(Format.plural(drifted.length, 'wallet'))} ${drifted.length === 1 ? 'does' : 'do'} not reconcile.</p>
                    <ul class="divide-y divide-border-primary">${rows}</ul>
                </div>`, true);
        } catch (error) {
            render(`<p class="px-4 py-3 text-sm text-error bg-error/10 border border-error/40 rounded-lg">${esc(error.message || 'The reconcile check could not be run.')}</p>`, true);
        } finally {
            if (typeof LoadingManager !== 'undefined') LoadingManager.setButtonLoading(button, false);
            else button.disabled = false;
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
