/**
 * EmptyState - the script twin of Pages/Shared/Components/_EmptyState.cshtml.
 *
 * For regions a script fills after load (AJAX lists, tab panels, search results) so they show
 * the same empty, filtered-empty and error states as server-rendered pages. The markup and
 * classes match the partial; change both together. Text goes in through textContent, so
 * nothing the caller passes is ever parsed as HTML.
 *
 *   EmptyState.render(container, { type: 'noResults', title: 'No matches', description: '...' });
 *   EmptyState.error(container, { onRetry: load });                 // plain text + Retry
 *   EmptyState.filtered(container, { noun: 'users', onClear: reset });
 *
 * Options: type ('noData' | 'noResults' | 'firstTime' | 'error' | 'noPermission' | 'offline'),
 * title, description, icon (an SVG path, replaces the type's), size ('compact' | 'default' |
 * 'large'), headingLevel (1-6, default 3), announce (role="status"), id, and
 * action / secondary: { text, url, onClick, iconPath, attributes }. iconPath '' means no icon.
 *
 * Exposed as window.EmptyState (browser) and module.exports (Node/tests).
 */
(function (root, factory) {
    if (typeof module === 'object' && module.exports) {
        module.exports = factory();
    } else {
        root.EmptyState = factory();
    }
})(typeof self !== 'undefined' ? self : this, function () {
    'use strict';

    var SVG_NS = 'http://www.w3.org/2000/svg';

    /** Icon paths per type: the same ones _EmptyState uses. */
    var ICONS = {
        noData: 'M3 7v10a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-6l-2-2H5a2 2 0 00-2 2z',
        noResults: 'M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z',
        firstTime: 'M9.813 15.904L9 18.75l-.813-2.846a4.5 4.5 0 00-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 003.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 003.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 00-3.09 3.09zM18.259 8.715L18 9.75l-.259-1.035a3.375 3.375 0 00-2.455-2.456L14.25 6l1.036-.259a3.375 3.375 0 002.455-2.456L18 2.25l.259 1.035a3.375 3.375 0 002.456 2.456L21.75 6l-1.035.259a3.375 3.375 0 00-2.456 2.456zM16.894 20.567L16.5 21.75l-.394-1.183a2.25 2.25 0 00-1.423-1.423L13.5 18.75l1.183-.394a2.25 2.25 0 001.423-1.423l.394-1.183.394 1.183a2.25 2.25 0 001.423 1.423l1.183.394-1.183.394a2.25 2.25 0 00-1.423 1.423z',
        error: 'M12 9v3.75m9-.75a9 9 0 11-18 0 9 9 0 0118 0zm-9 3.75h.008v.008H12v-.008z',
        noPermission: 'M16.5 10.5V6.75a4.5 4.5 0 10-9 0v3.75m-.75 11.25h10.5a2.25 2.25 0 002.25-2.25v-6.75a2.25 2.25 0 00-2.25-2.25H6.75a2.25 2.25 0 00-2.25 2.25v6.75a2.25 2.25 0 002.25 2.25z',
        offline: 'M8.288 15.038a5.25 5.25 0 017.424 0M5.106 11.856c3.807-3.808 9.98-3.808 13.788 0M1.924 8.674c5.565-5.565 14.587-5.565 20.152 0M12.53 18.22l-.53.53-.53-.53a.75.75 0 011.06 0z'
    };

    var DEFAULT_ACTION_ICON = 'M12 4v16m8-8H4';

    /** Class sets per size: the same ones _EmptyState uses. */
    var SIZES = {
        compact: { container: 'max-w-[320px] space-y-3 py-5', iconBox: 'p-3', icon: 'w-10 h-10', title: 'text-base', desc: 'text-xs', button: 'btn-sm', gap: 'gap-1.5', titleGap: 'mb-1' },
        default: { container: 'max-w-[400px] space-y-4 py-8', iconBox: 'p-4', icon: 'w-16 h-16', title: 'text-lg', desc: 'text-sm', button: '', gap: 'gap-2', titleGap: 'mb-2' },
        large: { container: 'max-w-[500px] space-y-6 py-12', iconBox: 'p-5', icon: 'w-20 h-20', title: 'text-2xl', desc: 'text-base', button: 'btn-lg', gap: 'gap-2.5', titleGap: 'mb-2' }
    };

    /**
     * Work out everything a render needs from the options, without touching the DOM.
     * @returns {{ type, size, iconPath, strokeWidth, heading, sizeClasses, actionIconPath }}
     */
    function resolve(options) {
        var o = options || {};
        var type = ICONS[o.type] ? o.type : 'noData';
        var size = SIZES[o.size] ? o.size : 'default';
        var level = Math.min(6, Math.max(1, parseInt(o.headingLevel, 10) || 3));
        var action = o.action || null;
        return {
            type: type,
            size: size,
            iconPath: o.icon || ICONS[type],
            strokeWidth: type === 'noData' ? '1.5' : '2',
            heading: 'h' + level,
            sizeClasses: SIZES[size],
            actionIconPath: action && action.iconPath !== undefined && action.iconPath !== null
                ? action.iconPath
                : DEFAULT_ACTION_ICON
        };
    }

    function el(tag, className, text) {
        var node = document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined && text !== null) node.textContent = text;
        return node;
    }

    function svg(className, path, strokeWidth) {
        var node = document.createElementNS(SVG_NS, 'svg');
        if (className) node.setAttribute('class', className);
        node.setAttribute('fill', 'none');
        node.setAttribute('viewBox', '0 0 24 24');
        node.setAttribute('stroke', 'currentColor');
        node.setAttribute('aria-hidden', 'true');
        var p = document.createElementNS(SVG_NS, 'path');
        p.setAttribute('stroke-linecap', 'round');
        p.setAttribute('stroke-linejoin', 'round');
        p.setAttribute('stroke-width', strokeWidth);
        p.setAttribute('d', path);
        node.appendChild(p);
        return node;
    }

    /**
     * True for a relative URL or an http or https one. Anything else with a scheme (javascript:, data:,
     * vbscript:) is refused: these URLs can come from page data, and the browser ignores tabs and
     * newlines inside a scheme, so those are stripped before looking.
     */
    function isSafeUrl(url) {
        if (typeof url !== 'string') return false;
        var compact = url.replace(/[\u0000-\u0020\u007f]/g, '');
        if (compact === '') return false;
        return !/^[a-z][a-z0-9+.-]*:/i.test(compact) || /^https?:/i.test(compact);
    }

    var URL_ATTRIBUTES = /^(href|src|action|formaction|xlink:href)$/i;

    function applyAttributes(node, attributes) {
        if (!attributes) return;
        Object.keys(attributes).forEach(function (key) {
            // setAttribute encodes the value; the name must be a plain attribute name
            if (URL_ATTRIBUTES.test(key) && !isSafeUrl(String(attributes[key]))) return;
            if (/^[a-zA-Z_][\w:.-]*$/.test(key) && !/^on/i.test(key)) {
                node.setAttribute(key, String(attributes[key]));
            }
        });
    }

    function actionControl(action, resolved) {
        var s = resolved.sizeClasses;
        var className = ['btn btn-primary', s.button, s.gap].filter(Boolean).join(' ');
        var node;
        if (isSafeUrl(action.url)) {
            node = el('a', className);
            node.setAttribute('href', action.url);
        } else {
            node = el('button', className);
            node.setAttribute('type', 'button');
        }
        applyAttributes(node, action.attributes);
        if (resolved.actionIconPath) node.appendChild(svg('', resolved.actionIconPath, '2'));
        node.appendChild(document.createTextNode(action.text));
        if (typeof action.onClick === 'function') node.addEventListener('click', action.onClick);
        return node;
    }

    /**
     * Build the empty state element (not yet in the page).
     * @returns {HTMLElement}
     */
    function create(options) {
        var o = options || {};
        var r = resolve(o);
        var s = r.sizeClasses;

        var root = el('div', 'empty-state flex flex-col items-center text-center ' + s.container + ' mx-auto');
        if (o.id) root.id = o.id;
        if (o.announce) root.setAttribute('role', 'status');

        var iconBox = el('div', 'empty-state-icon ' + s.iconBox + ' rounded-full');
        iconBox.appendChild(svg(s.icon, r.iconPath, r.strokeWidth));
        root.appendChild(iconBox);

        var text = el('div');
        text.appendChild(el(r.heading, s.title + ' font-semibold text-text-primary ' + s.titleGap, o.title || ''));
        text.appendChild(el('p', s.desc + ' text-text-secondary', o.description || ''));
        root.appendChild(text);

        var action = o.action && o.action.text ? o.action : null;
        var secondary = o.secondary && o.secondary.text && isSafeUrl(o.secondary.url) ? o.secondary : null;
        if (action || secondary) {
            var actions = el('div', 'flex flex-col sm:flex-row items-center gap-3');
            if (action) actions.appendChild(actionControl(action, r));
            if (secondary) {
                var link = el('a', s.desc + ' text-accent-blue hover:text-accent-blue-hover transition-colors', secondary.text);
                link.setAttribute('href', secondary.url);
                actions.appendChild(link);
            }
            root.appendChild(actions);
        }
        return root;
    }

    /** Replace a container's content with an empty state. Returns the new element. */
    function render(container, options) {
        var node = create(options);
        container.textContent = '';
        container.appendChild(node);
        return node;
    }

    /**
     * A load failure in plain language, with Retry. The message should say what happened and what
     * to do; never pass an exception's text.
     */
    function error(container, options) {
        var o = options || {};
        return render(container, {
            type: 'error',
            title: o.title || 'Could not load this',
            description: o.description || 'Something went wrong while loading. Check your connection and try again.',
            size: o.size,
            announce: true,
            headingLevel: o.headingLevel,
            action: typeof o.onRetry === 'function'
                ? { text: o.retryText || 'Retry', iconPath: '', onClick: o.onRetry }
                : null
        });
    }

    /** "Nothing matches these filters", with a Clear filters action. */
    function filtered(container, options) {
        var o = options || {};
        var noun = o.noun || 'results';
        return render(container, {
            type: 'noResults',
            title: o.title || 'No ' + noun + ' match your filters',
            description: o.description || 'Try different filters, or clear them to see everything.',
            size: o.size,
            announce: true,
            headingLevel: o.headingLevel,
            action: typeof o.onClear === 'function'
                ? { text: o.clearText || 'Clear filters', iconPath: '', onClick: o.onClear }
                : null
        });
    }

    return {
        ICONS: ICONS,
        SIZES: SIZES,
        resolve: resolve,
        isSafeUrl: isSafeUrl,
        create: create,
        render: render,
        error: error,
        filtered: filtered
    };
});
