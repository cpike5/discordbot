/**
 * Service worker for the admin portal PWA.
 *
 * Deliberately conservative: pages and API responses are never cached, because they
 * are per-user and authenticated, and a stale moderation or guild view is worse than
 * none. What it does:
 *   - precaches the offline page and the app icons;
 *   - serves static assets (/css, /js, /images, /lib) from a runtime cache,
 *     cache-first when the URL is versioned by asp-append-version (?v=...), and
 *     stale-while-revalidate otherwise;
 *   - falls back to the offline page when a navigation fails.
 * Everything else (API, SignalR, auth, health, metrics, non-GET, cross-origin)
 * goes straight to the network untouched.
 *
 * Bump CACHE_VERSION when the precache list or caching behaviour changes.
 */
const CACHE_VERSION = 'v1';
const PRECACHE = `discordbot-precache-${CACHE_VERSION}`;
const RUNTIME = `discordbot-runtime-${CACHE_VERSION}`;
const OFFLINE_URL = '/offline.html';
const RUNTIME_MAX_ENTRIES = 150;

const PRECACHE_URLS = [
    OFFLINE_URL,
    '/images/logo.svg',
    '/images/icons/icon-192.png',
    '/images/icons/icon-512.png'
];

const STATIC_PREFIXES = ['/css/', '/js/', '/images/', '/lib/'];
const BYPASS_PREFIXES = [
    '/api/', '/hubs/', '/account/', '/signin-', '/signout', '/health', '/metrics', '/swagger', '/exports/'
];

/**
 * Decides how a request is handled. Pure so it can be unit tested.
 * @param {{method: string, mode: string, url: string}} request
 * @param {string} origin - the service worker's own origin
 * @returns {'bypass'|'navigate'|'static-immutable'|'static'}
 */
function classifyRequest(request, origin) {
    if (request.method !== 'GET') return 'bypass';

    const url = new URL(request.url);
    if (url.origin !== origin) return 'bypass';

    const path = url.pathname.toLowerCase();
    if (BYPASS_PREFIXES.some(p => path.startsWith(p))) return 'bypass';

    if (request.mode === 'navigate') return 'navigate';

    if (STATIC_PREFIXES.some(p => path.startsWith(p))) {
        return url.searchParams.has('v') ? 'static-immutable' : 'static';
    }

    return 'bypass';
}

async function trimCache(cacheName, maxEntries) {
    const cache = await caches.open(cacheName);
    const keys = await cache.keys();
    for (let i = 0; i < keys.length - maxEntries; i++) {
        await cache.delete(keys[i]);
    }
}

async function putInRuntimeCache(request, response) {
    if (!response || !response.ok || response.redirected || response.type !== 'basic') return;
    const cache = await caches.open(RUNTIME);
    await cache.put(request, response);
    await trimCache(RUNTIME, RUNTIME_MAX_ENTRIES);
}

async function cacheFirst(event) {
    const cached = await caches.match(event.request);
    if (cached) return cached;
    const response = await fetch(event.request);
    event.waitUntil(putInRuntimeCache(event.request, response.clone()));
    return response;
}

async function staleWhileRevalidate(event) {
    const cached = await caches.match(event.request);
    const network = fetch(event.request).then(response => {
        event.waitUntil(putInRuntimeCache(event.request, response.clone()));
        return response;
    });
    if (cached) {
        event.waitUntil(network.catch(() => undefined));
        return cached;
    }
    return network;
}

async function networkWithOfflineFallback(event) {
    try {
        return await fetch(event.request);
    } catch (err) {
        const offline = await caches.match(OFFLINE_URL);
        if (offline) return offline;
        throw err;
    }
}

if (typeof self !== 'undefined' && typeof self.addEventListener === 'function') {
    self.addEventListener('install', event => {
        event.waitUntil(
            caches.open(PRECACHE)
                .then(cache => cache.addAll(PRECACHE_URLS))
                .then(() => self.skipWaiting())
        );
    });

    self.addEventListener('activate', event => {
        const keep = new Set([PRECACHE, RUNTIME]);
        event.waitUntil(
            caches.keys()
                .then(names => Promise.all(names
                    .filter(name => name.startsWith('discordbot-') && !keep.has(name))
                    .map(name => caches.delete(name))))
                .then(() => self.clients.claim())
        );
    });

    self.addEventListener('fetch', event => {
        switch (classifyRequest(event.request, self.location.origin)) {
            case 'navigate':
                event.respondWith(networkWithOfflineFallback(event));
                break;
            case 'static-immutable':
                event.respondWith(cacheFirst(event));
                break;
            case 'static':
                event.respondWith(staleWhileRevalidate(event));
                break;
            default:
                // Let the browser handle it normally.
                break;
        }
    });
}

if (typeof module !== 'undefined' && module.exports) {
    module.exports = { classifyRequest, CACHE_VERSION, PRECACHE_URLS };
}
