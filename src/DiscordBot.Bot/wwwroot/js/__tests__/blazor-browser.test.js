const test = require('node:test');
const assert = require('node:assert/strict');

// browser.js is an ES module (see wwwroot/js/blazor/package.json); dynamic
// import() works from this CommonJS test file regardless. It is imported
// once per test via a fresh promise per test.afterEach reset since the
// storage helpers close over `window.localStorage`, which we stub per test.
const browserModulePromise = import('../blazor/browser.js');

test.afterEach(() => {
    delete global.window;
    delete global.navigator;
});

test('resolveTimeZone returns the IANA zone from Intl.DateTimeFormat().resolvedOptions()', async () => {
    const { resolveTimeZone } = await browserModulePromise;

    const stubIntl = {
        DateTimeFormat: () => ({
            resolvedOptions: () => ({ timeZone: 'America/New_York' })
        })
    };

    assert.equal(resolveTimeZone(stubIntl), 'America/New_York');
});

test('resolveTimeZone falls back to UTC when Intl reports no timeZone', async () => {
    const { resolveTimeZone } = await browserModulePromise;

    const stubIntl = {
        DateTimeFormat: () => ({ resolvedOptions: () => ({}) })
    };

    assert.equal(resolveTimeZone(stubIntl), 'UTC');
});

test('resolveTimeZone falls back to UTC when Intl throws', async () => {
    const { resolveTimeZone } = await browserModulePromise;

    const stubIntl = {
        DateTimeFormat: () => {
            throw new Error('not supported');
        }
    };

    assert.equal(resolveTimeZone(stubIntl), 'UTC');
});

test('getTimeZone delegates to the global Intl', async () => {
    const { getTimeZone } = await browserModulePromise;

    const original = global.Intl;
    global.Intl = {
        DateTimeFormat: () => ({ resolvedOptions: () => ({ timeZone: 'Europe/Berlin' }) })
    };
    try {
        assert.equal(getTimeZone(), 'Europe/Berlin');
    } finally {
        global.Intl = original;
    }
});

function stubWindow(storageImpl) {
    global.window = { localStorage: storageImpl };
}

test('storageGet returns the stored value', async () => {
    const { storageGet } = await browserModulePromise;
    stubWindow({ getItem: (key) => (key === 'theme' ? 'dark' : null) });

    assert.equal(storageGet('theme'), 'dark');
});

test('storageGet returns null when localStorage throws (private browsing, etc.)', async () => {
    const { storageGet } = await browserModulePromise;
    stubWindow({
        getItem: () => {
            throw new Error('SecurityError');
        }
    });

    assert.equal(storageGet('theme'), null);
});

test('storageSet writes the value and reports success', async () => {
    const { storageSet } = await browserModulePromise;
    let captured;
    stubWindow({ setItem: (key, value) => { captured = { key, value }; } });

    const ok = storageSet('theme', 'light');

    assert.equal(ok, true);
    assert.deepEqual(captured, { key: 'theme', value: 'light' });
});

test('storageSet reports failure without throwing when localStorage throws', async () => {
    const { storageSet } = await browserModulePromise;
    stubWindow({
        setItem: () => {
            throw new Error('QuotaExceededError');
        }
    });

    assert.equal(storageSet('theme', 'light'), false);
});

test('storageRemove reports success', async () => {
    const { storageRemove } = await browserModulePromise;
    let removedKey;
    stubWindow({ removeItem: (key) => { removedKey = key; } });

    assert.equal(storageRemove('theme'), true);
    assert.equal(removedKey, 'theme');
});

test('storageRemove reports failure without throwing when localStorage throws', async () => {
    const { storageRemove } = await browserModulePromise;
    stubWindow({
        removeItem: () => {
            throw new Error('blocked');
        }
    });

    assert.equal(storageRemove('theme'), false);
});
