/**
 * Unsaved-changes protection: per-form dirty tracking with a beforeunload warning.
 *
 * Opt in with one attribute:
 *
 *   <form method="post" data-unsaved-changes> ... </form>
 *
 * A form is dirty when its controls differ from how they were when the page loaded, so editing a
 * field and then typing the old value back leaves it clean. Submitting the form is not a loss and
 * does not warn. Other attributes:
 *
 *   data-unsaved-dirty-on-load   Start dirty. Put it on a form the server re-rendered after a
 *                                failed validation: the input on screen is not saved yet. The bare
 *                                attribute and "true" start dirty; "false" does not, so a form
 *                                element (where Razor cannot add the attribute conditionally) can
 *                                write data-unsaved-dirty-on-load="@(isPostBack ? "true" : "false")".
 *   data-unsaved-ignore          On a control (or a container of controls) that should not count,
 *                                such as a search box or a filter beside the editable fields.
 *   data-unsaved-indicator       An element inside the form, shown (its `hidden` class removed)
 *                                only while the form is dirty: "Unsaved changes".
 *
 * A form that saves over fetch calls UnsavedChanges.markClean(form) once the save succeeds, which
 * takes the saved values as the new baseline. The form gets `data-dirty="true|false"` and fires a
 * bubbling `unsavedchange` event ({ detail: { dirty } }) whenever that flips.
 *
 * The browser decides the wording of the leave-page prompt; a script cannot set it.
 *
 * Exposed as window.UnsavedChanges (browser) and module.exports (Node/tests). The tracker core
 * (serialize, createTracker, createRegistry, handleBeforeUnload) has no DOM dependency.
 */
(function (root, factory) {
    if (typeof module === 'object' && module.exports) {
        module.exports = factory();
    } else {
        root.UnsavedChanges = factory();
    }
})(typeof self !== 'undefined' ? self : this, function () {
    'use strict';

    /** Control types that carry no user-edited value. */
    var NON_VALUE_TYPES = { submit: 1, button: 1, reset: 1, image: 1, file: 1 };

    /** Names of fields the framework writes, which never change. */
    var FRAMEWORK_FIELDS = { __RequestVerificationToken: 1 };

    function isIgnored(control) {
        if (control.dataset && control.dataset.unsavedIgnore !== undefined) return true;
        if (typeof control.hasAttribute === 'function' && control.hasAttribute('data-unsaved-ignore')) return true;
        return typeof control.closest === 'function' && !!control.closest('[data-unsaved-ignore]');
    }

    /**
     * Reduce a form's controls to one comparable string. Checkboxes and radios count only when
     * checked, a multiple select contributes every selected option, and buttons, file inputs,
     * disabled controls and framework fields are left out.
     *
     * @param {Iterable<Object>} controls - `form.elements`, or control-like objects in tests
     * @returns {string}
     */
    function serialize(controls) {
        var entries = [];
        Array.prototype.forEach.call(controls, function (control) {
            if (!control || !control.name || control.disabled) return;
            if (FRAMEWORK_FIELDS[control.name]) return;
            var type = String(control.type || '').toLowerCase();
            if (NON_VALUE_TYPES[type]) return;
            if (isIgnored(control)) return;

            if (type === 'checkbox' || type === 'radio') {
                if (control.checked) entries.push([control.name, control.value]);
            } else if (type === 'select-multiple') {
                Array.prototype.forEach.call(control.options || [], function (option) {
                    if (option.selected) entries.push([control.name, option.value]);
                });
            } else {
                entries.push([control.name, control.value]);
            }
        });
        return JSON.stringify(entries);
    }

    /**
     * A dirty tracker for one form.
     *
     * @param {Object} options
     * @param {function(): string} options.snapshot - returns the form's current comparable state
     * @param {boolean} [options.startDirty=false] - dirty until markClean, whatever the fields say
     * @param {function(boolean)} [options.onChange] - called when dirty flips
     */
    function createTracker(options) {
        var snapshot = options.snapshot;
        var onChange = options.onChange || function () {};
        var baseline = snapshot();
        var forced = !!options.startDirty;
        var submitting = false;
        var dirty = forced;

        function evaluate() {
            var next = forced || snapshot() !== baseline;
            if (next !== dirty) {
                dirty = next;
                onChange(dirty);
            }
            return dirty;
        }

        return {
            /** Recompute after a control changed. */
            evaluate: evaluate,
            isDirty: function () { return dirty; },
            /** Take the current values as saved. */
            markClean: function () {
                forced = false;
                submitting = false;
                baseline = snapshot();
                return evaluate();
            },
            /** The form is being submitted: leaving is not a loss. */
            markSubmitting: function () { submitting = true; },
            /** The submit did not leave the page (cancelled, failed, back navigation). */
            cancelSubmitting: function () { submitting = false; },
            isSubmitting: function () { return submitting; },
            /** True when leaving the page now would lose edits. */
            shouldWarn: function () { return dirty && !submitting; }
        };
    }

    /** The set of tracked forms; the page asks it once whether to warn. */
    function createRegistry() {
        var trackers = [];
        return {
            add: function (tracker) { trackers.push(tracker); return tracker; },
            remove: function (tracker) {
                var index = trackers.indexOf(tracker);
                if (index >= 0) trackers.splice(index, 1);
            },
            size: function () { return trackers.length; },
            anyDirty: function () {
                return trackers.some(function (t) { return t.isDirty(); });
            },
            shouldWarn: function () {
                return trackers.some(function (t) { return t.shouldWarn(); });
            }
        };
    }

    /**
     * beforeunload handler: asks the browser to confirm leaving only when a tracked form would
     * lose edits. Returns true when it asked.
     */
    function handleBeforeUnload(event, registry) {
        if (!registry.shouldWarn()) return false;
        event.preventDefault();
        // Older browsers show the prompt only when returnValue is set
        event.returnValue = '';
        return true;
    }

    /**
     * A submit event reached the end of its dispatch. One nothing cancelled is leaving the page,
     * so the tracker stops warning; a handler that cancels it later (a save over fetch) puts the
     * warning back on the next tick, and a navigation that never happens does so after 15 s.
     */
    function handleSubmit(event, tracker, schedule) {
        if (!tracker || event.defaultPrevented) return false;
        tracker.markSubmitting();
        schedule(function () { if (event.defaultPrevented) tracker.cancelSubmitting(); }, 0);
        schedule(function () { tracker.cancelSubmitting(); }, 15000);
        return true;
    }

    // ---------------------------------------------------------------- browser glue

    var registry = createRegistry();
    var byForm = typeof WeakMap === 'function' ? new WeakMap() : null;

    function setIndicators(form, dirty) {
        form.setAttribute('data-dirty', dirty ? 'true' : 'false');
        Array.prototype.forEach.call(form.querySelectorAll('[data-unsaved-indicator]'), function (el) {
            el.classList.toggle('hidden', !dirty);
        });
        form.dispatchEvent(new CustomEvent('unsavedchange', { bubbles: true, detail: { dirty: dirty } }));
    }

    /**
     * Start tracking a form. Safe to call twice for the same form.
     * @param {HTMLFormElement} form
     * @returns the form's tracker
     */
    function track(form) {
        if (!form || !byForm) return null;
        if (byForm.has(form)) return byForm.get(form);

        var tracker = createTracker({
            snapshot: function () { return serialize(form.elements); },
            startDirty: form.hasAttribute('data-unsaved-dirty-on-load') &&
                form.getAttribute('data-unsaved-dirty-on-load') !== 'false',
            onChange: function (dirty) { setIndicators(form, dirty); }
        });
        byForm.set(form, tracker);
        registry.add(tracker);
        setIndicators(form, tracker.isDirty());

        var recheck = function () { tracker.evaluate(); };
        form.addEventListener('input', recheck);
        form.addEventListener('change', recheck);
        // reset restores the defaults after the event, so look again afterwards
        form.addEventListener('reset', function () { setTimeout(recheck, 0); });
        return tracker;
    }

    /** Stop tracking a form (for example before removing it from the page). */
    function untrack(form) {
        var tracker = byForm && byForm.get(form);
        if (!tracker) return;
        registry.remove(tracker);
        byForm.delete(form);
        // A dirty form that is gone no longer needs the beforeunload listener
        syncUnloadListener();
    }

    /** Track every opted-in form under `scope`. Run on load; call again after inserting forms. */
    function init(scope) {
        var base = scope || document;
        Array.prototype.forEach.call(base.querySelectorAll('form[data-unsaved-changes]'), track);
    }

    /** True when the form (or, with no argument, any tracked form) has unsaved edits. */
    function isDirty(form) {
        if (form) {
            var tracker = byForm && byForm.get(form);
            return !!tracker && tracker.isDirty();
        }
        return registry.anyDirty();
    }

    /** Take the form's current values as saved. Call it after a successful fetch save. */
    function markClean(form) {
        var tracker = byForm && byForm.get(form);
        if (tracker) tracker.markClean();
    }

    // A page that has a beforeunload listener cannot enter the back/forward cache in some
    // browsers, so the listener exists only while a tracked form is dirty.
    var unloadListening = false;
    function onBeforeUnload(event) {
        handleBeforeUnload(event, registry);
    }
    function syncUnloadListener() {
        var want = registry.anyDirty();
        if (want && !unloadListening) {
            window.addEventListener('beforeunload', onBeforeUnload);
            unloadListening = true;
        } else if (!want && unloadListening) {
            window.removeEventListener('beforeunload', onBeforeUnload);
            unloadListening = false;
        }
    }

    if (typeof window !== 'undefined' && typeof document !== 'undefined') {
        // Every tracked form reports flips as an unsavedchange event; follow them
        document.addEventListener('unsavedchange', syncUnloadListener);

        // A submit that nothing cancelled is leaving the page on purpose. Registered on the window
        // so it runs after every document-level handler, whenever they were registered: a
        // fetch-based save calls preventDefault, stays on the page, and keeps its protection.
        window.addEventListener('submit', function (event) {
            var form = event.target;
            handleSubmit(event, form && byForm && byForm.get(form), setTimeout);
        });

        // Coming back through the back/forward cache restores the page as the user left it
        window.addEventListener('pageshow', function (event) {
            if (!event.persisted) return;
            document.querySelectorAll('form[data-unsaved-changes]').forEach(function (form) {
                var tracker = byForm.get(form);
                if (tracker) {
                    tracker.cancelSubmitting();
                    tracker.evaluate();
                }
            });
        });

        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', function () { init(document); });
        } else {
            init(document);
        }
    }

    return {
        init: init,
        track: track,
        untrack: untrack,
        isDirty: isDirty,
        markClean: markClean,
        // The tracker core, for tests
        serialize: serialize,
        createTracker: createTracker,
        createRegistry: createRegistry,
        handleBeforeUnload: handleBeforeUnload,
        handleSubmit: handleSubmit
    };
});
