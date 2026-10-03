const test = require('node:test');
const assert = require('node:assert/strict');
const ApiClient = require('../api-client.js');

function mockFetch(handler) {
    global.fetch = async (url, options) => handler(url, options);
}

test.afterEach(() => {
    delete global.fetch;
    delete global.document;
});

test('getHtml asks for text/html and returns the markup of an ok response', async () => {
    global.document = { querySelector: () => null };
    let headers;
    mockFetch(async (url, options) => {
        headers = options.headers;
        return {
            ok: true,
            status: 200,
            headers: { get: () => 'text/html; charset=utf-8' },
            text: async () => '<div class="row">logs</div>'
        };
    });

    const html = await ApiClient.getHtml('/api/commands/logs');

    assert.equal(html, '<div class="row">logs</div>');
    assert.equal(headers['Accept'], 'text/html');
    assert.equal(headers['X-Requested-With'], 'XMLHttpRequest');
});

test('getHtml throws the server message of a 4xx JSON error', async () => {
    global.document = { querySelector: () => null };
    mockFetch(async () => ({
        ok: false,
        status: 400,
        headers: { get: () => 'application/problem+json' },
        text: async () => JSON.stringify({ title: 'Bad request', detail: 'Date range cannot exceed 90 days.' })
    }));

    await assert.rejects(() => ApiClient.getHtml('/api/commands/logs'), (err) => {
        assert.equal(err.status, 400);
        assert.equal(err.message, 'Date range cannot exceed 90 days.');
        return true;
    });
});

test('getHtml never shows an error page as the message of a 500', async () => {
    global.document = { querySelector: () => null };
    mockFetch(async () => ({
        ok: false,
        status: 500,
        headers: { get: () => 'text/html' },
        text: async () => '<html><body>Stack trace</body></html>'
    }));

    await assert.rejects(() => ApiClient.getHtml('/api/commands/logs'), (err) => {
        assert.equal(err.status, 500);
        assert.ok(!/stack trace|<html/i.test(err.message));
        return true;
    });
});

test('requestRaw without responseType html still refuses an HTML body', async () => {
    global.document = { querySelector: () => null };
    mockFetch(async () => ({
        ok: true,
        status: 200,
        headers: { get: () => 'text/html' },
        text: async () => '<div>page</div>'
    }));

    const result = await ApiClient.requestRaw('/api/thing');
    assert.equal(result.ok, false);
});
