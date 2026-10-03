/**
 * Preset bar settings source (Pages/Shared/Components/_PresetBar.cshtml).
 *
 * Saving a custom preset reads the page's current voice, style, speed and pitch. The member portal
 * keeps the voice in the `VoiceSelector` widget (`portalVoiceSelector`) and the style in
 * `#portalStyleSelector-select`; the admin Text-to-Speech page uses a plain `<select id="voiceSelect">`
 * and `#styleSelector-select`. The bar names its source on its container:
 *
 * - `data-voice-input`: id of a plain input/select holding the voice. Absent means the portal's
 *   `VoiceSelector` widget.
 * - `data-style-input`: id of the style select. Absent means `portalStyleSelector-select`.
 * - Speed and pitch are `#speedSlider` and `#pitchSlider` on both pages.
 *
 * Exposed as window.PresetBar (browser) and module.exports (Node/tests).
 */
(function (root, factory) {
    if (typeof module === 'object' && module.exports) {
        module.exports = factory(root);
    } else {
        root.PresetBar = factory(root);
    }
})(typeof window !== 'undefined' ? window : globalThis, function (root) {
    'use strict';

    var PORTAL_VOICE_SELECTOR = 'portalVoiceSelector';
    var PORTAL_STYLE_INPUT = 'portalStyleSelector-select';

    function valueOf(doc, id) {
        var el = id ? doc.getElementById(id) : null;
        return el ? el.value : undefined;
    }

    /**
     * The voice, style, speed and pitch currently chosen on the page the bar sits on.
     * `voice` is '' when nothing is chosen; `style` is null for "no style".
     */
    function readSettings(bar, doc, win) {
        doc = doc || root.document;
        win = win || root;
        var voiceInput = bar && bar.getAttribute ? bar.getAttribute('data-voice-input') : null;
        var styleInput = bar && bar.getAttribute ? bar.getAttribute('data-style-input') : null;

        var voice;
        if (voiceInput) {
            voice = valueOf(doc, voiceInput) || '';
        } else {
            voice = win.VoiceSelector && win.VoiceSelector.getValue
                ? win.VoiceSelector.getValue(PORTAL_VOICE_SELECTOR) || ''
                : '';
        }

        return {
            voice: voice,
            style: valueOf(doc, styleInput || PORTAL_STYLE_INPUT) || null,
            speed: parseFloat(valueOf(doc, 'speedSlider')) || 1.0,
            pitch: parseFloat(valueOf(doc, 'pitchSlider')) || 1.0
        };
    }

    return { readSettings: readSettings };
});
