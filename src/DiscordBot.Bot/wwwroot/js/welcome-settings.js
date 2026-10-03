/**
 * Welcome settings page: token buttons, embed colour picker and the live preview.
 * The preview text goes through DiscordMarkdown, which escapes before it formats, so the message
 * (and the server name) can contain anything without becoming markup.
 */
(function () {
    'use strict';

    var form = document.getElementById('welcome-form');
    if (!form) return;

    var HEX = /^#[0-9A-Fa-f]{6}$/;

    var enabled = document.getElementById('Input_IsEnabled');
    var useEmbed = document.getElementById('Input_UseEmbed');
    var includeAvatar = document.getElementById('Input_IncludeAvatar');
    var message = document.getElementById('Input_WelcomeMessage');
    var colorText = document.getElementById('Input_EmbedColor');
    var colorPicker = document.getElementById('embed-color-picker');
    var colorContainer = document.getElementById('embed-color-container');

    var offNote = document.getElementById('preview-off-note');
    var plain = document.getElementById('preview-plain');
    var embed = document.getElementById('preview-embed');
    var embedText = document.getElementById('preview-embed-text');
    var thumb = document.getElementById('preview-thumb');

    var guildName = form.getAttribute('data-guild-name') || 'Server';

    var tokens = {
        '{user}': { text: '@NewMember', mention: true },
        '{username}': 'NewMember',
        '{server}': guildName,
        '{memberCount}': '1,234'
    };

    function currentColor() {
        var value = colorText ? colorText.value.trim() : '';
        return HEX.test(value) ? value : '#5865F2';
    }

    function updatePreview() {
        var text = message ? message.value : '';
        var html = text.trim()
            ? window.DiscordMarkdown.render(text, { tokens: tokens })
            : '<span class="discord-preview-muted italic">Enter a message to see the preview</span>';

        var asEmbed = !!(useEmbed && useEmbed.checked);
        plain.hidden = asEmbed;
        embed.hidden = !asEmbed;
        (asEmbed ? embedText : plain).innerHTML = html;

        if (asEmbed) {
            embed.style.borderLeftColor = currentColor();
            thumb.hidden = !(includeAvatar && includeAvatar.checked);
        }

        if (offNote) offNote.hidden = !!(enabled && enabled.checked);
        if (colorContainer && useEmbed) colorContainer.classList.toggle('hidden', !useEmbed.checked);
    }

    // Token buttons insert at the caret
    form.addEventListener('click', function (event) {
        var button = event.target.closest && event.target.closest('[data-insert-token]');
        if (!button || !message) return;
        var token = button.getAttribute('data-insert-token');
        var start = message.selectionStart === null ? message.value.length : message.selectionStart;
        var end = message.selectionEnd === null ? start : message.selectionEnd;
        message.value = message.value.slice(0, start) + token + message.value.slice(end);
        message.focus();
        message.setSelectionRange(start + token.length, start + token.length);
        message.dispatchEvent(new Event('input', { bubbles: true }));
    });

    // The colour picker and the hex field edit the same value
    if (colorPicker && colorText) {
        colorPicker.addEventListener('input', function () {
            colorText.value = colorPicker.value.toUpperCase();
            colorText.dispatchEvent(new Event('input', { bubbles: true }));
        });
        colorText.addEventListener('input', function () {
            if (HEX.test(colorText.value.trim())) colorPicker.value = colorText.value.trim();
        });
    }

    form.addEventListener('input', updatePreview);
    form.addEventListener('change', updatePreview);
    updatePreview();
})();
