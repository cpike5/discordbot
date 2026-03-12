/**
 * Shared SSML marker parsing utilities.
 *
 * Visual marker patterns used in Pro TTS mode:
 *   **text**       → strong emphasis
 *   *text*         → moderate emphasis
 *   [# text #]     → say-as cardinal
 *   [📅 text 📅]   → say-as date
 *   [⏸️ Nms]       → break (pause)
 *
 * Exposed on window.SsmlMarkers so that both portal-tts.js and the
 * EmphasisToolbar inline script can share a single definition.
 */
(function() {
    'use strict';

    var MARKER_PATTERN = '\\*\\*(.+?)\\*\\*(?!\\*)|\\*(?!\\*)(.+?)\\*(?!\\*)|\\[#\\s(.+?)\\s#\\]|\\[📅\\s(.+?)\\s📅\\]|\\[⏸️\\s(\\d+)ms\\]';

    /**
     * Returns a fresh RegExp with the /g flag.
     * Because /g is stateful (lastIndex), callers must not reuse instances across searches.
     */
    function markerRegex() {
        return new RegExp(MARKER_PATTERN, 'g');
    }

    /**
     * Parse visual markers in `text` and return an elements array suitable for
     * the SSML build-ssml API.
     *
     * Each element has the shape:
     *   { type: 'text'|'emphasis'|'say-as'|'break', text: string|null, attributes: {} }
     */
    function parseMarkers(text) {
        var re = markerRegex();
        var elements = [];
        var cursor = 0;

        for (var match of text.matchAll(re)) {
            // Plain text before this match
            if (match.index > cursor) {
                elements.push({ type: 'text', text: text.substring(cursor, match.index), attributes: {} });
            }

            if (match[1] != null) {
                elements.push({ type: 'emphasis', text: match[1], attributes: { level: 'strong' } });
            } else if (match[2] != null) {
                elements.push({ type: 'emphasis', text: match[2], attributes: { level: 'moderate' } });
            } else if (match[3] != null) {
                elements.push({ type: 'say-as', text: match[3], attributes: { 'interpret-as': 'cardinal' } });
            } else if (match[4] != null) {
                elements.push({ type: 'say-as', text: match[4], attributes: { 'interpret-as': 'date' } });
            } else if (match[5] != null) {
                elements.push({ type: 'break', text: null, attributes: { duration: match[5] + 'ms' } });
            }

            cursor = match.index + match[0].length;
        }

        // Remaining text after last match
        if (cursor < text.length) {
            elements.push({ type: 'text', text: text.substring(cursor), attributes: {} });
        }

        return elements;
    }

    /**
     * Strip all visual markers from `text`, returning plain text content.
     * Emphasis/say-as markers are replaced with their inner text; break markers are removed.
     */
    function stripMarkers(text) {
        return text.replace(markerRegex(), function(match, strong, moderate, cardinal, date) {
            return strong || moderate || cardinal || date || '';
        });
    }

    window.SsmlMarkers = {
        MARKER_RE: markerRegex,
        parseMarkers: parseMarkers,
        stripMarkers: stripMarkers
    };
})();
