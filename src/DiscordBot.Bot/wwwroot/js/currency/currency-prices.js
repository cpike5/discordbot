/**
 * The soundboard prices table (/Guilds/{guildId}/Currency/Prices).
 *
 * Each row carries the feature key the server built with CurrencyFeatureKeys.Soundboard(soundId),
 * and that exact string is what the save and remove calls send. The charge seam looks a sound's
 * price up by the same key, so a key assembled in JavaScript instead of read off the row would
 * save a price that nothing ever charges.
 *
 * Role IDs stay strings for the same reason they do everywhere else: a snowflake does not survive
 * a round trip through a JavaScript number.
 */
(function () {
    'use strict';

    const config = window.currencyPrices;
    if (!config) return;

    const state = {
        featureKey: null
    };

    function el(id) {
        return document.getElementById(id);
    }

    function toast(message, type) {
        if (window.quickActions && typeof window.quickActions.showToast === 'function') {
            window.quickActions.showToast(message, type);
        }
    }

    function row(featureKey) {
        return document.querySelector(`[data-price-row="${CSS.escape(featureKey)}"]`);
    }

    function symbolFor(currencyId) {
        const match = (config.currencies || []).find(c => c.id === currencyId);
        return match ? match.symbol : '';
    }

    function formError(message) {
        const box = el('price-form-error');
        box.textContent = message || '';
        box.classList.toggle('hidden', !message);
    }

    // ---- filtering ---------------------------------------------------------

    function applyFilter() {
        const term = (el('price-search').value || '').trim().toLowerCase();
        const mode = el('price-filter').value;

        document.querySelectorAll('[data-price-row]').forEach(function (tr) {
            const name = (tr.getAttribute('data-sound-name') || '').toLowerCase();
            const priced = tr.getAttribute('data-priced') === 'true';

            const matchesTerm = !term || name.includes(term);
            const matchesMode = mode === 'all' || (mode === 'priced' ? priced : !priced);

            tr.classList.toggle('hidden', !(matchesTerm && matchesMode));
        });
    }

    // ---- editor ------------------------------------------------------------

    function openEditor(featureKey) {
        const tr = row(featureKey);
        if (!tr) return;

        state.featureKey = featureKey;

        const priced = tr.getAttribute('data-priced') === 'true';
        const currencyId = tr.getAttribute('data-currency-id');
        const amount = tr.getAttribute('data-amount');
        const exempt = (tr.getAttribute('data-exempt-roles') || '').split(',').filter(Boolean);

        el('price-modal-title').textContent = priced ? 'Edit price' : 'Set price';
        el('price-modal-subject').textContent = tr.getAttribute('data-sound-name') || '';
        el('price-amount').value = amount || '';

        const currencySelect = el('price-currency');
        if (currencyId) currencySelect.value = currencyId;

        document.querySelectorAll('[data-exempt-role]').forEach(function (checkbox) {
            checkbox.checked = exempt.includes(checkbox.value);
        });

        formError('');
        el('price-modal').classList.remove('hidden');
        el('price-amount').focus();
    }

    function closeEditor() {
        el('price-modal').classList.add('hidden');
        state.featureKey = null;
    }

    /**
     * Repaints one row from a saved price, or from nothing when the feature went back to free, so
     * the table stays current without losing the reader's search and filter.
     */
    function repaint(featureKey, price) {
        const tr = row(featureKey);
        if (!tr) return;

        const priceCell = tr.querySelector('[data-price-cell]');
        const exemptCell = tr.querySelector('[data-exempt-cell]');
        const editButton = tr.querySelector('[data-price-action="edit"]');
        const actionCell = editButton ? editButton.parentElement : null;

        if (price) {
            const symbol = price.currencySymbol || symbolFor(price.currencyId);
            const exempt = price.exemptRoleIds || [];

            tr.setAttribute('data-priced', 'true');
            tr.setAttribute('data-currency-id', price.currencyId);
            tr.setAttribute('data-amount', price.amount);
            tr.setAttribute('data-exempt-roles', exempt.join(','));

            priceCell.innerHTML =
                `<span class="px-2 py-0.5 text-xs font-medium rounded-full bg-accent-orange/10 text-accent-orange border border-accent-orange/40">${Number(price.amount).toLocaleString()} ${symbol}</span>`;
            exemptCell.innerHTML = exempt.length
                ? `${exempt.length} role(s)`
                : '<span class="text-text-tertiary">—</span>';

            if (editButton) editButton.textContent = 'Edit price';

            if (actionCell && !actionCell.querySelector('[data-price-action="remove"]')) {
                const remove = document.createElement('button');
                remove.type = 'button';
                remove.setAttribute('data-price-action', 'remove');
                remove.setAttribute('data-feature-key', featureKey);
                remove.className = 'px-3 py-1.5 text-xs font-medium text-error border border-error/40 rounded-md hover:bg-error/10 transition-colors ml-1';
                remove.textContent = 'Make free';
                actionCell.appendChild(remove);
            }
        } else {
            tr.setAttribute('data-priced', 'false');
            tr.setAttribute('data-currency-id', '');
            tr.setAttribute('data-amount', '');
            tr.setAttribute('data-exempt-roles', '');

            priceCell.innerHTML = '<span class="text-text-tertiary">Free</span>';
            exemptCell.innerHTML = '<span class="text-text-tertiary">—</span>';

            if (editButton) editButton.textContent = 'Set price';

            const remove = tr.querySelector('[data-price-action="remove"]');
            if (remove) remove.remove();
        }

        applyFilter();
    }

    async function save(event) {
        event.preventDefault();

        const currencyId = el('price-currency').value;
        const amount = Number(el('price-amount').value);

        if (!currencyId) {
            formError('Pick the currency to charge.');
            return;
        }

        if (!Number.isInteger(amount) || amount <= 0) {
            formError('The price must be a whole number greater than zero.');
            return;
        }

        const exemptRoleIds = Array.from(document.querySelectorAll('[data-exempt-role]'))
            .filter(checkbox => checkbox.checked)
            .map(checkbox => checkbox.value);

        const saveButton = el('price-save');
        saveButton.disabled = true;

        try {
            const price = await window.ApiClient.put(
                `/api/guilds/${config.guildId}/prices/${encodeURIComponent(state.featureKey)}`,
                { currencyId: currencyId, amount: amount, exemptRoleIds: exemptRoleIds, isActive: true },
                { errorMessage: 'Failed to save the price' });

            repaint(state.featureKey, price);
            closeEditor();
            toast('Price saved.', 'success');
        } catch (error) {
            formError(error.message || 'Failed to save the price.');
        } finally {
            saveButton.disabled = false;
        }
    }

    async function remove(featureKey) {
        const tr = row(featureKey);
        const name = tr ? tr.getAttribute('data-sound-name') : 'this sound';

        const confirmed = window.quickActions && typeof window.quickActions.confirm === 'function'
            ? await window.quickActions.confirm({
                title: 'Make it free',
                message: `Stop charging for ${name}? The exempt roles are kept in case you price it again.`,
                variant: 'warning',
                confirmText: 'Make free'
            })
            : window.confirm(`Stop charging for ${name}?`);

        if (!confirmed) return;

        try {
            await window.ApiClient.del(
                `/api/guilds/${config.guildId}/prices/${encodeURIComponent(featureKey)}`,
                { errorMessage: 'Failed to remove the price' });

            repaint(featureKey, null);
            toast('That sound is free again.', 'success');
        } catch (error) {
            window.ApiClient.showErrorToast(error.message || 'Failed to remove the price.');
        }
    }

    // ---- wiring ------------------------------------------------------------

    function onClick(event) {
        const target = event.target.closest('[data-price-action]');
        if (!target) return;

        const action = target.getAttribute('data-price-action');
        const featureKey = target.getAttribute('data-feature-key');

        if (action === 'edit') openEditor(featureKey);
        if (action === 'remove') remove(featureKey);
        if (action === 'close') closeEditor();
    }

    function init() {
        document.addEventListener('click', onClick);
        el('price-form').addEventListener('submit', save);
        el('price-search').addEventListener('input', applyFilter);
        el('price-filter').addEventListener('change', applyFilter);

        document.addEventListener('keydown', function (event) {
            if (event.key === 'Escape') closeEditor();
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
