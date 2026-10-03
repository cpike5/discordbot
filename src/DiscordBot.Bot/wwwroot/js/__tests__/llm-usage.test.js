const test = require('node:test');
const assert = require('node:assert/strict');
const LlmUsage = require('../llm-usage.js');

test('the records query carries the UTC range, the filters, the user and the page', () => {
    const query = new URLSearchParams(LlmUsage.buildQuery({
        fromUtc: '2026-09-01T07:00:00.000Z',
        toUtc: '2026-09-11T06:59:59.999Z',
        guildId: '123456789012345678',
        mode: 'Assistant'
    }, '987654321098765432', 2));

    assert.equal(query.get('from'), '2026-09-01T07:00:00.000Z');
    assert.equal(query.get('to'), '2026-09-11T06:59:59.999Z');
    // Snowflakes stay strings all the way through
    assert.equal(query.get('guildId'), '123456789012345678');
    assert.equal(query.get('userId'), '987654321098765432');
    assert.equal(query.get('mode'), 'Assistant');
    assert.equal(query.get('page'), '2');
    assert.equal(query.get('pageSize'), String(LlmUsage.PAGE_SIZE));
});

test('unset filters are left out of the query', () => {
    const query = new URLSearchParams(LlmUsage.buildQuery({ fromUtc: 'a', toUtc: 'b', guildId: null, mode: null }, null, 1));
    assert.equal(query.has('guildId'), false);
    assert.equal(query.has('mode'), false);
    assert.equal(query.has('userId'), false);
});

test('the pager text counts pages from the page size', () => {
    assert.match(LlmUsage.pagerText(1, 0), /^Page 1 of 1 \(0 messages\)$/);
    assert.match(LlmUsage.pagerText(2, 113), /^Page 2 of 5 \(113 messages\)$/);
    assert.match(LlmUsage.pagerText(1, 1), /^Page 1 of 1 \(1 message\)$/);
});
