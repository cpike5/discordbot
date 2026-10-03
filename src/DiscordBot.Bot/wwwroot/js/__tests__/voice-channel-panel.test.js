const test = require('node:test');
const assert = require('node:assert/strict');
const { resolveVoiceEndpoint, voiceJoinBody } = require('../voice-channel-panel.js');

const GUILD = '123456789012345678';

test.describe('resolveVoiceEndpoint on the member portal', () => {
    const base = `/api/portal/soundboard/${GUILD}`;

    test('uses the portal endpoints, never the Viewer-gated admin ones', () => {
        for (const action of ['join', 'leave', 'stop', 'status']) {
            const target = resolveVoiceEndpoint(base, GUILD, action, '1');
            assert.ok(target.url.startsWith(base), `${action} stays under the portal base`);
            assert.ok(!target.url.includes('/api/guilds/'), `${action} does not touch the admin API`);
        }
    });

    test('joins with POST /channel and leaves with DELETE /channel', () => {
        assert.deepEqual(resolveVoiceEndpoint(base, GUILD, 'join', '42'), { url: `${base}/channel`, method: 'POST' });
        assert.deepEqual(resolveVoiceEndpoint(base, GUILD, 'leave'), { url: `${base}/channel`, method: 'DELETE' });
    });

    test('reads status with GET and stops with POST', () => {
        assert.deepEqual(resolveVoiceEndpoint(base, GUILD, 'status'), { url: `${base}/status`, method: 'GET' });
        assert.deepEqual(resolveVoiceEndpoint(base, GUILD, 'stop'), { url: `${base}/stop`, method: 'POST' });
    });

    test('has no queue skip: members cannot remove other people\'s sounds', () => {
        assert.equal(resolveVoiceEndpoint(base, GUILD, 'skip', 1), null);
    });
});

test.describe('resolveVoiceEndpoint on the admin pages', () => {
    test('keeps the guild audio endpoints the management pages already use', () => {
        assert.deepEqual(resolveVoiceEndpoint(null, GUILD, 'join', '987'), { url: `/api/guilds/${GUILD}/audio/join/987`, method: 'POST' });
        assert.deepEqual(resolveVoiceEndpoint(null, GUILD, 'leave'), { url: `/api/guilds/${GUILD}/audio/leave`, method: 'POST' });
        assert.deepEqual(resolveVoiceEndpoint(null, GUILD, 'stop'), { url: `/api/guilds/${GUILD}/audio/stop`, method: 'POST' });
        assert.deepEqual(resolveVoiceEndpoint(null, GUILD, 'skip', 3), { url: `/api/guilds/${GUILD}/audio/queue/3`, method: 'DELETE' });
    });

    test('treats a missing api base the same as an empty one', () => {
        assert.equal(resolveVoiceEndpoint('', GUILD, 'stop').url, `/api/guilds/${GUILD}/audio/stop`);
        assert.equal(resolveVoiceEndpoint(undefined, GUILD, 'stop').url, `/api/guilds/${GUILD}/audio/stop`);
    });
});

test.describe('voiceJoinBody', () => {
    test('keeps every digit of a snowflake that a JavaScript number would round', () => {
        const id = '900000000000000201';
        assert.notEqual(String(Number(id)), id, 'the id really is beyond the safe integer range');
        assert.equal(voiceJoinBody(id), '{"channelId":900000000000000201}');
    });

    test('is valid JSON the server can bind to a ulong', () => {
        const body = voiceJoinBody('123456789012345678');
        assert.doesNotThrow(() => JSON.parse(body));
    });

    test('drops anything that is not a digit, so an id cannot break out of the JSON', () => {
        assert.equal(voiceJoinBody('12"}, {"x":1'), '{"channelId":121}');
    });
});
