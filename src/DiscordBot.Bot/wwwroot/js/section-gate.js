/**
 * Section gate: a settings section that only applies while a master switch is on.
 *
 * A dimmed overlay with pointer-events:none still leaves every control in the tab order and
 * readable to a screen reader, so it only looks disabled. This makes the section really
 * unavailable with `inert` (no focus, no clicks, hidden from assistive technology) and puts the
 * reason next to it, outside the inert part, where it can still be read.
 *
 *   <input type="checkbox" id="Input_IsEnabled" ...>
 *   <div data-section-gate="#Input_IsEnabled">
 *       <p data-section-gate-note hidden>Turn welcome messages on to edit these settings.</p>
 *       <div data-section-gate-body> ...controls... </div>
 *   </div>
 *
 * `inert` does not remove controls from form submission, so the saved values travel with the
 * form unchanged while the section is off. Opt-in markup only; no handler text.
 *
 * Exposed as window.SectionGate (browser) and module.exports (Node/tests). `apply` has no DOM
 * dependency beyond the two objects it is given.
 */
(function (root, factory) {
    if (typeof module === 'object' && module.exports) {
        module.exports = factory();
    } else {
        root.SectionGate = factory();
    }
})(typeof self !== 'undefined' ? self : this, function () {
    'use strict';

    /**
     * Put a section in the state the switch asks for.
     * @param {{inert: boolean, classList: {toggle: Function}}} body - the controls
     * @param {{hidden: boolean}|null} note - the explanation, shown while the section is off
     * @param {boolean} on - whether the switch is on
     */
    function apply(body, note, on) {
        if (body) {
            body.inert = !on;
            if (body.classList) body.classList.toggle('section-gated', !on);
        }
        if (note) note.hidden = on;
    }

    function parts(container) {
        return {
            body: container.querySelector('[data-section-gate-body]'),
            note: container.querySelector('[data-section-gate-note]')
        };
    }

    function switchFor(container) {
        var selector = container.getAttribute('data-section-gate');
        return selector ? document.querySelector(selector) : null;
    }

    /** Bring every gate under `scope` in line with its switch. */
    function init(scope) {
        Array.prototype.forEach.call((scope || document).querySelectorAll('[data-section-gate]'), function (container) {
            var toggle = switchFor(container);
            if (!toggle) return;
            var p = parts(container);
            apply(p.body, p.note, !!toggle.checked);
        });
    }

    if (typeof document !== 'undefined') {
        // Run now: the script sits after the markup, so the first paint is already correct and
        // the unsaved-changes baseline (taken on DOMContentLoaded) sees the final state.
        init(document);
        document.addEventListener('change', function (event) {
            var target = event.target;
            if (!target || !target.id) return;
            Array.prototype.forEach.call(document.querySelectorAll('[data-section-gate]'), function (container) {
                var toggle = switchFor(container);
                if (toggle === target) {
                    var p = parts(container);
                    apply(p.body, p.note, !!toggle.checked);
                }
            });
        });
    }

    return { apply: apply, init: init };
});
