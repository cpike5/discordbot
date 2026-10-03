const test = require('node:test');
const assert = require('node:assert/strict');
const C = require('../commands-page.js');
const Skeleton = require('../skeleton.js');

test('popstateNeedsReload ignores hash-only steps and reloads when the path or query changed', () => {
    assert.equal(C.popstateNeedsReload('/Commands?tab=analytics', '/Commands', '?tab=analytics'), false);
    assert.equal(C.popstateNeedsReload('/Commands', '/Commands', ''), false, 'a skip link only moves the hash');
    assert.equal(C.popstateNeedsReload('/Commands?tab=analytics', '/Commands', '?tab=execution-logs'), true);
    assert.equal(C.popstateNeedsReload('/Commands?tab=analytics', '/Commands', ''), true);
    assert.equal(C.popstateNeedsReload('/Commands', '/Other', ''), true);
});

/** The real delayed loader with a hand-cranked clock, so the 300 ms timer can be fired on demand. */
function fakeSkeleton() {
    const timers = [];
    const drawn = [];
    return {
        drawn,
        flush() { timers.splice(0).forEach((t) => { if (!t.cancelled) t.fn(); }); },
        show(region, options) {
            const loader = Skeleton.createDelayedLoader({
                schedule: (fn) => { const t = { fn, cancelled: false }; timers.push(t); return t; },
                cancel: (t) => { t.cancelled = true; },
                show: () => drawn.push(options.label),
                hide: () => {}
            });
            loader.start();
            return { hide: loader.stop };
        }
    };
}

test('hiding the previous skeleton stops its timer from drawing over a newer load', () => {
    const fake = fakeSkeleton();
    const slot = C.createSkeletonSlot((region, options) => fake.show(region, options));

    slot.show({}, { label: 'first' });
    // A newer load starts before the first one's 300 ms are up: abortLoad hides what was showing
    slot.hideActive();
    slot.show({}, { label: 'second' });
    slot.hideActive(); // the second load rendered
    fake.flush(); // the clock now reaches both timers

    assert.deepEqual(fake.drawn, [], 'neither skeleton may draw after its load has been replaced or finished');
});

test('a skeleton handle hides once, so a stale load finishing late cannot touch the newer one', () => {
    let hides = 0;
    const slot = C.createSkeletonSlot(() => ({ hide() { hides += 1; } }));

    const stale = slot.show({}, {});
    slot.hideActive();
    const current = slot.show({}, {});
    stale.hide(); // the aborted request settles after the next one started
    stale.hide();

    assert.equal(hides, 1, 'only the abort hid the stale skeleton; the late calls did nothing');
    current.hide();
    assert.equal(hides, 2);
});
