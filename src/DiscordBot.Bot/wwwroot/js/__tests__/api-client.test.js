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

test('injects the RequestVerificationToken header from the hidden form input', async () => {
    global.document = {
        querySelector(selector) {
            assert.equal(selector, 'input[name="__RequestVerificationToken"]');
            return { value: 'the-token' };
        }
    };

    let capturedHeaders;
    mockFetch(async (url, options) => {
        capturedHeaders = options.headers;
        return {
            ok: true,
            status: 200,
            text: async () => JSON.stringify({ success: true })
        };
    });

    await ApiClient.post('/api/thing', { a: 1 });

    assert.equal(capturedHeaders['RequestVerificationToken'], 'the-token');
});

test('omits the token header when no anti-forgery input is present', async () => {
    global.document = { querySelector: () => null };

    let capturedHeaders;
    mockFetch(async (url, options) => {
        capturedHeaders = options.headers;
        return { ok: true, status: 200, text: async () => JSON.stringify({ success: true }) };
    });

    await ApiClient.get('/api/thing');

    assert.equal(capturedHeaders['RequestVerificationToken'], undefined);
});

test('resolves with parsed JSON on a successful response (success path)', async () => {
    global.document = { querySelector: () => null };
    mockFetch(async () => ({
        ok: true,
        status: 200,
        text: async () => JSON.stringify({ success: true, message: 'Saved', value: 42 })
    }));

    const data = await ApiClient.post('/api/thing', { x: 1 });

    assert.deepEqual(data, { success: true, message: 'Saved', value: 42 });
});

test('requestRaw never throws on a non-ok response and returns ok:false with parsed data', async () => {
    global.document = { querySelector: () => null };
    mockFetch(async () => ({
        ok: false,
        status: 400,
        text: async () => JSON.stringify({ success: false, message: 'Bad input' })
    }));

    const { ok, status, data } = await ApiClient.postRaw('/api/thing', { x: 1 });

    assert.equal(ok, false);
    assert.equal(status, 400);
    assert.equal(data.message, 'Bad input');
});

test('the throwing request() helper parses an app-style JSON error body and throws ApiClientError', async () => {
    global.document = { querySelector: () => null };
    mockFetch(async () => ({
        ok: false,
        status: 400,
        text: async () => JSON.stringify({ success: false, message: 'Validation failed' })
    }));

    await assert.rejects(
        () => ApiClient.post('/api/thing', { x: 1 }),
        (err) => {
            assert.ok(err instanceof ApiClient.ApiClientError);
            assert.equal(err.status, 400);
            assert.equal(err.message, 'Validation failed');
            return true;
        }
    );
});

test('the throwing request() helper parses an ASP.NET Core ProblemDetails error body', async () => {
    global.document = { querySelector: () => null };
    mockFetch(async () => ({
        ok: false,
        status: 422,
        text: async () => JSON.stringify({
            title: 'One or more validation errors occurred.',
            status: 422,
            errors: { Name: ['The Name field is required.'] }
        })
    }));

    await assert.rejects(
        () => ApiClient.put('/api/thing', {}),
        (err) => {
            assert.ok(err instanceof ApiClient.ApiClientError);
            assert.equal(err.status, 422);
            assert.equal(err.message, 'The Name field is required.');
            return true;
        }
    );
});

test('tolerates an empty response body (e.g. 204 No Content)', async () => {
    global.document = { querySelector: () => null };
    mockFetch(async () => ({ ok: true, status: 204, text: async () => '' }));

    const data = await ApiClient.del('/api/thing/1');

    assert.equal(data, null);
});

test('FormData bodies are sent through untouched, without a Content-Type override', async () => {
    global.document = { querySelector: () => null };
    class FakeFormData {}
    global.FormData = FakeFormData;
    const formData = new FakeFormData();

    let captured;
    mockFetch(async (url, options) => {
        captured = options;
        return { ok: true, status: 200, text: async () => JSON.stringify({ success: true }) };
    });

    await ApiClient.post('/api/thing', formData);

    assert.equal(captured.body, formData);
    assert.equal(captured.headers['Content-Type'], undefined);
    delete global.FormData;
});
