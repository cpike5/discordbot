/**
 * ApiClient - shared fetch wrapper module.
 *
 * Centralizes anti-forgery token injection, JSON (de)serialization, and
 * error parsing/toast reporting that were previously duplicated across
 * individual page scripts (settings.js, moderation-settings.js, portal-tts.js, ...).
 *
 * Exposed as window.ApiClient (browser) and module.exports (Node/tests).
 */
(function (root, factory) {
    if (typeof module === 'object' && module.exports) {
        module.exports = factory();
    } else {
        root.ApiClient = factory();
    }
})(typeof self !== 'undefined' ? self : this, function () {
    'use strict';

    /**
     * Typed error thrown by the throwing request helpers (request/get/post/put/del).
     * Carries the HTTP status and, where available, the parsed error body
     * (a JSON error payload or an ASP.NET Core ProblemDetails object).
     */
    class ApiClientError extends Error {
        constructor(message, status, data) {
            super(message);
            this.name = 'ApiClientError';
            this.status = status;
            this.data = data;
        }
    }

    /**
     * Locate the anti-forgery token the same way the existing page scripts do:
     * a hidden `<input name="__RequestVerificationToken">` somewhere on the page.
     * Returns null if none is present (e.g. anonymous pages).
     */
    function getAntiForgeryToken() {
        if (typeof document === 'undefined') return null;
        const input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : null;
    }

    /**
     * Best-effort extraction of a human-readable message from an error body.
     * Supports the ad-hoc `{ success, message }` / `{ errors: [...] }` shape
     * used by this app's Razor Pages handlers as well as ASP.NET Core
     * ProblemDetails (`{ title, detail, errors }`).
     */
    function extractErrorMessage(data, fallback) {
        if (!data) return fallback;
        if (typeof data === 'string') return data || fallback;
        if (data.message) return data.message;
        if (data.detail) return data.detail;
        if (Array.isArray(data.errors)) return data.errors.join(', ');
        if (data.errors && typeof data.errors === 'object') {
            const messages = Object.values(data.errors).flat();
            if (messages.length) return messages.join(', ');
        }
        if (data.title) return data.title;
        return fallback;
    }

    /**
     * Parse a fetch Response body as JSON, tolerating empty/non-JSON bodies
     * (e.g. 204 No Content, or handlers that return nothing).
     */
    async function parseBody(response) {
        const text = await response.text();
        if (!text) return null;
        try {
            return JSON.parse(text);
        } catch {
            return text;
        }
    }

    /**
     * Core request helper. Never throws for HTTP error statuses -
     * it always resolves with { ok, status, data }, matching the
     * `response.ok && data.success` pattern used throughout the app.
     * Network failures / body parsing failures still reject.
     *
     * @param {string} url
     * @param {object} [options]
     * @param {string} [options.method='GET']
     * @param {object|FormData|null} [options.body] - plain objects are JSON-serialized;
     *   FormData is sent as-is (browser sets the multipart Content-Type/boundary).
     * @param {object} [options.headers]
     * @param {boolean} [options.token=true] - inject the RequestVerificationToken header.
     * @param {string} [options.credentials='same-origin']
     */
    async function requestRaw(url, options = {}) {
        const {
            method = 'GET',
            body,
            headers = {},
            token = true,
            credentials = 'same-origin'
        } = options;

        const finalHeaders = Object.assign({}, headers);
        let finalBody = body;

        const isFormData = typeof FormData !== 'undefined' && body instanceof FormData;
        if (body !== undefined && body !== null && !isFormData && typeof body !== 'string') {
            finalBody = JSON.stringify(body);
            if (!finalHeaders['Content-Type']) {
                finalHeaders['Content-Type'] = 'application/json';
            }
        }

        if (token) {
            const tokenValue = getAntiForgeryToken();
            if (tokenValue) {
                finalHeaders['RequestVerificationToken'] = tokenValue;
            }
        }

        const response = await fetch(url, {
            method,
            headers: finalHeaders,
            body: finalBody,
            credentials
        });

        const data = await parseBody(response);
        return { ok: response.ok, status: response.status, data, response };
    }

    /**
     * Same as requestRaw, but throws ApiClientError for non-2xx responses.
     */
    async function request(url, options = {}) {
        const { ok, status, data } = await requestRaw(url, options);
        if (!ok) {
            const message = extractErrorMessage(data, `Request failed with status ${status}`);
            throw new ApiClientError(message, status, data);
        }
        return data;
    }

    function get(url, options = {}) {
        return request(url, Object.assign({}, options, { method: 'GET' }));
    }
    function post(url, body, options = {}) {
        return request(url, Object.assign({}, options, { method: 'POST', body }));
    }
    function put(url, body, options = {}) {
        return request(url, Object.assign({}, options, { method: 'PUT', body }));
    }
    function del(url, options = {}) {
        return request(url, Object.assign({}, options, { method: 'DELETE' }));
    }

    function getRaw(url, options = {}) {
        return requestRaw(url, Object.assign({}, options, { method: 'GET' }));
    }
    function postRaw(url, body, options = {}) {
        return requestRaw(url, Object.assign({}, options, { method: 'POST', body }));
    }
    function putRaw(url, body, options = {}) {
        return requestRaw(url, Object.assign({}, options, { method: 'PUT', body }));
    }
    function delRaw(url, options = {}) {
        return requestRaw(url, Object.assign({}, options, { method: 'DELETE' }));
    }

    /**
     * Shared "show an error toast" hook, used by pages that previously
     * duplicated this fallback chain (window.quickActions -> window.showToast -> console).
     */
    function showErrorToast(message) {
        if (typeof window !== 'undefined') {
            if (window.quickActions && typeof window.quickActions.showToast === 'function') {
                window.quickActions.showToast(message, 'error');
                return;
            }
            if (typeof window.showToast === 'function') {
                window.showToast('error', message);
                return;
            }
        }
        console.error(message);
    }

    return {
        ApiClientError,
        getAntiForgeryToken,
        extractErrorMessage,
        request,
        requestRaw,
        get,
        post,
        put,
        del,
        getRaw,
        postRaw,
        putRaw,
        delRaw,
        showErrorToast
    };
});
