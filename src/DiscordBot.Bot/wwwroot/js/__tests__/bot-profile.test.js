const test = require('node:test');
const assert = require('node:assert/strict');
const botProfile = require('../bot-profile.js');

test('imageProblem: no file, and an acceptable file, are not problems', () => {
    assert.equal(botProfile.imageProblem(null, 'avatar'), null);
    assert.equal(botProfile.imageProblem({ size: 1024, type: 'image/png' }, 'avatar'), null);
    // A browser that reports no type leaves the decision to the server
    assert.equal(botProfile.imageProblem({ size: 1024, type: '' }, 'banner'), null);
});

test('imageProblem: empty, oversized and non-image files are named by their field', () => {
    assert.match(botProfile.imageProblem({ size: 0, type: 'image/png' }, 'avatar'), /avatar file is empty/);
    assert.match(botProfile.imageProblem({ size: 8 * 1024 * 1024 + 1, type: 'image/png' }, 'banner'), /banner file is too large/);
    assert.equal(botProfile.imageProblem({ size: 8 * 1024 * 1024, type: 'image/png' }, 'banner'), null);
    assert.match(botProfile.imageProblem({ size: 10, type: 'application/pdf' }, 'avatar'), /PNG, JPEG, GIF or WebP/);
});

test('usernameProblem: 2 to 32 characters after trimming', () => {
    assert.equal(botProfile.usernameProblem('ab'), null);
    assert.equal(botProfile.usernameProblem('x'.repeat(32)), null);
    assert.match(botProfile.usernameProblem(' a '), /2 to 32/);
    assert.match(botProfile.usernameProblem('x'.repeat(33)), /2 to 32/);
    assert.match(botProfile.usernameProblem(''), /2 to 32/);
});

test('readSaveResult: saved, unchanged, and errors that prefer the server list', () => {
    assert.equal(botProfile.readSaveResult({ ok: true, data: { success: true, message: 'Bot profile saved: username.', changeCount: 1 } }).kind, 'saved');
    assert.equal(botProfile.readSaveResult({ ok: true, data: { success: true, message: 'Nothing changed.', changeCount: 0 } }).kind, 'unchanged');

    const listed = botProfile.readSaveResult({ ok: false, data: { success: false, message: 'The profile was not saved.', errors: ['Too long.', 'Not an image.'] } });
    assert.equal(listed.kind, 'error');
    assert.equal(listed.message, 'Too long. Not an image.');

    assert.equal(botProfile.readSaveResult({ ok: false, data: { success: false, message: 'Discord said no.' } }).message, 'Discord said no.');
    assert.match(botProfile.readSaveResult({ ok: false, data: null }).message, /could not be saved/);
});
