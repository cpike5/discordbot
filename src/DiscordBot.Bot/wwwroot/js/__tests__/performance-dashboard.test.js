const test = require('node:test');
const assert = require('node:assert/strict');

const dashboard = require('../performance/dashboard.js');
const alerts = require('../performance/tabs/alerts.js');
const ChartUtils = require('../performance/components/chart-utils.js');

// ---- dashboard.js: address and cache helpers ---------------------------------------------

test('parseLocation reads the tab and range from the query string', () => {
    const loc = dashboard.parseLocation('?tab=health&hours=168', '');
    assert.deepEqual(loc, { tab: 'health', hours: 168, fromHash: false });
});

test('parseLocation still opens the tab named by an old #hash, and says it came from the hash', () => {
    const loc = dashboard.parseLocation('', '#alerts');
    assert.equal(loc.tab, 'alerts');
    assert.equal(loc.fromHash, true);
});

test('the query tab wins over the hash', () => {
    const loc = dashboard.parseLocation('?tab=api', '#alerts');
    assert.equal(loc.tab, 'api');
    assert.equal(loc.fromHash, false);
});

test('an unknown tab or hash opens nothing in particular', () => {
    assert.equal(dashboard.parseLocation('?tab=bogus', '#nope').tab, null);
    assert.equal(dashboard.parseLocation('', '#main-content').tab, null);
});

test('tab ids are case-insensitive', () => {
    assert.equal(dashboard.parseLocation('?tab=Commands', '').tab, 'commands');
});

test('normalizeHours clamps to the supported ranges', () => {
    assert.equal(dashboard.normalizeHours('24'), 24);
    assert.equal(dashboard.normalizeHours('-5'), 24);
    assert.equal(dashboard.normalizeHours('0'), 24);
    assert.equal(dashboard.normalizeHours('48'), 168);
    assert.equal(dashboard.normalizeHours('168'), 168);
    assert.equal(dashboard.normalizeHours('500'), 720);
    assert.equal(dashboard.normalizeHours('100000'), 720);
    assert.equal(dashboard.normalizeHours('abc'), null);
    assert.equal(dashboard.normalizeHours(undefined), null);
});

test('an out-of-range ?hours= in the address is clamped, not trusted', () => {
    assert.equal(dashboard.parseLocation('?hours=99999', '').hours, 720);
    assert.equal(dashboard.parseLocation('?hours=-1', '').hours, 24);
});

test('buildUrl writes the tab, drops the default range and the hash, and keeps other values', () => {
    assert.equal(dashboard.buildUrl('/Admin/Performance', '', 'health', 24), '/Admin/Performance?tab=health');
    assert.equal(dashboard.buildUrl('/Admin/Performance', '?hours=720&tab=api', 'alerts', 168), '/Admin/Performance?hours=168&tab=alerts');
    assert.equal(dashboard.buildUrl('/Admin/Performance', '?x=1&hours=720', 'system', 24), '/Admin/Performance?x=1&tab=system');
});

test('a cached tab is fresh for five minutes and only for the same range', () => {
    const entry = { hours: 24, fetchedAt: 1000 };
    assert.equal(dashboard.isFresh(entry, 24, 1000 + dashboard.CACHE_MS - 1), true);
    assert.equal(dashboard.isFresh(entry, 24, 1000 + dashboard.CACHE_MS), false);
    assert.equal(dashboard.isFresh(entry, 168, 1500), false);
    assert.equal(dashboard.isFresh(undefined, 24, 1500), false);
});

// ---- alerts.js: threshold validation (F-3) ------------------------------------------------

test('warning must be lower than critical', () => {
    const result = alerts.validateRow({ warning: '90', critical: '80', originalWarning: '85', originalCritical: '95' });
    assert.match(result.warning, /lower than the critical/);
    assert.equal(alerts.isValid(result), false);
});

test('warning equal to critical is rejected', () => {
    assert.equal(alerts.isValid(alerts.validateRow({ warning: '50', critical: '50', originalWarning: '40', originalCritical: '60' })), false);
});

test('a valid pair passes', () => {
    assert.equal(alerts.isValid(alerts.validateRow({ warning: '60', critical: '90', originalWarning: '70', originalCritical: '95' })), true);
});

test('a blank warning is fine for a metric that never had one (event metrics)', () => {
    const result = alerts.validateRow({ warning: '', critical: '1', originalWarning: '', originalCritical: '1' });
    assert.equal(alerts.isValid(result), true);
});

test('a threshold that is set cannot be cleared, because the server reads blank as "leave it"', () => {
    const result = alerts.validateRow({ warning: '', critical: '95', originalWarning: '85', originalCritical: '95' });
    assert.match(result.warning, /cannot be cleared/);
});

test('negative and non-numeric values are rejected with a message on that field', () => {
    assert.match(alerts.validateRow({ warning: '-1', critical: '10', originalWarning: '5', originalCritical: '10' }).warning, /zero or greater/);
    assert.match(alerts.validateRow({ warning: '5', critical: 'abc', originalWarning: '5', originalCritical: '10' }).critical, /zero or greater/);
    assert.match(alerts.validateRow({ warning: '5', critical: 'Infinity', originalWarning: '5', originalCritical: '10' }).critical, /zero or greater/);
});

test('buildPayload sends only what changed, with server field names', () => {
    assert.deepEqual(alerts.buildPayload({ warning: '70' }), { warningThreshold: 70 });
    assert.deepEqual(alerts.buildPayload({ critical: '95.5', enabled: false }), { criticalThreshold: 95.5, isEnabled: false });
    assert.deepEqual(alerts.buildPayload({ enabled: true }), { isEnabled: true });
});

test('valueTone colours a current value against the thresholds being typed', () => {
    assert.equal(alerts.valueTone(95, 80, 90), 'text-error');
    assert.equal(alerts.valueTone(85, 80, 90), 'text-warning');
    assert.equal(alerts.valueTone(10, 80, 90), 'text-success');
    assert.equal(alerts.valueTone(NaN, 80, 90), null);
    assert.equal(alerts.valueTone(10, null, null), 'text-success');
});

// ---- chart-utils.js: text alternatives (A-6) ---------------------------------------------

test('summarize gives min, max and latest per series', () => {
    const text = ChartUtils.summarize(['a', 'b', 'c'], [{ label: 'Commands', data: [2, 9, 4] }], '');
    assert.match(text, /Commands: 3 values from 2 to 9, latest 4/);
});

test('summarize says so when a series has no values', () => {
    assert.match(ChartUtils.summarize([], [{ label: 'Latency', data: [] }], 'ms'), /Latency: no values/);
});

test('tableModel keeps the latest rows and says how many were dropped', () => {
    const labels = Array.from({ length: 100 }, (_, i) => 'p' + i);
    const model = ChartUtils.tableModel(labels, [{ label: 'X', data: labels.map((_, i) => i) }], 60);
    assert.equal(model.rows.length, 60);
    assert.equal(model.truncated, 40);
    assert.equal(model.rows[0][0], 'p40');
    assert.deepEqual(model.headers, ['Period', 'X']);
});

test('tableModel leaves a cell empty where a series has no value', () => {
    const model = ChartUtils.tableModel(['a', 'b'], [{ label: 'X', data: [1] }], 10);
    assert.deepEqual(model.rows, [['a', '1'], ['b', '']]);
});

test('applyThemeColors fills a dataset from its spec and leaves others alone', () => {
    const ds = { themeColors: { borderColor: c => c.secondary, backgroundColor: c => [c.success, c.track] } };
    ChartUtils.applyThemeColors(ds, { secondary: 'blue', success: 'green', track: 'grey' });
    assert.equal(ds.borderColor, 'blue');
    assert.deepEqual(ds.backgroundColor, ['green', 'grey']);
    const plain = { borderColor: 'red' };
    ChartUtils.applyThemeColors(plain, { secondary: 'blue' });
    assert.equal(plain.borderColor, 'red');
});
