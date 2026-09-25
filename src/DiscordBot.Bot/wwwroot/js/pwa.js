/**
 * Registers the service worker that makes the admin portal installable as a PWA.
 * See /sw.js for what it caches (static assets only, never pages or API data).
 */
(function () {
    'use strict';

    if (!('serviceWorker' in navigator) || !window.isSecureContext) {
        return;
    }

    window.addEventListener('load', function () {
        navigator.serviceWorker.register('/sw.js', { scope: '/' }).catch(function (err) {
            console.warn('[PWA] Service worker registration failed:', err);
        });
    });
})();
