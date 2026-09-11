const test = require('node:test');
const assert = require('node:assert/strict');

// browser.js uses ES module syntax with no "type": "module" package.json above
// it (there is deliberately none under wwwroot - it is publicly served); the
// "test" script's `--experimental-detect-module` flag is what lets Node's
// dynamic import() parse it as ESM from this CommonJS test file regardless.
// It is imported once per test via a fresh promise per test.afterEach reset
// since the storage helpers close over `window.localStorage`, which we stub
// per test.
const browserModulePromise = import('../blazor/browser.js');

test.afterEach(() => {
    delete global.window;
    delete global.navigator;
    delete global.document;
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

function fakeElement() {
    return { querySelectorAll: () => [], addEventListener() {}, removeEventListener() {} };
}

test('releaseAll does nothing and does not throw when nothing is registered', async () => {
    const { releaseAll } = await browserModulePromise;

    assert.doesNotThrow(() => releaseAll());
});

test('releaseAll releases every focus trap, matchMedia watcher and click-outside listener', async () => {
    const { trapFocus, releaseFocus, matchMedia, unwatchMedia, onClickOutside, offClickOutside, releaseAll } =
        await browserModulePromise;

    // Stub just enough of `window`/`document` for these three registration
    // functions - none of them need a real DOM.
    let matchMediaRemoved = false;
    global.window = {
        matchMedia: () => ({
            matches: false,
            addEventListener() {},
            removeEventListener: () => { matchMediaRemoved = true; }
        })
    };
    let clickOutsideRemoved = false;
    global.document = {
        activeElement: null,
        addEventListener() {},
        removeEventListener: () => { clickOutsideRemoved = true; }
    };
    const dotNetRef = { invokeMethodAsync: () => Promise.resolve() };

    trapFocus(fakeElement());
    matchMedia('(max-width: 1023px)', dotNetRef);
    onClickOutside(fakeElement(), dotNetRef);

    assert.doesNotThrow(() => releaseAll());
    assert.equal(matchMediaRemoved, true);
    assert.equal(clickOutsideRemoved, true);

    // A second releaseAll (e.g. both the enhancedload and pagehide hooks
    // firing) must be a no-op, not an error, since every handle above has
    // already been released.
    assert.doesNotThrow(() => releaseAll());

    // The handles are gone: releasing them again individually is a no-op too.
    assert.doesNotThrow(() => releaseFocus(1));
    assert.doesNotThrow(() => unwatchMedia(1));
    assert.doesNotThrow(() => offClickOutside(1));
});
