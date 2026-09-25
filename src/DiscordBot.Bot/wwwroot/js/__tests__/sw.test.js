const test = require('node:test');
const assert = require('node:assert/strict');
const { classifyRequest, PRECACHE_URLS } = require('../../sw.js');

const ORIGIN = 'https://bot.example.com';

function req(path, { method = 'GET', mode = 'cors', origin = ORIGIN } = {}) {
    return { method, mode, url: origin + path };
}

test('navigations go network-first with an offline fallback', () => {
    assert.equal(classifyRequest(req('/Guilds/123', { mode: 'navigate' }), ORIGIN), 'navigate');
    assert.equal(classifyRequest(req('/', { mode: 'navigate' }), ORIGIN), 'navigate');
});

test('versioned static assets are cache-first', () => {
    assert.equal(classifyRequest(req('/css/app.css?v=abc123'), ORIGIN), 'static-immutable');
    assert.equal(classifyRequest(req('/js/toast.js?v=xyz'), ORIGIN), 'static-immutable');
});

test('unversioned static assets are stale-while-revalidate', () => {
    assert.equal(classifyRequest(req('/images/logo.svg'), ORIGIN), 'static');
    assert.equal(classifyRequest(req('/lib/signalr/signalr.min.js'), ORIGIN), 'static');
});

test('API, SignalR, auth, and health requests are never intercepted', () => {
    for (const path of ['/api/guilds', '/hubs/dashboard/negotiate', '/Account/Login', '/signin-discord', '/health', '/metrics']) {
        assert.equal(classifyRequest(req(path), ORIGIN), 'bypass', path);
        assert.equal(classifyRequest(req(path, { mode: 'navigate' }), ORIGIN), 'bypass', path);
    }
});

test('non-GET and cross-origin requests are never intercepted', () => {
    assert.equal(classifyRequest(req('/css/app.css', { method: 'POST' }), ORIGIN), 'bypass');
    assert.equal(classifyRequest(req('/css2?family=DM+Sans', { origin: 'https://fonts.googleapis.com' }), ORIGIN), 'bypass');
});

test('other same-origin subresources (page fetches, JSON) are not cached', () => {
    assert.equal(classifyRequest(req('/Guilds/123?handler=Partial'), ORIGIN), 'bypass');
});

test('the offline page is precached', () => {
    assert.ok(PRECACHE_URLS.includes('/offline.html'));
});
