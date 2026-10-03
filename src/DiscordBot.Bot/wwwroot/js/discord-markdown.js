/**
 * Discord-style message preview: escape first, then render the markdown Discord supports.
 *
 * One renderer for every message preview (Welcome, Scheduled Messages create and edit), so the
 * same text looks the same everywhere and no preview writes raw user text into innerHTML.
 *
 *   element.innerHTML = DiscordMarkdown.render(textarea.value, {
 *       tokens: { '{user}': { text: '@NewMember', mention: true }, '{server}': 'My Server' }
 *   });
 *
 * Order matters and is the safety property: the text is HTML-escaped before any markup is added,
 * so a message that contains `<script>` or `<img onerror=...>` previews as literal text. Code spans
 * and blocks are lifted out first so markdown inside them stays literal.
 *
 * Supported: **bold**, *italic* and _italic_, __underline__, ~~strikethrough~~, ||spoiler||,
 * `inline code`, ```code blocks```, "> quote" lines, "# / ## / ###" headings, raw URLs,
 * <@id> and <#id> mentions, @everyone and @here, and line breaks. Custom emoji, timestamps and
 * masked links are shown as the plain text that was typed.
 *
 * Exposed as window.DiscordMarkdown (browser) and module.exports (Node/tests).
 */
(function (root, factory) {
    if (typeof module === 'object' && module.exports) {
        module.exports = factory();
    } else {
        root.DiscordMarkdown = factory();
    }
})(typeof self !== 'undefined' ? self : this, function () {
    'use strict';

    // Private-use characters mark lifted-out pieces; user text never produces them.
    var OPEN = '\uE000';
    var CLOSE = '\uE001';
    var PLACEHOLDER = new RegExp(OPEN + '(\\d+)' + CLOSE, 'g');

    // Same escape set as SafeHtml.escape. Kept here because this module also runs standalone under
    // node (its tests) and is exported as DiscordMarkdown.escapeHtml, so it cannot assume SafeHtml.
    function escapeHtml(value) {
        return String(value === null || value === undefined ? '' : value)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    /** Remove the private-use marker characters from user text so it cannot forge a placeholder. */
    function stripMarkers(text) {
        return String(text === null || text === undefined ? '' : text).replace(/[\uE000\uE001]/g, '');
    }

    /**
     * Render a message to safe HTML.
     *
     * @param {string} text - the raw message
     * @param {Object} [options]
     * @param {Object<string, string|{text: string, mention?: boolean}>} [options.tokens] -
     *   literal tokens such as "{user}" and what they preview as. A string is shown bold; an
     *   object with `mention: true` is shown as a Discord mention chip. Values are escaped here.
     * @returns {string} HTML, safe for innerHTML
     */
    function render(text, options) {
        var tokens = (options && options.tokens) || {};
        var pieces = [];

        function stash(html) {
            pieces.push(html);
            return OPEN + (pieces.length - 1) + CLOSE;
        }

        var out = escapeHtml(stripMarkers(text));

        // Tokens first, so they survive markdown that wraps them ("**{server}**").
        Object.keys(tokens).forEach(function (token) {
            var value = tokens[token];
            var html;
            if (value && typeof value === 'object') {
                html = value.mention
                    ? '<span class="discord-mention">' + escapeHtml(value.text) + '</span>'
                    : '<strong>' + escapeHtml(value.text) + '</strong>';
            } else {
                html = '<strong>' + escapeHtml(value) + '</strong>';
            }
            out = out.split(escapeHtml(token)).join(stash(html));
        });

        // Code blocks and inline code: lifted out so nothing inside them is formatted.
        out = out.replace(/```(?:[a-zA-Z0-9_+-]*\n)?([\s\S]*?)```/g, function (match, code) {
            return stash('<pre class="discord-md-codeblock"><code>' + code.replace(/^\n+|\n+$/g, '') + '</code></pre>');
        });
        out = out.replace(/(`+)([^`\n][\s\S]*?[^`\n]|[^`\n])\1(?!`)/g, function (match, ticks, code) {
            return stash('<code class="discord-md-code">' + code + '</code>');
        });

        // Mentions and URLs
        out = out.replace(/&lt;@[!&]?(\d+)&gt;/g, function () { return stash('<span class="discord-mention">@user</span>'); });
        out = out.replace(/&lt;#(\d+)&gt;/g, function () { return stash('<span class="discord-mention">#channel</span>'); });
        out = out.replace(/@(everyone|here)\b/g, function (match) { return stash('<span class="discord-mention">' + match + '</span>'); });
        out = out.replace(/\bhttps?:\/\/[^\s<]+[^\s<.,:;"')\]!?]/g, function (url) { return stash('<span class="discord-md-link">' + url + '</span>'); });

        // Inline styles. Longest delimiters first so "***x***" and "__x__" resolve sensibly.
        out = out
            .replace(/\|\|([\s\S]+?)\|\|/g, '<span class="discord-md-spoiler" tabindex="0" title="Spoiler: hover or focus to reveal">$1</span>')
            .replace(/\*\*\*([\s\S]+?)\*\*\*/g, '<strong><em>$1</em></strong>')
            .replace(/\*\*([\s\S]+?)\*\*/g, '<strong>$1</strong>')
            .replace(/__([\s\S]+?)__/g, '<u>$1</u>')
            .replace(/~~([\s\S]+?)~~/g, '<s>$1</s>')
            .replace(/\*(?!\s)([^*\n]+?)\*/g, '<em>$1</em>')
            .replace(/(^|[^\w])_(?!\s)([^_\n]+?)_(?![\w])/g, '$1<em>$2</em>');

        // Line-level: quotes and headings are blocks; every other line break is a <br>.
        var lines = out.split('\n');
        var html = '';
        var previousWasBlock = true; // no <br> before the first line
        for (var i = 0; i < lines.length; i++) {
            var line = lines[i];
            var quote = /^&gt;\s?(.*)$/.exec(line);
            var heading = /^(#{1,3})\s+(.+)$/.exec(line);
            if (quote || heading) {
                html += quote
                    ? '<div class="discord-md-quote">' + quote[1] + '</div>'
                    : '<div class="discord-md-h' + heading[1].length + '">' + heading[2] + '</div>';
                previousWasBlock = true;
            } else {
                html += (previousWasBlock ? '' : '<br>') + line;
                previousWasBlock = false;
            }
        }
        out = html;

        // Put the lifted-out pieces back (nested lifts, such as a token inside a code span, too).
        for (var pass = 0; pass < 3 && PLACEHOLDER.test(out); pass++) {
            PLACEHOLDER.lastIndex = 0;
            out = out.replace(PLACEHOLDER, function (match, index) { return pieces[Number(index)]; });
        }
        PLACEHOLDER.lastIndex = 0;
        return out;
    }

    return {
        render: render,
        escapeHtml: escapeHtml
    };
});
