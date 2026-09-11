const test = require('node:test');
const assert = require('node:assert/strict');

// charts.js uses ES module syntax with no "type": "module" package.json above
// it (there is deliberately none under wwwroot - it is publicly served); the
// "test" script's `--experimental-detect-module` flag is what lets Node's
// dynamic import() parse it as ESM from this CommonJS test file regardless.
const chartsModulePromise = import('../blazor/charts.js');

test('buildDefaultOptions reads every color from the supplied CSS var getter', async () => {
    const { buildDefaultOptions } = await chartsModulePromise;

    const values = {
        '--color-text-secondary': ' #a09c96 ',
        '--color-bg-tertiary': '#1c2025',
        '--color-text-primary': '#e7e4df',
        '--color-border-primary': 'rgba(255, 255, 255, 0.08)',
        '--color-text-tertiary': '#6d6a66'
    };

    const options = buildDefaultOptions((name) => values[name]);

    assert.equal(options.responsive, true);
    assert.equal(options.maintainAspectRatio, false);
    assert.equal(options.plugins.legend.labels.color, '#a09c96'); // trimmed
    assert.equal(options.plugins.tooltip.backgroundColor, '#1c2025');
    assert.equal(options.plugins.tooltip.titleColor, '#e7e4df');
    assert.equal(options.plugins.tooltip.bodyColor, '#a09c96');
    assert.equal(options.plugins.tooltip.borderColor, 'rgba(255, 255, 255, 0.08)');
    assert.equal(options.scales.y.grid.color, '#1c2025');
    assert.equal(options.scales.y.ticks.color, '#6d6a66');
    assert.equal(options.scales.x.grid.display, false);
    assert.equal(options.scales.x.ticks.color, '#6d6a66');
});

test('buildDefaultOptions falls back to the Graphite defaults when a CSS var is missing or blank', async () => {
    const { buildDefaultOptions } = await chartsModulePromise;

    const options = buildDefaultOptions((name) => {
        if (name === '--color-bg-tertiary') return '   '; // blank counts as missing
        return undefined;
    });

    assert.equal(options.plugins.tooltip.backgroundColor, '#1c2025');
    assert.equal(options.plugins.tooltip.titleColor, '#e7e4df');
    assert.equal(options.plugins.tooltip.bodyColor, '#a09c96');
});

test('buildDefaultOptions tolerates a missing cssVarGetter entirely (all fallbacks)', async () => {
    const { buildDefaultOptions } = await chartsModulePromise;

    const options = buildDefaultOptions(undefined);

    assert.equal(options.plugins.tooltip.backgroundColor, '#1c2025');
    assert.equal(options.scales.y.grid.color, '#1c2025');
});

test('buildPalette maps the Graphite accent tokens to the chart palette', async () => {
    const { buildPalette } = await chartsModulePromise;

    const values = {
        '--color-accent-blue': '#3d9ad6',
        '--color-accent-orange': '#e6602b',
        '--color-success': '#2fbf7f',
        '--color-warning': '#f0a323',
        '--color-error': '#ef4f4f',
        '--color-info': '#2fb3cc'
    };

    const palette = buildPalette((name) => values[name]);

    assert.equal(palette.primary, '#3d9ad6');
    assert.equal(palette.secondary, '#e6602b');
    assert.equal(palette.success, '#2fbf7f');
    assert.equal(palette.warning, '#f0a323');
    assert.equal(palette.error, '#ef4f4f');
    assert.equal(palette.info, '#2fb3cc');
});

test('buildPalette falls back to hardcoded Graphite colors when unset', async () => {
    const { buildPalette } = await chartsModulePromise;

    const palette = buildPalette(() => undefined);

    assert.equal(palette.primary, '#3d9ad6');
    assert.equal(palette.secondary, '#e6602b');
});

test('mergeDeep deep-merges nested objects without mutating either input', async () => {
    const { mergeDeep } = await chartsModulePromise;

    const base = { plugins: { legend: { display: true }, tooltip: { enabled: true } } };
    const override = { plugins: { legend: { display: false } } };

    const merged = mergeDeep(base, override);

    assert.equal(merged.plugins.legend.display, false);
    assert.equal(merged.plugins.tooltip.enabled, true, 'a key absent from override survives from base');
    assert.equal(base.plugins.legend.display, true, 'base must not be mutated');
});

test('mergeDeep ignores __proto__/constructor/prototype keys in override', async () => {
    const { mergeDeep } = await chartsModulePromise;

    const maliciousOverride = JSON.parse('{"__proto__": {"polluted": true}, "constructor": {"polluted": true}, "prototype": {"polluted": true}, "safe": 1}');

    const merged = mergeDeep({}, maliciousOverride);

    assert.equal(merged.safe, 1);
    assert.equal({}.polluted, undefined, 'Object.prototype must not have been polluted');
    assert.equal(Object.prototype.polluted, undefined);
});
