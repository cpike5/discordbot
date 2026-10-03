const test = require('node:test');
const assert = require('node:assert/strict');
const LinkDiscord = require('../link-discord.js');
const PrivacyPage = require('../privacy.js');

test('LinkDiscord.msUntil: time left until an ISO instant, null when it does not parse', () => {
    const now = Date.parse('2026-10-03T12:00:00Z');
    assert.equal(LinkDiscord.msUntil('2026-10-03T12:15:00Z', now), 15 * 60 * 1000);
    assert.equal(LinkDiscord.msUntil('2026-10-03T11:59:00Z', now), -60 * 1000);
    assert.equal(LinkDiscord.msUntil('not a date', now), null);
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
