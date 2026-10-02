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

test('responseType: "blob" returns a Blob body on a successful response', async () => {
    global.document = { querySelector: () => null };
    class FakeBlob {}
    const blob = new FakeBlob();
    mockFetch(async () => ({
        ok: true,
        status: 200,
        headers: { get: () => null },
        blob: async () => blob,
        text: async () => { throw new Error('should not read text for a blob response'); }
    }));

    const result = await ApiClient.post('/api/thing/preview', { x: 1 }, { responseType: 'blob' });

    assert.equal(result, blob);
});

test('responseType: "blob" still parses the error body as JSON on a non-ok response', async () => {
    global.document = { querySelector: () => null };
    mockFetch(async () => ({
        ok: false,
        status: 400,
        headers: { get: () => null },
        text: async () => JSON.stringify({ message: 'Bad request' })
    }));

    await assert.rejects(
        () => ApiClient.post('/api/thing/preview', {}, { responseType: 'blob' }),
        (err) => {
            assert.ok(err instanceof ApiClient.ApiClientError);
            assert.equal(err.message, 'Bad request');
            return true;
        }
    );
});

test('a 429 response surfaces status and a parsed Retry-After header on ApiClientError', async () => {
    global.document = { querySelector: () => null };
    mockFetch(async () => ({
        ok: false,
        status: 429,
        headers: { get: (name) => (name === 'Retry-After' ? '30' : null) },
        text: async () => JSON.stringify({ message: 'Rate limit exceeded' })
    }));

    await assert.rejects(
        () => ApiClient.post('/api/thing/send', {}),
        (err) => {
            assert.ok(err instanceof ApiClient.ApiClientError);
            assert.equal(err.status, 429);
            assert.equal(err.message, 'Rate limit exceeded');
            assert.deepEqual(err.retryAfter, { raw: '30', seconds: 30 });
            return true;
        }
    );
});

test('requestRaw exposes the raw Response so callers can read headers on a 429', async () => {
    global.document = { querySelector: () => null };
    mockFetch(async () => ({
        ok: false,
        status: 429,
        headers: { get: (name) => (name === 'Retry-After' ? '5' : null) },
        text: async () => JSON.stringify({ message: 'Slow down' })
    }));

    const { ok, status, response } = await ApiClient.postRaw('/api/thing/send', {});

    assert.equal(ok, false);
    assert.equal(status, 429);
    assert.equal(response.headers.get('Retry-After'), '5');
});

test('a custom errorMessage option is used as the fallback when the body carries no message', async () => {
    global.document = { querySelector: () => null };
    mockFetch(async () => ({
        ok: false,
        status: 500,
        headers: { get: () => null },
        text: async () => ''
    }));

    await assert.rejects(
        () => ApiClient.post('/api/thing/send', {}, { errorMessage: 'Failed to send message' }),
        (err) => {
            assert.equal(err.message, 'Failed to send message');
            return true;
        }
    );
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

test('sends X-Requested-With so the server answers an expired session with 401 JSON', async () => {
    global.document = { querySelector: () => null };
    let capturedHeaders;
    mockFetch(async (url, options) => {
        capturedHeaders = options.headers;
        return { ok: true, status: 200, text: async () => '{}' };
    });

    await ApiClient.get('/api/thing');

    assert.equal(capturedHeaders['X-Requested-With'], 'XMLHttpRequest');
    assert.equal(capturedHeaders['Accept'], 'application/json');
});

test('prefers a ProblemDetails detail over its title and an app message', async () => {
    assert.equal(
        ApiClient.extractErrorMessage({ title: 'Bad Request', detail: 'The channel was deleted.', message: 'Failed' }, 'x'),
        'The channel was deleted.');
});

test('a body-less failure gets a plain-language message, never "status 500"', async () => {
    global.document = { querySelector: () => null };
    mockFetch(async () => ({ ok: false, status: 500, text: async () => '' }));

    await assert.rejects(
        () => ApiClient.get('/api/thing'),
        (err) => {
            assert.equal(err.status, 500);
            assert.equal(err.message, 'Something went wrong on the server. Try again.');
            return true;
        }
    );
});

test('a 401 is reported as an expired session and shows one sign-in toast', async () => {
    global.document = { querySelector: () => null };
    const toasts = [];
    global.window = { toast: { error: (message, options) => toasts.push({ message, options }) }, location: { pathname: '/x', search: '' } };
    mockFetch(async () => ({
        ok: false,
        status: 401,
        text: async () => JSON.stringify({ title: 'Session expired', detail: 'Your session has expired.' })
    }));

    try {
        await assert.rejects(
            () => ApiClient.post('/api/thing', {}),
            (err) => {
                assert.equal(err.status, 401);
                assert.equal(err.kind, 'session-expired');
                assert.equal(err.sessionExpired, true);
                return true;
            }
        );
        assert.equal(toasts.length, 1);
        assert.equal(toasts[0].options.key, 'session-expired');
        assert.equal(toasts[0].options.action.label, 'Sign in');
    } finally {
        delete global.window;
    }
});

test('a redirect to the sign-in page is an expired session, not HTML data', async () => {
    global.document = { querySelector: () => null };
    mockFetch(async () => ({
        ok: true,
        status: 200,
        redirected: true,
        url: 'http://localhost/Account/Login?ReturnUrl=%2Fapi%2Fthing',
        headers: { get: () => 'text/html; charset=utf-8' },
        text: async () => '<!DOCTYPE html><html><body>Sign in</body></html>'
    }));

    const result = await ApiClient.getRaw('/api/thing');

    assert.equal(result.ok, false);
    assert.equal(result.status, 401);
    assert.equal(result.sessionExpired, true);
    assert.equal(typeof result.data, 'object');
});

test('an HTML error page is never returned as data', async () => {
    global.document = { querySelector: () => null };
    mockFetch(async () => ({
        ok: false,
        status: 404,
        headers: { get: () => 'text/html' },
        text: async () => '<!DOCTYPE html><html><body>Not found</body></html>'
    }));

    await assert.rejects(
        () => ApiClient.get('/api/missing'),
        (err) => {
            assert.equal(err.status, 404);
            assert.ok(!err.message.includes('<'), 'the message is not HTML');
            return true;
        }
    );
});

test('a network failure rejects with a plain-language ApiClientError', async () => {
    global.document = { querySelector: () => null };
    mockFetch(async () => { throw new TypeError('Failed to fetch'); });

    await assert.rejects(
        () => ApiClient.getRaw('/api/thing'),
        (err) => {
            assert.ok(err instanceof ApiClient.ApiClientError);
            assert.equal(err.kind, 'network');
            assert.equal(err.status, 0);
            assert.match(err.message, /Could not reach the server/);
            return true;
        }
    );
});

test('a request that outlives its timeout is aborted and reported as a timeout', async () => {
    global.document = { querySelector: () => null };
    mockFetch((url, options) => new Promise((resolve, reject) => {
        options.signal.addEventListener('abort', () => {
            const err = new Error('aborted');
            err.name = 'AbortError';
            reject(err);
        });
    }));

    await assert.rejects(
        () => ApiClient.get('/api/slow', { timeout: 10 }),
        (err) => {
            assert.equal(err.kind, 'timeout');
            assert.match(err.message, /took too long/);
            return true;
        }
    );
});

test("a caller's own abort passes through as an AbortError", async () => {
    global.document = { querySelector: () => null };
    mockFetch((url, options) => new Promise((resolve, reject) => {
        options.signal.addEventListener('abort', () => {
            const err = new Error('aborted');
            err.name = 'AbortError';
            reject(err);
        });
    }));
    const controller = new AbortController();

    const pending = ApiClient.get('/api/search', { signal: controller.signal });
    controller.abort();

    await assert.rejects(pending, (err) => err.name === 'AbortError');
});

test('a server error never shows its detail, which can be exception text', async () => {
    global.document = { querySelector: () => null };
    mockFetch(async () => ({
        ok: false,
        status: 500,
        text: async () => JSON.stringify({ message: 'Failed to load metrics', detail: 'Npgsql.NpgsqlException: connection refused', statusCode: 500 })
    }));

    await assert.rejects(
        () => ApiClient.get('/api/metrics'),
        (err) => {
            assert.equal(err.message, 'Failed to load metrics');
            return true;
        }
    );
});
