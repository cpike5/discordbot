const test = require('node:test');
const assert = require('node:assert/strict');
const TabPanel = require('../tab-panel.js');

function container(tabIds, disabled = []) {
    const tabs = tabIds.map((id) => ({
        dataset: { tabId: id },
        disabled: disabled.includes(id),
        hasAttribute: () => false
    }));
    return { querySelectorAll: () => tabs };
}

function withHash(hash, fn) {
    global.window = { location: { hash } };
    global.document = { querySelector: () => null };
    try { return fn(); } finally { delete global.window; delete global.document; }
}

test('hasTab accepts only the container\'s own enabled tabs', () => {
    const c = container(['a', 'b', 'c'], ['c']);
    assert.equal(TabPanel.hasTab(c, 'a'), true);
    assert.equal(TabPanel.hasTab(c, 'c'), false);
    assert.equal(TabPanel.hasTab(c, 'main-content'), false);
    assert.equal(TabPanel.hasTab(c, ''), false);
    assert.equal(TabPanel.hasTab(null, 'a'), false);
});

test('an unrelated URL hash is ignored, so no panel gets hidden', () => {
    const c = container(['overview', 'logs']);
    withHash('#main-content', () => {
        assert.equal(TabPanel.restoreActiveTab('p', 'urlhash', c), null);
    });
    withHash('#execution-logs/details/123', () => {
        assert.equal(TabPanel.restoreActiveTab('p', 'urlhash', c), null);
    });
});

test('a hash that names one of the tabs is restored', () => {
    const c = container(['overview', 'logs']);
    withHash('#logs', () => {
        assert.equal(TabPanel.restoreActiveTab('p', 'urlhash', c), 'logs');
    });
});

test('an empty hash restores nothing', () => {
    const c = container(['overview']);
    withHash('', () => {
        assert.equal(TabPanel.restoreActiveTab('p', 'urlhash', c), null);
    });
});
