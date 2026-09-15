const test = require('node:test');
const assert = require('node:assert/strict');

// audio.js uses ES module syntax with no "type": "module" package.json above
// it (there is deliberately none under wwwroot - it is publicly served); the
// "test" script's `--experimental-detect-module` flag is what lets Node's
// dynamic import() parse it as ESM from this CommonJS test file regardless.
//
// Only `releaseAll` is covered here: every other export (playPreview,
// getDuration, upload, ...) drives real `Audio`/`XMLHttpRequest`/`FormData`
// browser APIs Node has no equivalent for, so those stay covered by the
// Moq-based C# tests in tests/DiscordBot.Tests/Blazor/Interop/AudioInteropTests.cs
// plus review, per docs/articles/blazor-interop.md.
const audioModulePromise = import('../blazor/audio.js');

test.afterEach(() => {
    delete global.window;
});

function fakeDropZoneElement() {
    return { addEventListener() {}, removeEventListener() {} };
}

test('releaseAll does nothing and does not throw when nothing is registered', async () => {
    const { releaseAll } = await audioModulePromise;

    assert.doesNotThrow(() => releaseAll());
});

test('releaseAll unregisters every drop zone registered since the last call', async () => {
    const { registerDropZone, unregisterDropZone, releaseAll } = await audioModulePromise;
    const dotNetRef = { invokeMethodAsync: () => Promise.resolve() };

    const handle = registerDropZone(fakeDropZoneElement(), dotNetRef);

    assert.doesNotThrow(() => releaseAll());

    // The handle is already released: unregistering it again must be a no-op,
    // not an error.
    assert.doesNotThrow(() => unregisterDropZone(handle));

    // A second releaseAll (e.g. both the enhancedload and pagehide hooks
    // firing) must also be a no-op.
    assert.doesNotThrow(() => releaseAll());
});

test('releaseAll discards dropped-but-never-uploaded files so upload("<stale token>", ...) sees none', async () => {
    const { registerDropZone, releaseAll, upload } = await audioModulePromise;
    let droppedToken;
    const dotNetRef = {
        invokeMethodAsync: (method, ...args) => {
            if (method === 'OnFilesDropped') {
                droppedToken = args[1];
            }
            return Promise.resolve();
        }
    };
    const element = fakeDropZoneElement();
    let onDrop;
    element.addEventListener = (event, handler) => {
        if (event === 'drop') {
            onDrop = handler;
        }
    };

    registerDropZone(element, dotNetRef);
    onDrop({
        preventDefault() {},
        stopPropagation() {},
        dataTransfer: { files: [{ name: 'clip.mp3' }] }
    });
    assert.ok(droppedToken, 'the drop handler should have produced a token');

    releaseAll();

    let failureMessage;
    const uploadDotNetRef = {
        invokeMethodAsync: (method, message) => {
            if (method === 'OnUploadFailed') {
                failureMessage = message;
            }
            return Promise.resolve();
        }
    };
    upload(droppedToken, '/api/guilds/1/sounds', 'token', uploadDotNetRef);

    assert.equal(failureMessage, 'No file selected.', 'releaseAll must have discarded the dropped file for this token');
});
