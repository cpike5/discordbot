const test = require('node:test');
const assert = require('node:assert/strict');
const DashboardStats = require('../dashboard-stats.js');

// A page with the four hero values, found by their data-stat-* attributes.
function fakePage() {
    const cards = {
        '[data-stat-servers]': { textContent: '3' },
        '[data-stat-members]': { textContent: '1,200' },
        '[data-stat-commands]': { textContent: '17' },
        '[data-stat-uptime]': { textContent: '99.9%' }
    };
    return { cards, querySelector: (selector) => cards[selector] || null };
}

const format = {
    number: (value, options) => {
        const digits = options && options.maximumFractionDigits;
        return value.toLocaleString('en-US', digits === undefined ? undefined : { maximumFractionDigits: digits });
    }
};

test('the payload field names are the ones the server sends', () => {
    // DashboardStatsDto camelCased; DashboardStatsTests checks the C# side against this file
    assert.deepEqual(Object.keys(DashboardStats.FIELDS).sort(),
        ['commandsLast24Hours', 'totalMembers', 'totalServers', 'uptimePercent24Hours']);
});

test('apply writes every field into its card', () => {
    const page = fakePage();
    const updated = DashboardStats.apply(page, {
        totalServers: 4, totalMembers: 12345, commandsLast24Hours: 18, uptimePercent24Hours: 97.46
    }, format);

    assert.equal(updated, 4);
    assert.equal(page.cards['[data-stat-servers]'].textContent, '4');
    assert.equal(page.cards['[data-stat-members]'].textContent, '12,345');
    assert.equal(page.cards['[data-stat-commands]'].textContent, '18');
    assert.equal(page.cards['[data-stat-uptime]'].textContent, '97.5%');
});

test('a missing or non-numeric field leaves its card alone', () => {
    const page = fakePage();
    const updated = DashboardStats.apply(page, { commandsLast24Hours: 20, totalServers: null, totalMembers: 'lots' }, format);

    assert.equal(updated, 1);
    assert.equal(page.cards['[data-stat-commands]'].textContent, '20');
    assert.equal(page.cards['[data-stat-servers]'].textContent, '3');
    assert.equal(page.cards['[data-stat-members]'].textContent, '1,200');
});

test('zero is a value, not a missing field', () => {
    const page = fakePage();
    DashboardStats.apply(page, { commandsLast24Hours: 0, uptimePercent24Hours: 0 }, format);

    assert.equal(page.cards['[data-stat-commands]'].textContent, '0');
    assert.equal(page.cards['[data-stat-uptime]'].textContent, '0%');
});

test('a page without a card for a field is not an error', () => {
    const page = { querySelector: () => null };
    assert.equal(DashboardStats.apply(page, { totalServers: 1 }, format), 0);
});

test('nothing to apply returns zero', () => {
    assert.equal(DashboardStats.apply(null, { totalServers: 1 }, format), 0);
    assert.equal(DashboardStats.apply(fakePage(), null, format), 0);
});
