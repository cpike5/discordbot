/**
 * Currency management: the create/edit form, deactivation, and the mint authority list.
 *
 * Shared by the guild currency page (/Guilds/{guildId}/Currency) and the bot-wide one
 * (/Admin/Currency); the page supplies the two endpoints that differ through
 * window.currencyPage, and everything keyed by currency id is the same on both.
 *
 * Discord snowflakes are handled as strings throughout - a role or user ID is larger than
 * Number.MAX_SAFE_INTEGER and rendering one as a number silently rounds the last digits.
 */
(function () {
    'use strict';

    const config = window.currencyPage;
    if (!config) return;

    const PRINCIPAL_LABELS = { 0: 'User', 1: 'Role', 2: 'System' };

    const state = {
        currencies: [],
        editingId: null,
        authorityCurrencyId: null
    };

    function el(id) {
        return document.getElementById(id);
    }

    function toast(message, type) {
        if (window.quickActions && typeof window.quickActions.showToast === 'function') {
            window.quickActions.showToast(message, type);
            return;
        }
        console.log(`[currency] ${type}: ${message}`);
    }

    function showFormError(elementId, message) {
        const box = el(elementId);
        if (!box) return;
        box.textContent = message;
        box.classList.toggle('hidden', !message);
    }

    function escapeHtml(value) {
        const div = document.createElement('div');
        div.textContent = value === null || value === undefined ? '' : String(value);
        return div.innerHTML;
    }

    // ---- currency form -----------------------------------------------------

    function openModal(currency) {
        state.editingId = currency ? currency.id : null;

        el('currency-modal-title').textContent = currency ? 'Edit Currency' : 'New Currency';
        el('currency-name').value = currency ? currency.name : '';
        el('currency-symbol').value = currency ? currency.symbol : '';
        el('currency-transferable').checked = currency ? !!currency.isTransferable : config.scope !== 'global';
        el('currency-allow-negative').checked = currency ? !!currency.allowNegative : false;
        el('currency-debt-floor').value = currency && currency.debtFloor !== null && currency.debtFloor !== undefined
            ? currency.debtFloor
            : (config.defaultDebtFloor !== undefined ? config.defaultDebtFloor : -100);

        showFormError('currency-form-error', '');
        syncDebtFloorVisibility();

        el('currency-modal').classList.remove('hidden');
        el('currency-name').focus();
    }

    function closeModal() {
        el('currency-modal').classList.add('hidden');
        state.editingId = null;
    }

    function syncDebtFloorVisibility() {
        const allowNegative = el('currency-allow-negative').checked;
        el('currency-debt-floor-row').classList.toggle('hidden', !allowNegative);
    }

    function readForm() {
        const allowNegative = el('currency-allow-negative').checked;
        const floorRaw = el('currency-debt-floor').value;

        return {
            name: el('currency-name').value.trim(),
            symbol: el('currency-symbol').value.trim(),
            isTransferable: el('currency-transferable').checked,
            allowNegative: allowNegative,
            debtFloor: allowNegative && floorRaw !== '' ? Number(floorRaw) : null
        };
    }

    async function submitForm(event) {
        event.preventDefault();

        const payload = readForm();

        if (!payload.name || !payload.symbol) {
            showFormError('currency-form-error', 'A name and a symbol are required.');
            return;
        }

        if (payload.allowNegative && (payload.debtFloor === null || payload.debtFloor >= 0)) {
            showFormError('currency-form-error', 'A debt floor is required when debt is allowed, and it must be negative.');
            return;
        }

        const saveButton = el('currency-save');
        saveButton.disabled = true;

        try {
            if (state.editingId) {
                await window.ApiClient.put(`/api/currencies/${state.editingId}`, payload, {
                    errorMessage: 'Failed to save the currency'
                });
                toast('Currency updated.', 'success');
            } else {
                await window.ApiClient.post(config.createUrl, payload, {
                    errorMessage: 'Failed to create the currency'
                });
                toast('Currency created.', 'success');
            }

            window.location.reload();
        } catch (error) {
            showFormError('currency-form-error', error.message || 'Failed to save the currency.');
        } finally {
            saveButton.disabled = false;
        }
    }

    async function deactivate(currencyId, currencyName) {
        const confirmed = window.quickActions && typeof window.quickActions.confirm === 'function'
            ? await window.quickActions.confirm({
                title: 'Deactivate currency',
                message: `Deactivate ${currencyName}? No more minting, spending, transfers or fines. Balances and history stay readable, and currencies are never deleted.`,
                variant: 'warning',
                confirmText: 'Deactivate'
            })
            : window.confirm(`Deactivate ${currencyName}?`);

        if (!confirmed) return;

        try {
            await window.ApiClient.post(`/api/currencies/${currencyId}/deactivate`, null, {
                errorMessage: 'Failed to deactivate the currency'
            });
            toast('Currency deactivated.', 'success');
            window.location.reload();
        } catch (error) {
            window.ApiClient.showErrorToast(error.message || 'Failed to deactivate the currency.');
        }
    }

    // ---- mint authorities --------------------------------------------------

    function renderAuthorities(authorities) {
        const list = el('authorities-list');

        if (!authorities.length) {
            list.innerHTML = '<p class="text-sm text-text-tertiary">No one may mint this currency yet.</p>';
            return;
        }

        list.innerHTML = authorities.map(function (authority) {
            const label = PRINCIPAL_LABELS[authority.principalType] || 'Unknown';
            const principal = authority.principalId
                ? `<span class="font-mono text-xs">${escapeHtml(authority.principalId)}</span>`
                : '<span class="text-text-tertiary">background jobs</span>';

            return `
                <div class="flex items-center justify-between gap-3 px-3 py-2 bg-bg-primary border border-border-primary rounded-md">
                    <div class="min-w-0">
                        <span class="text-sm text-text-primary">${escapeHtml(label)}</span>
                        <span class="text-sm text-text-secondary ml-2">${principal}</span>
                    </div>
                    <button type="button" data-authority-revoke="${escapeHtml(authority.id)}"
                            class="px-2 py-1 text-xs font-medium text-error border border-error/40 rounded-md hover:bg-error/10 transition-colors">
                        Revoke
                    </button>
                </div>`;
        }).join('');
    }

    async function loadAuthorities(currencyId) {
        const list = el('authorities-list');
        list.textContent = 'Loading…';
        showFormError('authorities-error', '');

        try {
            const authorities = await window.ApiClient.get(`/api/currencies/${currencyId}/mint-authorities`, {
                errorMessage: 'Failed to load mint authorities'
            });
            renderAuthorities(authorities || []);
        } catch (error) {
            list.textContent = '';
            showFormError('authorities-error', error.message || 'Failed to load mint authorities.');
        }
    }

    function openAuthorities(currencyId, currencyName) {
        state.authorityCurrencyId = currencyId;
        el('authorities-modal-title').textContent = `Mint Authorities — ${currencyName}`;
        el('authority-id').value = '';
        el('authorities-modal').classList.remove('hidden');
        loadAuthorities(currencyId);
    }

    function closeAuthorities() {
        el('authorities-modal').classList.add('hidden');
        state.authorityCurrencyId = null;
    }

    async function grantAuthority(event) {
        event.preventDefault();

        const principalType = Number(el('authority-type').value);
        const principalIdRaw = el('authority-id').value.trim();

        // System grants name no principal; user and role grants are a snowflake, kept as a string
        // so JavaScript never rounds it.
        if (principalType !== 2 && !/^\d{1,20}$/.test(principalIdRaw)) {
            showFormError('authorities-error', 'Enter the Discord ID of the user or role to grant.');
            return;
        }

        try {
            await window.ApiClient.post(`/api/currencies/${state.authorityCurrencyId}/mint-authorities`, {
                principalType: principalType,
                principalId: principalType === 2 ? null : principalIdRaw
            }, { errorMessage: 'Failed to grant mint authority' });

            el('authority-id').value = '';
            toast('Mint authority granted.', 'success');
            loadAuthorities(state.authorityCurrencyId);
        } catch (error) {
            showFormError('authorities-error', error.message || 'Failed to grant mint authority.');
        }
    }

    async function revokeAuthority(authorityId) {
        try {
            await window.ApiClient.del(
                `/api/currencies/${state.authorityCurrencyId}/mint-authorities/${authorityId}`,
                { errorMessage: 'Failed to revoke mint authority' });

            toast('Mint authority revoked.', 'success');
            loadAuthorities(state.authorityCurrencyId);
        } catch (error) {
            showFormError('authorities-error', error.message || 'Failed to revoke mint authority.');
        }
    }

    // ---- wiring ------------------------------------------------------------

    async function loadCurrencies() {
        try {
            state.currencies = await window.ApiClient.get(`${config.listUrl}?includeInactive=true`, {
                errorMessage: 'Failed to load currencies'
            }) || [];
        } catch (error) {
            // The page is already rendered server-side; the list is only needed to prefill the
            // edit form, so a failure here is reported rather than fatal.
            window.ApiClient.showErrorToast(error.message || 'Failed to load currencies.');
        }
    }

    function onClick(event) {
        const actionTarget = event.target.closest('[data-currency-action]');

        if (actionTarget) {
            const action = actionTarget.getAttribute('data-currency-action');
            const currencyId = actionTarget.getAttribute('data-currency-id');
            const currencyName = actionTarget.getAttribute('data-currency-name') || 'this currency';

            if (action === 'create') openModal(null);
            if (action === 'close-modal') closeModal();
            if (action === 'close-authorities') closeAuthorities();
            if (action === 'deactivate') deactivate(currencyId, currencyName);
            if (action === 'authorities') openAuthorities(currencyId, currencyName);

            if (action === 'edit') {
                const currency = state.currencies.find(c => c.id === currencyId);
                if (currency) {
                    openModal(currency);
                } else {
                    window.ApiClient.showErrorToast('That currency could not be loaded. Refresh and try again.');
                }
            }

            return;
        }

        const revokeTarget = event.target.closest('[data-authority-revoke]');
        if (revokeTarget) {
            revokeAuthority(revokeTarget.getAttribute('data-authority-revoke'));
        }
    }

    function init() {
        document.addEventListener('click', onClick);
        el('currency-form').addEventListener('submit', submitForm);
        el('currency-allow-negative').addEventListener('change', syncDebtFloorVisibility);
        el('authority-form').addEventListener('submit', grantAuthority);

        document.addEventListener('keydown', function (event) {
            if (event.key !== 'Escape') return;
            closeModal();
            closeAuthorities();
        });

        loadCurrencies();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
