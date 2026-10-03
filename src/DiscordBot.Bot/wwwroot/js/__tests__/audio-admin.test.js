const test = require('node:test');
const assert = require('node:assert/strict');

const AjaxSort = require('../ajax-sort.js');
const SoundboardAdmin = require('../soundboard-admin.js');
const TtsPage = require('../tts-page.js');
const AudioSettings = require('../audio-settings.js');

test.describe('AjaxSort helpers', () => {
    test('buildPartialUrl sets the sort and keeps the handler', () => {
        const url = AjaxSort.buildPartialUrl('/Guilds/Soundboard/1?handler=Partial', 'sort', 'name-desc', 'http://localhost:5000');
        assert.equal(url, 'http://localhost:5000/Guilds/Soundboard/1?handler=Partial&sort=name-desc');
    });

    test('buildPartialUrl replaces an existing sort', () => {
        const url = AjaxSort.buildPartialUrl('/x?sort=oldest', 'sort', 'newest', 'http://h');
        assert.equal(url, 'http://h/x?sort=newest');
    });

    test('sortFromSearch reads the sort Back should restore, or the default', () => {
        assert.equal(AjaxSort.sortFromSearch('?sort=newest', 'sort', 'name-asc'), 'newest');
        assert.equal(AjaxSort.sortFromSearch('', 'sort', 'name-asc'), 'name-asc');
        assert.equal(AjaxSort.sortFromSearch('?other=1', 'sort', 'name-asc'), 'name-asc');
    });
});

test.describe('Soundboard upload checks', () => {
    const limits = { maxBytes: 5 * 1024 * 1024, maxSounds: 50, slotsLeft: 10 };

    test('accepts a supported file within the limits', () => {
        assert.equal(SoundboardAdmin.validateFile({ name: 'Airhorn.MP3', size: 1000 }, limits), null);
    });

    test('refuses an unsupported type by name', () => {
        assert.match(SoundboardAdmin.validateFile({ name: 'notes.txt', size: 10 }, limits), /supported type/);
        assert.match(SoundboardAdmin.validateFile({ name: 'noextension', size: 10 }, limits), /supported type/);
    });

    test('refuses an empty file and one over the size limit', () => {
        assert.match(SoundboardAdmin.validateFile({ name: 'a.wav', size: 0 }, limits), /empty/);
        const big = SoundboardAdmin.validateFile({ name: 'a.wav', size: 6 * 1024 * 1024 }, limits);
        assert.match(big, /Larger than the 5 MB limit/);
    });

    test('refuses a file when the server has no room left', () => {
        assert.match(SoundboardAdmin.validateFile({ name: 'a.ogg', size: 10 }, { ...limits, slotsLeft: 0 }), /limit of 50 sounds/);
    });

    test('the button copy matches how many files are sent', () => {
        assert.equal(SoundboardAdmin.uploadButtonLabel(0), 'Upload sounds');
        assert.equal(SoundboardAdmin.uploadButtonLabel(1), 'Upload sound');
        assert.equal(SoundboardAdmin.uploadButtonLabel(3), 'Upload 3 sounds');
    });

    test('formatBytes', () => {
        assert.equal(SoundboardAdmin.formatBytes(512), '512 B');
        assert.equal(SoundboardAdmin.formatBytes(2048), '2.0 KB');
        assert.equal(SoundboardAdmin.formatBytes(3 * 1024 * 1024), '3.0 MB');
    });
});

test.describe('TTS request body', () => {
    const screen = { message: '  hello ', voice: 'en-GB-RyanNeural', speed: '1.5', pitch: '0.9', volume: '0.5', mode: 'standard', style: '', styleIntensity: '1.0', ssml: '' };

    test('carries the voice settings on screen, not the saved defaults', () => {
        const body = TtsPage.buildRequestBody(screen);
        assert.deepEqual(body, { message: 'hello', voice: 'en-GB-RyanNeural', speed: 1.5, pitch: 0.9, volume: 0.5 });
    });

    test('adds the style in Standard mode, not in Simple mode', () => {
        const styled = { ...screen, style: 'cheerful', styleIntensity: '1.2' };
        assert.equal(TtsPage.buildRequestBody(styled).style, 'cheerful');
        assert.equal(TtsPage.buildRequestBody(styled).styleIntensity, 1.2);
        assert.equal('style' in TtsPage.buildRequestBody({ ...styled, mode: 'simple' }), false);
    });

    test('posts SSML only in Pro mode', () => {
        const withSsml = { ...screen, ssml: '<speak>hi</speak>' };
        assert.equal(TtsPage.buildRequestBody({ ...withSsml, mode: 'pro' }).ssml, '<speak>hi</speak>');
        assert.equal('ssml' in TtsPage.buildRequestBody({ ...withSsml, mode: 'standard' }), false);
    });

    test('a garbled number is left out, so the server uses its default', () => {
        const body = TtsPage.buildRequestBody({ ...screen, speed: 'fast', volume: '' });
        assert.equal(body.speed, null);
        assert.equal(body.volume, null);
    });

    test('parseWholeNumber never turns junk into 0', () => {
        assert.equal(TtsPage.parseWholeNumber('', 1, 60), null);
        assert.equal(TtsPage.parseWholeNumber('abc', 1, 60), null);
        assert.equal(TtsPage.parseWholeNumber('0', 1, 60), null);
        assert.equal(TtsPage.parseWholeNumber('61', 1, 60), null);
        assert.equal(TtsPage.parseWholeNumber('5.5', 1, 60), null);
        assert.equal(TtsPage.parseWholeNumber('12', 1, 60), 12);
    });

    test('sliderText', () => {
        assert.equal(TtsPage.sliderText('speed', 1.25), '1.3x');
        assert.equal(TtsPage.sliderText('volume', 0.8), '80%');
    });
});

test.describe('Audio settings number checks', () => {
    const rule = { label: 'the maximum duration', min: 1, max: 300, unit: 'seconds' };

    test('parseWholeNumber reads whole numbers only', () => {
        assert.equal(AudioSettings.parseWholeNumber(' 42 '), 42);
        assert.equal(AudioSettings.parseWholeNumber('0'), 0);
        assert.equal(AudioSettings.parseWholeNumber(''), null);
        assert.equal(AudioSettings.parseWholeNumber('1e3'), null);
        assert.equal(AudioSettings.parseWholeNumber('12abc'), null);
        assert.equal(AudioSettings.parseWholeNumber('3.5'), null);
        assert.equal(AudioSettings.parseWholeNumber(undefined), null);
    });

    test('an empty field is an error, not a silent default', () => {
        assert.equal(AudioSettings.checkNumber('', rule), 'Enter the maximum duration as a whole number.');
    });

    test('out of range names the range and unit', () => {
        assert.equal(AudioSettings.checkNumber('999', rule), 'The maximum duration must be from 1 to 300 seconds.');
        assert.equal(AudioSettings.checkNumber('0', rule), 'The maximum duration must be from 1 to 300 seconds.');
    });

    test('a unitless rule has no trailing space', () => {
        assert.equal(AudioSettings.checkNumber('5', { label: 'the SSML complexity limit', min: 10, max: 200, unit: '' }),
            'The SSML complexity limit must be from 10 to 200.');
    });

    test('in range passes', () => {
        assert.equal(AudioSettings.checkNumber('45', rule), null);
        assert.equal(AudioSettings.checkNumber('0', { label: 'the timeout', min: 0, max: 60, unit: 'minutes' }), null);
    });
});
