const test = require('node:test');
const assert = require('node:assert/strict');
const C = require('../commands-page.js');

test('normalizeTab accepts the aliases other pages link with', () => {
    assert.equal(C.normalizeTab('logs'), 'execution-logs');
    assert.equal(C.normalizeTab('Execution-Logs'), 'execution-logs');
    assert.equal(C.normalizeTab('stats'), 'analytics');
    assert.equal(C.normalizeTab('nonsense'), 'command-list');
    assert.equal(C.normalizeTab(null), 'command-list');
});

test('parseLocation reads the tab, filters, page, search and open log from the query', () => {
    const state = C.parseLocation(
        '?tab=execution-logs&StartDate=2026-09-01&EndDate=2026-09-30&GuildId=123456789012345678&CommandName=ping&StatusFilter=false&SearchTerm=bob&pageNumber=3&q=sound&log=3f2504e0-4f89-11d3-9a0c-0305e82c3301');
    assert.equal(state.tab, 'execution-logs');
    assert.deepEqual(state.filters, {
        StartDate: '2026-09-01', EndDate: '2026-09-30', GuildId: '123456789012345678',
        CommandName: 'ping', StatusFilter: 'false', SearchTerm: 'bob'
    });
    assert.equal(state.page, 3);
    assert.equal(state.q, 'sound');
    assert.equal(state.log, '3f2504e0-4f89-11d3-9a0c-0305e82c3301');
});

test('GuildId stays a string, so a snowflake never loses digits', () => {
    const state = C.parseLocation('?GuildId=123456789012345678');
    assert.equal(typeof state.filters.GuildId, 'string');
    assert.equal(C.buildApiQuery('analytics', state.filters, 1), 'guildId=123456789012345678');
});

test('the Search page links (tab=logs&search=...) land on the logs tab with the term', () => {
    const state = C.parseLocation('?tab=logs&search=ping');
    assert.equal(state.tab, 'execution-logs');
    assert.equal(state.filters.SearchTerm, 'ping');
});

test('an old #analytics link still picks the tab, a stray hash does not', () => {
    assert.equal(C.parseLocation('', '#analytics').tab, 'analytics');
    assert.equal(C.parseLocation('', '#main-content').tab, 'command-list');
    assert.equal(C.parseLocation('?tab=analytics', '#execution-logs').tab, 'analytics');
});

test('a bad page number or log id is dropped', () => {
    const state = C.parseLocation('?pageNumber=-4&log=not-a-guid');
    assert.equal(state.page, 1);
    assert.equal(state.log, '');
});

test('buildPageQuery uses the page model names and leaves defaults out', () => {
    assert.equal(C.buildPageQuery({ tab: 'command-list', filters: {}, page: 1, q: '', log: '' }), '');
    const query = C.buildPageQuery({
        tab: 'execution-logs',
        filters: { StartDate: '2026-09-01', EndDate: '2026-09-30', StatusFilter: 'true', CommandName: '' },
        page: 2, q: '', log: ''
    });
    assert.equal(query, 'tab=execution-logs&StartDate=2026-09-01&EndDate=2026-09-30&StatusFilter=true&pageNumber=2');
});

test('an empty date range is kept in the URL, so the default range stays off', () => {
    const query = C.buildPageQuery({ tab: 'execution-logs', filters: { StartDate: '', EndDate: '' }, page: 1, q: '', log: '' });
    assert.equal(query, 'tab=execution-logs&StartDate=&EndDate=');
    assert.equal(C.needsDefaultRange(C.parseLocation('?' + query)), false);
});

test('the page number is only written for the logs tab', () => {
    assert.equal(C.buildPageQuery({ tab: 'analytics', filters: {}, page: 4, q: '', log: '' }), 'tab=analytics');
});

test('page state survives a round trip through the URL', () => {
    const original = {
        tab: 'execution-logs',
        filters: { StartDate: '2026-09-01', EndDate: '2026-09-30', GuildId: '123456789012345678', SearchTerm: 'bob smith' },
        page: 5, q: 'sound', log: ''
    };
    const parsed = C.parseLocation('?' + C.buildPageQuery(original));
    assert.deepEqual(parsed, original);
});

test('buildApiQuery sends the names /api/commands binds, and only the fields the tab reads', () => {
    const filters = { StartDate: '2026-09-01', EndDate: '2026-09-30', GuildId: '1', CommandName: 'ping', StatusFilter: 'false', SearchTerm: 'bob' };
    assert.equal(
        C.buildApiQuery('execution-logs', filters, 1),
        'startDate=2026-09-01&endDate=2026-09-30&guildId=1&commandName=ping&statusFilter=false&searchTerm=bob');
    assert.equal(C.buildApiQuery('analytics', filters, 3), 'startDate=2026-09-01&endDate=2026-09-30&guildId=1');
    assert.equal(C.buildApiQuery('execution-logs', filters, 3).endsWith('&pageNumber=3'), true);
});

test('apiUrl maps tabs to routes and never sends the old dateFrom / status names', () => {
    const url = C.apiUrl('execution-logs', { StartDate: '2026-09-01', StatusFilter: 'true' }, 1);
    assert.equal(url, '/api/commands/logs?startDate=2026-09-01&statusFilter=true');
    assert.ok(!/dateFrom|dateTo|[?&]status=/.test(url));
    assert.equal(C.apiUrl('analytics', {}, 1), '/api/commands/analytics');
    assert.equal(C.apiUrl('command-list', {}, 1), '/api/commands/list');
});

test('validateRange enforces the 90 day limit the server enforces', () => {
    assert.equal(C.validateRange('2026-01-01', '2026-03-31'), null); // 89 days
    assert.equal(C.validateRange('2026-01-01', '2026-04-01'), null); // 90 days
    assert.match(C.validateRange('2026-01-01', '2026-04-02'), /90 days or less/); // 91
    assert.match(C.validateRange('2026-10-02', '2026-10-01'), /start date must be on or before/i);
    assert.equal(C.validateRange('', '2026-10-01'), null);
    assert.equal(C.validateRange('2026-10-01', ''), null);
});

test('the default last-7-days range only applies to a bare logs view', () => {
    const logs = (search) => C.parseLocation(search);
    assert.equal(C.needsDefaultRange(logs('?tab=execution-logs')), true);
    // a Search "View all" link carries a term and must show every match
    assert.equal(C.needsDefaultRange(logs('?tab=logs&search=ping')), false);
    assert.equal(C.needsDefaultRange(logs('?tab=execution-logs&CommandName=ping')), false);
    assert.equal(C.needsDefaultRange(logs('?tab=execution-logs&StartDate=2026-09-01')), false);
    assert.equal(C.needsDefaultRange(logs('?tab=analytics')), false);
    assert.equal(C.needsDefaultRange(logs('')), false);
});

test('matchesQuery needs every term, in any order, ignoring case', () => {
    const text = '/sound play  play a sound from the soundboard';
    assert.equal(C.matchesQuery(text, ''), true);
    assert.equal(C.matchesQuery(text, 'PLAY sound'), true);
    assert.equal(C.matchesQuery(text, 'soundboard play'), true);
    assert.equal(C.matchesQuery(text, 'tts'), false);
    assert.equal(C.matchesQuery(text, 'sound tts'), false);
});

test('countFilters counts only the fields a tab reads', () => {
    const filters = { StartDate: '2026-09-01', EndDate: '', GuildId: '1', SearchTerm: 'bob' };
    assert.equal(C.countFilters('execution-logs', filters), 3);
    assert.equal(C.countFilters('analytics', filters), 2);
    assert.equal(C.countFilters('command-list', filters), 0);
});

test('pageFromHref reads pageNumber and nothing else', () => {
    assert.equal(C.pageFromHref('/Commands?tab=execution-logs&pageNumber=4'), 4);
    assert.equal(C.pageFromHref('/Commands?page=4'), null);
    assert.equal(C.pageFromHref('#'), null);
});
