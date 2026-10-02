/**
 * Quote-safe HTML escaping for page scripts that build markup from user data.
 *
 * Prefer textContent and data-* attributes. When a template string has to go into
 * innerHTML, pass every user-supplied value through SafeHtml.escape: it escapes
 * & < > " and ', so the result is safe in element content and in quoted attributes.
 * It is not safe inside inline event handlers (onclick="...") or <script>; put the
 * value in a data-* attribute and read it from the element instead.
 */
window.SafeHtml = Object.freeze({
    escape(value) {
        if (value === null || value === undefined) {
            return '';
        }

        return String(value)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }
});
