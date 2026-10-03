/**
 * Skeleton - loading placeholders for regions a script fills.
 *
 * The shapes match the _Skeleton* partials. The point of the helper is the delay: a skeleton
 * that flashes for 80ms is worse than none, so Skeleton.show waits (300ms by default) and draws
 * nothing at all if the data arrives first.
 *
 *   const loading = Skeleton.show(panel, { kind: 'table', rows: 6, columns: 4 });
 *   try {
 *       const data = await ApiClient.get(url);
 *       loading.hide();
 *       renderRows(panel, data);
 *   } catch (err) {
 *       loading.hide();
 *       EmptyState.error(panel, { onRetry: load });
 *   }
 *
 * show() marks the container `aria-busy="true"` while loading and, once the skeleton is drawn,
 * adds a visually hidden "Loading" text. Whether a screen reader announces text inserted into a
 * busy region varies, so do not rely on it: the page's own status line or the result of the load
 * is what tells the user. The skeleton replaces the container's content (stale content from an
 * earlier load must not stay on screen).
 *
 * Kinds: 'lines' ({ count }), 'table' ({ rows, columns }), 'list' ({ rows }),
 * 'card' ({ type: 'stats' | 'server' | 'activity' | 'table' | 'list' | 'form', showHeader }).
 * Other options: delay (ms, default 300; 0 draws at once), label (the status text).
 * Animation stops under prefers-reduced-motion (site.css).
 *
 * Exposed as window.Skeleton (browser) and module.exports (Node/tests).
 */
(function (root, factory) {
    if (typeof module === 'object' && module.exports) {
        module.exports = factory();
    } else {
        root.Skeleton = factory();
    }
})(typeof self !== 'undefined' ? self : this, function () {
    'use strict';

    var DEFAULT_DELAY_MS = 300;

    // ---------------------------------------------------------------- timing core

    /**
     * The delay rule, without a DOM: start() schedules the draw, stop() cancels it or, if it
     * already happened, removes it. Timers are injectable for tests.
     */
    function createDelayedLoader(options) {
        var delay = options.delay === undefined ? DEFAULT_DELAY_MS : options.delay;
        var schedule = options.schedule || setTimeout;
        var cancel = options.cancel || clearTimeout;
        var timer = null;
        var shown = false;
        var started = false;

        function draw() {
            timer = null;
            shown = true;
            options.show();
        }

        return {
            start: function () {
                if (started) return;
                started = true;
                if (delay <= 0) {
                    draw();
                } else {
                    timer = schedule(draw, delay);
                }
            },
            stop: function () {
                if (timer !== null) {
                    cancel(timer);
                    timer = null;
                }
                if (shown) {
                    shown = false;
                    options.hide();
                }
                started = false;
            },
            isShown: function () { return shown; }
        };
    }

    // ---------------------------------------------------------------- shapes

    function el(className, tag) {
        var node = document.createElement(tag || 'div');
        if (className) node.className = className;
        return node;
    }

    function bone(className) {
        return el('skeleton ' + className);
    }

    function appendAll(parent, children) {
        children.forEach(function (child) { parent.appendChild(child); });
        return parent;
    }

    /** N text lines, the last one shorter. */
    function lines(count) {
        var n = Math.max(1, count || 3);
        var wrap = el('space-y-2');
        for (var i = 0; i < n; i++) {
            wrap.appendChild(bone('h-4 rounded ' + (i === n - 1 && n > 1 ? 'w-2/3' : 'w-full')));
        }
        return wrap;
    }

    /** Rows of avatar + two lines. */
    function list(rows) {
        var wrap = el('space-y-4');
        for (var i = 0; i < Math.max(1, rows || 4); i++) {
            wrap.appendChild(appendAll(el('flex items-start gap-3'), [
                bone('w-10 h-10 rounded-full flex-shrink-0'),
                appendAll(el('flex-1 space-y-2'), [bone('w-full h-4 rounded'), bone('w-3/4 h-3 rounded')])
            ]));
        }
        return wrap;
    }

    /** Table rows: `columns` cells per row, the last pushed right. */
    function table(rows, columns) {
        var cols = Math.max(1, columns || 4);
        var widths = ['w-32', 'w-24', 'w-20', 'w-16'];
        var wrap = el('space-y-3');
        for (var r = 0; r < Math.max(1, rows || 5); r++) {
            var row = el('flex items-center gap-4');
            for (var c = 0; c < cols; c++) {
                var cls = 'h-4 rounded ' + widths[c % widths.length] + (c === cols - 1 && cols > 1 ? ' ml-auto' : '');
                row.appendChild(bone(cls));
            }
            wrap.appendChild(row);
        }
        return wrap;
    }

    function cardBody(type) {
        if (type === 'server') {
            var stat = function () {
                return appendAll(el('space-y-2'), [bone('w-16 h-6 rounded'), bone('w-20 h-3 rounded')]);
            };
            return appendAll(el('flex flex-col gap-4'), [
                appendAll(el('flex items-center gap-3'), [
                    bone('w-12 h-12 rounded-lg flex-shrink-0'),
                    appendAll(el('flex-1 space-y-2'), [bone('w-32 h-5 rounded'), bone('w-24 h-4 rounded')])
                ]),
                appendAll(el('grid grid-cols-3 gap-4'), [stat(), stat(), stat()])
            ]);
        }
        if (type === 'activity') return list(3);
        if (type === 'table') return table(5, 3);
        if (type === 'list') return list(4);
        if (type === 'form') {
            var form = el('space-y-4');
            for (var i = 0; i < 3; i++) {
                form.appendChild(appendAll(el('space-y-2'), [bone('w-24 h-4 rounded'), bone('w-full h-10 rounded-md')]));
            }
            form.appendChild(bone('w-28 h-10 rounded-md'));
            return form;
        }
        // stats
        return appendAll(el('flex items-start gap-4'), [
            bone('w-12 h-12 rounded-lg flex-shrink-0'),
            appendAll(el('flex-1 space-y-3'), [bone('w-20 h-8 rounded'), bone('w-24 h-4 rounded')])
        ]);
    }

    /** A card-shaped placeholder. */
    function card(type, options) {
        var wrap = el('card');
        if (options && options.showHeader) {
            wrap.appendChild(appendAll(el('card-header'), [bone('w-32 h-6 rounded')]));
        }
        wrap.appendChild(appendAll(el('card-body'), [cardBody(type || 'stats')]));
        return wrap;
    }

    /** Build a skeleton for the given options (not yet in the page). */
    function build(options) {
        var o = options || {};
        var node;
        switch (o.kind) {
            case 'table': node = table(o.rows, o.columns); break;
            case 'list': node = list(o.rows); break;
            case 'card': node = card(o.type, { showHeader: o.showHeader }); break;
            default: node = lines(o.count); break;
        }
        node.setAttribute('aria-hidden', 'true');
        return node;
    }

    // ---------------------------------------------------------------- region

    /**
     * Show a skeleton in a container after a delay.
     * @param {HTMLElement} container
     * @param {Object} [options] kind, rows, columns, count, type, showHeader, delay, label
     * @returns {{ hide: function(), isShown: function(): boolean }}
     */
    function show(container, options) {
        var o = options || {};
        var drawn = [];

        container.setAttribute('aria-busy', 'true');
        container.classList.add('skeleton-region');

        var loader = createDelayedLoader({
            delay: o.delay,
            show: function () {
                container.textContent = '';
                var status = el('sr-only', 'span');
                status.setAttribute('role', 'status');
                status.textContent = o.label || 'Loading';
                drawn = [build(o), status];
                drawn.forEach(function (node) { container.appendChild(node); });
            },
            hide: function () {
                drawn.forEach(function (node) {
                    if (node.parentNode === container) container.removeChild(node);
                });
                drawn = [];
            }
        });
        loader.start();

        return {
            hide: function () {
                loader.stop();
                container.removeAttribute('aria-busy');
                container.classList.remove('skeleton-region');
            },
            isShown: loader.isShown
        };
    }

    return {
        DEFAULT_DELAY_MS: DEFAULT_DELAY_MS,
        createDelayedLoader: createDelayedLoader,
        lines: lines,
        list: list,
        table: table,
        card: card,
        build: build,
        show: show
    };
});
