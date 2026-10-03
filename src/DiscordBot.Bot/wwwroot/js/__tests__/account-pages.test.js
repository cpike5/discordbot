const test = require('node:test');
const assert = require('node:assert/strict');
const LinkDiscord = require('../link-discord.js');
const PrivacyPage = require('../privacy.js');
const ProfilePage = require('../profile.js');

test('LinkDiscord.msUntil: time left until an ISO instant, null when it does not parse', () => {
    const now = Date.parse('2026-10-03T12:00:00Z');
    assert.equal(LinkDiscord.msUntil('2026-10-03T12:15:00Z', now), 15 * 60 * 1000);
    assert.equal(LinkDiscord.msUntil('2026-10-03T11:59:00Z', now), -60 * 1000);
    assert.equal(LinkDiscord.msUntil('not a date', now), null);
});

test('LinkDiscord.deadline: counts the server-measured seconds from now, so a wrong device clock does not matter', () => {
    const now = Date.parse('2031-01-01T00:00:00Z'); // a device clock years away from the server's
    assert.equal(LinkDiscord.deadline('900', '2026-10-03T12:15:00.000Z', now), now + 900 * 1000);
    assert.equal(LinkDiscord.deadline(0, '2026-10-03T12:15:00.000Z', now), now, 'already expired');
});

test('LinkDiscord.deadline: falls back to the ISO expiry when the seconds are missing or bad', () => {
    const now = Date.parse('2026-10-03T12:00:00Z');
    const iso = '2026-10-03T12:15:00.000Z';
    assert.equal(LinkDiscord.deadline(null, iso, now), Date.parse(iso));
    assert.equal(LinkDiscord.deadline('', iso, now), Date.parse(iso));
    assert.equal(LinkDiscord.deadline('abc', iso, now), Date.parse(iso));
    assert.equal(LinkDiscord.deadline('-5', iso, now), Date.parse(iso));
    assert.equal(LinkDiscord.deadline(null, 'nope', now), null);
});

test('LinkDiscord.msUntil: understands the three-digit UTC form the server writes', () => {
    const now = Date.parse('2026-10-03T12:00:00.000Z');
    assert.equal(LinkDiscord.msUntil('2026-10-03T12:00:30.250Z', now), 30250);
});

test('LinkDiscord.clock: minutes and zero-padded seconds, rounding up, never negative', () => {
    assert.equal(LinkDiscord.clock(14 * 60 * 1000 + 32 * 1000), '14:32');
    assert.equal(LinkDiscord.clock(5 * 1000), '0:05');
    assert.equal(LinkDiscord.clock(4100), '0:05');
    assert.equal(LinkDiscord.clock(-1000), '0:00');
});

test('PrivacyPage.consentCopy: names what is being turned on or off', () => {
    const off = PrivacyPage.consentCopy('revoke', 'Message Logging');
    assert.equal(off.confirmText, 'Turn off');
    assert.match(off.title, /Message Logging/);
    assert.equal(off.variant, 'warning');

    const on = PrivacyPage.consentCopy('grant', 'AI Assistant Usage');
    assert.equal(on.confirmText, 'Turn on');
    assert.match(on.message, /AI Assistant Usage/);
});

test('ProfilePage.init: clears the browser\'s saved theme once when the page says "Match my system" was just chosen', () => {
    const calls = [];
    const themeManager = { clearTheme: (persist) => calls.push(persist) };
    const doc = { querySelector: (selector) => (selector === '[data-theme-cleared]' ? {} : null) };

    assert.equal(ProfilePage.init(doc, themeManager), true);
    assert.deepEqual(calls, [false], 'cleared locally only: the server already forgot the choice');
});

test('ProfilePage.init: leaves the saved theme alone on an ordinary visit, or without ThemeManager', () => {
    const calls = [];
    const themeManager = { clearTheme: (persist) => calls.push(persist) };
    const plain = { querySelector: () => null };
    const flagged = { querySelector: () => ({}) };

    assert.equal(ProfilePage.init(plain, themeManager), false);
    assert.equal(ProfilePage.init(flagged, null), false);
    assert.equal(ProfilePage.init(flagged, {}), false);
    assert.deepEqual(calls, []);
});
