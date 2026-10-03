const test = require('node:test');
const assert = require('node:assert/strict');
const DateRangeFilter = require('../date-range-filter.js');

// Built from local components, so the assertions hold in whatever zone the test runs in.
const localNoon = (y, m, d) => new Date(y, m - 1, d, 12, 0, 0);
const localLateEvening = (y, m, d) => new Date(y, m - 1, d, 23, 30, 0);

test('today is the local calendar day, even late in the evening', () => {
    const range = DateRangeFilter.presetRange('today', localLateEvening(2026, 10, 3));
    assert.deepEqual(range, { start: '2026-10-03', end: '2026-10-03' });
});

test('yesterday is a single local day', () => {
    assert.deepEqual(DateRangeFilter.presetRange('yesterday', localNoon(2026, 10, 3)), {
        start: '2026-10-02',
        end: '2026-10-02'
    });
});

test('7days, 30days and 90days end today and start N days earlier', () => {
    const now = localNoon(2026, 10, 3);
    assert.deepEqual(DateRangeFilter.presetRange('7days', now), { start: '2026-09-26', end: '2026-10-03' });
    assert.deepEqual(DateRangeFilter.presetRange('30days', now), { start: '2026-09-03', end: '2026-10-03' });
    assert.deepEqual(DateRangeFilter.presetRange('90days', now), { start: '2026-07-05', end: '2026-10-03' });
});

test('presets cross month and year boundaries', () => {
    assert.deepEqual(DateRangeFilter.presetRange('7days', localNoon(2026, 1, 3)), {
        start: '2025-12-27',
        end: '2026-01-03'
    });
    assert.deepEqual(DateRangeFilter.presetRange('yesterday', localNoon(2026, 3, 1)), {
        start: '2026-02-28',
        end: '2026-02-28'
    });
});

test('an unknown preset has no range', () => {
    assert.equal(DateRangeFilter.presetRange('fortnight', localNoon(2026, 10, 3)), null);
});

test('detectPreset recognises each preset and nothing else', () => {
    const now = localNoon(2026, 10, 3);
    for (const name of DateRangeFilter.PRESETS) {
        const range = DateRangeFilter.presetRange(name, now);
        assert.equal(DateRangeFilter.detectPreset(range.start, range.end, now), name);
    }
    assert.equal(DateRangeFilter.detectPreset('2026-09-01', '2026-10-03', now), null);
    assert.equal(DateRangeFilter.detectPreset('', '2026-10-03', now), null);
});

test('formatDateForInput pads and uses local components', () => {
    assert.equal(DateRangeFilter.formatDateForInput(new Date(2026, 0, 5, 23, 59)), '2026-01-05');
});
