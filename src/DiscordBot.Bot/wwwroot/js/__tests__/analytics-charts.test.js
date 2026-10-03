const test = require('node:test');
const assert = require('node:assert/strict');

const A = require('../analytics-charts.js');

test('heatStep: none for 0, then four steps up to the maximum', () => {
    assert.equal(A.heatStep(0, 10), 0);
    assert.equal(A.heatStep(1, 10), 1);
    assert.equal(A.heatStep(3, 10), 2);
    assert.equal(A.heatStep(6, 10), 3);
    assert.equal(A.heatStep(10, 10), 4);
    assert.equal(A.heatStep(5, 0), 0, 'a zero maximum has no steps');
});

test('each gives every bar its own colour entry', () => {
    // A plain string would be shared between bars, and a theme change would not repaint it
    const colours = A.each('#123456', 3);
    assert.deepEqual(colours, ['#123456', '#123456', '#123456']);
    assert.notEqual(A.each('x', 2), A.each('x', 2));
});

test('compact abbreviates thousands for axis ticks only', () => {
    assert.equal(A.compact(999), '999');
    assert.equal(A.compact(1500), '1.5k');
});

test('dayLabel falls back to the date itself when Format is not loaded', () => {
    assert.equal(A.dayLabel('2026-10-01'), '2026-10-01');
});

test('number falls back to String when Format is not loaded', () => {
    assert.equal(A.number(1234), '1234');
});
