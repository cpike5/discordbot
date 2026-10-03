const test = require('node:test');
const assert = require('node:assert/strict');
const PresetBar = require('../preset-bar.js');

function doc(values) {
    return { getElementById: (id) => (id in values ? { value: values[id] } : null) };
}
function bar(attrs) {
    return { getAttribute: (name) => (name in attrs ? attrs[name] : null) };
}

test('readSettings: the portal bar reads the voice from the VoiceSelector widget', () => {
    const win = { VoiceSelector: { getValue: (id) => (id === 'portalVoiceSelector' ? 'en-US-JennyNeural' : '') } };
    const d = doc({ 'portalStyleSelector-select': 'cheerful', speedSlider: '1.2', pitchSlider: '0.9' });
    assert.deepEqual(PresetBar.readSettings(bar({}), d, win),
        { voice: 'en-US-JennyNeural', style: 'cheerful', speed: 1.2, pitch: 0.9 });
});

test('readSettings: the admin bar reads the voice and style from the inputs it names', () => {
    const d = doc({
        voiceSelect: 'en-GB-RyanNeural',
        'styleSelector-select': 'newscast',
        speedSlider: '0.8',
        pitchSlider: '1.3'
    });
    const b = bar({ 'data-voice-input': 'voiceSelect', 'data-style-input': 'styleSelector-select' });
    // No VoiceSelector widget on the admin page
    assert.deepEqual(PresetBar.readSettings(b, d, {}),
        { voice: 'en-GB-RyanNeural', style: 'newscast', speed: 0.8, pitch: 1.3 });
});

test('readSettings: a named voice input wins over a VoiceSelector that happens to be present', () => {
    const win = { VoiceSelector: { getValue: () => 'portal-voice' } };
    const b = bar({ 'data-voice-input': 'voiceSelect' });
    assert.equal(PresetBar.readSettings(b, doc({ voiceSelect: 'admin-voice' }), win).voice, 'admin-voice');
});

test('readSettings: nothing chosen gives an empty voice, no style and neutral speed and pitch', () => {
    const b = bar({ 'data-voice-input': 'voiceSelect', 'data-style-input': 'styleSelector-select' });
    const d = doc({ voiceSelect: '', 'styleSelector-select': '', speedSlider: 'x' });
    assert.deepEqual(PresetBar.readSettings(b, d, {}), { voice: '', style: null, speed: 1, pitch: 1 });
    assert.equal(PresetBar.readSettings(bar({}), doc({}), {}).voice, '', 'portal bar without the widget');
});
